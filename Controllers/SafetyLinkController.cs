using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OcufiiAPI.Configs;
using OcufiiAPI.Data;
using OcufiiAPI.DTO;
using OcufiiAPI.Enums;
using OcufiiAPI.Extensions;
using OcufiiAPI.Models;
using OcufiiAPI.Repositories;
using OcufiiAPI.Services;
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

[ApiController]
[Route("api/safetylink")]
[Authorize]
[Produces("application/json")]
public class SafetyLinkController : ControllerBase
{
    private readonly OcufiiDbContext _db;
    private readonly IRepository<User> _userRepo;
    private readonly IRepository<SafetyLink> _safetyLinkRepo;
    private readonly IRepository<SubscriptionPlan> _subscriptionRepo;
    private readonly IEmailService _emailService;
    private readonly INotificationService _notificationService;
    private readonly EmailTemplates _emailTemplates;
    private readonly IMemoryCache _cache;

    public SafetyLinkController(
        OcufiiDbContext db,
        IRepository<User> userRepo,
        IRepository<SafetyLink> safetyLinkRepo,
        IRepository<SubscriptionPlan> subscriptionRepo,
        IEmailService emailService,
        INotificationService notificationService,
        IOptions<EmailTemplates> emailTemplates,
        IMemoryCache cache)
    {
        _db = db;
        _userRepo = userRepo;
        _safetyLinkRepo = safetyLinkRepo;
        _subscriptionRepo = subscriptionRepo;
        _emailService = emailService;
        _notificationService = notificationService;
        _emailTemplates = emailTemplates.Value;
        _cache = cache;
    }

    private async Task<SubscriptionPlan> GetUserSubscription(Guid userId)
    {
        var cacheKey = $"subscription_{userId}";
        if (_cache.TryGetValue(cacheKey, out SubscriptionPlan? cachedPlan))
            return cachedPlan;

        var plan = await _subscriptionRepo.Query()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive);

