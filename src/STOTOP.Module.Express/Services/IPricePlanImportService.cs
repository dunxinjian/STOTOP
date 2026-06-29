using Microsoft.AspNetCore.Http;
using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

/// <summary>
/// 报价方案导入服务接口
/// </summary>
public interface IPricePlanImportService
{
    /// <summary>生成报价方案Excel模板</summary>
    Task<byte[]> GenerateTemplateAsync();

    /// <summary>从 Excel导入报价方案</summary>
    Task<QuotationDto> ImportFromExcelAsync(string brandCode, string planName, IFormFile file);
}
