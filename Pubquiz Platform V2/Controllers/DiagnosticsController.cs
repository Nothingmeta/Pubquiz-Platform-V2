using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Pubquiz_Platform_V2.Controllers
{
    public class DiagnosticsController : Controller
    {
        [AllowAnonymous]
        public IActionResult Health()
        {
            return Json(new { status = "ok", timestamp = DateTime.UtcNow });
        }

        [AllowAnonymous]
        public IActionResult Config()
        {
            return Json(new 
            { 
                environment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                net_version = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
            });
        }
    }
}