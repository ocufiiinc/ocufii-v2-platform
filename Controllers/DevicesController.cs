using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OcufiiAPI.Data;
using OcufiiAPI.Models;
using System.Security.Cryptography;
using System.Text.Json;

namespace OcufiiAPI.Controllers;

[ApiController]
[Route("devices")]
[Authorize] // All endpoints require JWT
public class DevicesController : ControllerBase
{
    private readonly OcufiiDbContext _db;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(OcufiiDbContext db, ILogger<DevicesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // 5.1 GET /devices
    [HttpGet]
    public async Task<ActionResult> ListDevices(
        [FromQuery] string? type,
        [FromQuery] bool? isEnabled,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = _db.Devices
            .Include(d => d.DeviceType)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(d => d.DeviceType.Key == type.ToLower());

        if (isEnabled.HasValue)
            query = query.Where(d => d.IsEnabled == isEnabled.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.ToLower();
            query = query.Where(d =>
                d.Name != null && d.Name.ToLower().Contains(search) ||
                d.MacAddress.ToLower().Contains(search));
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
                d.Attributes,
                d.CreatedAt,
                d.UpdatedAt
            })
            .ToListAsync();

        return Ok(new { items, total, page, pageSize });
    }

    // 5.2 POST /devices
    [HttpPost]
    public async Task<ActionResult> CreateDevice([FromBody] CreateDeviceRequest request)
    {
        var deviceType = await _db.DeviceTypes
            .FirstOrDefaultAsync(dt => dt.Key == request.Type.ToLower());

        if (deviceType == null)
            return BadRequest("Invalid device type");

        if (await _db.Devices.AnyAsync(d => d.MacAddress == request.MacAddress.Trim()))
            return Conflict("MacAddress already exists");

        var device = new Device
        {
            DeviceTypeId = deviceType.Id,
            MacAddress = request.MacAddress.Trim(),
            Name = request.Name?.Trim(),
            Location = request.Location?.Trim(),
            Information = request.Information,
            Attributes = JsonSerializer.Serialize(request.Attributes ?? new { }),
            IsEnabled = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Devices.Add(device);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetDevice), new { deviceId = device.Id }, new { deviceId = device.Id });
    }

    // 5.3 GET /devices/{deviceId}
    [HttpGet("{deviceId:guid}")]
    public async Task<ActionResult> GetDevice(Guid deviceId)
    {
        var device = await _db.Devices
            .Include(d => d.DeviceType)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId && !d.IsDeleted);

        if (device == null) return NotFound();

        return Ok(new
        {
            device.Id,
            type = device.DeviceType.Key,
            device.MacAddress,
            device.Name,
            device.Location,
            device.IsEnabled,
            device.IsDeleted,
            device.Attributes,
            device.CreatedAt,
            device.UpdatedAt
        });
    }

    // 5.4 PATCH /devices/{deviceId}
    [HttpPatch("{deviceId:guid}")]
    public async Task<IActionResult> UpdateDevice(Guid deviceId, [FromBody] JsonElement payload)
    {
        var device = await _db.Devices.FindAsync(deviceId);
        if (device == null || device.IsDeleted) return NotFound();

        foreach (var prop in payload.EnumerateObject())
        {
            switch (prop.Name)
            {
                case "name":
                    device.Name = prop.Value.GetString();
                    break;
                case "location":
                    device.Location = prop.Value.GetString();
                    break;
                case "isEnabled":
                    device.IsEnabled = prop.Value.GetBoolean();
                    break;
                case "attributes":
                    device.Attributes = prop.Value.ToString();
                    break;
            }
        }

        device.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // 5.5 DELETE /devices/{deviceId}
    [HttpDelete("{deviceId:guid}")]
    public async Task<IActionResult> DeleteDevice(Guid deviceId)
    {
        var device = await _db.Devices.FindAsync(deviceId);
        if (device == null || device.IsDeleted) return NotFound();

        device.IsDeleted = true;
        device.IsEnabled = false;
        device.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // 6.1 POST /devices/{deviceId}/credentials
    [HttpPost("{deviceId:guid}/credentials")]
    public async Task<ActionResult> IssueCredentials(Guid deviceId, [FromBody] IssueCredentialsRequest request)
    {
        var device = await _db.Devices
            .Include(d => d.DeviceType)
            .FirstOrDefaultAsync(d => d.Id == deviceId && !d.IsDeleted);

        if (device == null) return NotFound();
        if (!device.DeviceType.ConnectsToMqtt) return BadRequest("Device does not support MQTT");

        var cred = await _db.DeviceCredentials
            .FirstOrDefaultAsync(c => c.DeviceId == deviceId);

        if (cred != null && !request.Regenerate)
            return Conflict("Credentials already exist. Use regenerate=true");

        if (cred != null)
        {
            _db.DeviceCredentials.Remove(cred);
        }

        var username = $"{device.DeviceType.Key}_{device.MacAddress.Replace(":", "")}".ToLower();
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var newCred = new DeviceCredential
        {
            DeviceId = device.Id,
            MqttUsername = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            IsEnabled = true,
            LastRotatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.DeviceCredentials.Add(newCred);
        await _db.SaveChangesAsync();

        return Created("", new
        {
            mqttUsername = username,
            mqttPassword = password // Returned only once!
        });
    }

    // 6.2 GET /devices/{deviceId}/credentials
    [HttpGet("{deviceId:guid}/credentials")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> GetCredentialsMetadata(Guid deviceId)
    {
        var cred = await _db.DeviceCredentials
            .FirstOrDefaultAsync(c => c.DeviceId == deviceId);

        if (cred == null) return NotFound();

        return Ok(new
        {
            cred.MqttUsername,
            cred.IsEnabled,
            cred.LastRotatedAt
        });
    }

    // 6.3 DELETE /devices/{deviceId}/credentials
    [HttpDelete("{deviceId:guid}/credentials")]
    public async Task<IActionResult> RevokeCredentials(Guid deviceId)
    {
        var cred = await _db.DeviceCredentials
            .FirstOrDefaultAsync(c => c.DeviceId == deviceId);

        if (cred == null) return NotFound();

        _db.DeviceCredentials.Remove(cred);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // 7.1 GET /devices/{deviceId}/telemetry
    [HttpGet("{deviceId:guid}/telemetry")]
    public async Task<ActionResult> GetTelemetry(
        Guid deviceId,
        [FromQuery] int limit = 100,
        [FromQuery] DateTime? since = null)
    {
        if (limit < 1 || limit > 1000) limit = 100;

        var query = _db.DeviceTelemetry
            .Where(t => t.DeviceId == deviceId);

        if (since.HasValue)
            query = query.Where(t => t.ReceivedAt >= since.Value);

        var items = await query
            .OrderByDescending(t => t.ReceivedAt)
            .Take(limit)
            .Select(t => new
            {
                t.Id,
                sourceType = t.SourceType.ToString().ToLower(),
                t.ViaDeviceId,
                t.BatteryLevel,
                t.SignalStrength,
                t.SignalQuality,
                t.Payload,
                t.ReceivedAt,
                t.DeviceTimestamp
            })
            .ToListAsync();

        return Ok(new { items });
    }
}

// DTOs
public class CreateDeviceRequest
{
    public string Type { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Location { get; set; }
    public string? Information { get; set; }
    public object? Attributes { get; set; }
}

public class IssueCredentialsRequest
{
    public bool Regenerate { get; set; } = false;
}