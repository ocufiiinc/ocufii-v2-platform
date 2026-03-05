using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using OcufiiAPI.Configs;
using OcufiiAPI.Data;
using OcufiiAPI.Extensions;
using OcufiiAPI.Models;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace OcufiiAPI.Controllers
{
    [ApiController]
    [Route("mqtt-filter")]
    [Authorize]
    public class MqttFilterController : ControllerBase
    {
        private readonly OcufiiDbContext _db;
        private readonly MqttConfig _mqttConfig;
        private readonly ILogger<MqttFilterController> _logger;

        public MqttFilterController(
            OcufiiDbContext db,
            IOptions<MqttConfig> mqttConfig,
            ILogger<MqttFilterController> logger)
        {
            _db = db;
            _mqttConfig = mqttConfig.Value;
            _logger = logger;
        }

        [HttpPost("apply-gateway-filter")]
        public async Task<IActionResult> ApplyGatewayFilterWithAcknowledgement([FromBody] ApplyGatewayFilterRequest request)
        {
            var userId = User.GetUserId();

            const int relationshipRule = 1;
            const int duplicateRule = 1;
            const int duplicateTime = 10;

            var allFlowAttempts = new List<object>();

            try
            {
                // Load DeviceType IDs from DB (gateway & beacon) for ID-based comparison
                var gatewayType = await _db.DeviceTypes.FirstOrDefaultAsync(t => t.Key == "gateway");
                var beaconType = await _db.DeviceTypes.FirstOrDefaultAsync(t => t.Key == "beacon");

                if (gatewayType == null || beaconType == null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "DeviceType configuration is missing in the database"
                    });
                }

                // Look up device by ID — no Include needed, DeviceTypeId is a direct FK column
                var device = await _db.Devices
                    .FirstOrDefaultAsync(d => d.Id == request.DeviceId && d.IsDeleted == false);

                if (device == null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = $"Device with ID {request.DeviceId} not found"
                    });
                }

                // Verify device belongs to the logged-in user
                if (device.UserId != userId)
                {
                    return Forbid();
                }

                var cleanMac = device.MacAddress.Replace(":", "").Replace("-", "").ToUpperInvariant();

                // Branch based on DeviceTypeId (FK column) — no string Key comparison
                if (device.DeviceTypeId == gatewayType.Id)
                {
                    // ── GATEWAY FLOW ──────────────────────────────────────────
                    // Find all beacons for logged-in user using beacon DeviceTypeId
                    var beaconDevices = await _db.Devices
                        .Where(d => d.UserId == userId && d.DeviceTypeId == beaconType.Id && d.IsDeleted == false)
                        .ToListAsync();

                    var beaconMacs = beaconDevices
                        .Select(b => b.MacAddress.Replace(":", "").Replace("-", "").ToUpperInvariant())
                        .ToArray();

                    // Execute gateway registration flow (Steps 1, 2, 3)
                    var result = await ExecuteGatewayFlow(
                        gatewayMac: cleanMac,
                        beaconMacs: beaconMacs,
                        relationshipRule: relationshipRule,
                        userId: device.UserId!.ToString(),
                        deviceId: device.Id.ToString(),
                        duplicateRule: duplicateRule,
                        duplicateTime: duplicateTime,
                        allFlowAttempts: allFlowAttempts
                    );

                    return Ok(result);
                }
                else if (device.DeviceTypeId == beaconType.Id)
                {
                    // ── BEACON FLOW ───────────────────────────────────────────
                    // Find all gateways for logged-in user using gateway DeviceTypeId
                    var gatewayDevices = await _db.Devices
                        .Where(d => d.UserId == userId && d.DeviceTypeId == gatewayType.Id && d.IsDeleted == false)
                        .ToListAsync();

                    if (gatewayDevices.Count == 0)
                    {
                        return Ok(new
                        {
                            success = false,
                            message = "No gateways found for this user"
                        });
                    }

                    // Load all user beacons once (used for whitelist on every gateway)
                    var allUserBeaconMacs = await _db.Devices
                        .Where(d => d.UserId == userId && d.DeviceTypeId == beaconType.Id && d.IsDeleted == false)
                        .Select(b => b.MacAddress.Replace(":", "").Replace("-", "").ToUpper())
                        .ToArrayAsync();

                    var gatewayResults = new List<object>();

                    // For each gateway, push updated beacon whitelist (msg_id=1028 only)
                    // using that gateway's own MQTT topic (iot/stg/{userId}/gw/{deviceId}/down)
                    foreach (var gateway in gatewayDevices)
                    {
                        var gatewayMac = gateway.MacAddress.Replace(":", "").Replace("-", "").ToUpperInvariant();
                        var gatewayFlowAttempts = new List<object>();

                        var result = await ExecuteBeaconAddFlow(
                            gatewayMac: gatewayMac,
                            userId: gateway.UserId!.ToString(),
                            deviceId: gateway.Id.ToString(),
                            beaconMacs: allUserBeaconMacs,
                            allFlowAttempts: gatewayFlowAttempts
                        );

                        gatewayResults.Add(new
                        {
                            gatewayId = gateway.Id,
                            gatewayMac,
                            result
                        });
                    }

                    return Ok(new
                    {
                        success = true,
                        message = $"Beacon whitelist pushed to {gatewayDevices.Count} gateway(s)",
                        beaconMac = cleanMac,
                        gatewayResults
                    });
                }
                else
                {
                    return Ok(new
                    {
                        success = false,
                        message = $"Device type with ID {device.DeviceTypeId} is not supported by this endpoint"
                    });
                }

            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    error = ex.Message,
                    innerError = ex.InnerException?.Message,
                    stack = ex.StackTrace,
                    flowAttempts = allFlowAttempts
                });
            }
        }

        private async Task<object> ExecuteGatewayFlow(
            string gatewayMac,
            string[] beaconMacs,
            int relationshipRule,
            string userId,
            string deviceId,
            int duplicateRule,
            int duplicateTime,
            List<object> allFlowAttempts)
        {
            var publishTopic = $"iot/stg/{userId}/gw/{deviceId}/down";
            var subscribeTopic = $"iot/stg/{userId}/gw/{deviceId}/up";

            var steps = new List<object>();
            var receivedMessages = new ConcurrentBag<(int? msgId, string payload, DateTime timestamp)>();
            IMqttClient? mqttClient = null;

            const int maxFlowRetries = 2;

            try
            {
                for (int flowAttempt = 1; flowAttempt <= maxFlowRetries; flowAttempt++)
                {
                    steps = new List<object>();
                    receivedMessages = new ConcurrentBag<(int? msgId, string payload, DateTime timestamp)>();

                    try
                    {
                        var factory = new MqttFactory();
                        mqttClient?.Dispose();
                        mqttClient = factory.CreateMqttClient();

                        mqttClient.ApplicationMessageReceivedAsync += e =>
                        {
                            var topic = e.ApplicationMessage.Topic;
                            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

                            try
                            {
                                var json = JsonSerializer.Deserialize<JsonDocument>(payload);
                                var msgId = json?.RootElement.GetProperty("msg_id").GetInt32();
                                receivedMessages.Add((msgId, payload, DateTime.UtcNow));
                                _logger.LogInformation($"Gateway {gatewayMac}: Received msg_id={msgId} on topic {topic}");
                            }
                            catch
                            {
                                receivedMessages.Add((null, payload, DateTime.UtcNow));
                                _logger.LogWarning($"Gateway {gatewayMac}: Failed to parse msg_id from: {payload}");
                            }

                            return Task.CompletedTask;
                        };

                        steps.Add(new
                        {
                            step = "flow_attempt",
                            attempt = flowAttempt,
                            maxAttempts = maxFlowRetries,
                            message = $"Starting gateway registration flow attempt {flowAttempt}/{maxFlowRetries}"
                        });

                        var clientOptions = new MqttClientOptionsBuilder()
                            .WithTcpServer(_mqttConfig.Host, _mqttConfig.Port)
                            .WithCredentials(_mqttConfig.Username, _mqttConfig.Password)
                            .WithClientId($"api-stg-{Guid.NewGuid()}")
                            .WithTlsOptions(o =>
                            {
                                o.UseTls(_mqttConfig.UseTls);
                                o.WithCertificateValidationHandler(_ => true);
                            })
                            .Build();

                        await mqttClient.ConnectAsync(clientOptions, CancellationToken.None);
                        steps.Add(new { step = "connect", success = true, message = $"Connected to {_mqttConfig.Host}:{_mqttConfig.Port}" });

                        await Task.Delay(1000);
                        steps.Add(new { step = "wait_after_connect", success = true });

                        await mqttClient.SubscribeAsync(subscribeTopic);
                        steps.Add(new { step = "subscribe", success = true, topic = subscribeTopic });

                        async Task<object> PublishAndWaitForAck(int msgId, object payload, string stepName, int maxRetries = 3, int timeoutSeconds = 3)
                        {
                            for (int attempt = 1; attempt <= maxRetries; attempt++)
                            {
                                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                                {
                                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                                });

                                var message = new MqttApplicationMessageBuilder()
                                    .WithTopic(publishTopic)
                                    .WithPayload(json)
                                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                                    .Build();

                                var startTime = DateTime.UtcNow;
                                await mqttClient.PublishAsync(message, CancellationToken.None);

                                var timeout = TimeSpan.FromSeconds(timeoutSeconds);
                                while ((DateTime.UtcNow - startTime) < timeout)
                                {
                                    var ack = receivedMessages.FirstOrDefault(m => m.msgId == msgId);

                                    if (ack.msgId == msgId)
                                    {
                                        return new
                                        {
                                            step = stepName,
                                            msgId,
                                            success = true,
                                            published = true,
                                            acknowledged = true,
                                            attempt,
                                            totalAttempts = maxRetries,
                                            topic = publishTopic,
                                            payload = json,
                                            response = new { ack.msgId, ack.payload, ack.timestamp },
                                            message = $"msg_id={msgId} published and acknowledged on attempt {attempt}/{maxRetries}"
                                        };
                                    }

                                    await Task.Delay(50);
                                }

                                if (attempt < maxRetries)
                                {
                                    steps.Add(new
                                    {
                                        step = $"{stepName}_retry",
                                        attempt,
                                        message = $"Attempt {attempt}/{maxRetries} timed out, retrying..."
                                    });
                                    await Task.Delay(500);
                                }
                            }

                            return new
                            {
                                step = stepName,
                                msgId,
                                success = false,
                                published = true,
                                acknowledged = false,
                                attempt = maxRetries,
                                totalAttempts = maxRetries,
                                topic = publishTopic,
                                message = $"msg_id={msgId} published but no acknowledgement received after {maxRetries} attempts (timeout: {timeoutSeconds}s each)"
                            };
                        }

                        // Step 1: Relationship Filter
                        var step1Payload = new
                        {
                            msg_id = 1025,
                            device_info = new { device_id = gatewayMac, mac = gatewayMac },
                            data = new { rule = relationshipRule }
                        };
                        var step1Result = await PublishAndWaitForAck(1025, step1Payload, "1 - Relationship Filter");
                        steps.Add(step1Result);

                        var step1Success = (bool)(step1Result.GetType().GetProperty("acknowledged")?.GetValue(step1Result) ?? false);
                        if (!step1Success)
                        {
                            await mqttClient.DisconnectAsync();
                            steps.Add(new { step = "disconnect_on_failure", reason = "Step 1 failed" });
                            allFlowAttempts.Add(new { attempt = flowAttempt, steps, receivedMessages });

                            if (flowAttempt < maxFlowRetries)
                            {
                                await Task.Delay(1000);
                                continue;
                            }

                            return (object)new
                            {
                                success = false,
                                message = $"Step 1 (msg_id=1025) failed after {maxFlowRetries} flow attempts",
                                gatewayMac,
                                deviceId,
                                publishTopic,
                                subscribeTopic,
                                flowAttempts = allFlowAttempts
                            };
                        }

                        await Task.Delay(500);

                        // Step 2: Duplicate Filter
                        var step2Payload = new
                        {
                            msg_id = 1010,
                            device_info = new { device_id = gatewayMac, mac = gatewayMac },
                            data = new { rule = duplicateRule, time = duplicateTime }
                        };
                        var step2Result = await PublishAndWaitForAck(1010, step2Payload, "2 - Duplicate Filter");
                        steps.Add(step2Result);

                        var step2Success = (bool)(step2Result.GetType().GetProperty("acknowledged")?.GetValue(step2Result) ?? false);
                        if (!step2Success)
                        {
                            await mqttClient.DisconnectAsync();
                            steps.Add(new { step = "disconnect_on_failure", reason = "Step 2 failed" });
                            allFlowAttempts.Add(new { attempt = flowAttempt, steps, receivedMessages });

                            if (flowAttempt < maxFlowRetries)
                            {
                                await Task.Delay(1000);
                                continue;
                            }

                            return (object)new
                            {
                                success = false,
                                message = $"Step 2 (msg_id=1010) failed after {maxFlowRetries} flow attempts",
                                gatewayMac,
                                deviceId,
                                publishTopic,
                                subscribeTopic,
                                flowAttempts = allFlowAttempts
                            };
                        }

                        await Task.Delay(500);

                        // Step 3: MAC Filter
                        object step3Data;
                        if (beaconMacs.Length == 0)
                        {
                            // No beacons - send single zeros MAC
                            step3Data = new
                            {
                                precise = 0,
                                reverse = 0,
                                array_num = 1,
                                rule = new[] { "000000000000" }
                            };
                        }
                        else
                        {
                            // Has beacons - send beacon MACs
                            step3Data = new
                            {
                                precise = 0,
                                reverse = 0,
                                array_num = beaconMacs.Length,
                                rule = beaconMacs
                            };
                        }

                        var step3Payload = new
                        {
                            msg_id = 1028,
                            device_info = new { device_id = gatewayMac, mac = gatewayMac },
                            data = step3Data
                        };
                        var step3Result = await PublishAndWaitForAck(1028, step3Payload, "3 - MAC Filter");
                        steps.Add(step3Result);

                        var step3Success = (bool)(step3Result.GetType().GetProperty("acknowledged")?.GetValue(step3Result) ?? false);
                        if (!step3Success)
                        {
                            await mqttClient.DisconnectAsync();
                            steps.Add(new { step = "disconnect_on_failure", reason = "Step 3 failed" });
                            allFlowAttempts.Add(new { attempt = flowAttempt, steps, receivedMessages });

                            if (flowAttempt < maxFlowRetries)
                            {
                                await Task.Delay(1000);
                                continue;
                            }

                            return (object)new
                            {
                                success = false,
                                message = $"Step 3 (msg_id=1028) failed after {maxFlowRetries} flow attempts",
                                gatewayMac,
                                deviceId,
                                publishTopic,
                                subscribeTopic,
                                flowAttempts = allFlowAttempts
                            };
                        }

                        await mqttClient.DisconnectAsync();
                        steps.Add(new { step = "disconnect", success = true });

                        allFlowAttempts.Add(new { attempt = flowAttempt, steps, receivedMessages });

                        return (object)new
                        {
                            success = true,
                            message = "✅ Gateway registered successfully! All 3 steps completed with acknowledgements.",
                            gatewayMac,
                            deviceId,
                            beaconCount = beaconMacs.Length,
                            beaconMacs = beaconMacs,
                            publishTopic,
                            subscribeTopic,
                            flowAttempt,
                            steps,
                            receivedMessages,
                            allFlowAttempts
                        };
                    }
                    catch (Exception innerEx)
                    {
                        steps.Add(new { step = "error_in_flow", error = innerEx.Message });
                        allFlowAttempts.Add(new { attempt = flowAttempt, steps, receivedMessages, error = innerEx.Message });

                        if (mqttClient != null)
                        {
                            try { await mqttClient.DisconnectAsync(); } catch { }
                        }

                        if (flowAttempt < maxFlowRetries)
                        {
                            await Task.Delay(1000);
                            continue;
                        }

                        throw;
                    }
                }

                return (object)new
                {
                    success = false,
                    message = "Unexpected end of flow",
                    flowAttempts = allFlowAttempts
                };
            }
            finally
            {
                if (mqttClient != null)
                {
                    mqttClient.Dispose();
                }
            }
        }

        private async Task<object> ExecuteBeaconAddFlow(
            string gatewayMac,
            string userId,
            string deviceId,
            string[] beaconMacs,
            List<object> allFlowAttempts)
        {
            var publishTopic = $"iot/stg/{userId}/gw/{deviceId}/down";
            var subscribeTopic = $"iot/stg/{userId}/gw/{deviceId}/up";

            var steps = new List<object>();
            var receivedMessages = new ConcurrentBag<(int? msgId, string payload, DateTime timestamp)>();
            IMqttClient? mqttClient = null;

            const int maxFlowRetries = 2;

            try
            {
                for (int flowAttempt = 1; flowAttempt <= maxFlowRetries; flowAttempt++)
                {
                    steps = new List<object>();
                    receivedMessages = new ConcurrentBag<(int? msgId, string payload, DateTime timestamp)>();

                    try
                    {
                        var factory = new MqttFactory();
                        mqttClient?.Dispose();
                        mqttClient = factory.CreateMqttClient();

                        mqttClient.ApplicationMessageReceivedAsync += e =>
                        {
                            var topic = e.ApplicationMessage.Topic;
                            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

                            try
                            {
                                var json = JsonSerializer.Deserialize<JsonDocument>(payload);
                                var msgId = json?.RootElement.GetProperty("msg_id").GetInt32();
                                receivedMessages.Add((msgId, payload, DateTime.UtcNow));
                                _logger.LogInformation($"Beacon flow: Received msg_id={msgId} on topic {topic}");
                            }
                            catch
                            {
                                receivedMessages.Add((null, payload, DateTime.UtcNow));
                                _logger.LogWarning($"Beacon flow: Failed to parse msg_id from: {payload}");
                            }

                            return Task.CompletedTask;
                        };

                        steps.Add(new
                        {
                            step = "flow_attempt",
                            attempt = flowAttempt,
                            maxAttempts = maxFlowRetries,
                            message = $"Starting beacon add flow attempt {flowAttempt}/{maxFlowRetries}"
                        });

                        var clientOptions = new MqttClientOptionsBuilder()
                            .WithTcpServer(_mqttConfig.Host, _mqttConfig.Port)
                            .WithCredentials(_mqttConfig.Username, _mqttConfig.Password)
                            .WithClientId($"api-test-{Guid.NewGuid()}")
                            .WithTlsOptions(o =>
                            {
                                o.UseTls(_mqttConfig.UseTls);
                                o.WithCertificateValidationHandler(_ => true);
                            })
                            .Build();

                        await mqttClient.ConnectAsync(clientOptions, CancellationToken.None);
                        steps.Add(new { step = "connect", success = true, message = $"Connected to {_mqttConfig.Host}:{_mqttConfig.Port}" });

                        await Task.Delay(1000);

                        await mqttClient.SubscribeAsync(subscribeTopic);
                        steps.Add(new { step = "subscribe", success = true, topic = subscribeTopic });

                        async Task<object> PublishAndWaitForAck(int msgId, object payload, string stepName, int maxRetries = 3, int timeoutSeconds = 3)
                        {
                            for (int attempt = 1; attempt <= maxRetries; attempt++)
                            {
                                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                                {
                                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                                });

                                var message = new MqttApplicationMessageBuilder()
                                    .WithTopic(publishTopic)
                                    .WithPayload(json)
                                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                                    .Build();

                                var startTime = DateTime.UtcNow;
                                await mqttClient.PublishAsync(message, CancellationToken.None);

                                var timeout = TimeSpan.FromSeconds(timeoutSeconds);
                                while ((DateTime.UtcNow - startTime) < timeout)
                                {
                                    var ack = receivedMessages.FirstOrDefault(m => m.msgId == msgId);

                                    if (ack.msgId == msgId)
                                    {
                                        return new
                                        {
                                            step = stepName,
                                            msgId,
                                            success = true,
                                            published = true,
                                            acknowledged = true,
                                            attempt,
                                            totalAttempts = maxRetries,
                                            topic = publishTopic,
                                            payload = json,
                                            response = new { ack.msgId, ack.payload, ack.timestamp },
                                            message = $"msg_id={msgId} published and acknowledged on attempt {attempt}/{maxRetries}"
                                        };
                                    }

                                    await Task.Delay(50);
                                }

                                if (attempt < maxRetries)
                                {
                                    steps.Add(new
                                    {
                                        step = $"{stepName}_retry",
                                        attempt,
                                        message = $"Attempt {attempt}/{maxRetries} timed out, retrying..."
                                    });
                                    await Task.Delay(500);
                                }
                            }

                            return new
                            {
                                step = stepName,
                                msgId,
                                success = false,
                                published = true,
                                acknowledged = false,
                                attempt = maxRetries,
                                totalAttempts = maxRetries,
                                topic = publishTopic,
                                message = $"msg_id={msgId} published but no acknowledgement received after {maxRetries} attempts (timeout: {timeoutSeconds}s each)"
                            };
                        }

                        // Only Step 3: MAC Filter
                        object step3Data;
                        if (beaconMacs.Length == 0)
                        {
                            // No beacons - send single zeros MAC
                            step3Data = new
                            {
                                precise = 0,
                                reverse = 0,
                                array_num = 1,
                                rule = new[] { "000000000000" }
                            };
                        }
                        else
                        {
                            // Has beacons - send beacon MACs
                            step3Data = new
                            {
                                precise = 0,
                                reverse = 0,
                                array_num = beaconMacs.Length,
                                rule = beaconMacs
                            };
                        }

                        var step3Payload = new
                        {
                            msg_id = 1028,
                            device_info = new { device_id = gatewayMac, mac = gatewayMac },
                            data = step3Data
                        };
                        var step3Result = await PublishAndWaitForAck(1028, step3Payload, "3 - MAC Filter");
                        steps.Add(step3Result);

                        var step3Success = (bool)(step3Result.GetType().GetProperty("acknowledged")?.GetValue(step3Result) ?? false);
                        if (!step3Success)
                        {
                            await mqttClient.DisconnectAsync();
                            steps.Add(new { step = "disconnect_on_failure", reason = "Step 3 failed" });
                            allFlowAttempts.Add(new { attempt = flowAttempt, steps, receivedMessages });

                            if (flowAttempt < maxFlowRetries)
                            {
                                await Task.Delay(1000);
                                continue;
                            }

                            return new
                            {
                                success = false,
                                message = $"Step 3 (msg_id=1028) failed after {maxFlowRetries} flow attempts",
                                gatewayMac,
                                deviceId,
                                publishTopic,
                                subscribeTopic,
                                flowAttempts = allFlowAttempts
                            };
                        }

                        await mqttClient.DisconnectAsync();
                        steps.Add(new { step = "disconnect", success = true });

                        allFlowAttempts.Add(new { attempt = flowAttempt, steps, receivedMessages });

                        return new
                        {
                            success = true,
                            message = "✅ Beacon added successfully! Step 3 (MAC Filter) completed with acknowledgement.",
                            gatewayMac,
                            deviceId,
                            beaconCount = beaconMacs.Length,
                            beaconMacs = beaconMacs,
                            publishTopic,
                            subscribeTopic,
                            flowAttempt,
                            steps,
                            receivedMessages
                        };
                    }
                    catch (Exception innerEx)
                    {
                        steps.Add(new { step = "error_in_flow", error = innerEx.Message });
                        allFlowAttempts.Add(new { attempt = flowAttempt, steps, receivedMessages, error = innerEx.Message });

                        if (mqttClient != null)
                        {
                            try { await mqttClient.DisconnectAsync(); } catch { }
                        }

                        if (flowAttempt < maxFlowRetries)
                        {
                            await Task.Delay(1000);
                            continue;
                        }

                        throw;
                    }
                }

                return new
                {
                    success = false,
                    message = "Unexpected end of flow",
                    flowAttempts = allFlowAttempts
                };
            }
            finally
            {
                if (mqttClient != null)
                {
                    mqttClient.Dispose();
                }
            }
        }
    }

    public class ApplyGatewayFilterRequest
    {
        /// <summary>The database ID of the device (gateway or beacon).</summary>
        public Guid DeviceId { get; set; }
    }
}