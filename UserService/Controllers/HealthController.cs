using Microsoft.AspNetCore.Mvc;
using UserService.Modles;

namespace UserService.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "healthy" });
}