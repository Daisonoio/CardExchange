using Microsoft.AspNetCore.Authorization;

namespace CardExchange.API.Authorization
{
    /// <summary>
    /// Attributo per richiedere uno o più permessi specifici
    /// </summary>
    public class RequirePermissionAttribute : AuthorizeAttribute
    {
        public RequirePermissionAttribute(params string[] permissions)
        {
            // Combina i permessi in un singolo string separato da virgole
            Policy = string.Join(",", permissions.Select(p => p.Trim()));
        }
    }
}