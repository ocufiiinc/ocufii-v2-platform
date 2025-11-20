using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OcufiiAPI.DTO;
using OcufiiAPI.Models;
using OcufiiAPI.Repositories;
using System.Text.Json;
using OcufiiAPI.Extensions;
using Microsoft.Extensions.Options;
using OcufiiAPI.Configs;

[ApiController]
[Route("api/settings/me")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly IRepository<UserSetting> _userSettingRepo;
    private readonly IRepository<UserAssistSetting> _assistRepo;
    private readonly SettingsDefaultsConfig _defaults;
    private readonly AssistDefaultsConfig _assistDefaults;

    public SettingsController(
        IRepository<UserSetting> userSettingRepo,
        IRepository<UserAssistSetting> assistRepo,
        IOptions<SettingsDefaultsConfig> defaults,
        IOptions<AssistDefaultsConfig> assistDefaults)
    {
        _userSettingRepo = userSettingRepo;
        _assistRepo = assistRepo;
        _defaults = defaults.Value;
        _assistDefaults = assistDefaults.Value;
    }

    private async Task<UserSetting> GetOrCreateUserSetting(Guid userId)
    {
        var setting = (await _userSettingRepo.FindAsync(s => s.UserId == userId)).FirstOrDefault()
                      ?? new UserSetting { UserId = userId };

        if (setting.Id == Guid.Empty)
        {
            setting.MovementSound = _defaults.MovementSound;
            setting.MovementVibration = _defaults.MovementVibration;
            setting.NotificationSound = Enum.Parse<NotificationSoundType>(_defaults.NotificationSound);
            setting.AutoLogoutEnabled = _defaults.AutoLogoutEnabled;
            setting.AutoLogoutInterval = _defaults.AutoLogoutInterval;
            setting.BypassFocus = _defaults.BypassFocus;

            await _userSettingRepo.AddAsync(setting);
            await _userSettingRepo.SaveAsync();
        }

        return setting;
    }

    private async Task<UserAssistSetting> GetOrCreateAssistSetting(Guid userId)
    {
        var setting = (await _assistRepo.FindAsync(s => s.UserId == userId)).FirstOrDefault()
                      ?? new UserAssistSetting
                      {
                          UserId = userId,
                          Config = JsonSerializer.Serialize(_assistDefaults.Config)
                      };

        if (setting.Config == "{}" || string.IsNullOrEmpty(setting.Config))
        {
            setting.Config = JsonSerializer.Serialize(_assistDefaults.Config);
            await _assistRepo.AddAsync(setting);
            await _assistRepo.SaveAsync();
        }

        return setting;
    }

    // PATCH /api/settings/me
    [HttpPatch]
    public async Task<IActionResult> Update([FromBody] UpdateSettingsDto dto)
    {
        var userId = User.GetUserId();
        var setting = await GetOrCreateUserSetting(userId);

        if (dto.MovementSound.HasValue) setting.MovementSound = dto.MovementSound.Value;
        if (dto.MovementVibration.HasValue) setting.MovementVibration = dto.MovementVibration.Value;
        if (!string.IsNullOrEmpty(dto.NotificationSound))
        {
            var upper = dto.NotificationSound.ToUpper();
            if (!_defaults.AllowedNotificationSounds.Contains(upper))
                return BadRequest(new ApiResponse(false, $"notificationSound must be one of: {string.Join(", ", _defaults.AllowedNotificationSounds)}"));

            setting.NotificationSound = Enum.Parse<NotificationSoundType>(upper);
        }
        if (dto.AutoLogoutEnabled.HasValue) setting.AutoLogoutEnabled = dto.AutoLogoutEnabled.Value;
        if (dto.AutoLogoutInterval.HasValue)
        {
            if (dto.AutoLogoutInterval < _defaults.MinAutoLogoutInterval || dto.AutoLogoutInterval > _defaults.MaxAutoLogoutInterval)
                return BadRequest(new ApiResponse(false, $"autoLogoutInterval must be between {_defaults.MinAutoLogoutInterval} and {_defaults.MaxAutoLogoutInterval} minutes"));
            setting.AutoLogoutInterval = dto.AutoLogoutInterval.Value;
        }
        if (dto.BypassFocus.HasValue) setting.BypassFocus = dto.BypassFocus.Value;
        if (dto.PersonalSafetyUsername != null) setting.PersonalSafetyUsername = dto.PersonalSafetyUsername;

        setting.UpdatedAt = DateTime.UtcNow;
        _userSettingRepo.Update(setting);
        await _userSettingRepo.SaveAsync();

        return Ok(new ApiResponse(true, "Settings updated"));
    }

    // GET /api/settings/me
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = User.GetUserId();
        var setting = await GetOrCreateUserSetting(userId);

        var data = new
        {
            setting.MovementSound,
            setting.MovementVibration,
            notificationSound = setting.NotificationSound.ToString(),
            setting.AutoLogoutEnabled,
            setting.AutoLogoutInterval,
            setting.BypassFocus,
            setting.PersonalSafetyUsername,
            setting.TosId,
            setting.TosVersion,
            termsAcceptedAt = setting.TermsAcceptedAt?.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        return Ok(new { ok = true, data });
    }

    // GET /api/settings/me/assist
    [HttpGet("assist")]
    public async Task<IActionResult> GetAssist()
    {
        var userId = User.GetUserId();
        var setting = await GetOrCreateAssistSetting(userId);
        return Ok(JsonSerializer.Deserialize<object>(setting.Config));
    }

    // PATCH /api/settings/me/assist/{key}
    [HttpPatch("assist/{key}")]
    public async Task<IActionResult> UpdateAssist(string key, [FromBody] UpdateAssistDto dto)
    {
        var validKeys = new[] { "emergency", "distress", "activeShooter", "emergency911", "emergency988" };
        if (!validKeys.Contains(key))
            return BadRequest(new ApiResponse(false, "Invalid assist key"));

        var userId = User.GetUserId();
        var setting = await GetOrCreateAssistSetting(userId);

        var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(setting.Config)!;
        if (!config.ContainsKey(key)) config[key] = JsonSerializer.Deserialize<JsonElement>("{}")!;

        var profile = config[key].Deserialize<Dictionary<string, JsonElement>>()!;

        if (dto.AlarmSound.HasValue) profile["alarmSound"] = JsonDocument.Parse(dto.AlarmSound.Value.ToString().ToLower()).RootElement;
        if (dto.AlertMessage != null) profile["alertMessage"] = JsonDocument.Parse($"\"{dto.AlertMessage}\"").RootElement;
        if (dto.FlashOn.HasValue) profile["flashOn"] = JsonDocument.Parse(dto.FlashOn.Value.ToString().ToLower()).RootElement;
        if (dto.IsEnabled.HasValue) profile["isEnabled"] = JsonDocument.Parse(dto.IsEnabled.Value.ToString().ToLower()).RootElement;
        if (dto.ScreenFlashing.HasValue) profile["screenFlashing"] = JsonDocument.Parse(dto.ScreenFlashing.Value.ToString().ToLower()).RootElement;

        config[key] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(profile))!;
        setting.Config = JsonSerializer.Serialize(config);

        _assistRepo.Update(setting);
        await _assistRepo.SaveAsync();

        return Ok(new ApiResponse(true, "Assist settings updated"));
    }

    // POST /api/settings/me/terms
    [HttpPost("terms")]
    public async Task<IActionResult> AcceptTerms([FromBody] AcceptTermsDto dto)
    {
        var userId = User.GetUserId();
        var setting = await GetOrCreateUserSetting(userId);

        setting.TosId = dto.TosId;
        setting.TosVersion = dto.TosVersion;
        setting.TermsAcceptedAt = DateTime.UtcNow;
        setting.UpdatedAt = DateTime.UtcNow;

        _userSettingRepo.Update(setting);
        await _userSettingRepo.SaveAsync();

        return Ok(new ApiResponse(true, "Terms accepted"));
    }

    // GET /api/settings/me/terms
    [HttpGet("terms")]
    public async Task<IActionResult> GetTerms()
    {
        var userId = User.GetUserId();
        var setting = (await _userSettingRepo.FindAsync(s => s.UserId == userId)).FirstOrDefault();

        if (setting?.TermsAcceptedAt == null)
            return NotFound(new ApiResponse(false, "Terms not accepted"));

        return Ok(new
        {
            setting.TosId,
            setting.TosVersion,
            termsAcceptedAt = setting.TermsAcceptedAt.Value.ToString("yyyy-MM-ddTHH:mm:ssZ")
        });
    }
}