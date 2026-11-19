using Microsoft.AspNetCore.Authorization;

namespace HCMS4.Shared.Identity.Authorization
{
    public static class AuthorizationServiceCollectionExtensions
    {
        /// <summary>
        /// One policy per permission, satisfied by any role that holds it (<see cref="RolePermissions"/>).
        /// Every endpoint also requires a signed-in user unless it is marked [AllowAnonymous].
        /// </summary>
        public static IServiceCollection AddHcmsAuthorization(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();

                foreach (var permission in RolePermissions.AllPermissions)
                {
                    var roles = RolePermissions.RolesWith(permission);
                    if (roles.Count == 0)
                    {
                        throw new InvalidOperationException($"Permission '{permission}' is not given to any role.");
                    }

                    options.AddPolicy(permission, policy => policy.RequireRole(roles));
                }
            });

            return services;
        }
    }
}
