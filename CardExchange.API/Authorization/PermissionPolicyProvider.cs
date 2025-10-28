using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CardExchange.API.Authorization
{
    public class PermissionPolicyProvider : IAuthorizationPolicyProvider
    {
        private readonly DefaultAuthorizationPolicyProvider _fallbackPolicyProvider;

        public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        {
            _fallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
        }

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
        {
            return _fallbackPolicyProvider.GetDefaultPolicyAsync();
        }

        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
        {
            return _fallbackPolicyProvider.GetFallbackPolicyAsync();
        }

        public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            // Se il policy name contiene virgole, sono permessi multipli
            if (policyName.Contains(','))
            {
                var permissions = policyName.Split(',', StringSplitOptions.RemoveEmptyEntries);

                var policy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement(permissions))
                    .Build();

                return Task.FromResult<AuthorizationPolicy?>(policy);
            }

            // Altrimenti usa il fallback provider
            return _fallbackPolicyProvider.GetPolicyAsync(policyName);
        }
    }
}