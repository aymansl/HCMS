using System.Security.Claims;
using HCMS4.Shared.Identity.Models;
using Microsoft.AspNetCore.Identity;

namespace HCMS4.Shared.Identity.Services
{
    /// <summary>The signed-in user of the current request. Replaces the lookups that lived in BaseController.</summary>
    public interface ICurrentUserService
    {
        /// <summary>Identity user id, or null for anonymous requests.</summary>
        string? UserId { get; }

        Task<ApplicationUser?> GetUserAsync();
    }

    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserManager<ApplicationUser> _userManager;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor, UserManager<ApplicationUser> userManager)
        {
            _httpContextAccessor = httpContextAccessor;
            _userManager = userManager;
        }

        public string? UserId => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

        public async Task<ApplicationUser?> GetUserAsync()
        {
            var userId = UserId;
            return string.IsNullOrEmpty(userId) ? null : await _userManager.FindByIdAsync(userId);
        }
    }
}
