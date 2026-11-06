using System.Security.Claims;
using CardExchange.API.Services.Entitlements;
using CardExchange.Core.Rules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardExchange.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/me")]
    public class MeController : ControllerBase
    {
        private readonly IEntitlementService _entitlements;
        public MeController(IEntitlementService entitlements) => _entitlements = entitlements;

        /// <summary>Diritti effettivi dell'utente (piano, funzioni, limiti): l'interfaccia mostra o blocca le funzioni da qui.</summary>
        [HttpGet("entitlements")]
        public async Task<IActionResult> Entitlements()
        {
            var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
            var e = await _entitlements.GetAsync(userId);
            return Ok(new
            {
                tier = e.Tier.ToString(),
                planName = e.PlanName,
                isPaid = e.IsPaid,
                capabilities = e.Capabilities.Select(c => c.ToString()).OrderBy(c => c),
                // int.MaxValue = illimitato → null
                limits = e.Limits.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value == PlanCatalog.Unlimited ? (int?)null : kv.Value)
            });
        }
    }
}
