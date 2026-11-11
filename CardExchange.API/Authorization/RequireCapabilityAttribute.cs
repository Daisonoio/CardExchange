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
                    message = CapabilityText.UpgradeMessage(_capability)
                }) { StatusCode = 403 };
                return;
            }
            await next();
        }
    }

    public static class CapabilityText
    {
        /// <summary>Messaggio quando manca una funzione: dice a quale tipo di profilo è riservata.</summary>
        public static string UpgradeMessage(Capability c) => c switch
        {
            Capability.FeaturedStore or Capability.LocalDemand or Capability.Buylist or Capability.Reservations or Capability.Loyalty
                => "Funzione riservata ai negozi",
            Capability.SellerShowcase => "Funzione riservata ai venditori",
            Capability.EventExhibitors => "Funzione riservata a negozi e organizzatori",
            _ => "Funzione non disponibile"
        };
    }
}
