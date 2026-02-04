using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OcufiiAPI.Data;
using OcufiiAPI.Models;
using OcufiiAPI.Extensions;
using System.Text.Json;

namespace OcufiiAPI.Controllers;

[ApiController]
[Route("notifications")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly OcufiiDbContext _db;

    public NotificationController(OcufiiDbContext db)
    {
        _db = db;
    }

    private Guid CurrentUserId => User.GetUserId();

    [HttpGet]
    public async Task<ActionResult<ApiResponse>> List(
        [FromQuery] string? state,
        [FromQuery] string? categoryKey,
        [FromQuery] string? typeKey,
        [FromQuery] Guid? deviceId,
        [FromQuery] DateTime? since,
        [FromQuery] int? lastDays,
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null)
    {
        limit = Math.Clamp(limit, 1, 250);

        var query = _db.NotificationRecipients
            .Where(nr => nr.RecipientUserId == CurrentUserId)
            .Include(nr => nr.Notification)
                .ThenInclude(n => n.Category)
            .Include(nr => nr.Notification)
                .ThenInclude(n => n.Type)
            .Include(nr => nr.Notification)
                .ThenInclude(n => n.Device)
            .AsQueryable();

        if (!string.IsNullOrEmpty(state) && Enum.TryParse<NotificationState>(state, true, out var parsedState))
            query = query.Where(nr => nr.Notification.State == parsedState);

        if (!string.IsNullOrEmpty(categoryKey))
            query = query.Where(nr => nr.Notification.Category.Key == categoryKey.ToLower());

        if (!string.IsNullOrEmpty(typeKey))
        {
            query = query.Where(nr =>
                nr.Notification.TypeKey == typeKey ||
                (nr.Notification.Type != null && nr.Notification.Type.Key == typeKey));
        }

        if (deviceId.HasValue)
            query = query.Where(nr => nr.Notification.DeviceId == deviceId);

        if (since.HasValue)
            query = query.Where(nr => nr.Notification.EventTimestamp >= since.Value);
        else if (lastDays.HasValue && lastDays < 0)
            query = query.Where(nr => nr.Notification.EventTimestamp >= DateTime.UtcNow.AddDays(lastDays.Value));

        query = query.OrderByDescending(nr => nr.Notification.EventTimestamp);

        var rawItems = await query
    .Take(limit)
    .OrderByDescending(nr => nr.Notification.EventTimestamp)
    .Select(nr => new
    {
        notificationId = nr.Notification.Id,
        categoryKey = nr.Notification.Category.Key,
        typeKey = nr.Notification.TypeKey ??
                  (nr.Notification.Type == null ? null : nr.Notification.Type.Key),
        priority = nr.Notification.Priority.ToString().ToLower(),
        state = nr.Notification.State.ToString().ToLower(),
        title = nr.Notification.Title,
        body = nr.Notification.Body,
        sound = nr.Notification.Sound,
        contentAvailable = nr.Notification.ContentAvailable,
        ownerUserId = nr.Notification.OwnerUserId,
        initiatorUserId = nr.Notification.InitiatorUserId,
        initiatorDeviceId = nr.Notification.InitiatorDeviceId,
        deviceId = nr.Notification.DeviceId,
        viaDeviceId = nr.Notification.ViaDeviceId,
        telemetryId = nr.Notification.TelemetryId,
        batteryLevel = nr.Notification.BatteryLevel,
        signalStrength = nr.Notification.SignalStrength,
        signalQuality = nr.Notification.SignalQuality,
        locationJson = nr.Notification.Location,
        eventTimestamp = nr.Notification.EventTimestamp
    })
    .ToListAsync();

        var items = rawItems.Select(x => new
        {
            notificationId = x.notificationId,
            categoryKey = x.categoryKey,
            typeKey = x.typeKey,
            priority = x.priority,
            state = x.state,
            title = x.title,
            body = x.body,
            sound = x.sound,
            contentAvailable = x.contentAvailable,
            ownerUserId = x.ownerUserId,
            initiatorUserId = x.initiatorUserId,
            initiatorDeviceId = x.initiatorDeviceId,
            deviceId = x.deviceId,
            viaDeviceId = x.viaDeviceId,
            telemetryId = x.telemetryId,
            batteryLevel = x.batteryLevel,
            signalStrength = x.signalStrength,
            signalQuality = x.signalQuality,
            location = string.IsNullOrEmpty(x.locationJson)
        ? null
        : JsonSerializer.Deserialize<object>(x.locationJson),
            eventTimestamp = x.eventTimestamp
        }).ToList();

        return Ok(new ApiResponse(true, "Notifications retrieved")
        {
            Data = new { items, page = new { nextCursor = (string?)null } }
        });
    }

    [HttpGet("{notificationId:guid}")]
    public async Task<ActionResult<ApiResponse>> GetDetails(Guid notificationId)
    {
        var delivery = await _db.NotificationRecipients
            .Include(nr => nr.Notification)
                .ThenInclude(n => n.Category)
            .Include(nr => nr.Notification)
                .ThenInclude(n => n.Type)
            .Include(nr => nr.Notification)
                .ThenInclude(n => n.Device)
            .Include(nr => nr.Notification)
                .ThenInclude(n => n.InitiatorUser)
            .Include(nr => nr.Notification)
                .ThenInclude(n => n.OwnerUser)
            .FirstOrDefaultAsync(nr => nr.NotificationId == notificationId && nr.RecipientUserId == CurrentUserId);

        if (delivery == null)
            return NotFound(new ApiResponse(false, "Notification not found or access denied"));

        var n = delivery.Notification;

        return Ok(new ApiResponse(true, "Notification retrieved")
        {
            Data = new
            {
                notificationId = n.Id,
                categoryKey = n.Category.Key,
                typeKey = n.TypeKey ?? n.Type?.Key,
                priority = n.Priority.ToString().ToLower(),
                state = n.State.ToString().ToLower(),
                title = n.Title,
                body = n.Body,
                sound = n.Sound,
                contentAvailable = n.ContentAvailable,
                ownerUserId = n.OwnerUserId,
                initiatorUserId = n.InitiatorUserId,
                initiatorDeviceId = n.InitiatorDeviceId,
                deviceId = n.DeviceId,
                viaDeviceId = n.ViaDeviceId,
                telemetryId = n.TelemetryId,
                batteryLevel = n.BatteryLevel,
                signalStrength = n.SignalStrength,
                signalQuality = n.SignalQuality,
                location = n.Location,
                originDisplay = delivery.OriginDisplay,
                rawEvent = n.RawEvent,
                eventTimestamp = n.EventTimestamp,
                createdAt = n.CreatedAt
            }
        });
    }

    [HttpGet("{notificationId:guid}/actions")]
    public async Task<ActionResult<ApiResponse>> GetActions(Guid notificationId)
    {
        var exists = await _db.NotificationRecipients
            .AnyAsync(nr => nr.NotificationId == notificationId && nr.RecipientUserId == CurrentUserId);

        if (!exists)
            return Forbid();

        var actions = await _db.NotificationActions
            .Where(na => na.NotificationId == notificationId)
            .Include(na => na.ActorUser)
            .OrderByDescending(na => na.CreatedAt)
            .Select(na => new
            {
                actionId = na.Id,
                actionType = na.ActionType.ToString().ToLower(),
                comment = na.Comment,
                actor = new { userId = na.ActorUserId, name = na.ActorUser.FirstName + " " + na.ActorUser.LastName },
                createdAt = na.CreatedAt
            })
            .ToListAsync();

        return Ok(new ApiResponse(true, "Actions retrieved")
        {
            Data = new { items = actions }
        });
    }

    [HttpPost("{notificationId:guid}/acknowledge")]
    public async Task<ActionResult<ApiResponse>> Acknowledge(Guid notificationId, [FromBody] AcknowledgeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Comment))
            return BadRequest(new ApiResponse(false, "Comment is required"));

        var delivery = await _db.NotificationRecipients
            .FirstOrDefaultAsync(nr => nr.NotificationId == notificationId && nr.RecipientUserId == CurrentUserId);

        if (delivery == null)
            return Forbid();

        var notification = await _db.Notifications.FindAsync(notificationId);
        if (notification == null)
            return NotFound(new ApiResponse(false, "Notification not found"));

        notification.State = NotificationState.Acknowledged;
        notification.UpdatedAt = DateTime.UtcNow;

        var action = new NotificationAction
        {
            NotificationId = notificationId,
            ActorUserId = CurrentUserId,
            ActionType = NotificationActionType.Acknowledge,
            Comment = request.Comment
        };

        _db.NotificationActions.Add(action);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Notification acknowledged")
        {
            Data = new { notificationId, state = "acknowledged", actionId = action.Id }
        });
    }

    [HttpPost("{notificationId:guid}/resolve")]
    public async Task<ActionResult<ApiResponse>> Resolve(Guid notificationId, [FromBody] AcknowledgeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Comment))
            return BadRequest(new ApiResponse(false, "Comment is required"));

        var delivery = await _db.NotificationRecipients
            .FirstOrDefaultAsync(nr => nr.NotificationId == notificationId && nr.RecipientUserId == CurrentUserId);

        if (delivery == null)
            return Forbid();

        var notification = await _db.Notifications.FindAsync(notificationId);
        if (notification == null)
            return NotFound(new ApiResponse(false, "Notification not found"));

        notification.State = NotificationState.Resolved;
        notification.UpdatedAt = DateTime.UtcNow;

        var action = new NotificationAction
        {
            NotificationId = notificationId,
            ActorUserId = CurrentUserId,
            ActionType = NotificationActionType.Resolve,
            Comment = request.Comment
        };

        _db.NotificationActions.Add(action);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Notification resolved")
        {
            Data = new { notificationId, state = "resolved", actionId = action.Id }
        });
    }

    [HttpGet("snooze-reasons")]
    public IActionResult GetSnoozeReasons()
    {
        var reasons = new[]
        {
            new { key = "moving_item_temporarily", label = "Moving item temporarily" },
            new { key = "cleaning_or_maintenance", label = "Cleaning or Maintenance" },
            new { key = "conceal_carry", label = "Conceal Carry" },
            new { key = "going_to_range", label = "Going to range" },
            new { key = "traveling", label = "Traveling" }
        };

        return Ok(new ApiResponse(true, "Snooze reasons retrieved")
        {
            Data = new { items = reasons }
        });
    }

    [HttpGet("ids")]
    public async Task<ActionResult<ApiResponse>> GetIdsByDevice([FromQuery] Guid deviceId)
    {
        if (deviceId == Guid.Empty)
            return BadRequest(new ApiResponse(false, "deviceId required"));

        var ids = await _db.Notifications
            .Where(n => n.DeviceId == deviceId || n.ViaDeviceId == deviceId || n.InitiatorDeviceId == deviceId)
            .Select(n => n.Id.ToString())
            .ToListAsync();

        return Ok(new ApiResponse(true, "Notification IDs retrieved")
        {
            Data = new { ids }
        });
    }

    [HttpGet("unacknowledged")]
    public async Task<ActionResult<ApiResponse>> GetUnacknowledged()
    {
        var items = await _db.NotificationRecipients
            .Where(nr => nr.RecipientUserId == CurrentUserId &&
                        nr.Notification.State == NotificationState.Open &&
                        nr.Notification.Priority >= NotificationPriority.High)
            .Include(nr => nr.Notification)
                .ThenInclude(n => n.Device)
            .Select(nr => new
            {
                notificationId = nr.Notification.Id,
                state = nr.Notification.State.ToString().ToLower(),
                categoryKey = nr.Notification.Category.Key,
                deviceId = nr.Notification.DeviceId
            })
            .ToListAsync();

        return Ok(new ApiResponse(true, "Unacknowledged notifications retrieved")
        {
            Data = new { items }
        });
    }

    [HttpPost("acknowledge/batch")]
    public async Task<ActionResult<ApiResponse>> BatchAcknowledge([FromBody] BatchActionRequest request)
    {
        var results = new List<object>();

        foreach (var item in request.Items)
        {
            var delivery = await _db.NotificationRecipients
                .FirstOrDefaultAsync(nr => nr.NotificationId == item.NotificationId && nr.RecipientUserId == CurrentUserId);

            if (delivery == null)
            {
                results.Add(new { notificationId = item.NotificationId, ok = false, error = new { code = "FORBIDDEN" } });
                continue;
            }

            var notification = await _db.Notifications.FindAsync(item.NotificationId);
            if (notification != null)
            {
                notification.State = NotificationState.Acknowledged;
                notification.UpdatedAt = DateTime.UtcNow;

                var action = new NotificationAction
                {
                    NotificationId = item.NotificationId,
                    ActorUserId = CurrentUserId,
                    ActionType = NotificationActionType.Acknowledge,
                    Comment = item.Comment ?? "Acknowledged via batch"
                };

                _db.NotificationActions.Add(action);
                results.Add(new { notificationId = item.NotificationId, ok = true, state = "acknowledged", actionId = action.Id });
            }
        }

        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Batch acknowledge processed")
        {
            Data = new { results }
        });
    }

    [HttpPost("resolve/batch")]
    public async Task<ActionResult<ApiResponse>> BatchResolve([FromBody] BatchActionRequest request)
    {
        var results = new List<object>();

        foreach (var item in request.Items)
        {
            var delivery = await _db.NotificationRecipients
                .FirstOrDefaultAsync(nr => nr.NotificationId == item.NotificationId && nr.RecipientUserId == CurrentUserId);

            if (delivery == null)
            {
                results.Add(new { notificationId = item.NotificationId, ok = false, error = new { code = "FORBIDDEN" } });
                continue;
            }

            var notification = await _db.Notifications.FindAsync(item.NotificationId);
            if (notification != null)
            {
                notification.State = NotificationState.Resolved;
                notification.UpdatedAt = DateTime.UtcNow;

                var action = new NotificationAction
                {
                    NotificationId = item.NotificationId,
                    ActorUserId = CurrentUserId,
                    ActionType = NotificationActionType.Resolve,
                    Comment = item.Comment ?? "Resolved via batch"
                };

                _db.NotificationActions.Add(action);
                results.Add(new { notificationId = item.NotificationId, ok = true, state = "resolved", actionId = action.Id });
            }
        }

        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Batch resolve processed")
        {
            Data = new { results }
        });
    }
}

public class AcknowledgeRequest
{
    public string Comment { get; set; } = string.Empty;
}

public class BatchActionRequest
{
    public List<BatchActionItem> Items { get; set; } = new();
}

public class BatchActionItem
{
    public Guid NotificationId { get; set; }
    public string? Comment { get; set; }
}