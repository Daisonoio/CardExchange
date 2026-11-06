using CardExchange.API.Services.Entitlements;
using CardExchange.Core.Rules;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CardExchange.API.Authorization
{
    /// <summary>La funzione richiede un diritto del piano; altrimenti 403 con l'invito al piano giusto.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireCapabilityAttribute : TypeFilterAttribute
    {
        public RequireCapabilityAttribute(Capability capability) : base(typeof(RequireCapabilityFilter))
        {
            Arguments = new object[] { capability };
        }
    }

    public class RequireCapabilityFilter : IAsyncActionFilter
    {
        private readonly IEntitlementService _entitlements;
        private readonly Capability _capability;

        public RequireCapabilityFilter(IEntitlementService entitlements, Capability capability)
        {
            _entitlements = entitlements;
            _capability = capability;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var claim = context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (claim == null || !int.TryParse(claim.Value, out var userId))
            {
                context.Result = new UnauthorizedObjectResult(new { message = "Autenticazione richiesta" });
                return;
            }
            if (!await _entitlements.HasAsync(userId, _capability))
            {
                context.Result = new ObjectResult(new
                {
                    status = 403,
                    code = "PLAN_REQUIRED",
                    capability = _capability.ToString(),
                    message = CapabilityText.UpgradeMessage(_capability),
                    upgradeUrl = "/premium"
                }) { StatusCode = 403 };
                return;
            }
            await next();
        }
    }

    public static class CapabilityText
    {
        public static string UpgradeMessage(Capability c) => c switch
        {
            Capability.FeaturedStore => "Funzione inclusa nel piano Negozio",
            Capability.LocalDemand => "La domanda locale è inclusa nel piano Negozio Pro",
            Capability.Buylist => "La lista acquisti è inclusa nel piano Negozio Pro",
            Capability.Reservations => "Prenota e ritira è incluso nel piano Negozio Pro",
            Capability.Loyalty => "La tessera fedeltà è inclusa nel piano Negozio",
            Capability.SellerShowcase => "Funzione inclusa nel piano Banco",
            _ => "Funzionalità riservata agli utenti Premium"
        };
    }
}