        if (plan == null)
        {
            plan = new SubscriptionPlan
            {
                UserId = userId,
                PlanType = SubscriptionPlanType.Free,
                MaxActiveLinks = 1,
                IsActive = true,
                ExpiryDate = DateTime.UtcNow.AddYears(10),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        _cache.Set(cacheKey, plan, TimeSpan.FromMinutes(30));
        return plan;
    }

    private string GenerateOTP()
    {
        return new Random().Next(100000, 999999).ToString();
    }

    private bool IsValidEmail(string email)
    {
        return Regex.IsMatch(email, @"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$");
    }

    [HttpPost("resend-invite/{linkId:guid}")]
    public async Task<ActionResult<ApiResponse>> ResendInvitation(Guid linkId)
    {
        var senderId = User.GetUserId();
        var link = await _safetyLinkRepo.Query().FirstOrDefaultAsync(l => l.Id == linkId && l.SenderId == senderId && l.Status == SafetyLinkStatus.Pending);
        if (link == null) return NotFound(new ApiResponse(false, "Pending link not found")
        {
            ErrorCode = "OC-038"
        });

        link.OTP = GenerateOTP();
        link.OTPExpiry = DateTime.UtcNow.AddMinutes(30);
        link.UpdatedAt = DateTime.UtcNow;

        _safetyLinkRepo.Update(link);
        await _safetyLinkRepo.SaveAsync();

        var sender = await _userRepo.GetByIdAsync(senderId);
        var recipient = await _userRepo.GetByIdAsync(link.RecipientId);

        var senderName = $"{sender.FirstName} {sender.LastName}".Trim();
        var bodyTemplate = link.EnableLocation ? _emailTemplates.InvitationBodyWithLocation : _emailTemplates.InvitationBodyWithoutLocation;
        var body = bodyTemplate.Replace("{recipientName}", recipient.FirstName ?? recipient.Email)
                               .Replace("{senderName}", senderName)
                               .Replace("{code}", link.OTP);

        await _emailService.SendEmailAsync(recipient.Email, _emailTemplates.InvitationSubject, body, isHtml: true);

        return Ok(new ApiResponse(true, "OTP resent")
        {
            ErrorCode = null
        });
    }

    [HttpPatch("update-linked/{linkId:guid}")]
    [SwaggerOperation(Summary = "Update Linked Member", Description = "Updates toggles and alias for an accepted link.")]
    [SwaggerResponse(200, "Updated")]
    [SwaggerResponse(404, "Link not found")]
    public async Task<ActionResult<ApiResponse>> UpdateLinkedMember(Guid linkId, [FromBody] UpdateLinkedDto dto)
    {
        var senderId = User.GetUserId();
        var link = await _safetyLinkRepo.Query().FirstOrDefaultAsync(l => l.Id == linkId && l.SenderId == senderId && l.Status == SafetyLinkStatus.Accepted);
        if (link == null) return NotFound(new ApiResponse(false, "Accepted link not found")
        {
            ErrorCode = "OC-039"
        });

        if (dto.AliasName != null) link.AliasName = dto.AliasName;
        if (dto.EnableLocation.HasValue) link.EnableLocation = dto.EnableLocation.Value;
        if (dto.EnableSafety.HasValue) link.EnableSafety = dto.EnableSafety.Value;
        if (dto.EnableSecurity.HasValue) link.EnableSecurity = dto.EnableSecurity.Value;

        link.UpdatedAt = DateTime.UtcNow;

        _safetyLinkRepo.Update(link);
        await _safetyLinkRepo.SaveAsync();

        return Ok(new ApiResponse(true, "Linked member updated")
        {
            ErrorCode = null
        });
    }

    [HttpPatch("snooze/{linkId:guid}")]
    [SwaggerOperation(Summary = "Apply Snooze", Description = "Applies snooze on notifications for a link.")]
    [SwaggerResponse(200, "Snooze applied")]
    [SwaggerResponse(400, "Invalid snooze period")]
    [SwaggerResponse(404, "Link not found")]
    public async Task<ActionResult<ApiResponse>> ApplySnooze(Guid linkId, [FromBody] SnoozeDto dto)
    {
        var userId = User.GetUserId();
        var link = await _safetyLinkRepo.Query().FirstOrDefaultAsync(l => l.Id == linkId && (l.SenderId == userId || l.RecipientId == userId) && l.Status == SafetyLinkStatus.Accepted);
        if (link == null) return NotFound(new ApiResponse(false, "Accepted link not found")
        {
            ErrorCode = "OC-039"
        });

        if (dto.StartTime >= dto.EndTime) return BadRequest(new ApiResponse(false, "Start time must be before end time")
        {
            ErrorCode = "OC-040"
        });

        link.Snooze = true;
        link.SnoozeStartTime = dto.StartTime;
        link.SnoozeEndTime = dto.EndTime;
        link.UpdatedAt = DateTime.UtcNow;

        _safetyLinkRepo.Update(link);
        await _safetyLinkRepo.SaveAsync();

        return Ok(new ApiResponse(true, "Snooze applied")
        {
            ErrorCode = null
        });
    }

    [HttpPatch("status/{linkId:guid}")]
    [SwaggerOperation(Summary = "Update SafetyLink Status", Description = "Updates status of a link/invitation. Use string values: Pending, Accepted, Rejected, Block, Inactive.")]
    [SwaggerResponse(200, "Status updated")]
    [SwaggerResponse(400, "Invalid status value")]
    [SwaggerResponse(404, "Link not found")]
    public async Task<ActionResult<ApiResponse>> UpdateStatus(Guid linkId, [FromBody] UpdateStatusDto dto)
    {
        var userId = User.GetUserId();
        var link = await _safetyLinkRepo.Query()
            .FirstOrDefaultAsync(l => l.Id == linkId && (l.SenderId == userId || l.RecipientId == userId));

        if (link == null)
            return NotFound(new ApiResponse(false, "Link not found")
            {
                ErrorCode = "OC-041"
            });

        if (!Enum.TryParse<SafetyLinkStatus>(dto.Status, true, out var newStatus))
        {
            return BadRequest(new ApiResponse(false,
                "Invalid status. Allowed values: Pending, Accepted, Rejected, Block, Inactive")
            {
                ErrorCode = "OC-042"
            });
        }

        if (link.RecipientId == userId)
        {
            if (newStatus != SafetyLinkStatus.Rejected && newStatus != SafetyLinkStatus.Block)
                return BadRequest(new ApiResponse(false, "Recipient can only set Rejected or Block")
                {
                    ErrorCode = "OC-043"
                });
        }
        else if (link.SenderId == userId)
        {
            if (newStatus != SafetyLinkStatus.Inactive)
                return BadRequest(new ApiResponse(false, "Sender can only set Inactive")
                {
                    ErrorCode = "OC-044"
                });
        }

        link.Status = newStatus;
        link.UpdatedAt = DateTime.UtcNow;

        _safetyLinkRepo.Update(link);
        await _safetyLinkRepo.SaveAsync();

        return Ok(new ApiResponse(true, $"Status updated to {newStatus}")
        {
            ErrorCode = null
        });
    }

    [HttpPost("invite")]
    [SwaggerOperation(Summary = "Send SafetyLink Invitation", Description = "Sends invitation to link with trusted contact. Checks subscription limits. Allows re-invite after rejection.")]
    public async Task<ActionResult<ApiResponse>> SendInvitation([FromBody] SendInvitationDto dto)
    {
        if (!IsValidEmail(dto.Email))
            return BadRequest(new ApiResponse(false, "Invalid email format") { ErrorCode = "OC-030" });

        var senderId = User.GetUserId();
        var sender = await _userRepo.GetByIdAsync(senderId);
        if (sender == null) return NotFound(new ApiResponse(false, "Sender not found") { ErrorCode = "OC-031" });

        var recipient = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email && !u.IsDeleted);
        if (recipient == null) return BadRequest(new ApiResponse(false, "Recipient email does not have an account") { ErrorCode = "OC-032" });

        if (recipient.UserId == senderId) return BadRequest(new ApiResponse(false, "Cannot invite yourself") { ErrorCode = "OC-033" });

        if (string.IsNullOrWhiteSpace(dto.AliasName)) return BadRequest(new ApiResponse(false, "AliasName required") { ErrorCode = "OC-034" });

        var existingLink = await _safetyLinkRepo.Query()
            .FirstOrDefaultAsync(l => l.SenderId == senderId && l.RecipientId == recipient.UserId);

        string senderName = $"{sender.FirstName} {sender.LastName}".Trim();
        string bodyTemplate = dto.EnableLocation ? _emailTemplates.InvitationBodyWithLocation : _emailTemplates.InvitationBodyWithoutLocation;
        string body = string.Empty;

        if (existingLink != null)
        {
            if (existingLink.Status == SafetyLinkStatus.Pending)
                return Conflict(new ApiResponse(false, "Invitation is already pending – use Resend OTP instead") { ErrorCode = "OC-035" });

            if (existingLink.Status == SafetyLinkStatus.Accepted)
                return Conflict(new ApiResponse(false, "Already linked") { ErrorCode = "OC-036" });

            existingLink.Status = SafetyLinkStatus.Pending;
            existingLink.AliasName = dto.AliasName;
            existingLink.EnableLocation = dto.EnableLocation;
            existingLink.EnableSafety = dto.EnableSafety;
            existingLink.EnableSecurity = dto.EnableSecurity;
            existingLink.OTP = GenerateOTP();
            existingLink.OTPExpiry = DateTime.UtcNow.AddHours(48);
            existingLink.UpdatedAt = DateTime.UtcNow;
            _safetyLinkRepo.Update(existingLink);
            await _safetyLinkRepo.SaveAsync();

            body = bodyTemplate.Replace("{recipientName}", recipient.FirstName ?? recipient.Email)
                               .Replace("{senderName}", senderName)
                               .Replace("{code}", existingLink.OTP);

            await _emailService.SendEmailAsync(recipient.Email, _emailTemplates.InvitationSubject, body, isHtml: true);

            return Ok(new ApiResponse(true, "Invitation re-sent successfully") { ErrorCode = null });
        }

        var subscription = await GetUserSubscription(senderId);
        var activeCount = await _safetyLinkRepo.Query()
            .CountAsync(l => l.SenderId == senderId && l.Status == SafetyLinkStatus.Accepted);

        int maxLinks = subscription.PlanType == SubscriptionPlanType.AddOn ? 6 : 1;
        if (activeCount >= maxLinks)
            return BadRequest(new ApiResponse(false, $"Subscription limit reached ({activeCount}/{maxLinks} active links)") { ErrorCode = "OC-037" });

        var link = new SafetyLink
        {
            SenderId = senderId,
            RecipientId = recipient.UserId,
            Status = SafetyLinkStatus.Pending,
            AliasName = dto.AliasName,
            EnableLocation = dto.EnableLocation,
            EnableSafety = dto.EnableSafety,
            EnableSecurity = dto.EnableSecurity,
            OTP = GenerateOTP(),
            OTPExpiry = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _safetyLinkRepo.AddAsync(link);
        await _safetyLinkRepo.SaveAsync();

        body = bodyTemplate.Replace("{recipientName}", recipient.FirstName ?? recipient.Email)
                           .Replace("{senderName}", senderName)
                           .Replace("{code}", link.OTP);

        await _emailService.SendEmailAsync(recipient.Email, _emailTemplates.InvitationSubject, body, isHtml: true);

        return Ok(new ApiResponse(true, "Invitation sent successfully") { ErrorCode = null });
    }

    [HttpPost("accept-invitation")]
    [SwaggerOperation(Summary = "Accept SafetyLink Invitation", Description = "Accepts a pending invitation and creates the reverse bidirectional link if not already present.")]
    public async Task<ActionResult<ApiResponse>> AcceptInvitation([FromBody] AcceptInvitationDto dto)
    {
        var recipientId = User.GetUserId();

        var link = await _safetyLinkRepo.Query()
            .FirstOrDefaultAsync(l => l.RecipientId == recipientId
                                   && l.Status == SafetyLinkStatus.Pending);

        if (link == null)
            return NotFound(new ApiResponse(false, "Pending invitation not found") { ErrorCode = "OC-045" });

        if (link.OTPExpiry < DateTime.UtcNow || link.OTP != dto.OTP)
            return BadRequest(new ApiResponse(false, "Invalid or expired OTP") { ErrorCode = "OC-046" });

        var senderId = link.SenderId;

        var subscription = await GetUserSubscription(senderId);
        var currentActiveCount = await _safetyLinkRepo.Query()
            .CountAsync(l => l.SenderId == senderId && l.Status == SafetyLinkStatus.Accepted);

        int maxAllowed = subscription.PlanType == SubscriptionPlanType.AddOn ? 6 : 1;
        if (currentActiveCount >= maxAllowed)
        {
            link.Status = SafetyLinkStatus.Inactive;
            link.UpdatedAt = DateTime.UtcNow;
            await _safetyLinkRepo.SaveAsync();
            return BadRequest(new ApiResponse(false,
                $"Cannot accept - sender has reached their subscription limit ({currentActiveCount}/{maxAllowed} active links)")
            { ErrorCode = "OC-047" });
        }

        link.Status = SafetyLinkStatus.Accepted;
        link.OTP = string.Empty;
        link.OTPExpiry = null;
        link.UpdatedAt = DateTime.UtcNow;
        _safetyLinkRepo.Update(link);

        var existingReverse = await _safetyLinkRepo.Query()
            .FirstOrDefaultAsync(l => l.SenderId == recipientId && l.RecipientId == senderId);

        if (existingReverse == null)
        {
            var reverseLink = new SafetyLink
            {
                SenderId = recipientId,
                RecipientId = senderId,
                Status = SafetyLinkStatus.Accepted,
                AliasName = $"From {User.FindFirst("name")?.Value ?? "User"}",
                EnableLocation = link.EnableLocation,
                EnableSafety = link.EnableSafety,
                EnableSecurity = link.EnableSecurity,
                Snooze = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _safetyLinkRepo.AddAsync(reverseLink);
        }
        else
        {
            if (existingReverse.Status != SafetyLinkStatus.Accepted)
            {
                existingReverse.Status = SafetyLinkStatus.Accepted;
                existingReverse.UpdatedAt = DateTime.UtcNow;
                _safetyLinkRepo.Update(existingReverse);
            }
        }

        await _safetyLinkRepo.SaveAsync();

        return Ok(new ApiResponse(true, "Invitation accepted and bidirectional link created/verified")
        {
            ErrorCode = null
        });
    }

    [HttpGet("linked-members")]
    public async Task<ActionResult<ApiResponse>> GetLinkedMembers()
    {
        var userId = User.GetUserId();
        var myEmail = await _db.Users
        .Where(u => u.UserId == userId)
        .Select(u => u.Email)
        .FirstOrDefaultAsync() ?? "unknown@email.com";

        var allLinks = await _safetyLinkRepo.Query()
            .Where(l => (l.SenderId == userId || l.RecipientId == userId)
                     && l.Status == SafetyLinkStatus.Accepted)
            .Select(l => new
            {
                LinkId = l.Id,
                OtherEmail = l.SenderId == userId
                    ? _db.Users.Where(u => u.UserId == l.RecipientId).Select(u => u.Email).FirstOrDefault()
                    : _db.Users.Where(u => u.UserId == l.SenderId).Select(u => u.Email).FirstOrDefault(),
                AliasName = l.AliasName ?? "No alias set",
                Status = l.Status,
                EnableLocation = l.EnableLocation,
                EnableSafety = l.EnableSafety,
                EnableSecurity = l.EnableSecurity,
                Snooze = l.Snooze,
                SnoozeStartTime = l.SnoozeStartTime,
                SnoozeEndTime = l.SnoozeEndTime,
                IsOtpExpired = false,
                IsOutbound = l.SenderId == userId
            })
            .ToListAsync();

        var outbound = allLinks
            .Where(l => l.IsOutbound)
            .Select(l => new LinkedMemberDto
            {
                LinkId = l.LinkId,
                Email = myEmail,
                AliasName = l.AliasName,
                Status = l.Status,
                EnableLocation = l.EnableLocation,
                EnableSafety = l.EnableSafety,
                EnableSecurity = l.EnableSecurity,
                Snooze = l.Snooze,
                SnoozeStartTime = l.SnoozeStartTime,
                SnoozeEndTime = l.SnoozeEndTime,
                IsOtpExpired = l.IsOtpExpired
            })
            .ToList();

        var inbound = allLinks
            .Where(l => !l.IsOutbound)
            .Select(l => new LinkedMemberDto
            {
                LinkId = l.LinkId,
                Email = l.OtherEmail,
                AliasName = l.AliasName,
                Status = l.Status,
                EnableLocation = l.EnableLocation,
                EnableSafety = l.EnableSafety,
                EnableSecurity = l.EnableSecurity,
                Snooze = l.Snooze,
                SnoozeStartTime = l.SnoozeStartTime,
                SnoozeEndTime = l.SnoozeEndTime,
                IsOtpExpired = l.IsOtpExpired
            })
            .ToList();

        return Ok(new ApiResponse(true, "Linked members retrieved")
        {
            Data = new { Outbound = outbound, Inbound = inbound },
            ErrorCode = null
        });
    }

    [HttpDelete("delete-linked/{linkId:guid}")]
    public async Task<ActionResult<ApiResponse>> DeleteLinkedMember(Guid linkId)
    {
        var userId = User.GetUserId();

        var link = await _safetyLinkRepo.Query()
            .FirstOrDefaultAsync(l => l.Id == linkId && (l.SenderId == userId || l.RecipientId == userId));

        if (link == null)
            return NotFound(new ApiResponse(false, "Link not found")
            {
                ErrorCode = "OC-048"
            });

        _safetyLinkRepo.Delete(link);
        await _safetyLinkRepo.SaveAsync();

        return Ok(new ApiResponse(true, "Linked member deleted")
        {
            ErrorCode = null
        });
    }

    [HttpPost("test-notification/{linkId:guid}")]
    public async Task<ActionResult<ApiResponse>> SendTestNotification(Guid linkId)
    {
        var senderId = User.GetUserId();
        var link = await _safetyLinkRepo.Query().FirstOrDefaultAsync(l => l.Id == linkId && l.SenderId == senderId && l.Status == SafetyLinkStatus.Accepted);
        if (link == null) return NotFound(new ApiResponse(false, "Accepted link not found")
        {
            ErrorCode = "OC-049"
        });

        var recipientTokens = await _db.DeviceToken.Where(dt => dt.UserId == link.RecipientId).Select(dt => dt.DeviceTokenValue).ToListAsync();
        if (!recipientTokens.Any()) return BadRequest(new ApiResponse(false, "Recipient has no device tokens")
        {
            ErrorCode = "OC-050"
        });

        await _notificationService.SendToMultipleAsync(recipientTokens, "Ocufii Test Notification", "This is a test from SafetyLink", new Dictionary<string, string> { { "test", "true" } });

        return Ok(new ApiResponse(true, "Test notification sent")
        {
            ErrorCode = null
        });
    }

    [HttpGet("subscription")]
    public async Task<ActionResult<ApiResponse>> GetSubscription()
    {
        var userId = User.GetUserId();
        var subscription = await GetUserSubscription(userId);
        var activeLinks = await _safetyLinkRepo.Query().CountAsync(l => l.SenderId == userId && l.Status == SafetyLinkStatus.Accepted);

        return Ok(new ApiResponse(true, "Subscription retrieved")
        {
            Data = new
            {
                subscription.PlanType,
                subscription.MaxActiveLinks,
                subscription.IsActive,
                subscription.ExpiryDate,
                ActiveLinks = activeLinks
            },
            ErrorCode = null
        });
    }
}