using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;

namespace STOTOP.Module.System.Services.Interfaces;

public interface ICodeRuleService
{
    Task<string> GenerateNextCodeAsync(string ruleCode, long? orgId = null);
    Task<List<string>> GenerateBatchCodesAsync(string ruleCode, int count, long? orgId = null);
    Task<ApiResult<List<CodeRuleDto>>> GetAllRulesAsync();
    Task<ApiResult<CodeRuleDto>> GetRuleByIdAsync(long id);
    Task<ApiResult<CodeRuleDto>> UpdateRuleAsync(long id, CodeRuleUpdateDto dto);
    Task<ApiResult<CodeRuleDto>> CreateRuleAsync(CodeRuleCreateDto dto);
    Task<ApiResult> DeleteRuleAsync(long id);
    Task<ApiResult<string>> PreviewCodeAsync(long id);
}
