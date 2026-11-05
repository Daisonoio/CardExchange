using Microsoft.AspNetCore.Mvc;

namespace CardExchange.API.Services.Stores
{
    public static class StoreActivityExtensions
    {
        /// <summary>Il registratore delle attività, se disponibile (nei test che creano i controller a mano non c'è).</summary>
        public static IStoreActivityRecorder? StoreActivity(this ControllerBase controller) =>
            controller.HttpContext?.RequestServices?.GetService(typeof(IStoreActivityRecorder)) as IStoreActivityRecorder;

        public static string? VisitorKey(this ControllerBase controller) =>
            controller.HttpContext?.Connection.RemoteIpAddress?.ToString();
    }
}
