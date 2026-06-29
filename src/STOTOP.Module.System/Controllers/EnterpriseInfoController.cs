using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Controllers;

/// <summary>
/// 企业信息配置控制器
/// </summary>
[Route("api/system/enterprise-info")]
[ApiController]
public class EnterpriseInfoController : ControllerBase
{
    private readonly IEnterpriseInfoService _enterpriseInfoService;

    public EnterpriseInfoController(IEnterpriseInfoService enterpriseInfoService)
    {
        _enterpriseInfoService = enterpriseInfoService;
    }

    /// <summary>
    /// 获取企业信息（无需认证，登录页也需要用）
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ApiResult<EnterpriseInfoDto>> Get()
    {
        return await _enterpriseInfoService.GetEnterpriseInfoAsync();
    }

    /// <summary>
    /// 更新企业信息（需管理员权限）
    /// </summary>
    [HttpPut]
    [Authorize]
    public async Task<ApiResult<EnterpriseInfoDto>> Update([FromBody] EnterpriseInfoUpdateDto dto)
    {
        return await _enterpriseInfoService.UpdateEnterpriseInfoAsync(dto);
    }
}
