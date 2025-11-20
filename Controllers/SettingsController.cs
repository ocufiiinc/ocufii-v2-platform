using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OcufiiAPI.DTO;
using OcufiiAPI.Models;
using OcufiiAPI.Repositories;
using System.Text.Json;
using OcufiiAPI.Extensions;

namespace OcufiiAPI.Controllers
{
    [ApiController]
    [Route("api/settings/me")]
    [Authorize]
    public class SettingsController : ControllerBase
    {
        private readonly IRepository<Setting> _settingRepo;
        private readonly IRepository<User> _userRepo;

        public SettingsController(IRepository<Setting> settingRepo, IRepository<User> userRepo)
        {
            _settingRepo = settingRepo;
            _userRepo = userRepo;
        }

        private async Task<Setting> GetOrCreateSettings(Guid userId)
        {
            var setting = (await _settingRepo.FindAsync(s => s.UserId == userId)).FirstOrDefault();
            if (setting == null)
            {
                setting = new Setting { UserId = userId };
                await _settingRepo.AddAsync(setting);
                await _settingRepo.SaveAsync();
            }
            return setting;
        }

        // GET /api/settings/me
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var userId = User.GetUserId();
            var setting = await GetOrCreateSettings(userId);

            var data = new
            {
                setting.MovementSound,
                setting.MovementVibration,
                setting.NotificationSound,
                setting.AutoLogoutEnabled,
                setting.AutoLogoutInterval,
                setting.BypassFocus,
                setting.PersonalSafetyUsername,
                setting.TosId,
                setting.TosVersion,
                TermsAcceptedAt = setting.TermsAcceptedAt?.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            return Ok(new { ok = true, data });
        }

        // PATCH /api/settings/me
        [HttpPatch]
        public async Task<IActionResult> Update([FromBody] UpdateSettingsDto dto)
        {
            var userId = User.GetUserId();
            var setting = await GetOrCreateSettings(userId);

            if (dto.MovementSound.HasValue) setting.MovementSound = dto.MovementSound.Value;
            if (dto.MovementVibration.HasValue) setting.MovementVibration = dto.MovementVibration.Value;
            var validSounds = new[] { "DEFAULT", "FIRE", "EMERGENCY" };
            if (!string.IsNullOrEmpty(dto.NotificationSound) && validSounds.Contains(dto.NotificationSound.ToUpper()))
            {
                setting.NotificationSound = dto.NotificationSound.ToUpper();
            }
            else if (!string.IsNullOrEmpty(dto.NotificationSound))
            {
                return BadRequest(new ApiResponse(false, "notificationSound must be DEFAULT, FIRE, or EMERGENCY"));
            }
            if (dto.AutoLogoutEnabled.HasValue) setting.AutoLogoutEnabled = dto.AutoLogoutEnabled.Value;
            if (dto.AutoLogoutInterval.HasValue && dto.AutoLogoutInterval >= 5 && dto.AutoLogoutInterval <= 120)
                setting.AutoLogoutInterval = dto.AutoLogoutInterval.Value;
            if (dto.BypassFocus.HasValue) setting.BypassFocus = dto.BypassFocus.Value;
            if (dto.PersonalSafetyUsername != null) setting.PersonalSafetyUsername = dto.PersonalSafetyUsername;

            _settingRepo.Update(setting);
            await _settingRepo.SaveAsync();

            return Ok(new ApiResponse(true, "Settings updated"));
        }

        // DELETE /api/settings/me
        [HttpDelete]
        public async Task<IActionResult> Delete()
        {
            var userId = User.GetUserId();
            var setting = (await _settingRepo.FindAsync(s => s.UserId == userId)).FirstOrDefault();
            if (setting != null)
            {
                _settingRepo.Delete(setting);
                await _settingRepo.SaveAsync();
            }
            return Ok(new ApiResponse(true, "Settings deleted"));
        }

        // GET /api/settings/me/assist
        [HttpGet("assist")]
        public async Task<IActionResult> GetAssist()
        {
            var userId = User.GetUserId();
            var setting = await GetOrCreateSettings(userId);
            var json = setting.AssistSettings ?? "{}";
            return Ok(JsonSerializer.Deserialize<object>(json));
        }

        // PATCH /api/settings/me/assist/{key}
        [HttpPatch("assist/{key}")]
        public async Task<IActionResult> UpdateAssist(string key, [FromBody] UpdateAssistDto dto)
        {
            var validKeys = new[] { "emergency", "distress", "activeShooter", "emergency911", "emergency988" };
            if (!validKeys.Contains(key))
                return BadRequest(new ApiResponse(false, "Invalid assist key"));

            var userId = User.GetUserId();
            var setting = await GetOrCreateSettings(userId);

            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(setting.AssistSettings ?? "{}")!;
            var profile = dict.ContainsKey(key) ? dict[key] as Dictionary<string, object> : new Dictionary<string, object>();

            if (dto.AlarmSound.HasValue) profile["alarmSound"] = dto.AlarmSound.Value;
            if (dto.AlertMessage != null) profile["alertMessage"] = dto.AlertMessage;
            if (dto.FlashOn.HasValue) profile["flashOn"] = dto.FlashOn.Value;
            if (dto.IsEnabled.HasValue) profile["isEnabled"] = dto.IsEnabled.Value;
            if (dto.ScreenFlashing.HasValue) profile["screenFlashing"] = dto.ScreenFlashing.Value;

            dict[key] = profile;
            setting.AssistSettings = JsonSerializer.Serialize(dict);

            _settingRepo.Update(setting);
            await _settingRepo.SaveAsync();

            return Ok(new ApiResponse(true, "Assist settings updated"));
        }

        // POST /api/settings/me/terms
        [HttpPost("terms")]
        public async Task<IActionResult> AcceptTerms([FromBody] AcceptTermsDto dto)
        {
            var userId = User.GetUserId();
            var setting = await GetOrCreateSettings(userId);

            setting.TosId = dto.TosId;
            setting.TosVersion = dto.TosVersion;
            setting.TermsAcceptedAt = DateTime.UtcNow;

            _settingRepo.Update(setting);
            await _settingRepo.SaveAsync();

            return Ok(new ApiResponse(true, "Terms accepted"));
        }

        // GET /api/settings/me/terms
        [HttpGet("terms")]
        public async Task<IActionResult> GetTerms()
        {
            var userId = User.GetUserId();
            var setting = (await _settingRepo.FindAsync(s => s.UserId == userId)).FirstOrDefault();

            if (setting?.TermsAcceptedAt == null)
                return NotFound(new ApiResponse(false, "Terms not accepted"));

            return Ok(new
            {
                setting.TosId,
                setting.TosVersion,
                TermsAcceptedAt = setting.TermsAcceptedAt.Value.ToString("yyyy-MM-ddTHH:mm:ssZ")
            });
        }
    }
}
