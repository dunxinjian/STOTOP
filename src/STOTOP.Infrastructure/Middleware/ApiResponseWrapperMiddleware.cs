using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace STOTOP.Infrastructure.Middleware;

public class ApiResponseWrapperMiddleware
{
    private readonly RequestDelegate _next;

    public ApiResponseWrapperMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 直接传递，不进行包装
        // Controller 直接返回 ApiResult 即可
        await _next(context);
    }
}

public static class ApiResponseWrapperMiddlewareExtensions
{
    public static IApplicationBuilder UseApiResponseWrapperMiddleware(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApiResponseWrapperMiddleware>();
    }
}
