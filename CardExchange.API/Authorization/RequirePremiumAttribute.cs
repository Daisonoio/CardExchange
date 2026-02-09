using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CardExchange.API.Authorization
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequirePremiumAttribute : TypeFilterAttribute
    {
        public RequirePremiumAttribute() : base(typeof(RequirePremiumFilter))
        {
        }
    }

    public class RequirePremiumFilter : IAsyncActionFilter
    {
        private readonly ISubscriptionService _subscriptionService;

        public RequirePremiumFilter(ISubscriptionService subscriptionService)
        {
            _subscriptionService = subscriptionService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var userIdClaim = context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                context.Result = new UnauthorizedObjectResult(new { message = "Autenticazione richiesta" });
                return;
            }

            var isPremium = await _subscriptionService.HasActiveSubscriptionAsync(userId);
            if (!isPremium)
            {
                context.Result = new ObjectResult(new
                {
                    status = 403,
                    message = "Funzionalità riservata agli utenti Premium",
                    upgradeUrl = "/api/subscriptions/plans"
                })
                {
                    StatusCode = 403
                };
                return;
            }

            await next();
        }
    }
}
