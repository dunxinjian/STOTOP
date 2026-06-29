using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace STOTOP.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VersionController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public VersionController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult GetVersion()
    {
        return Ok(new
        {
            version = _configuration["AppVersion"] ?? "unknown",
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
        });
    }
}
