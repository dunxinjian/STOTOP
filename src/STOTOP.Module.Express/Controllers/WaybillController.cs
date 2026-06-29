using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Services;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 运单管理
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/waybills")]
public class WaybillController : ControllerBase
{
    private readonly IWaybillService _waybillService;
    private readonly IWaybillImportService _importService;
    private readonly ShopAutoDiscoveryJob _discoveryJob;

    public WaybillController(
        IWaybillService waybillService,
        IWaybillImportService importService,
        ShopAutoDiscoveryJob discoveryJob)
    {
        _waybillService = waybillService;
        _importService = importService;
        _discoveryJob = discoveryJob;
    }

    /// <summary>
    /// 分页查询运单列表
    /// </summary>
    [HttpGet]
    public async Task<ApiResult<PagedResult<WaybillListItemDto>>> GetList([FromQuery] WaybillQueryRequest request)
    {
        var result = await _waybillService.GetListAsync(request);
        return ApiResult<PagedResult<WaybillListItemDto>>.Success(result);
    }

    /// <summary>
    /// 获取运单详情
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ApiResult<WaybillDto>> GetById(long id)
    {
        var result = await _waybillService.GetByIdAsync(id);
        if (result == null)
            return ApiResult<WaybillDto>.Fail("运单不存在");
        return ApiResult<WaybillDto>.Success(result);
    }

    /// <summary>
    /// 按运单号+品牌查询
    /// </summary>
    [HttpGet("by-no")]
    public async Task<ApiResult<WaybillDto>> GetByNo([FromQuery] string waybillNo, [FromQuery] string brandCode)
    {
        var result = await _waybillService.GetByWaybillNoAsync(waybillNo, brandCode);
        if (result == null)
            return ApiResult<WaybillDto>.Fail("运单不存在");
        return ApiResult<WaybillDto>.Success(result);
    }

    /// <summary>
    /// Excel导入运单
    /// </summary>
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    public async Task<ApiResult<WaybillImportResult>> Import(IFormFile file, [FromQuery] string brandCode)
    {
        if (file == null || file.Length == 0)
            return ApiResult<WaybillImportResult>.Fail("请选择要导入的文件");

        using var stream = file.OpenReadStream();
        var result = await _importService.ImportAsync(stream, brandCode);
        return ApiResult<WaybillImportResult>.Success(result, "导入完成");
    }

    /// <summary>
    /// 手动触发新店铺发现
    /// </summary>
    [HttpPost("discover-shops")]
    public async Task<ApiResult<ShopDiscoveryResult>> DiscoverShops()
    {
        var result = await _discoveryJob.ExecuteAsync();
        return ApiResult<ShopDiscoveryResult>.Success(result, $"发现 {result.NewShopsCount} 个新店铺");
    }
}
