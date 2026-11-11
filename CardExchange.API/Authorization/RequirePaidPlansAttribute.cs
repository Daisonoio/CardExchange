using CardExchange.API.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace CardExchange.API.Authorization
{
    /// <summary>
    /// Endpoint attivo solo con i piani a pagamento accesi (Features:PaidPlans). Spenti: 404, come se non esistesse,
    /// così nessuno può pagare un piano o una promozione che oggi non dà nulla (le funzioni vengono dal tipo di profilo).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequirePaidPlansAttribute : Attribute, IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var options = context.HttpContext.RequestServices.GetService<IOptions<FeatureOptions>>();
            if (options?.Value.PaidPlans == true) return;
            context.Result = new NotFoundObjectResult(new { message = "Funzione non disponibile", code = "PAID_PLANS_OFF" });
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
