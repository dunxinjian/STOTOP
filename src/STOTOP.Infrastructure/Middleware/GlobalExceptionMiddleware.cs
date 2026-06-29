using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;

namespace STOTOP.Infrastructure.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    /// <summary>
    /// 与 ASP.NET Core MVC 默认行为一致的 camelCase 序列化选项
    /// </summary>
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");
            await HandleExceptionAsync(context, ex, _env);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, IHostEnvironment env)
    {
        context.Response.ContentType = "application/json";

        // 对象级授权失败（如越权操作他人工作项）返回 403
        if (exception is UnauthorizedAccessException)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            var forbiddenResponse = ApiResult.Fail(exception.Message, 403);
            var forbiddenJson = JsonSerializer.Serialize(forbiddenResponse, CamelCaseOptions);
            await context.Response.WriteAsync(forbiddenJson);
            return;
        }

        // 对 InvalidOperationException 返回 400 并透传消息（如"用户名或密码错误"）
        if (exception is InvalidOperationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            var badRequestResponse = ApiResult.Fail(exception.Message, 400);
            var badRequestJson = JsonSerializer.Serialize(badRequestResponse, CamelCaseOptions);
            await context.Response.WriteAsync(badRequestJson);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        // 开发环境透出异常类型+消息便于定位，生产环境仅返回笼统提示
        var message = env.IsDevelopment()
            ? $"服务器内部错误: [{exception.GetType().Name}] {exception.Message}"
            : "服务器内部错误";

        // 对 DbUpdateException 额外透出 inner exception 详情
        if (env.IsDevelopment() && exception is System.Data.Common.DbException or Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            var inner = exception.InnerException;
            while (inner != null)
            {
                message += $" | Inner: [{inner.GetType().Name}] {inner.Message}";
                inner = inner.InnerException;
            }
        }

        var response = ApiResult.Fail(message, 500);
        var json = JsonSerializer.Serialize(response, CamelCaseOptions);
        await context.Response.WriteAsync(json);
    }
}

public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionMiddleware(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
