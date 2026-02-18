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
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OcufiiAPI.Controllers;

[ApiController]
[Route("devices")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly OcufiiDbContext _db;
    private readonly ILogger<DevicesController> _logger;
    private readonly MqttConfig _mqttConfig;

    public DevicesController(OcufiiDbContext db, ILogger<DevicesController> logger, IOptions<MqttConfig> mqttConfig)
    {
        _db = db;
        _logger = logger;
        _mqttConfig = mqttConfig.Value;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse>> ListDevices(
        [FromQuery] string? type,
        [FromQuery] bool? isEnabled,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 50;

        var query = _db.Devices
            .Include(d => d.DeviceType)
            .Where(d => !d.IsDeleted)
            .AsNoTracking();

        var currentUserId = User.GetUserId();
        var isAdmin = User.IsInRole("admin");

        if (!isAdmin)
        {
            query = query.Where(d => d.UserId == currentUserId || d.UserId == null);
        }

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(d => d.DeviceType.Key == type.ToLowerInvariant());

        if (isEnabled.HasValue)
            query = query.Where(d => d.IsEnabled == isEnabled.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLowerInvariant();
            query = query.Where(d =>
                (d.Name != null && d.Name.ToLower().Contains(s)) ||
                d.MacAddress.ToLower().Contains(s));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                d.Id,
                type = d.DeviceType.Key,
                d.MacAddress,
                d.Name,
                d.Location,
                d.IsEnabled,
                d.IsDeleted,
                Attributes = d.Attributes,
                d.CreatedAt,
                d.UpdatedAt
            })
            .ToListAsync();

        return Ok(new ApiResponse(true, "Devices retrieved successfully")
        {
            Data = new { items, total, page, pageSize }
        });
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse>> CreateDevice([FromBody] JsonElement body)
    {
        if (!body.TryGetProperty("type", out var typeProp) || string.IsNullOrWhiteSpace(typeProp.GetString()))
            return BadRequest(new ApiResponse(false, "Missing or invalid 'type'"));

        var typeKey = typeProp.GetString()!.Trim().ToLowerInvariant();
        var deviceType = await _db.DeviceTypes.FirstOrDefaultAsync(dt => dt.Key == typeKey);
        if (deviceType == null)
            return BadRequest(new ApiResponse(false, "Invalid device type"));

        if (!body.TryGetProperty("macAddress", out var macProp) || string.IsNullOrWhiteSpace(macProp.GetString()))
            return BadRequest(new ApiResponse(false, "Missing or invalid 'macAddress'"));

        var macAddress = macProp.GetString()!.Trim();
        var normalizedMac = macAddress.ToUpperInvariant().Replace(":", "");

        var activeDevice = await _db.Devices.FirstOrDefaultAsync(d =>
            d.MacAddress.ToUpper() == normalizedMac && !d.IsDeleted);

        if (activeDevice != null)
            return Conflict(new ApiResponse(false, "MacAddress already exists and is active"));

        var currentUserId = User.GetUserId();
        var tenantIdClaim = User.FindFirst("tenant_id")?.Value
                            ?? Guid.Parse("00000000-0000-0000-0000-000000000001").ToString();

        var softDeletedDevice = await _db.Devices.FirstOrDefaultAsync(d =>
            d.MacAddress.ToUpper() == normalizedMac && d.IsDeleted);

        Device device;

        if (softDeletedDevice != null)
        {
            device = softDeletedDevice;
            device.IsDeleted = false;
            device.IsEnabled = true;
            device.UserId = currentUserId;
            device.TenantId = Guid.Parse(tenantIdClaim);
            device.UpdatedAt = DateTime.UtcNow;

            device.Name = body.TryGetProperty("name", out var n) ? n.GetString()?.Trim() : device.Name;
            device.Location = body.TryGetProperty("location", out var l) ? l.GetString()?.Trim() : device.Location;
            device.Attributes = body.TryGetProperty("attributes", out var a) ? a.ToString() : device.Attributes ?? "{}";
        }
        else
        {
            device = new Device
            {
                Id = Guid.NewGuid(),
                DeviceTypeId = deviceType.Id,
                MacAddress = normalizedMac,
                Name = body.TryGetProperty("name", out var n) ? n.GetString()?.Trim() : null,
                Location = body.TryGetProperty("location", out var l) ? l.GetString()?.Trim() : null,
                Information = body.TryGetProperty("information", out var i) ? i.GetString()?.Trim() : null,
                Attributes = body.TryGetProperty("attributes", out var a) ? a.ToString() : "{}",
                UserId = currentUserId,
                TenantId = Guid.Parse(tenantIdClaim),
                IsEnabled = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Devices.Add(device);
        }

        await _db.SaveChangesAsync();

        return Created($"/devices/{device.Id}", new ApiResponse(true, "Device created/reused successfully")
        {
            Data = new { deviceId = device.Id }
        });
    }

    [HttpGet("{deviceId:guid}")]
    public async Task<ActionResult<ApiResponse>> GetDevice(Guid deviceId)
    {
        var device = await _db.Devices
            .Include(d => d.DeviceType)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId && !d.IsDeleted);

        if (device == null)
            return NotFound(new ApiResponse(false, "Device not found"));

        return Ok(new ApiResponse(true, "Device retrieved successfully")
        {
            Data = new
            {
                device.Id,
                type = device.DeviceType.Key,
                device.MacAddress,
                device.Name,
                device.Location,
                device.IsEnabled,
                device.IsDeleted,
                Attributes = device.Attributes,
                device.CreatedAt,
                device.UpdatedAt
            }
        });
    }

    [HttpPatch("{deviceId:guid}")]
    public async Task<ActionResult<ApiResponse>> UpdateDevice(Guid deviceId, [FromBody] JsonElement payload)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId && !d.IsDeleted);
        if (device == null)
            return NotFound(new ApiResponse(false, "Device not found"));

        if (payload.ValueKind != JsonValueKind.Object)
            return BadRequest(new ApiResponse(false, "Invalid JSON payload"));

        foreach (var prop in payload.EnumerateObject())
        {
            switch (prop.Name.ToLowerInvariant())
            {
                case "name": device.Name = prop.Value.GetString()?.Trim(); break;
                case "location": device.Location = prop.Value.GetString()?.Trim(); break;
                case "isenabled":
                    if (prop.Value.ValueKind == JsonValueKind.True) device.IsEnabled = true;
                    else if (prop.Value.ValueKind == JsonValueKind.False) device.IsEnabled = false;
                    break;
                case "attributes": device.Attributes = prop.Value.ToString(); break;
            }
        }

        device.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Device updated successfully"));
    }

    [HttpDelete("{deviceId:guid}")]
    public async Task<ActionResult<ApiResponse>> DeleteDevice(Guid deviceId)
    {
        var device = await _db.Devices
            .Include(d => d.DeviceType)
            .FirstOrDefaultAsync(d => d.Id == deviceId && !d.IsDeleted);
            
        if (device == null)
            return NotFound(new ApiResponse(false, "Device not found"));

        var deviceTypeKey = device.DeviceType.Key.ToLowerInvariant();
        
        // MQTT communication for gateway or beacon deletion
        if (deviceTypeKey == "gateway" || deviceTypeKey == "beacon")
        {
            var mqttSuccess = await SendDeviceDeletionMqttMessage(device);
            
            if (!mqttSuccess)
            {
                return StatusCode(500, new ApiResponse(false, "Failed to communicate with device via MQTT after multiple retries"));
            }
        }

        // If MQTT succeeded or device is not gateway/beacon, proceed with soft delete
        device.IsDeleted = true;
        device.IsEnabled = false;
        device.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Device deleted successfully"));
    }

    private async Task<bool> SendDeviceDeletionMqttMessage(Device device)
    {
        var cleanMac = device.MacAddress.Replace(":", "").Replace("-", "").ToUpperInvariant();

        if (device.DeviceType.Key.ToLowerInvariant() == "gateway")
        {
            // Gateway deletion - send msg_id=1001 for reset
            return await SendGatewayResetMessage(cleanMac);
        }
        else if (device.DeviceType.Key.ToLowerInvariant() == "beacon")
        {
            // Beacon deletion - send msg_id=1028 with remaining beacons to all gateways
            return await SendBeaconDeletionToGateways(device);
        }

        return true;
    }

    private async Task<bool> SendGatewayResetMessage(string gatewayMac)
    {
        const int maxRetries = 2;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            IMqttClient? mqttClient = null;

            try
            {
                var factory = new MqttFactory();
                mqttClient?.Dispose();
                mqttClient = factory.CreateMqttClient();

                var deviceId = $"test{gatewayMac}";
                var publishTopic = $"MINI-02-58B6/{deviceId}/app_to_device";
                var subscribeTopic = $"MINI-02-58B6/test567/device_to_app";

                var receivedMessages = new ConcurrentBag<(int? msgId, string payload, DateTime timestamp)>();

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

                var clientOptions = new MqttClientOptionsBuilder()
                    .WithTcpServer(_mqttConfig.Host, _mqttConfig.Port)
                    .WithCredentials(_mqttConfig.Username, _mqttConfig.Password)
                    .WithClientId($"api-delete-{Guid.NewGuid()}")
                    .WithTlsOptions(o =>
                    {
                        o.UseTls(_mqttConfig.UseTls);
                        o.WithCertificateValidationHandler(_ => true);
                    })
                    .Build();

                await mqttClient.ConnectAsync(clientOptions, CancellationToken.None);
                await Task.Delay(1000);

                await mqttClient.SubscribeAsync(subscribeTopic);

                var payload = new
                {
                    msg_id = 1001,
                    device_info = new { device_id = deviceId, mac = gatewayMac }
                };

                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(publishTopic)
                    .WithPayload(json)
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();

                _logger.LogInformation($"Gateway {gatewayMac}: Publishing reset message (msg_id=1001)");
                await mqttClient.PublishAsync(message, CancellationToken.None);

                // Wait for acknowledgment - filter only for msg_id=1001
                var startTime = DateTime.UtcNow;
                var timeout = TimeSpan.FromSeconds(3);

                while ((DateTime.UtcNow - startTime) < timeout)
                {
                    var ack = receivedMessages.FirstOrDefault(m => m.msgId == 1001);

                    if (ack.msgId == 1001)
                    {
                        _logger.LogInformation($"Gateway {gatewayMac}: Received acknowledgment for msg_id=1001 (attempt {attempt}/{maxRetries})");
                        await mqttClient.DisconnectAsync();
                        return true;
                    }

                    await Task.Delay(50);
                }

                _logger.LogWarning($"Gateway {gatewayMac}: No acknowledgment for msg_id=1001 within timeout (attempt {attempt}/{maxRetries}). Received {receivedMessages.Count} total messages.");
                await mqttClient.DisconnectAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Gateway reset MQTT attempt {attempt}/{maxRetries} failed for {gatewayMac}");
            }
            finally
            {
                mqttClient?.Dispose();
            }

            if (attempt < maxRetries)
            {
                await Task.Delay(1000);
            }
        }

        return false;
    }

    private async Task<bool> SendBeaconDeletionToGateways(Device beaconDevice)
    {
        var userId = beaconDevice.UserId;

        // Get all beacons for this user except the one being deleted
        var remainingBeacons = await _db.Devices
            .Include(d => d.DeviceType)
            .Where(d => d.UserId == userId && d.DeviceType.Key == "beacon" && d.Id != beaconDevice.Id && !d.IsDeleted)
            .ToListAsync();

        var beaconMacs = remainingBeacons
            .Select(b => b.MacAddress.Replace(":", "").Replace("-", "").ToUpperInvariant())
            .ToArray();

        // Get all gateways for this user
        var gateways = await _db.Devices
            .Include(d => d.DeviceType)
            .Where(d => d.UserId == userId && d.DeviceType.Key == "gateway" && !d.IsDeleted)
            .ToListAsync();

        if (gateways.Count == 0)
        {
            // No gateways, no need to update
            return true;
        }

        int successCount = 0;
        int failureCount = 0;

        // For each gateway, send update message with retry mechanism
        foreach (var gateway in gateways)
        {
            var gatewayMac = gateway.MacAddress.Replace(":", "").Replace("-", "").ToUpperInvariant();
            
            bool gatewaySuccess = await SendBeaconListToGateway(gatewayMac, beaconMacs);
            
            if (gatewaySuccess)
            {
                successCount++;
                _logger.LogInformation($"Successfully updated gateway {gatewayMac} for beacon deletion");
            }
            else
            {
                failureCount++;
                _logger.LogWarning($"Failed to update gateway {gatewayMac} for beacon deletion");
            }
        }

        // Consider it success if at least one gateway was updated successfully
        if (successCount > 0)
        {
            _logger.LogInformation($"Beacon deletion: {successCount}/{gateways.Count} gateways updated successfully");
            return true;
        }

        return false;
    }

    private async Task<bool> SendBeaconListToGateway(string gatewayMac, string[] beaconMacs)
    {
        const int maxRetries = 2;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            IMqttClient? mqttClient = null;

            try
            {
                var factory = new MqttFactory();
                mqttClient?.Dispose();
                mqttClient = factory.CreateMqttClient();

                var deviceId = $"test{gatewayMac}";
                var publishTopic = $"MINI-02-58B6/{deviceId}/app_to_device";
                var subscribeTopic = $"MINI-02-58B6/test567/device_to_app";

                var receivedMessages = new ConcurrentBag<(int? msgId, string payload, DateTime timestamp)>();

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

                var clientOptions = new MqttClientOptionsBuilder()
                    .WithTcpServer(_mqttConfig.Host, _mqttConfig.Port)
                    .WithCredentials(_mqttConfig.Username, _mqttConfig.Password)
                    .WithClientId($"api-delete-{Guid.NewGuid()}")
                    .WithTlsOptions(o =>
                    {
                        o.UseTls(_mqttConfig.UseTls);
                        o.WithCertificateValidationHandler(_ => true);
                    })
                    .Build();

                await mqttClient.ConnectAsync(clientOptions, CancellationToken.None);
                await Task.Delay(1000);

                await mqttClient.SubscribeAsync(subscribeTopic);

                object step3Data;
                if (beaconMacs.Length == 0)
                {
                    // No remaining beacons - send zeros
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
                    // Has remaining beacons
                    step3Data = new
                    {
                        precise = 0,
                        reverse = 0,
                        array_num = beaconMacs.Length,
                        rule = beaconMacs
                    };
                }

                var payload = new
                {
                    msg_id = 1028,
                    device_info = new { device_id = deviceId, mac = gatewayMac },
                    data = step3Data
                };

                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(publishTopic)
                    .WithPayload(json)
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();

                _logger.LogInformation($"Gateway {gatewayMac}: Publishing beacon list update (msg_id=1028) with {beaconMacs.Length} beacons");
                await mqttClient.PublishAsync(message, CancellationToken.None);

                // Wait for acknowledgment - filter only for msg_id=1028
                var startTime = DateTime.UtcNow;
                var timeout = TimeSpan.FromSeconds(3);

                while ((DateTime.UtcNow - startTime) < timeout)
                {
                    var ack = receivedMessages.FirstOrDefault(m => m.msgId == 1028);

                    if (ack.msgId == 1028)
                    {
                        _logger.LogInformation($"Gateway {gatewayMac}: Received acknowledgment for msg_id=1028 (attempt {attempt}/{maxRetries})");
                        await mqttClient.DisconnectAsync();
                        return true;
                    }

                    await Task.Delay(50);
                }

                _logger.LogWarning($"Gateway {gatewayMac}: No acknowledgment for msg_id=1028 within timeout (attempt {attempt}/{maxRetries}). Received {receivedMessages.Count} total messages.");
                await mqttClient.DisconnectAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Beacon list update MQTT attempt {attempt}/{maxRetries} failed for gateway {gatewayMac}");
            }
            finally
            {
                mqttClient?.Dispose();
            }

            if (attempt < maxRetries)
            {
                await Task.Delay(1000);
            }
        }

        return false;
    }

    [HttpPost("{deviceId:guid}/credentials")]
    public async Task<ActionResult<ApiResponse>> IssueCredentials(Guid deviceId, [FromBody] IssueCredentialsRequest? request)
    {
        request ??= new IssueCredentialsRequest();

        var device = await _db.Devices
            .Include(d => d.DeviceType)
            .FirstOrDefaultAsync(d => d.Id == deviceId && !d.IsDeleted);

        if (device == null)
            return NotFound(new ApiResponse(false, "Device not found"));
        if (!device.DeviceType.ConnectsToMqtt)
            return BadRequest(new ApiResponse(false, "This device type does not support MQTT"));

        var existing = await _db.DeviceCredentials.FirstOrDefaultAsync(c => c.DeviceId == deviceId);

        if (existing != null && !request.Regenerate)
            return Conflict(new ApiResponse(false, "Credentials already exist. Use regenerate=true"));

        if (existing != null) _db.DeviceCredentials.Remove(existing);

        var username = $"{device.DeviceType.Key}_{device.MacAddress.Replace(":", "").ToLowerInvariant()}";
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var cred = new DeviceCredential
        {
            DeviceId = device.Id,
            MqttUsername = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            IsEnabled = true,
            LastRotatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.DeviceCredentials.Add(cred);
        await _db.SaveChangesAsync();

        return Created("", new ApiResponse(true, "Credentials issued successfully")
        {
            Data = new { mqttUsername = username, mqttPassword = password }
        });
    }

    [HttpGet("{deviceId:guid}/credentials")]
    //[Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse>> GetCredentialsMetadata(Guid deviceId)
    {
        var cred = await _db.DeviceCredentials.FirstOrDefaultAsync(c => c.DeviceId == deviceId);
        if (cred == null)
            return NotFound(new ApiResponse(false, "Credentials not found"));

        return Ok(new ApiResponse(true, "Credentials retrieved")
        {
            Data = new
            {
                cred.MqttUsername,
                cred.IsEnabled,
                cred.LastRotatedAt
            }
        });
    }

    [HttpDelete("{deviceId:guid}/credentials")]
    public async Task<ActionResult<ApiResponse>> RevokeCredentials(Guid deviceId)
    {
        var cred = await _db.DeviceCredentials.FirstOrDefaultAsync(c => c.DeviceId == deviceId);
        if (cred == null)
            return NotFound(new ApiResponse(false, "Credentials not found"));

        _db.DeviceCredentials.Remove(cred);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Credentials revoked successfully"));
    }

    [HttpPost("{deviceId:guid}/verify-credentials")]
    public async Task<ActionResult<ApiResponse>> VerifyCredentials(Guid deviceId, [FromBody] VerifyCredentialsRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.MqttUsername) || string.IsNullOrEmpty(request.MqttPassword))
            return BadRequest(new ApiResponse(false, "MqttUsername and MqttPassword are required"));

        var cred = await _db.DeviceCredentials
            .FirstOrDefaultAsync(c => c.DeviceId == deviceId && c.IsEnabled);

        if (cred == null)
            return NotFound(new ApiResponse(false, "Credentials not found or disabled"));

        if (!string.Equals(cred.MqttUsername, request.MqttUsername, StringComparison.OrdinalIgnoreCase))
            return Ok(new ApiResponse(true, "Verification result")
            {
                Data = new { isValid = false }
            });

        bool isValid = BCrypt.Net.BCrypt.Verify(request.MqttPassword, cred.PasswordHash);

        return Ok(new ApiResponse(true, "Verification result")
        {
            Data = new { isValid }
        });
    }

    [HttpGet("{deviceId:guid}/telemetry")]
    public async Task<ActionResult<ApiResponse>> GetTelemetry(Guid deviceId, [FromQuery] int limit = 100, [FromQuery] DateTime? since = null)
    {
        if (limit < 1 || limit > 1000) limit = 100;

        var query = _db.DeviceTelemetry.Where(t => t.DeviceId == deviceId);
        if (since.HasValue) query = query.Where(t => t.ReceivedAt >= since.Value);

        var items = await query
            .OrderByDescending(t => t.ReceivedAt)
            .Take(limit)
            .Select(t => new
            {
                t.Id,
                sourceType = t.SourceType.ToString().ToLowerInvariant(),
                t.ViaDeviceId,
                t.BatteryLevel,
                t.SignalStrength,
                t.SignalQuality,
                t.Payload,
                t.ReceivedAt,
                t.DeviceTimestamp
            })
            .ToListAsync();

        return Ok(new ApiResponse(true, "Telemetry retrieved successfully")
        {
            Data = new { items }
        });
    }

    [HttpGet("mac-address/check")]
    public async Task<ActionResult<ApiResponse>> CheckMacAddressAvailability([FromQuery] string macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
            return BadRequest(new ApiResponse(false, "macAddress is required"));

        var normalizedMac = macAddress.Trim().ToUpperInvariant().Replace(":", "");

        var device = await _db.Devices
            .FirstOrDefaultAsync(d => d.MacAddress.ToUpper() == normalizedMac);

        bool isAvailable = device == null || device.IsDeleted;

        return Ok(new ApiResponse(true, "MacAddress availability checked")
        {
            Data = new
            {
                macAddress = normalizedMac,
                isAvailable,
                isSoftDeleted = device?.IsDeleted ?? false,
                existingDeviceId = device?.Id
            }
        });
    }

}

public class IssueCredentialsRequest
{
    public bool Regenerate { get; set; } = false;
}