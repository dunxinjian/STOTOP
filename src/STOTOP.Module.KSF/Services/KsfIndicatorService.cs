using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.KSF.Dtos;
using STOTOP.Module.KSF.Entities;

namespace STOTOP.Module.KSF.Services;

public class KsfIndicatorService : IKsfIndicatorService
{
    private readonly STOTOPDbContext _context;

    public KsfIndicatorService(STOTOPDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResult<List<KsfIndicatorDto>>> GetListAsync(long orgId, bool? enabled = null)
    {
        var query = _context.Set<KsfIndicator>().AsQueryable();
        if (enabled.HasValue)
            query = query.Where(x => x.F是否启用 == enabled.Value);

        var entities = await query
            .OrderByDescending(x => x.FID)
            .ToListAsync();

        var list = entities.Select(MapToDto).ToList();
        return ApiResult<List<KsfIndicatorDto>>.Success(list);
    }

    public async Task<ApiResult<KsfIndicatorDto>> CreateAsync(long orgId, KsfIndicatorCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return ApiResult<KsfIndicatorDto>.Fail("指标编码不能为空");
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiResult<KsfIndicatorDto>.Fail("指标名称不能为空");

        // 编码组织内唯一
        var codeExists = await _context.Set<KsfIndicator>()
            .AnyAsync(x => x.F编码 == request.Code);
        if (codeExists)
            return ApiResult<KsfIndicatorDto>.Fail($"指标编码已存在：{request.Code}");

        // SQL 模板安全校验
        if (request.FetchType == 1 && !string.IsNullOrWhiteSpace(request.FetchSql))
        {
            var sqlCheck = await ValidateSqlTemplateAsync(request.FetchSql);
            if (sqlCheck.Code != 200 || !sqlCheck.Data)
                return ApiResult<KsfIndicatorDto>.Fail(sqlCheck.Message);
        }

        var entity = new KsfIndicator
        {
            FOrgId = orgId,
            F编码 = request.Code.Trim(),
            F名称 = request.Name.Trim(),
            F计量单位 = request.Unit ?? string.Empty,
            F取数类型 = request.FetchType,
            F取数SQL = request.FetchSql,
            F取数Agent = request.FetchAgent,
            F取数参数JSON = request.FetchParamsJson,
            F方向 = request.Direction <= 0 ? 1 : request.Direction,
            F业务对象类型 = string.IsNullOrWhiteSpace(request.BizObjectType) ? "KSF" : request.BizObjectType,
            F是否启用 = request.IsEnabled,
            F创建时间 = DateTime.Now,
            F更新时间 = DateTime.Now
        };

        _context.Set<KsfIndicator>().Add(entity);
        await _context.SaveChangesAsync();
        return ApiResult<KsfIndicatorDto>.Success(MapToDto(entity), "创建成功");
    }

    public async Task<ApiResult<KsfIndicatorDto>> UpdateAsync(long orgId, long id, KsfIndicatorCreateRequest request)
    {
        var entity = await _context.Set<KsfIndicator>()
            .AsTracking()
            .FirstOrDefaultAsync(x => x.FID == id);
        if (entity == null)
            return ApiResult<KsfIndicatorDto>.Fail("指标不存在");

        // 编码变更时校验唯一
        if (!string.Equals(entity.F编码, request.Code, StringComparison.Ordinal))
        {
            var codeExists = await _context.Set<KsfIndicator>()
                .AnyAsync(x => x.F编码 == request.Code && x.FID != id);
            if (codeExists)
                return ApiResult<KsfIndicatorDto>.Fail($"指标编码已存在：{request.Code}");
        }

        // SQL 模板安全校验
        if (request.FetchType == 1 && !string.IsNullOrWhiteSpace(request.FetchSql))
        {
            var sqlCheck = await ValidateSqlTemplateAsync(request.FetchSql);
            if (sqlCheck.Code != 200 || !sqlCheck.Data)
                return ApiResult<KsfIndicatorDto>.Fail(sqlCheck.Message);
        }

        entity.F编码 = request.Code.Trim();
        entity.F名称 = request.Name.Trim();
        entity.F计量单位 = request.Unit ?? string.Empty;
        entity.F取数类型 = request.FetchType;
        entity.F取数SQL = request.FetchSql;
        entity.F取数Agent = request.FetchAgent;
        entity.F取数参数JSON = request.FetchParamsJson;
        entity.F方向 = request.Direction <= 0 ? 1 : request.Direction;
        entity.F业务对象类型 = string.IsNullOrWhiteSpace(request.BizObjectType) ? "KSF" : request.BizObjectType;
        entity.F是否启用 = request.IsEnabled;
        entity.F更新时间 = DateTime.Now;

        await _context.SaveChangesAsync();
        return ApiResult<KsfIndicatorDto>.Success(MapToDto(entity), "更新成功");
    }

    public async Task<ApiResult> DeleteAsync(long orgId, long id)
    {
        var entity = await _context.Set<KsfIndicator>()
            .AsTracking()
            .FirstOrDefaultAsync(x => x.FID == id);
        if (entity == null)
            return ApiResult.Fail("指标不存在");

        // 检查是否被方案明细引用
        var inUse = await _context.Set<KsfPlanDetail>().AnyAsync(d => d.F指标ID == id);
        if (inUse)
            return ApiResult.Fail("该指标已被方案引用，无法删除");

        _context.Set<KsfIndicator>().Remove(entity);
        await _context.SaveChangesAsync();
        return ApiResult.Ok("删除成功");
    }

    public Task<ApiResult<bool>> ValidateSqlTemplateAsync(string sqlTemplate)
    {
        if (string.IsNullOrWhiteSpace(sqlTemplate))
            return Task.FromResult(ApiResult<bool>.Fail("SQL 模板不能为空"));

        var sql = sqlTemplate.Trim();
        var upper = sql.ToUpperInvariant();

        // 必须以 SELECT 或 WITH 开头
        if (!upper.StartsWith("SELECT") && !upper.StartsWith("WITH"))
            return Task.FromResult(ApiResult<bool>.Fail("SQL 模板必须以 SELECT 或 WITH 开头"));

        // 禁用关键字（按词边界匹配，避免误伤列名）
        var blacklist = new[]
        {
            "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER",
            "EXEC", "EXECUTE", "TRUNCATE", "GRANT", "REVOKE", "MERGE",
            "BACKUP", "RESTORE", "BULK", "OPENROWSET", "OPENQUERY", "OPENDATASOURCE"
        };
        foreach (var kw in blacklist)
        {
            if (Regex.IsMatch(upper, $@"\b{kw}\b"))
                return Task.FromResult(ApiResult<bool>.Fail($"SQL 模板包含禁用关键字：{kw}"));
        }

        // 禁用系统存储过程前缀
        if (Regex.IsMatch(upper, @"\bSP_") || Regex.IsMatch(upper, @"\bXP_"))
            return Task.FromResult(ApiResult<bool>.Fail("SQL 模板禁止调用系统存储过程（sp_/xp_）"));

        // 禁止多语句注入
        if (sql.TrimEnd(';', ' ', '\t', '\r', '\n').Contains(';'))
            return Task.FromResult(ApiResult<bool>.Fail("SQL 模板不允许包含多语句分号"));

        return Task.FromResult(ApiResult<bool>.Success(true, "SQL 校验通过"));
    }

    private static KsfIndicatorDto MapToDto(KsfIndicator e) => new()
    {
        Id = e.FID,
        OrgId = e.FOrgId,
        Code = e.F编码,
        Name = e.F名称,
        Unit = e.F计量单位,
        FetchType = e.F取数类型,
        FetchSql = e.F取数SQL,
        FetchAgent = e.F取数Agent,
        FetchParamsJson = e.F取数参数JSON,
        Direction = e.F方向,
        BizObjectType = e.F业务对象类型,
        IsEnabled = e.F是否启用,
        CreateTime = e.F创建时间,
        UpdateTime = e.F更新时间
    };
}
