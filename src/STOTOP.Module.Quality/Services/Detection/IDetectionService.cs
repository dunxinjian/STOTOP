using STOTOP.Core.Models;

namespace STOTOP.Module.Quality.Services.Detection;

public interface IDetectionService
{
    /// <summary>执行规则检测</summary>
    Task<ApiResult<int>> RunDetectionAsync(long orgId, long? ruleId = null);
    /// <summary>检测单条规则</summary>
    Task<ApiResult<bool>> DetectByRuleAsync(long orgId, long ruleId);
}
