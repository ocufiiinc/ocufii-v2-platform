using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OcufiiAPI.DTO;
using OcufiiAPI.Models;
using OcufiiAPI.Repositories;
using OcufiiAPI.Extensions;

namespace OcufiiAPI.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IRepository<User> _userRepo;
        private readonly IRepository<Role> _roleRepo;

        public UsersController(IRepository<User> userRepo, IRepository<Role> roleRepo)
        {
            _userRepo = userRepo;
            _roleRepo = roleRepo;
        }

        // GET /api/users/{id}
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var currentUserId = User.GetUserId();
            var isAdmin = User.IsInRole("admin");

            if (!isAdmin && currentUserId != id)
                return Forbid();

            var user = await _userRepo.GetByIdAsync(id);
            if (user == null || user.IsDeleted)
                return NotFound(new ApiResponse(false, "User not found"));

            return Ok(new ApiResponse(true, "User found")
            {
                Data = new
                {
                    user.UserId,
                    user.Email,
                    user.FirstName,
                    user.LastName,
                    user.PhoneNumber,
                    user.Company,
                    user.IsEnabled,
                    user.AccountType
                }
            });
        }

        // GET /api/users
        [HttpGet]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var users = await _userRepo.FindAsync(u => !u.IsDeleted);
            var total = users.Count();
            var items = users.Skip((page - 1) * pageSize).Take(pageSize);

            return Ok(new
            {
                items = items.Select(u => new { u.UserId, u.Email, u.FirstName, u.IsEnabled }),
                page,
                pageSize,
                total
            });
        }

        // PATCH /api/users/{id}
        [HttpPatch("{id:guid}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProfileDto dto)
        {
            var user = await _userRepo.GetByIdAsync(id);
            if (user == null || user.IsDeleted) return NotFound();

            if (dto.FirstName != null) user.FirstName = dto.FirstName;
            if (dto.LastName != null) user.LastName = dto.LastName;
            if (dto.PhoneNumber != null) user.PhoneNumber = dto.PhoneNumber;
            if (dto.Company != null) user.Company = dto.Company;

            user.DateUpdated = DateTime.UtcNow;
            _userRepo.Update(user);
            await _userRepo.SaveAsync();

            return Ok(new ApiResponse(true, "User updated"));
        }

        // PATCH /api/users/{id}/status
        [HttpPatch("{id:guid}/status")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateUserStatusDto dto)
        {
            var user = await _userRepo.GetByIdAsync(id);
            if (user == null || user.IsDeleted) return NotFound();

            user.IsEnabled = dto.IsEnabled;
            user.DateUpdated = DateTime.UtcNow;
            _userRepo.Update(user);
            await _userRepo.SaveAsync();

            return Ok(new ApiResponse(true, "Status updated"));
        }

        // PATCH /api/users/me
        [HttpPatch("me")]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileDto dto)
        {
            var userId = User.GetUserId();
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null) return NotFound();

            if (dto.FirstName != null) user.FirstName = dto.FirstName;
            if (dto.LastName != null) user.LastName = dto.LastName;
            if (dto.PhoneNumber != null) user.PhoneNumber = dto.PhoneNumber;
            if (dto.Company != null) user.Company = dto.Company;

            user.DateUpdated = DateTime.UtcNow;
            _userRepo.Update(user);
            await _userRepo.SaveAsync();

            return Ok(new ApiResponse(true, "Profile updated"));
        }

        // DELETE /api/users/{id}
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var user = await _userRepo.GetByIdAsync(id);
            if (user == null || user.IsDeleted) return NotFound();

            user.IsDeleted = true;
            user.DateUpdated = DateTime.UtcNow;
            _userRepo.Update(user);
            await _userRepo.SaveAsync();

            return NoContent();
        }
    }
}
