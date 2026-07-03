using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services.Interfaces;
using STOTOP.Infrastructure.Events;
using System.Security.Claims;

namespace STOTOP.Module.System.Services;

public class OrganizationService : IOrganizationService
{
    private readonly STOTOPDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IChangeLogService _changeLogService;
    private readonly IEventDispatcher _eventDispatcher;

    public OrganizationService(STOTOPDbContext context, IHttpContextAccessor httpContextAccessor, IChangeLogService changeLogService, IEventDispatcher eventDispatcher)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _changeLogService = changeLogService;
        _eventDispatcher = eventDispatcher;
    }

    private (long? UserId, string? UserName) GetCurrentUser()
    {
        var claims = _httpContextAccessor.HttpContext?.User;
        var userIdStr = claims?.FindFirst("userId")?.Value ?? claims?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userName = claims?.FindFirst("userName")?.Value ?? claims?.FindFirst(ClaimTypes.Name)?.Value;
        long? userId = long.TryParse(userIdStr, out var id) ? id : null;
        return (userId, userName);
    }

    public async Task<ApiResult<List<OrganizationDto>>> GetTreeAsync()
    {
        var orgs = await _context.Set<SysOrganization>()
            .Include(o => o.OrgType)
            .OrderBy(o => o.FSort)
            .ThenBy(o => o.FCreateTime)
            .ToListAsync();

        var dtoList = orgs.Select(o => MapToDto(o)).ToList();

        var tree = BuildTree(dtoList);
        return ApiResult<List<OrganizationDto>>.Success(tree);
    }

    public async Task<ApiResult<List<OrganizationDto>>> GetOrgChartAsync()
    {
        var orgs = await _context.Set<SysOrganization>()
            .Include(o => o.Manager)
            .OrderBy(o => o.FSort)
            .ThenBy(o => o.FCreateTime)
            .ToListAsync();

        // 统计每个组织的实际人数
        var orgActualCounts = await _context.Set<SysUserOrganization>()
            .GroupBy(uo => uo.FOrgId)
            .Select(g => new { OrgId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OrgId, x => x.Count);

        // 查询钉钉绑定状态
        var dtoList = orgs.Select(o =>
        {
            var dto = MapToDto(o);
            dto.ManagerName = o.Manager?.FName;
            dto.ActualCount = orgActualCounts.GetValueOrDefault(o.FID, 0);
            return dto;
        }).ToList();

        var tree = BuildTree(dtoList);
        return ApiResult<List<OrganizationDto>>.Success(tree);
    }

    public async Task<ApiResult<OrganizationDto>> CreateAsync(CreateOrganizationRequest request)
    {
        if (await _context.Set<SysOrganization>().AnyAsync(o => o.FCode == request.Code))
        {
            return ApiResult<OrganizationDto>.Fail("组织编码已存在");
        }

        // 查询新节点的组织类型
        var orgType = await _context.Set<SysOrgType>().FindAsync(request.TypeId);
        if (orgType == null)
            return ApiResult<OrganizationDto>.Fail($"组织类型 {request.TypeId} 不存在");

        // 公司级组织(集团/区域公司)需强制可切换并自动关联 admin（M4：由 FKind 派生，替代 FCode 字面量）
        var isCompanyLevel = orgType.FKind == (int)OrgKind.Group || orgType.FKind == (int)OrgKind.Region;

        // 合法父子校验（M4：FKind 对合法性，支持跳级变深度）
        var (levelError, parentNode) = await ValidateOrgKindAsync(orgType, request.ParentId);
        if (levelError != null)
            return ApiResult<OrganizationDto>.Fail(levelError);

#pragma warning disable CS0618
        var org = new SysOrganization
        {
            FUID = Guid.NewGuid().ToString("N"),
            FName = request.Name,
            FCode = request.Code,
            FParentId = request.ParentId,
            FTypeId = request.TypeId,
            FKind = orgType.FKind,
            FParentKind = parentNode?.FKind, // 写入前置好，满足 CK_合法父子（RebuildAll 事后重算兜底）
            FType = orgType.FName,
            FSort = request.Sort,
            FStatus = request.Status,
            FDingTalkDeptId = request.DingTalkDeptId,
            FManagerId = request.ManagerId,
            FHeadcount = request.Headcount,
           // 子公司/集团类型强制可切换（确保 admin 切换时不会报"未列入切换列表"）
            FIsSwitchable = isCompanyLevel || (orgType.FCanSwitch && request.IsSwitchable),
            FDescription = request.Description
        };
#pragma warning restore CS0618

        await _context.Set<SysOrganization>().AddAsync(org);
        await _context.SaveChangesAsync();

        // 子公司/集团类型自动关联 admin 用户
        if (isCompanyLevel)
        {
            await EnsureAdminOrgAssociationAsync(org.FID);
        }
        // 其他可切换组织也自动关联 admin
        else if (org.FIsSwitchable)
        {
            await EnsureAdminOrgAssociationAsync(org.FID);
        }

        // M4：新节点入树后重算物化字段(父类别/网点公司/范围根/路径) + 闭包
        OrgTreeMaterializer.RebuildAll(_context);

        var dto = MapToDto(org);

        // 记录变更日志
        var (userId, userName) = GetCurrentUser();
        await _changeLogService.LogChangeAsync("组织架构", org.FID, org.FName,
            "创建", $"创建组织：{org.FName}（{org.FCode}），类型：{orgType.FName}", userId, userName);

        return ApiResult<OrganizationDto>.Success(dto);
    }

    public async Task<ApiResult<OrganizationDto>> UpdateAsync(long id, UpdateOrganizationRequest request)
    {
        var org = await _context.Set<SysOrganization>()
            .AsTracking()
            .FirstOrDefaultAsync(o => o.FID == id);
        if (org == null)
        {
            return ApiResult<OrganizationDto>.Fail("组织不存在");
        }

        if (await _context.Set<SysOrganization>().AnyAsync(o => o.FCode == request.Code && o.FID != id))
        {
            return ApiResult<OrganizationDto>.Fail("组织编码已存在");
        }

        // 查询新组织类型
        var orgType = await _context.Set<SysOrgType>().FindAsync(request.TypeId);
        if (orgType == null)
            return ApiResult<OrganizationDto>.Fail($"组织类型 {request.TypeId} 不存在");

        // 公司级组织(集团/区域公司)需强制可切换并自动关联 admin（M4：由 FKind 派生）
        var isCompanyLevel = orgType.FKind == (int)OrgKind.Group || orgType.FKind == (int)OrgKind.Region;

        // 合法父子校验（M4：FKind 对合法性）
        var (levelError, parentNode) = await ValidateOrgKindAsync(orgType, request.ParentId);
        if (levelError != null)
            return ApiResult<OrganizationDto>.Fail(levelError);

        // 记录旧值用于对比
#pragma warning disable CS0618
        var oldName = org.FName;
        var oldCode = org.FCode;
        var oldParentId = org.FParentId;
        var oldType = org.FType;
        var oldSort = org.FSort;
        var oldStatus = org.FStatus;
        var oldIsSwitchable = org.FIsSwitchable;
        var oldTypeId = org.FTypeId;
#pragma warning restore CS0618

#pragma warning disable CS0618
        org.FName = request.Name;
        org.FCode = request.Code;
        org.FParentId = request.ParentId;
        org.FTypeId = request.TypeId;
        org.FKind = orgType.FKind;
        org.FParentKind = parentNode?.FKind; // reparent/改类别时同步置好，满足 CK_合法父子
        org.FType = orgType.FName;
        org.FSort = request.Sort;
        if (request.Status.HasValue)
            org.FStatus = request.Status.Value;
        org.FDingTalkDeptId = request.DingTalkDeptId;
        org.FManagerId = request.ManagerId;
        org.FHeadcount = request.Headcount;
       // 子公司/集团类型强制可切换（确保 admin 切换时不会报"未列入切换列表"）
        org.FIsSwitchable = isCompanyLevel || (orgType.FCanSwitch && request.IsSwitchable);
        org.FDescription = request.Description;
        org.FUpdateTime = DateTime.Now;
#pragma warning restore CS0618

        await _context.SaveChangesAsync();

        // 子公司/集团类型自动关联 admin 用户
        if (isCompanyLevel && oldTypeId != request.TypeId)
        {
            await EnsureAdminOrgAssociationAsync(org.FID);
        }
        // 其他类型变为可切换时也自动关联 admin
        else if (org.FIsSwitchable && !oldIsSwitchable)
        {
            await EnsureAdminOrgAssociationAsync(org.FID);
        }

        // M4：节点类型/上级可能变（reparent/改类别）→ 重算物化字段 + 闭包
        OrgTreeMaterializer.RebuildAll(_context);

        // 名称变更时发布辅助核算同步事件
        if (oldName != request.Name)
        {
            await _eventDispatcher.PublishAsync(new AuxiliarySourceChangedEvent
            {
                SourceType = "SYS组织架构",
                SourceId = id,
                NewName = request.Name
            });
        }

        // 记录变更日志
        var changes = new Dictionary<string, object>();
        if (oldName != request.Name) changes["名称"] = new { 旧值 = oldName, 新值 = request.Name };
        if (oldCode != request.Code) changes["编码"] = new { 旧值 = oldCode, 新值 = request.Code };
        if (oldParentId != request.ParentId) changes["上级ID"] = new { 旧值 = oldParentId.ToString(), 新值 = request.ParentId.ToString() };
        if (oldType != orgType.FName) changes["类型"] = new { 旧值 = oldType, 新值 = orgType.FName };
        if (oldSort != request.Sort) changes["排序"] = new { 旧值 = oldSort.ToString(), 新值 = request.Sort.ToString() };
        if (request.Status.HasValue && oldStatus != request.Status.Value) changes["状态"] = new { 旧值 = oldStatus.ToString(), 新值 = request.Status.Value.ToString() };

        if (changes.Count > 0)
        {
            var changeJson = global::System.Text.Json.JsonSerializer.Serialize(changes,
                new global::System.Text.Json.JsonSerializerOptions { Encoder = global::System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            var (userId, userName) = GetCurrentUser();
            await _changeLogService.LogChangeAsync("组织架构", id, org.FName, "修改", changeJson, userId, userName);
        }

        var dto = MapToDto(org);
        return ApiResult<OrganizationDto>.Success(dto);
    }

    public async Task<ApiResult<bool>> DeleteAsync(long id)
    {
        var org = await _context.Set<SysOrganization>()
            .AsTracking()
            .FirstOrDefaultAsync(o => o.FID == id);
        if (org == null)
        {
            return ApiResult<bool>.Fail("组织不存在");
        }

        // 检查是否有子部门
        var hasChildren = await _context.Set<SysOrganization>().AnyAsync(o => o.FParentId == id);
        if (hasChildren)
        {
            return ApiResult<bool>.Fail("请先删除子部门");
        }

        // 检查是否被凭证引用（通过 FIN辅助核算项目）
        var isReferenced = await CheckFinReferenceAsync("SYS组织架构", id);
        if (isReferenced)
        {
            return ApiResult<bool>.Fail("该组织已被财务凭证引用，无法删除");
        }

        // 检查是否有用户在该组织任职
        var hasUserOrgs = await _context.Set<SysUserOrganization>()
            .AnyAsync(uo => uo.FOrgId == id);
        if (hasUserOrgs)
            return ApiResult<bool>.Fail("该组织存在用户任职记录，无法删除");

        // 检查是否有岗位关联该组织
        var hasPositionOrganizations = await _context.Set<SysPositionDepartment>()
            .AnyAsync(pd => pd.FOrganizationId == id);
        if (hasPositionOrganizations)
            return ApiResult<bool>.Fail("该组织存在岗位关联记录，无法删除");

        var orgName = org.FName;
        var orgCode = org.FCode;

        _context.Set<SysOrganization>().Remove(org);
        await _context.SaveChangesAsync();

        // M4：删除后重建闭包/物化（移除该节点相关闭包行）
        OrgTreeMaterializer.RebuildAll(_context);

        // 记录变更日志
        var (userId, userName) = GetCurrentUser();
        await _changeLogService.LogChangeAsync("组织架构", id, orgName, "删除",
            $"删除组织：{orgName}（{orgCode}）", userId, userName);

        return ApiResult<bool>.Success(true);
    }

    /// <summary>
    /// M4：基于 FKind (父,子) 对合法性校验父子（支持跳级变深度，替代旧 FLevel==父+1 严格规则）。
    /// 规则见 <see cref="OrgTreeMaterializer.IsLegalChild"/> / <see cref="OrgTreeMaterializer.IsLegalRootKind"/>。
    /// 顺带返回父节点——调用方须在 INSERT/UPDATE 前设 FParentKind(=父.FKind)，否则撞 DB CHECK(CK_合法父子 要求非根行 F父类别 非空;
    /// RebuildAll 在 SaveChanges 之后才重算，救不了写入瞬时态)。
    /// </summary>
    private async Task<(string? Error, SysOrganization? Parent)> ValidateOrgKindAsync(SysOrgType newType, long parentId)
    {
        var childKind = newType.FKind;

        // 根节点（无上级）
        if (parentId == 0)
        {
            if (!OrgTreeMaterializer.IsLegalRootKind(childKind))
                return ($"{newType.FName}不能作为根节点（仅集团/区域公司/网点公司可作根）", null);
            return (null, null);
        }

        var parent = await _context.Set<SysOrganization>()
            .FirstOrDefaultAsync(o => o.FID == parentId);
        if (parent == null)
            return ("上级组织不存在", null);

        if (!OrgTreeMaterializer.IsLegalChild(parent.FKind, childKind))
            return ($"{newType.FName}不能挂在该上级下（父子类别不合法）", parent);

        return (null, parent);
    }

    /// <summary>
    /// 检查是否被财务凭证引用
    /// </summary>
    private async Task<bool> CheckFinReferenceAsync(string sourceType, long sourceId)
    {
        try
        {
            var result = await _context.Database.SqlQueryRaw<int>(
                @"SELECT COUNT(*) AS [Value] FROM [FIN辅助核算项目] 
                  WHERE FAuxType = {0} AND CAST(FCode AS BIGINT) = {1}", sourceType, sourceId)
                .FirstOrDefaultAsync();

            return result > 0;
        }
        catch
        {
            return false;
        }
    }

#pragma warning disable CS0618
    private static OrganizationDto MapToDto(SysOrganization o)
    {
        return new OrganizationDto
        {
            Id = o.FID,
            FUID = o.FUID,
            Name = o.FName,
            Code = o.FCode,
            ParentId = o.FParentId,
            TypeId = o.FTypeId,
            Kind = o.FKind,
            TypeCode = o.OrgType?.FCode ?? string.Empty,
            TypeName = o.OrgType?.FName ?? o.FType,
            TypeLevel = o.OrgType?.FLevel ?? 0,
            CanBindAccountSet = o.OrgType?.FCanBindAccountSet ?? false,
            CanSwitch = o.OrgType?.FCanSwitch ?? false,
            Type = o.FType,
            Sort = o.FSort,
            Status = o.FStatus,
            DingTalkDeptId = o.FDingTalkDeptId,
            DingTalkBindStatus = o.FDingTalkBindStatus,
            DingTalkDeptName = o.FDingTalkDeptName,
            ManagerId = o.FManagerId,
            Headcount = o.FHeadcount,
            IsSwitchable = o.FIsSwitchable,
            Description = o.FDescription,
            CreateTime = o.FCreateTime
        };
    }
#pragma warning restore CS0618

    private static List<OrganizationDto> BuildTree(List<OrganizationDto> list)
    {
        var lookup = list.ToLookup(o => o.ParentId);
        var rootNodes = lookup[0].ToList();

        foreach (var node in rootNodes)
        {
            AddChildren(node, lookup);
        }

        return rootNodes;
    }

    private static void AddChildren(OrganizationDto node, ILookup<long, OrganizationDto> lookup)
    {
        var children = lookup[node.Id].ToList();
        node.Children = children;
        foreach (var child in children)
        {
            AddChildren(child, lookup);
        }
    }

    /// <summary>
    /// 确保 admin 用户与指定组织建立关联（幂等）
    /// </summary>
    private async Task EnsureAdminOrgAssociationAsync(long orgId)
    {
        var adminUser = await _context.Set<SysUser>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.FAccount == "admin");
        if (adminUser == null) return;

        var exists = await _context.Set<SysUserOrganization>()
            .IgnoreQueryFilters()
            .AnyAsync(uo => uo.FUserId == adminUser.FID && uo.FOrgId == orgId);
        if (exists) return;

        var hasPrimary = await _context.Set<SysUserOrganization>()
            .IgnoreQueryFilters()
            .AnyAsync(uo => uo.FUserId == adminUser.FID && uo.FIsPrimaryOrg == 1);

        var userOrg = new SysUserOrganization
        {
            FUserId = adminUser.FID,
            FOrgId = orgId,
            FIsPrimaryOrg = hasPrimary ? 0 : 1,
            FStatus = 1,
            FCreateTime = DateTime.Now,
            FUpdateTime = DateTime.Now
        };
        _context.Set<SysUserOrganization>().Add(userOrg);
        await _context.SaveChangesAsync();
    }
}
