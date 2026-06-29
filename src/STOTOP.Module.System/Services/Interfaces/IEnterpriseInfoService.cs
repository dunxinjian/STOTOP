using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;

namespace STOTOP.Module.System.Services.Interfaces;

/// <summary>
/// 企业信息服务接口
/// </summary>
public interface IEnterpriseInfoService
{
    /// <summary>
    /// 获取企业信息
    /// </summary>
    Task<ApiResult<EnterpriseInfoDto>> GetEnterpriseInfoAsync();

    /// <summary>
    /// 更新企业信息
    /// </summary>
    Task<ApiResult<EnterpriseInfoDto>> UpdateEnterpriseInfoAsync(EnterpriseInfoUpdateDto dto);
}
