using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace CardExchange.API.Authorization
{
    public class PermissionRequirement : IAuthorizationRequirement
    {
        public string[] Permissions { get; }

        public PermissionRequirement(params string[] permissions)
        {
            Permissions = permissions;
        }
    }

    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            // Verifica che l'utente abbia almeno uno dei permessi richiesti
            var userPermissions = context.User.FindAll("Permission").Select(c => c.Value).ToList();

            if (requirement.Permissions.Any(requiredPermission => userPermissions.Contains(requiredPermission)))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}