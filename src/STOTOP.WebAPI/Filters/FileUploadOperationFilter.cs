using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace STOTOP.WebAPI.Filters;

/// <summary>
/// 解决 Swashbuckle 无法正确处理 IFormFile 参数的 Schema 生成问题
/// 将 IFormFile 及 [FromForm] 参数统一为 multipart/form-data RequestBody
/// </summary>
public class FileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // 查找所有 IFormFile / IFormFile? 类型的参数
        var formFileParams = context.MethodInfo.GetParameters()
            .Where(p => IsFormFileType(p.ParameterType))
            .ToList();

        if (formFileParams.Count == 0)
            return;

        // 区分 form 参数和 query/route/header 参数
        var formParamNames = new HashSet<string>();
        var schemaProps = new Dictionary<string, OpenApiSchema>();
        var requiredProps = new HashSet<string>();

        foreach (var param in context.MethodInfo.GetParameters())
        {
            var isFormFile = IsFormFileType(param.ParameterType);
            var hasFromForm = param.GetCustomAttributes(true)
                .Any(a => a.GetType().Name == "FromFormAttribute");
            var hasFromOther = param.GetCustomAttributes(true)
                .Any(a => a.GetType().Name is "FromQueryAttribute" or "FromRouteAttribute" or "FromHeaderAttribute" or "FromBodyAttribute");

            // IFormFile 隐式属于 form，或明确标注 [FromForm] 的参数
            if ((isFormFile || hasFromForm) && !hasFromOther)
            {
                formParamNames.Add(param.Name!);
                schemaProps[param.Name!] = isFormFile
                    ? new OpenApiSchema { Type = "string", Format = "binary" }
                    : new OpenApiSchema { Type = MapClrTypeToOpenApiType(param.ParameterType) };

                if (!param.HasDefaultValue && param.ParameterType.IsValueType
                    && Nullable.GetUnderlyingType(param.ParameterType) == null)
                {
                    requiredProps.Add(param.Name!);
                }
            }
        }

        if (schemaProps.Count == 0)
            return;

        operation.RequestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Required = requiredProps,
                        Properties = schemaProps
                    }
                }
            }
        };

        // 仅移除已归入 RequestBody 的 form 参数，保留 query/route/header 参数
        if (operation.Parameters != null)
        {
            var toRemove = operation.Parameters
                .Where(p => formParamNames.Contains(p.Name))
                .ToList();
            foreach (var p in toRemove)
                operation.Parameters.Remove(p);
        }
    }

    private static bool IsFormFileType(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t == typeof(IFormFile)
            || t == typeof(IFormFileCollection)
            || t == typeof(Stream);
    }

    private static string MapClrTypeToOpenApiType(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return Type.GetTypeCode(t) switch
        {
            TypeCode.Int32 or TypeCode.Int64 => "integer",
            TypeCode.Decimal or TypeCode.Double or TypeCode.Single => "number",
            TypeCode.Boolean => "boolean",
            TypeCode.DateTime => "string",
            _ => "string"
        };
    }
}
