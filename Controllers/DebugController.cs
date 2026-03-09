using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using OcufiiAPI.Configs;
using OcufiiAPI.Data;
using OcufiiAPI.Enums;
using OcufiiAPI.Models;
using OcufiiAPI.Services;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace OcufiiAPI.Controllers
{
    [ApiController]
    [Route("api/debug")]
    public class DebugController : ControllerBase
    {
        private readonly OcufiiDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly MqttConfig _mqttConfig;
        private readonly IEmailService _emailService;
        private readonly INotificationService _notificationService;

        public DebugController(OcufiiDbContext db, IWebHostEnvironment env, IOptions<SnoozeReasonConfig> snoozeConfig, IOptions<MqttConfig> mqttConfig, IEmailService emailService,
            INotificationService notificationService)
        {
            _db = db;
            _env = env;
            _mqttConfig = mqttConfig.Value;
            _emailService = emailService;
            _notificationService = notificationService;
        }

        private bool AllowAll => _env.IsDevelopment() ||
                                 _env.EnvironmentName.Equals("TestDevWeb", StringComparison.OrdinalIgnoreCase);

        [HttpGet("tables")]
        public IActionResult GetTables()
        {
            if (!AllowAll) return Forbid();
            var tables = _db.Model.GetEntityTypes()
                .Select(t => t.GetTableName())
                .Where(n => n != null)
                .OrderBy(n => n)
                .ToList();
            return Ok(new { count = tables.Count, tables });
        }

        [HttpPost("test-gateway-filter")]
        public async Task<IActionResult> TestGatewayFilterWithAcknowledgement([FromBody] TestGatewayFilterRequest request)
        {
            if (!AllowAll) return Forbid();

            // Hard-coded configuration values
            const int relationshipRule = 1;
            const int duplicateRule = 1;
            const int duplicateTime = 10;

            var cleanMac = request.Mac.Replace(":", "").Replace("-", "").ToUpperInvariant();
            var allFlowAttempts = new List<object>();

            try
            {
                switch (request.Type)
                {
                    case DeviceTypeEnum.Gateway:
                        {
                            // Gateway registration - find all beacons for this gateway's user
                            var gatewayDevice = await _db.Devices
                                .Include(d => d.DeviceType)
                                .FirstOrDefaultAsync(d => d.MacAddress == cleanMac && d.DeviceType.Key == "gateway" && d.IsDeleted == false);

                            if (gatewayDevice == null)
                            {
                                return Ok(new
                                {
                                    success = false,
                                    message = $"Gateway with MAC {cleanMac} not found in database"
                                });
                            }

                            // Find all beacons for this user
                            var beaconDevices = await _db.Devices
                                .Include(d => d.DeviceType)
                                .Where(d => d.UserId == gatewayDevice.UserId && d.DeviceType.Key == "beacon" && d.IsDeleted == false)
                                .ToListAsync();

                            var beaconMacs = beaconDevices
                                .Select(b => b.MacAddress.Replace(":", "").Replace("-", "").ToUpperInvariant())
                                .ToArray();

                            // Execute gateway registration flow (Steps 1, 2, 3)
                            var result = await ExecuteGatewayFlow(
                                gatewayMac: cleanMac,
                                beaconMacs: beaconMacs,
                                relationshipRule: relationshipRule,
                                duplicateRule: duplicateRule,
                                duplicateTime: duplicateTime,
                                allFlowAttempts: allFlowAttempts
                            );

                            return result;
                        }

                    case DeviceTypeEnum.Beacon:
                        {
                            // Beacon addition - find all gateways for this beacon's user
                            var beaconDevice = await _db.Devices
                                .Include(d => d.DeviceType)
                                .FirstOrDefaultAsync(d => d.MacAddress == cleanMac && d.DeviceType.Key == "beacon" && d.IsDeleted == false);

                            if (beaconDevice == null)
                            {
                                return Ok(new
                                {
                                    success = false,
                                    message = $"Beacon with MAC {cleanMac} not found in database"
                                });
                            }

                            // Find all gateways for this user
                            var gatewayDevices = await _db.Devices
                                .Include(d => d.DeviceType)
                                .Where(d => d.UserId == beaconDevice.UserId && d.DeviceType.Key == "gateway" && d.IsDeleted == false)
                                .ToListAsync();

                            if (gatewayDevices.Count == 0)
                            {
                                return Ok(new
                                {
                                    success = false,
                                    message = "No gateways found for this beacon"
                                });
                            }

                            var gatewayResults = new List<object>();

                            // For each gateway, execute Step 3 with all beacons for this user
                            foreach (var gateway in gatewayDevices)
                            {
                                var gatewayMac = gateway.MacAddress.Replace(":", "").Replace("-", "").ToUpperInvariant();

                                // Find all beacons for this user (same as beaconDevice.UserId)
                                var beaconsForUser = await _db.Devices
                                    .Include(d => d.DeviceType)
                                    .Where(d => d.UserId == beaconDevice.UserId && d.DeviceType.Key == "beacon" && d.IsDeleted == false)
                                    .ToListAsync();

                                var beaconMacs = beaconsForUser
                                    .Select(b => b.MacAddress.Replace(":", "").Replace("-", "").ToUpperInvariant())
                                    .ToArray();

                                // Execute only Step 3 for this gateway
                                var result = await ExecuteBeaconAddFlow(
                                    gatewayMac: gatewayMac,
                                    beaconMacs: beaconMacs,
                                    allFlowAttempts: allFlowAttempts
                                );

                                gatewayResults.Add(new
                                {
                                    gateway = gatewayMac,
                                    result
                                });
                            }

                            return Ok(new
                            {
                                success = true,
                                message = $"Beacon added to {gatewayDevices.Count} gateway(s)",
                                beaconMac = cleanMac,
                                gatewayResults
                            });
                        }

                    case DeviceTypeEnum.SafetyCard:
                        // Safety card handling - to be implemented
                        return Ok(new
                        {
                            success = false,
                            message = "SafetyCard handling is not yet implemented"
                        });

                    default:
                        return BadRequest(new
                        {
                            success = false,
                            message = "Invalid device type specified"
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

        [HttpPost("test-email")]
        [SwaggerOperation(Summary = "Test Email Sending")]
        public async Task<IActionResult> TestEmail([FromBody] TestEmailRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ToEmail))
                return BadRequest("ToEmail is required");

            try
            {
                await _emailService.SendEmailAsync(
                    toEmail: request.ToEmail,
                    subject: "Ocufii V2 Test Email",
                    body: $"<h1>Hello from Ocufii V2 Platform!</h1><p>This is a test email sent at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC.</p><p>Service is working correctly.</p>",
                    isHtml: true
                );

                return Ok(new { message = $"Test email sent to {request.ToEmail}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Test FCM Notification Service - sends to a single device token
        /// </summary>
        /// <remarks>
        /// Paste a real FCM token from your mobile app (Android/iOS).
        /// You can get tokens from logs or your DeviceToken table.
        /// </remarks>
        [HttpPost("test-notification")]
        [SwaggerOperation(Summary = "Test FCM Notification")]
        public async Task<IActionResult> TestNotification([FromBody] TestNotificationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DeviceToken))
                return BadRequest("DeviceToken is required");

            try
            {
                await _notificationService.SendNotificationAsync(
                    deviceToken: request.DeviceToken,
                    title: "Ocufii Test Notification",
                    body: $"This is a test push from server at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC.",
                    data: new Dictionary<string, string>
                    {
                        { "testKey", "testValue" },
                        { "debug", "true" }
                    }
                );

                return Ok(new { message = $"Test notification sent to token: {request.DeviceToken}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private async Task<IActionResult> ExecuteGatewayFlow(
            string gatewayMac,
            string[] beaconMacs,
            int relationshipRule,
            int duplicateRule,
            int duplicateTime,
            List<object> allFlowAttempts)
        {
            var deviceId = $"test{gatewayMac}";
            var publishTopic = $"MINI-02-58B6/{deviceId}/app_to_device";
            var subscribeTopic = $"MINI-02-58B6/test567/device_to_app";

            var steps = new List<object>();
            var receivedMessages = new List<object>();
            IMqttClient? mqttClient = null;

            const int maxFlowRetries = 2;

            try
            {
                for (int flowAttempt = 1; flowAttempt <= maxFlowRetries; flowAttempt++)
                {
                    steps = new List<object>();
                    receivedMessages = new List<object>();

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

                                receivedMessages.Add(new
                                {
                                    topic,
                                    msgId,
                                    payload,
                                    timestamp = DateTime.UtcNow
                                });
                            }
                            catch
                            {
                                receivedMessages.Add(new { topic, payload, error = "Failed to parse msg_id" });
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
                        steps.Add(new { step = "wait_after_connect", success = true });

                        await mqttClient.SubscribeAsync(subscribeTopic);
                        steps.Add(new { step = "subscribe", success = true, topic = subscribeTopic });

                        async Task<object> PublishAndWaitForAck(int msgId, object payload, string stepName, int maxRetries = 3, int timeoutSeconds = 3)
                        {
                            for (int attempt = 1; attempt <= maxRetries; attempt++)
                            {
                                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                                {
                                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
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
                                    var ack = receivedMessages.FirstOrDefault(m =>
                                    {
                                        var msgIdProp = m.GetType().GetProperty("msgId");
                                        return msgIdProp != null && (int?)msgIdProp.GetValue(m) == msgId;
                                    });

                                    if (ack != null)
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
                                            response = ack,
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
                            device_info = new { device_id = deviceId, mac = gatewayMac },
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

                            return Ok(new
                            {
                                success = false,
                                message = $"Step 1 (msg_id=1025) failed after {maxFlowRetries} flow attempts",
                                gatewayMac,
                                deviceId,
                                publishTopic,
                                subscribeTopic,
                                flowAttempts = allFlowAttempts
                            });
                        }

                        await Task.Delay(500);

                        // Step 2: Duplicate Filter
                        var step2Payload = new
                        {
                            msg_id = 1010,
                            device_info = new { device_id = deviceId, mac = gatewayMac },
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

                            return Ok(new
                            {
                                success = false,
                                message = $"Step 2 (msg_id=1010) failed after {maxFlowRetries} flow attempts",
                                gatewayMac,
                                deviceId,
                                publishTopic,
                                subscribeTopic,
                                flowAttempts = allFlowAttempts
                            });
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
                            device_info = new { device_id = deviceId, mac = gatewayMac },
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

                            return Ok(new
                            {
                                success = false,
                                message = $"Step 3 (msg_id=1028) failed after {maxFlowRetries} flow attempts",
                                gatewayMac,
                                deviceId,
                                publishTopic,
                                subscribeTopic,
                                flowAttempts = allFlowAttempts
                            });
                        }

                        await mqttClient.DisconnectAsync();
                        steps.Add(new { step = "disconnect", success = true });

                        allFlowAttempts.Add(new { attempt = flowAttempt, steps, receivedMessages });

                        return Ok(new
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
                        });
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

                return Ok(new
                {
                    success = false,
                    message = "Unexpected end of flow",
                    flowAttempts = allFlowAttempts
                });
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
            string[] beaconMacs,
            List<object> allFlowAttempts)
        {
            var deviceId = $"test{gatewayMac}";
            var publishTopic = $"MINI-02-58B6/{deviceId}/app_to_device";
            var subscribeTopic = $"MINI-02-58B6/test567/device_to_app";

            var steps = new List<object>();
            var receivedMessages = new List<object>();
            IMqttClient? mqttClient = null;

            const int maxFlowRetries = 2;

            try
            {
                for (int flowAttempt = 1; flowAttempt <= maxFlowRetries; flowAttempt++)
                {
                    steps = new List<object>();
                    receivedMessages = new List<object>();

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

                                receivedMessages.Add(new
                                {
                                    topic,
                                    msgId,
                                    payload,
                                    timestamp = DateTime.UtcNow
                                });
                            }
                            catch
                            {
                                receivedMessages.Add(new { topic, payload, error = "Failed to parse msg_id" });
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
                                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
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
                                    var ack = receivedMessages.FirstOrDefault(m =>
                                    {
                                        var msgIdProp = m.GetType().GetProperty("msgId");
                                        return msgIdProp != null && (int?)msgIdProp.GetValue(m) == msgId;
                                    });

                                    if (ack != null)
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
                                            response = ack,
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
                            device_info = new { device_id = deviceId, mac = gatewayMac },
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

    public enum DeviceTypeEnum
    {
        Gateway = 1,
        Beacon = 2,
        SafetyCard = 3
    }

    public class TestEmailRequest
    {
        public string ToEmail { get; set; } = string.Empty;
    }

    public class TestNotificationRequest
    {
        public string DeviceToken { get; set; } = string.Empty;
    }

    public class TestGatewayFilterRequest
    {
        public string Mac { get; set; } = string.Empty;
        public DeviceTypeEnum Type { get; set; }
    }
}