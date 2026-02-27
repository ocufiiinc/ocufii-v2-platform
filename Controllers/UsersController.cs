using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OcufiiAPI.DTO;
using OcufiiAPI.Models;
using OcufiiAPI.Repositories;
using System.Text.Json;
using OcufiiAPI.Extensions;
using Microsoft.Extensions.Options;
using OcufiiAPI.Configs;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace OcufiiAPI.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 401)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public class UsersController : ControllerBase
    {
        private readonly IRepository<User> _userRepo;
        private readonly IRepository<Role> _roleRepo;

        public UsersController(IRepository<User> userRepo, IRepository<Role> roleRepo)
        {
            _userRepo = userRepo;
            _roleRepo = roleRepo;
        }

        [Authorize]
        [HttpGet("{id:guid}")]
        [SwaggerOperation(
            Summary = "Get User by ID",
            Description = "Retrieves basic user information by ID. Admins can access any user; regular users can only access their own profile."
        )]
        [SwaggerResponse(200, "User found", typeof(ApiResponse))]
        [SwaggerResponse(401, "Unauthorized - missing or invalid token")]
        [SwaggerResponse(403, "Forbidden - not authorized to view this user")]
        [SwaggerResponse(404, "User not found or deleted")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var currentUserId = User.GetUserId();
            var isAdmin = User.IsInRole("admin");

            if (!isAdmin && currentUserId != id)
                return Forbid();

            var user = await _userRepo.GetByIdAsync(id);
            if (user == null || user.IsDeleted)
                return NotFound(new ApiResponse(false, "User not found")
                {
                    ErrorCode = "OC-055"
                });

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
                },
                ErrorCode = null
            });
        }

        [HttpGet]
        [Authorize(Roles = "admin")]
        [SwaggerOperation(
            Summary = "List Users (Admin Only)",
            Description = "Returns paginated list of active users. Only accessible to admin role."
        )]
        [SwaggerResponse(200, "Users retrieved", typeof(object))]
        [SwaggerResponse(401, "Unauthorized - missing or invalid token")]
        [SwaggerResponse(403, "Forbidden - not admin")]
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

        [HttpPatch("{id:guid}")]
        [Authorize(Policy = "CanEditOwnProfile")]
        [SwaggerOperation(
            Summary = "Update User Profile",
            Description = "Partially updates user profile fields. Only the user themselves or admins can update."
        )]
        [SwaggerResponse(200, "User updated")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden - not authorized")]
        [SwaggerResponse(404, "User not found")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProfileDto dto)
        {
            var user = await _userRepo.GetByIdAsync(id);
            if (user == null || user.IsDeleted) return NotFound(new ApiResponse(false, "User not found")
            {
                ErrorCode = "OC-055"
            });

            if (dto.FirstName != null) user.FirstName = dto.FirstName;
            if (dto.LastName != null) user.LastName = dto.LastName;
            if (dto.PhoneNumber != null) user.PhoneNumber = dto.PhoneNumber;
            if (dto.Company != null) user.Company = dto.Company;

            user.DateUpdated = DateTime.UtcNow;
            _userRepo.Update(user);
            await _userRepo.SaveAsync();

            return Ok(new ApiResponse(true, "User updated")
            {
                ErrorCode = null
            });
        }

        [HttpPatch("{id:guid}/status")]
        [Authorize(Roles = "admin")]
        [SwaggerOperation(
            Summary = "Update User Status (Admin Only)",
            Description = "Enables or disables a user account. Admin only."
        )]
        [SwaggerResponse(200, "Status updated")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden - not admin")]
        [SwaggerResponse(404, "User not found")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateUserStatusDto dto)
        {
            var user = await _userRepo.GetByIdAsync(id);
            if (user == null || user.IsDeleted) return NotFound(new ApiResponse(false, "User not found")
            {
                ErrorCode = "OC-055"
            });

            user.IsEnabled = dto.IsEnabled;
            user.DateUpdated = DateTime.UtcNow;

            _userRepo.Update(user);
            await _userRepo.SaveAsync();

            return Ok(new ApiResponse(true, "Status updated")
            {
                ErrorCode = null
            });
        }

        [HttpPatch("me")]
        [Authorize(Policy = "CanEditOwnProfile")]
        [SwaggerOperation(
            Summary = "Update Current User's Profile",
            Description = "Partially updates the authenticated user's own profile fields"
        )]
        [SwaggerResponse(200, "Profile updated")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(404, "User not found")]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileDto dto)
        {
            var userId = User.GetUserId();
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null) return NotFound(new ApiResponse(false, "User not found")
            {
                ErrorCode = "OC-055"
            });

            if (dto.FirstName != null) user.FirstName = dto.FirstName;
            if (dto.LastName != null) user.LastName = dto.LastName;
            if (dto.PhoneNumber != null) user.PhoneNumber = dto.PhoneNumber;
            if (dto.Company != null) user.Company = dto.Company;

            user.DateUpdated = DateTime.UtcNow;
            _userRepo.Update(user);
            await _userRepo.SaveAsync();

            return Ok(new ApiResponse(true, "Profile updated")
            {
                ErrorCode = null
            });
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "CanEditOwnProfile")]
        [SwaggerOperation(
            Summary = "Delete User (Soft Delete)",
            Description = "Soft-deletes the user account (sets IsDeleted = true). Only the user themselves or admins can delete."
        )]
        [SwaggerResponse(204, "User deleted")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden - not authorized")]
        [SwaggerResponse(404, "User not found")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var user = await _userRepo.GetByIdAsync(id);
            if (user == null || user.IsDeleted) return NotFound(new ApiResponse(false, "User not found")
            {
                ErrorCode = "OC-055"
            });

            user.IsDeleted = true;
            user.DateUpdated = DateTime.UtcNow;

            _userRepo.Update(user);
            await _userRepo.SaveAsync();

            return NoContent();
        }
    }
}