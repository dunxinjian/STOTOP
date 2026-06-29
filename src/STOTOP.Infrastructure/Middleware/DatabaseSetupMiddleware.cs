using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace STOTOP.Infrastructure.Middleware;

public class DatabaseSetupMiddleware
{
    private readonly RequestDelegate _next;

    public DatabaseSetupMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);
    }
}

public static class DatabaseSetupMiddlewareExtensions
{
    public static IApplicationBuilder UseDatabaseSetupMiddleware(this IApplicationBuilder app)
    {
        return app.UseMiddleware<DatabaseSetupMiddleware>();
    }
}
