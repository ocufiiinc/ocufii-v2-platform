using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using OcufiiAPI.Configs;
using OcufiiAPI.Data;
using OcufiiAPI.DTO;
using OcufiiAPI.Extensions;
using OcufiiAPI.Models;
using Swashbuckle.AspNetCore.Annotations;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OcufiiAPI.Controllers;

[ApiController]
[Route("devices")]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse), 200)]
[ProducesResponseType(typeof(ApiResponse), 400)]
[ProducesResponseType(typeof(ApiResponse), 401)]
[ProducesResponseType(typeof(ApiResponse), 403)]
[ProducesResponseType(typeof(ApiResponse), 404)]
[ProducesResponseType(typeof(ApiResponse), 409)]
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
    [SwaggerOperation(Summary = "List Devices", Description = "Returns paginated list of devices with optional filtering by type, enabled status, and search. Admins see all, regular users see their own or unassigned devices.")]
    [SwaggerResponse(200, "Devices retrieved successfully", typeof(ApiResponse))]
    [SwaggerResponse(401, "Unauthorized - missing or invalid token")]
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
                d.Information,
                d.IsEnabled,
                d.IsDeleted,
                Attributes = d.Attributes,
                d.CreatedAt,
                d.UpdatedAt
            })
            .ToListAsync();

        return Ok(new ApiResponse(true, "Devices retrieved successfully")
        {
            Data = new { items, total, page, pageSize },
            ErrorCode = null
        });
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Create Device",
        Description = "Creates a new device. MacAddress must be unique and not active (soft-deleted MacAddresses can be reused)."
    )]
    public async Task<ActionResult<ApiResponse>> CreateDevice([FromBody] CreateDeviceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Type))
            return BadRequest(new ApiResponse(false, "Missing or invalid 'type'")
            {
                ErrorCode = "OC-016"
            });

        var typeKey = request.Type.Trim().ToLowerInvariant();
        var deviceType = await _db.DeviceTypes.FirstOrDefaultAsync(dt => dt.Key == typeKey);
        if (deviceType == null)
            return BadRequest(new ApiResponse(false, "Invalid device type")
            {
                ErrorCode = "OC-017"
            });

        if (string.IsNullOrWhiteSpace(request.MacAddress))
            return BadRequest(new ApiResponse(false, "Missing or invalid 'macAddress'")
            {
                ErrorCode = "OC-018"
            });

        var macAddress = request.MacAddress.Trim();
        var normalizedMac = macAddress.ToUpperInvariant().Replace(":", "");

        var activeDevice = await _db.Devices.FirstOrDefaultAsync(d =>
            d.MacAddress.ToUpper() == normalizedMac && !d.IsDeleted);

        if (activeDevice != null)
            return Conflict(new ApiResponse(false, "MacAddress already exists and is active")
            {
                ErrorCode = "OC-019"
            });

        var currentUserId = User.GetUserId();
        var tenantIdClaim = User.FindFirst("tenant_id")?.Value
                            ?? Guid.Parse("00000000-0000-0000-0000-000000000001").ToString();

        Device device;
        var softDeletedDevice = await _db.Devices.FirstOrDefaultAsync(d =>
            d.MacAddress.ToUpper() == normalizedMac && d.IsDeleted);

        if (softDeletedDevice != null)
        {
            device = softDeletedDevice;
            device.IsDeleted = false;
            device.IsEnabled = true;
            device.UserId = currentUserId;
            device.TenantId = Guid.Parse(tenantIdClaim);
            device.UpdatedAt = DateTime.UtcNow;
            device.Name = request.Name?.Trim();
            device.Location = request.Location?.Trim();
            device.Information = request.Information?.Trim();
            device.Attributes = request.Attributes ?? "{}";
        }
        else
        {
            device = new Device
            {
                Id = Guid.NewGuid(),
                DeviceTypeId = deviceType.Id,
                MacAddress = normalizedMac,
                Name = request.Name?.Trim(),
                Location = request.Location?.Trim(),
                Information = request.Information?.Trim(),
                Attributes = request.Attributes ?? "{}",
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
            Data = new { deviceId = device.Id },
            ErrorCode = null
        });
    }

    [HttpPatch("{deviceId:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Update Device",
        Description = "Partially updates device properties. Only provided fields are updated."
    )]
    public async Task<ActionResult<ApiResponse>> UpdateDevice(Guid deviceId, [FromBody] UpdateDeviceRequest request)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId && !d.IsDeleted);
        if (device == null)
            return NotFound(new ApiResponse(false, "Device not found")
            {
                ErrorCode = "OC-020"
            });

        if (request.Name != null) device.Name = request.Name.Trim();
        if (request.Location != null) device.Location = request.Location.Trim();
        if (request.Information != null) device.Information = request.Information.Trim();
        if (request.IsEnabled.HasValue) device.IsEnabled = request.IsEnabled.Value;
        if (request.Attributes != null) device.Attributes = request.Attributes;

        device.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Device updated successfully")
        {
            ErrorCode = null
        });
    }

    [HttpGet("{deviceId:guid}")]
    [SwaggerOperation(Summary = "Get Device Details", Description = "Retrieves details of a specific device by ID")]
    [SwaggerResponse(200, "Device retrieved")]
    [SwaggerResponse(404, "Device not found or deleted")]
    public async Task<ActionResult<ApiResponse>> GetDevice(Guid deviceId)
    {
        var device = await _db.Devices
            .Include(d => d.DeviceType)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId && !d.IsDeleted);

        if (device == null)
            return NotFound(new ApiResponse(false, "Device not found")
            {
                ErrorCode = "OC-020"
            });

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
                device.Information,
                device.IsDeleted,
                Attributes = device.Attributes,
                device.CreatedAt,
                device.UpdatedAt
            },
            ErrorCode = null
        });
    }

    [HttpDelete("{deviceId:guid}")]
    [SwaggerOperation(Summary = "Delete Device (Soft Delete)", Description = "Soft-deletes the device (sets IsDeleted = true)")]
    [SwaggerResponse(200, "Device deleted")]
    [SwaggerResponse(404, "Device not found")]
    public async Task<ActionResult<ApiResponse>> DeleteDevice(Guid deviceId)
    {
        var device = await _db.Devices
            .Include(d => d.DeviceType)
            .FirstOrDefaultAsync(d => d.Id == deviceId && !d.IsDeleted);

        if (device == null)
            return NotFound(new ApiResponse(false, "Device not found")
            {
                ErrorCode = "OC-020"
            });

        var deviceTypeKey = device.DeviceType.Key.ToLowerInvariant();

        if (deviceTypeKey == "gateway" || deviceTypeKey == "beacon")
        {
            var mqttSuccess = await SendDeviceDeletionMqttMessage(device);
            if (!mqttSuccess)
            {
                return StatusCode(500, new ApiResponse(false, "Failed to communicate with device via MQTT after multiple retries")
                {
                    ErrorCode = "OC-021"
                });
            }
        }

        device.IsDeleted = true;
        device.IsEnabled = false;
        device.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Device deleted successfully")
        {
            ErrorCode = null
        });
    }

    private async Task<bool> SendDeviceDeletionMqttMessage(Device device)
    {
        var cleanMac = device.MacAddress.Replace(":", "").Replace("-", "").ToUpperInvariant();
        if (device.DeviceType.Key.ToLowerInvariant() == "gateway")
        {
            return await SendGatewayResetMessage(cleanMac);
        }
        else if (device.DeviceType.Key.ToLowerInvariant() == "beacon")
        {
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
                    device_info = new { device_id = deviceId, mac = gatewayMac },
                    data = new { reset_state = 1 }
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
        var remainingBeacons = await _db.Devices
            .Include(d => d.DeviceType)
            .Where(d => d.UserId == userId && d.DeviceType.Key == "beacon" && d.Id != beaconDevice.Id && !d.IsDeleted)
            .ToListAsync();

        var beaconMacs = remainingBeacons
            .Select(b => b.MacAddress.Replace(":", "").Replace("-", "").ToUpperInvariant())
            .ToArray();

        var gateways = await _db.Devices
            .Include(d => d.DeviceType)
            .Where(d => d.UserId == userId && d.DeviceType.Key == "gateway" && !d.IsDeleted)
            .ToListAsync();

        if (gateways.Count == 0)
            return true;

        int successCount = 0;
        int failureCount = 0;

        foreach (var gateway in gateways)
        {
            var gatewayMac = gateway.MacAddress.Replace(":", "").Replace("-", "").ToUpperInvariant();
            bool gatewaySuccess = await SendBeaconListToGateway(gatewayMac, beaconMacs);
            if (gatewaySuccess) successCount++;
            else failureCount++;
        }

        if (successCount > 0)
            return true;

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
    [SwaggerOperation(Summary = "Issue Device Credentials", Description = "Issues or regenerates MQTT credentials for the device")]
    [SwaggerResponse(201, "Credentials issued")]
    [SwaggerResponse(400, "Device type does not support MQTT")]
    [SwaggerResponse(404, "Device not found")]
    [SwaggerResponse(409, "Credentials already exist")]
    public async Task<ActionResult<ApiResponse>> IssueCredentials(Guid deviceId, [FromBody] IssueCredentialsRequest? request)
    {
        request ??= new IssueCredentialsRequest();
        var device = await _db.Devices
            .Include(d => d.DeviceType)
            .FirstOrDefaultAsync(d => d.Id == deviceId && !d.IsDeleted);

        if (device == null)
            return NotFound(new ApiResponse(false, "Device not found")
            {
                ErrorCode = "OC-020"
            });

        if (!device.DeviceType.ConnectsToMqtt)
            return BadRequest(new ApiResponse(false, "This device type does not support MQTT")
            {
                ErrorCode = "OC-022"
            });

        var existing = await _db.DeviceCredentials.FirstOrDefaultAsync(c => c.DeviceId == deviceId);
        if (existing != null && !request.Regenerate)
            return Conflict(new ApiResponse(false, "Credentials already exist. Use regenerate=true")
            {
                ErrorCode = "OC-023"
            });

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
            Data = new { mqttUsername = username, mqttPassword = password },
            ErrorCode = null
        });
    }

    [HttpGet("{deviceId:guid}/credentials")]
    [SwaggerOperation(Summary = "Get Device Credentials Metadata", Description = "Retrieves metadata of MQTT credentials (username, enabled status, last rotated)")]
    [SwaggerResponse(200, "Credentials retrieved")]
    [SwaggerResponse(404, "Credentials not found")]
    public async Task<ActionResult<ApiResponse>> GetCredentialsMetadata(Guid deviceId)
    {
        var cred = await _db.DeviceCredentials.FirstOrDefaultAsync(c => c.DeviceId == deviceId);
        if (cred == null)
            return NotFound(new ApiResponse(false, "Credentials not found")
            {
                ErrorCode = "OC-024"
            });

        return Ok(new ApiResponse(true, "Credentials retrieved")
        {
            Data = new
            {
                cred.MqttUsername,
                cred.IsEnabled,
                cred.LastRotatedAt
            },
            ErrorCode = null
        });
    }

    [HttpDelete("{deviceId:guid}/credentials")]
    [SwaggerOperation(Summary = "Revoke Device Credentials", Description = "Revokes and deletes the MQTT credentials for the device")]
    [SwaggerResponse(200, "Credentials revoked")]
    [SwaggerResponse(404, "Credentials not found")]
    public async Task<ActionResult<ApiResponse>> RevokeCredentials(Guid deviceId)
    {
        var cred = await _db.DeviceCredentials.FirstOrDefaultAsync(c => c.DeviceId == deviceId);
        if (cred == null)
            return NotFound(new ApiResponse(false, "Credentials not found")
            {
                ErrorCode = "OC-024"
            });

        _db.DeviceCredentials.Remove(cred);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Credentials revoked successfully")
        {
            ErrorCode = null
        });
    }

    [HttpPost("verify-credentials")]
    [AllowAnonymous]
    [SwaggerOperation(
    Summary = "Verify MQTT Credentials (for EMQX)",
    Description = "EMQX HTTP authenticator endpoint."
)]
    [SwaggerResponse(200, "Credentials verified (allow/deny)", typeof(object))]
    public async Task<IActionResult> VerifyCredentials([FromBody] VerifyCredentialsRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.MqttUsername) || string.IsNullOrEmpty(request.MqttPassword))
        {
            return Ok(new { result = "deny" });
        }

        var cred = await _db.DeviceCredentials
            .FirstOrDefaultAsync(c => c.MqttUsername == request.MqttUsername && c.IsEnabled);

        if (cred == null)
        {
            return Ok(new { result = "deny" });
        }

        bool isValid = BCrypt.Net.BCrypt.Verify(request.MqttPassword, cred.PasswordHash);

        return Ok(new
        {
            result = isValid ? "allow" : "deny",
            is_superuser = false  // we don't support superuser flag
        });
    }

    [HttpGet("{deviceId:guid}/telemetry")]
    [SwaggerOperation(Summary = "Get Device Telemetry", Description = "Retrieves recent telemetry data for the device")]
    [SwaggerResponse(200, "Telemetry retrieved")]
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
            Data = new { items },
            ErrorCode = null
        });
    }

    [HttpGet("mac-address/check")]
    [SwaggerOperation(Summary = "Check MacAddress Availability", Description = "Checks if a MacAddress is available (true if new or soft-deleted)")]
    [SwaggerResponse(200, "Availability checked")]
    [SwaggerResponse(400, "macAddress is required")]
    public async Task<ActionResult<ApiResponse>> CheckMacAddressAvailability([FromQuery] string macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
            return BadRequest(new ApiResponse(false, "macAddress is required")
            {
                ErrorCode = "OC-026"
            });

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
            },
            ErrorCode = null
        });
    }
}