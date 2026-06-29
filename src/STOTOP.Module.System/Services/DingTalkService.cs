using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Events;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Services;

public class DingTalkService : IDingTalkService
{
    private readonly STOTOPDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DingTalkService> _logger;
    private readonly IDingTalkSyncProgressNotifier _progressNotifier;
    private readonly IEventDispatcher _eventDispatcher;
    
    // Token 缓存：按配置ID区分（全局配置用 key=0）
    private static readonly ConcurrentDictionary<long, (string Token, DateTime ExpireTime)> _tokenCache = new();
    
    // 同步状态追踪
    private static readonly object _syncLock = new object();
    private static SyncStatusInfo? _currentSyncStatus = null;

    // 进度通知频率控制
    private DateTime _lastNotifyTime = DateTime.MinValue;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>同步状态信息</summary>
    public class SyncStatusInfo
    {
        public bool IsSyncing { get; set; }
        public string Stage { get; set; } = "";
        public string Message { get; set; } = "";
        public int Current { get; set; }
        public int Total { get; set; }
        public int Percent { get; set; }
        public DateTime? StartTime { get; set; }
    }

    /// <summary>获取当前同步状态</summary>
    public static SyncStatusInfo? GetSyncStatus()
    {
        lock (_syncLock)
        {
            return _currentSyncStatus;
        }
    }

    private static void UpdateSyncStatus(string stage, string message, int current, int total, int percent)
    {
        lock (_syncLock)
        {
            if (_currentSyncStatus != null)
            {
                _currentSyncStatus.Stage = stage;
                _currentSyncStatus.Message = message;
                _currentSyncStatus.Current = current;
                _currentSyncStatus.Total = total;
                _currentSyncStatus.Percent = percent;
            }
        }
    }

    private static void ClearSyncStatus()
    {
        lock (_syncLock)
        {
            _currentSyncStatus = null;
        }
    }

    public DingTalkService(
        STOTOPDbContext context,
        IHttpClientFactory httpClientFactory,
        ILogger<DingTalkService> logger,
        IDingTalkSyncProgressNotifier progressNotifier,
        IEventDispatcher eventDispatcher)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _progressNotifier = progressNotifier;
        _eventDispatcher = eventDispatcher;
    }

    #region 从钉钉拉取

    public async Task<List<DingTalkDepartmentDto>> PullDepartmentsAsync()
    {
        var result = new List<DingTalkDepartmentDto>();
        try
        {
            var token = await GetAccessTokenAsync();
            await FetchDepartmentsRecursive(token, 1, result);

            // 标记已绑定的部门
            var boundDeptIds = await _context.Set<SysOrganization>()
                .Where(o => o.FDingTalkDeptId != null && o.FDingTalkBindStatus == 1)
                .Select(o => new { o.FID, o.FDingTalkDeptId })
                .ToListAsync();

            var boundMap = boundDeptIds.ToDictionary(x => x.FDingTalkDeptId!, x => x.FID);
            foreach (var dept in result)
            {
                var deptIdStr = dept.DeptId.ToString();
                if (boundMap.TryGetValue(deptIdStr, out var localId))
                {
                    dept.IsBound = true;
                    dept.LocalOrgId = localId;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "拉取钉钉部门列表失败");
            throw; // 重新抛出异常，让 Controller 返回错误给前端
        }
        return result;
    }

    public async Task<List<DingTalkUserDto>> PullUsersAsync()
    {
        var result = new List<DingTalkUserDto>();
        try
        {
            var token = await GetAccessTokenAsync();
            // 先获取所有部门
            var departments = new List<DingTalkDepartmentDto>();
            await FetchDepartmentsRecursive(token, 1, departments);

            // 遍历每个部门获取用户
            var userMap = new Dictionary<string, DingTalkUserDto>();

            // 先拉取根部门(deptId=1)下的直属用户
            var rootUsers = await FetchDepartmentUsers(token, 1);
            foreach (var user in rootUsers)
            {
                userMap.TryAdd(user.UserId, user);
            }

            // 遍历子部门获取用户
            foreach (var dept in departments)
            {
                var users = await FetchDepartmentUsers(token, dept.DeptId);
                foreach (var user in users)
                {
                    userMap.TryAdd(user.UserId, user);
                }
            }
            result = userMap.Values.ToList();

            // 标记已绑定的用户
            var boundUsers = await _context.Set<SysUser>()
                .Where(u => u.FDingTalkUserId != null && u.FDingTalkBindStatus == 1)
                .Select(u => new { u.FID, u.FDingTalkUserId })
                .ToListAsync();

            var boundMap = boundUsers.ToDictionary(x => x.FDingTalkUserId!, x => x.FID);
            foreach (var user in result)
            {
                if (boundMap.TryGetValue(user.UserId, out var localId))
                {
                    user.IsBound = true;
                    user.LocalUserId = localId;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "拉取钉钉用户列表失败");
            throw; // 重新抛出异常，让 Controller 返回错误给前端
        }
        return result;
    }

    public async Task<List<DingTalkPositionDto>> PullPositionsAsync()
    {
        // 钉钉已废弃 topapi/v2/position/list 接口，改为从用户数据的 title 字段提取职位
        var users = await PullUsersAsync();
        return ExtractPositionsFromUsers(users);
    }

    /// <summary>
    /// 从已拉取的用户列表中提取去重的职位信息（基于 title 字段）
    /// </summary>
    private List<DingTalkPositionDto> ExtractPositionsFromUsers(List<DingTalkUserDto> users)
    {
        var result = new List<DingTalkPositionDto>();
        try
        {
            // 从用户的 Title 字段提取去重的职位名称
            var uniqueTitles = users
                .Where(u => !string.IsNullOrWhiteSpace(u.Title))
                .Select(u => u.Title!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var title in uniqueTitles)
            {
                // 用职位名称的哈希作为稳定的 PositionId
                var positionId = $"title_{Convert.ToHexString(global::System.Security.Cryptography.MD5.HashData(global::System.Text.Encoding.UTF8.GetBytes(title)))[..12].ToLower()}";
                result.Add(new DingTalkPositionDto
                {
                    PositionId = positionId,
                    Name = title
                });
            }

            // 标记已绑定的职位（按名称匹配，因为不再有钉钉原始 PositionId）
            var boundPositions = _context.Set<SysPosition>()
                .Where(p => p.FDingTalkBindStatus == 1)
                .Select(p => new { p.FID, p.FName, p.FDingTalkPositionId })
                .ToList();

            var boundByName = boundPositions.ToDictionary(x => x.FName, x => x.FID, StringComparer.OrdinalIgnoreCase);
            var boundById = boundPositions
                .Where(x => x.FDingTalkPositionId != null)
                .ToDictionary(x => x.FDingTalkPositionId!, x => x.FID);

            foreach (var pos in result)
            {
                if (boundById.TryGetValue(pos.PositionId, out var localId) ||
                    boundByName.TryGetValue(pos.Name, out localId))
                {
                    pos.IsBound = true;
                    pos.LocalPositionId = localId;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从用户数据提取职位列表失败");
            throw;
        }
        return result;
    }

    #endregion

    #region 全量同步
    
    public async Task<SyncResultDto> FullSyncFromDingTalkAsync()
    {
        var syncResult = new SyncResultDto { Errors = new List<string>() };

        // 设置同步状态
        lock (_syncLock)
        {
            _currentSyncStatus = new SyncStatusInfo
            {
                IsSyncing = true,
                Stage = "starting",
                Message = "正在启动同步...",
                StartTime = DateTime.Now
            };
        }
    
        try
        {
            var token = await GetAccessTokenAsync();
    
            // 1. 同步部门 (Percent 0~35)
            await NotifyProgress("departments", "正在拉取钉钉部门...", 0, 0, 0);
    
            var departments = new List<DingTalkDepartmentDto>();
            await FetchDepartmentsRecursive(token, 1, departments);
    
            await NotifyProgress("departments", $"部门拉取完成，共 {departments.Count} 个，开始同步...", 0, departments.Count, 10);
    
            var deptProcessed = 0;
            await SyncDepartments(departments, syncResult, async (processed, total) =>
            {
                deptProcessed = processed;
                var percent = 10 + (int)(processed * 25.0 / Math.Max(total, 1));
                await NotifyProgress("departments", $"正在同步部门 ({processed}/{total})...", processed, total, percent);
            });
    
            await NotifyProgress("departments", $"部门同步完成，共处理 {deptProcessed} 个", deptProcessed, departments.Count, 35);
    
            // 2. 同步用户 (Percent 35~80)
            await NotifyProgress("users", "正在拉取钉钉用户...", 0, 0, 35);
    
            var userMap = new Dictionary<string, DingTalkUserDto>();

            // 先拉取根部门(deptId=1)下的直属用户
            var rootUsers = await FetchDepartmentUsers(token, 1);
            foreach (var user in rootUsers)
            {
                userMap.TryAdd(user.UserId, user);
            }

            // 遍历子部门获取用户
            var deptIndex = 0;
            foreach (var dept in departments)
            {
                deptIndex++;
                var users = await FetchDepartmentUsers(token, dept.DeptId);
                foreach (var user in users)
                {
                    userMap.TryAdd(user.UserId, user);
                }
                // 每处理完一个部门通知进度
                var pullPercent = 35 + (int)(deptIndex * 10.0 / Math.Max(departments.Count, 1));
                await NotifyProgress("users", $"正在拉取用户 (部门 {deptIndex}/{departments.Count})...", userMap.Count, 0, pullPercent);
            }
    
            await NotifyProgress("users", $"用户拉取完成，共 {userMap.Count} 个，开始同步...", 0, userMap.Count, 45);
    
            var userProcessed = 0;
            await SyncUsers(userMap.Values.ToList(), syncResult, async (processed, total) =>
            {
                userProcessed = processed;
                var percent = 45 + (int)(processed * 35.0 / Math.Max(total, 1));
                await NotifyProgress("users", $"正在同步用户 ({processed}/{total})...", processed, total, percent);
            });
    
            await NotifyProgress("users", $"用户同步完成，共处理 {userProcessed} 个", userProcessed, userMap.Count, 80);
    
            // 3. 同步职位 (Percent 80~95) — 从已拉取的用户数据中提取职位
            await NotifyProgress("positions", "正在从用户数据提取职位...", 0, 0, 80);
    
            var positions = ExtractPositionsFromUsers(userMap.Values.ToList());
    
            await NotifyProgress("positions", $"职位提取完成，共 {positions.Count} 个，开始同步...", 0, positions.Count, 85);
    
            var positionProcessed = 0;
            await SyncPositions(positions, syncResult, async (processed, total) =>
            {
                positionProcessed = processed;
                var percent = 85 + (int)(processed * 10.0 / Math.Max(total, 1));
                await NotifyProgress("positions", $"正在同步职位 ({processed}/{total})...", processed, total, percent);
            });
    
            await NotifyProgress("positions", $"职位同步完成，共处理 {positionProcessed} 个", positionProcessed, positions.Count, 95);
    
            // 4. 更新最后同步时间（仅更新时间字段，不覆盖其他配置）
            var globalConfig = GetConfigRecord();
            if (globalConfig != null)
            {
                DingTalkConfigHelper.UpdateLastSyncTime(globalConfig.Id);
            }
    
            syncResult.TotalCount = syncResult.SuccessCount + syncResult.FailCount + syncResult.SkipCount;
    
            // 完成
            await NotifyProgress("completed", "全量同步完成", 0, 0, 100, syncResult);

            // 发布同步完成事件
            await _eventDispatcher.PublishAsync(new DingTalkSyncCompletedEvent
            {
                SyncType = "full",
                SuccessCount = syncResult.SuccessCount,
                FailCount = syncResult.FailCount,
                AdminUserId = 0, // 全量同步无特定管理员上下文
                TriggeredByUserId = 0,
                ModuleCode = "system"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "全量同步失败");
            syncResult.Errors.Add($"同步异常: {ex.Message}");
            syncResult.FailCount++;
    
            // 异常通知
            await NotifyProgress("error", $"同步异常: {ex.Message}", 0, 0, 0);
        }
        finally
        {
            // 清理同步状态
            ClearSyncStatus();
        }
    
        syncResult.TotalCount = syncResult.SuccessCount + syncResult.FailCount + syncResult.SkipCount;
        return syncResult;
    }

    #endregion

    #region 绑定/解绑

    public async Task BindOrganizationAsync(BindOrganizationRequest request)
    {
        // 注意：由于全局配置了 NoTracking，必须使用 AsTracking() 才能正确更新
        var org = await _context.Set<SysOrganization>()
            .AsTracking()
            .FirstOrDefaultAsync(o => o.FID == request.OrgId);
        if (org == null) throw new InvalidOperationException("组织不存在");

        org.FDingTalkDeptId = request.DingTalkDeptId;
        org.FDingTalkBindStatus = 1;
        org.FUpdateTime = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    public async Task UnbindOrganizationAsync(long orgId)
    {
        // 注意：由于全局配置了 NoTracking，必须使用 AsTracking() 才能正确更新
        var org = await _context.Set<SysOrganization>()
            .AsTracking()
            .FirstOrDefaultAsync(o => o.FID == orgId);
        if (org == null) throw new InvalidOperationException("组织不存在");

        org.FDingTalkDeptId = null;
        org.FDingTalkDeptName = null;
        org.FDingTalkBindStatus = 0;
        org.FUpdateTime = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    public async Task BindUserAsync(BindUserRequest request)
    {
        // 注意：由于全局配置了 NoTracking，必须使用 AsTracking() 才能正确更新
        var user = await _context.Set<SysUser>()
            .AsTracking()
            .FirstOrDefaultAsync(u => u.FID == request.UserId);
        if (user == null) throw new InvalidOperationException("用户不存在");

        user.FDingTalkUserId = request.DingTalkUserId;
        user.FDingTalkBindStatus = 1;
        user.FUpdateTime = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    public async Task UnbindUserAsync(long userId)
    {
        // 注意：由于全局配置了 NoTracking，必须使用 AsTracking() 才能正确更新
        var user = await _context.Set<SysUser>()
            .AsTracking()
            .FirstOrDefaultAsync(u => u.FID == userId);
        if (user == null) throw new InvalidOperationException("用户不存在");

        user.FDingTalkUserId = null;
        user.FDingTalkUserName = null;
        user.FDingTalkUnionId = null;
        user.FDingTalkBindStatus = 0;
        user.FUpdateTime = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    public async Task BindPositionAsync(BindPositionRequest request)
    {
        // 注意：由于全局配置了 NoTracking，必须使用 AsTracking() 才能正确更新
        var position = await _context.Set<SysPosition>()
            .AsTracking()
            .FirstOrDefaultAsync(p => p.FID == request.PositionId);
        if (position == null) throw new InvalidOperationException("职位不存在");

        position.FDingTalkPositionId = request.DingTalkPositionId;
        position.FDingTalkBindStatus = 1;
        position.FUpdateTime = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    public async Task UnbindPositionAsync(long positionId)
    {
        // 注意：由于全局配置了 NoTracking，必须使用 AsTracking() 才能正确更新
        var position = await _context.Set<SysPosition>()
            .AsTracking()
            .FirstOrDefaultAsync(p => p.FID == positionId);
        if (position == null) throw new InvalidOperationException("职位不存在");

        position.FDingTalkPositionId = null;
        position.FDingTalkBindStatus = 0;
        position.FUpdateTime = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    #endregion

    #region 推送到钉钉

    public async Task<bool> PushDepartmentAsync(long orgId)
    {
        try
        {
            var org = await _context.Set<SysOrganization>().FindAsync(orgId);
            if (org == null || org.FDingTalkBindStatus != 1 || string.IsNullOrEmpty(org.FDingTalkDeptId))
            {
                _logger.LogDebug("组织 {OrgId} 未绑定钉钉，跳过推送", orgId);
                return false;
            }

            var token = await GetAccessTokenAsync();
            var client = _httpClientFactory.CreateClient();
            var body = new
            {
                dept_id = long.Parse(org.FDingTalkDeptId),
                name = org.FName
            };

            var response = await client.PostAsJsonAsync(
                $"https://oapi.dingtalk.com/topapi/v2/department/update?access_token={token}",
                body, _jsonOptions);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            if (json.TryGetProperty("errcode", out var code) && code.GetInt32() != 0)
            {
                _logger.LogWarning("推送部门到钉钉失败: OrgId={OrgId}, Error={Msg}",
                    orgId, json.GetProperty("errmsg").GetString());
                return false;
            }

            _logger.LogInformation("推送部门到钉钉成功: OrgId={OrgId}, DeptId={DeptId}", orgId, org.FDingTalkDeptId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "推送部门到钉钉异常: OrgId={OrgId}", orgId);
            return false;
        }
    }

    public async Task<bool> PushUserAsync(long userId)
    {
        try
        {
            var user = await _context.Set<SysUser>().FindAsync(userId);
            if (user == null || user.FDingTalkBindStatus != 1 || string.IsNullOrEmpty(user.FDingTalkUserId))
            {
                _logger.LogDebug("用户 {UserId} 未绑定钉钉，跳过推送", userId);
                return false;
            }

            var token = await GetAccessTokenAsync();
            var client = _httpClientFactory.CreateClient();
            var body = new
            {
                userid = user.FDingTalkUserId,
                name = user.FName,
                mobile = user.FPhone
            };

            var response = await client.PostAsJsonAsync(
                $"https://oapi.dingtalk.com/topapi/v2/user/update?access_token={token}",
                body, _jsonOptions);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            if (json.TryGetProperty("errcode", out var code) && code.GetInt32() != 0)
            {
                _logger.LogWarning("推送用户到钉钉失败: UserId={UserId}, Error={Msg}",
                    userId, json.GetProperty("errmsg").GetString());
                return false;
            }

            _logger.LogInformation("推送用户到钉钉成功: UserId={UserId}, DingTalkUserId={DtUserId}",
                userId, user.FDingTalkUserId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "推送用户到钉钉异常: UserId={UserId}", userId);
            return false;
        }
    }

    public async Task<bool> PushPositionAsync(long positionId)
    {
        try
        {
            var position = await _context.Set<SysPosition>().FindAsync(positionId);
            if (position == null || position.FDingTalkBindStatus != 1 || string.IsNullOrEmpty(position.FDingTalkPositionId))
            {
                _logger.LogDebug("职位 {PositionId} 未绑定钉钉，跳过推送", positionId);
                return false;
            }

            // 钉钉职位 API 目前无标准更新接口，记录日志
            _logger.LogInformation("职位推送预留: PositionId={PositionId}, DingTalkPositionId={DtPosId}",
                positionId, position.FDingTalkPositionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "推送职位到钉钉异常: PositionId={PositionId}", positionId);
            return false;
        }
    }

    #endregion

    #region 指定用户同步

    /// <summary>同步指定钉钉用户</summary>
    public async Task<SyncResultDto> SyncSpecificUsersAsync(List<string> dingTalkUserIds)
    {
        var syncResult = new SyncResultDto { Errors = new List<string>() };

        try
        {
            var token = await GetAccessTokenAsync();

            // 1. 逐个从钉钉拉取用户详情
            var users = new List<DingTalkUserDto>();
            foreach (var userId in dingTalkUserIds)
            {
                try
                {
                    var user = await FetchUserDetailAsync(token, userId);
                    if (user != null)
                        users.Add(user);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "拉取钉钉用户详情失败: UserId={UserId}", userId);
                    syncResult.FailCount++;
                    syncResult.Errors.Add($"拉取用户 {userId} 失败: {ex.Message}");
                }
            }

            // 2. 复用现有的 SyncUsers 方法同步到本地
            if (users.Count > 0)
            {
                await SyncUsers(users, syncResult);
            }

            await NotifyProgress("completed", $"指定用户同步完成: 成功{syncResult.SuccessCount} 跳过{syncResult.SkipCount} 失败{syncResult.FailCount}",
                users.Count, users.Count, 100, syncResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "指定用户同步异常");
            syncResult.FailCount++;
            syncResult.Errors?.Add($"同步异常: {ex.Message}");
            await NotifyProgress("error", $"同步异常: {ex.Message}", 0, 0, 0);
        }

        syncResult.TotalCount = syncResult.SuccessCount + syncResult.FailCount + syncResult.SkipCount;
        return syncResult;
    }

    /// <summary>从钉钉获取指定用户的详细信息</summary>
    private async Task<DingTalkUserDto?> FetchUserDetailAsync(string token, string userId)
    {
        var client = _httpClientFactory.CreateClient();
        var body = new { userid = userId, language = "zh_CN" };
        var response = await client.PostAsJsonAsync(
            $"https://oapi.dingtalk.com/topapi/v2/user/get?access_token={token}",
            body, _jsonOptions);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        if (json.TryGetProperty("errcode", out var code) && code.GetInt32() != 0)
        {
            var errorMsg = json.TryGetProperty("errmsg", out var msg) ? msg.GetString() : "未知错误";
            throw new InvalidOperationException($"钉钉API错误({code.GetInt32()}): {errorMsg}");
        }

        if (!json.TryGetProperty("result", out var result))
            return null;

        // 解析用户信息
        var user = new DingTalkUserDto
        {
            UserId = result.GetProperty("userid").GetString() ?? string.Empty,
            Name = result.GetProperty("name").GetString() ?? string.Empty,
            Mobile = result.TryGetProperty("mobile", out var mobile) ? mobile.GetString() : null,
            Email = result.TryGetProperty("email", out var email) ? email.GetString() : null,
            Title = result.TryGetProperty("title", out var title) ? title.GetString() : null,
            JobNumber = result.TryGetProperty("job_number", out var jobNum) ? jobNum.GetString() : null,
        };

        // 解析部门ID列表
        if (result.TryGetProperty("dept_id_list", out var deptIds))
        {
            user.DeptIdList = deptIds.EnumerateArray()
                .Select(d => d.GetInt64())
                .ToArray();
        }

        return user;
    }

    #endregion

    #region 私有方法
    
    /// <summary>发送钉钉同步进度通知（带频率控制）</summary>
    private async Task NotifyProgress(string stage, string message, int current, int total, int percent, SyncResultDto? result = null)
    {
        // 同步更新静态状态（不受频率控制）
        UpdateSyncStatus(stage, message, current, total, percent);

        // 频率控制：每秒最多通知一次（completed 和 error 阶段除外）
        var now = DateTime.UtcNow;
        if (stage != "completed" && stage != "error" && (now - _lastNotifyTime).TotalMilliseconds < 1000)
            return;
        _lastNotifyTime = now;
    
        await _progressNotifier.NotifyProgressAsync(stage, message, current, total, percent, result);
    }
    
    private DingTalkConfigRecord? GetConfigRecord()
        => DingTalkConfigHelper.GetGlobalConfig();

    public async Task<string> GetAccessTokenAsync()
    {
        // 获取全局配置的 Token
        var config = GetConfigRecord()
            ?? throw new InvalidOperationException("钉钉配置不存在，请先配置钉钉应用信息");

        // 验证配置字段有效性
        var missingFields = new List<string>();
        if (string.IsNullOrWhiteSpace(config.AppKey))
            missingFields.Add("AppKey");
        if (string.IsNullOrWhiteSpace(config.AppSecret))
            missingFields.Add("AppSecret");
        if (string.IsNullOrWhiteSpace(config.CorpId))
            missingFields.Add("CorpId");

        if (missingFields.Count > 0)
            throw new InvalidOperationException($"钉钉配置缺少必填字段: {string.Join(", ", missingFields)}，请完善配置");

        return await GetAccessTokenByConfigAsync(config);
    }

    /// <summary>按配置获取 AccessToken（核心方法）</summary>
    private async Task<string> GetAccessTokenByConfigAsync(DingTalkConfigRecord config)
    {
        var cacheKey = config.Id;

        // 1. 检查内存缓存
        if (_tokenCache.TryGetValue(cacheKey, out var cached) && DateTime.Now < cached.ExpireTime)
        {
            return cached.Token;
        }

        // 2. 调用钉钉 API 获取新 Token
        var client = _httpClientFactory.CreateClient();
        var appSecret = DingTalkConfigHelper.DecryptSecret(config.AppSecret);
        if (string.IsNullOrEmpty(appSecret))
            appSecret = config.AppSecret; // 兼容未加密的旧数据
        var body = new { appKey = config.AppKey, appSecret };
        var response = await client.PostAsJsonAsync("https://api.dingtalk.com/v1.0/oauth2/accessToken", body);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = json.GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("获取钉钉 AccessToken 失败：返回值为空");
        var expireIn = json.GetProperty("expireIn").GetInt32();
        // 提前5分钟过期
        var expireTime = DateTime.Now.AddSeconds(expireIn - 300);

        // 3. 更新内存缓存
        _tokenCache[cacheKey] = (accessToken, expireTime);

        _logger.LogInformation("获取钉钉 AccessToken 成功，ConfigId={ConfigId}, 有效期 {ExpireIn} 秒", config.Id, expireIn);
        return accessToken;
    }

    private async Task FetchDepartmentsRecursive(string token, long deptId, List<DingTalkDepartmentDto> result)
    {
        var client = _httpClientFactory.CreateClient();
        var body = new { dept_id = deptId, language = "zh_CN" };
        var response = await client.PostAsJsonAsync(
            $"https://oapi.dingtalk.com/topapi/v2/department/listsub?access_token={token}",
            body, _jsonOptions);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        if (json.TryGetProperty("errcode", out var code) && code.GetInt32() != 0)
        {
            var errorMsg = json.TryGetProperty("errmsg", out var msg) ? msg.GetString() : "未知错误";
            _logger.LogError("获取钉钉子部门失败: DeptId={DeptId}, ErrorCode={Code}, Error={Msg}",
                deptId, code.GetInt32(), errorMsg);
            throw new InvalidOperationException($"钉钉API错误({code.GetInt32()}): {errorMsg}");
        }

        if (!json.TryGetProperty("result", out var resultArray))
        {
            _logger.LogDebug("钉钉部门 {DeptId} 无子部门", deptId);
            return;
        }

        if (resultArray.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("钉钉API返回的result不是数组类型: DeptId={DeptId}, ValueKind={Kind}",
                deptId, resultArray.ValueKind);
            return;
        }

        foreach (var item in resultArray.EnumerateArray())
        {
            var subDeptId = item.GetProperty("dept_id").GetInt64();
            var name = item.GetProperty("name").GetString() ?? string.Empty;
            var parentId = item.GetProperty("parent_id").GetInt64();

            result.Add(new DingTalkDepartmentDto
            {
                DeptId = subDeptId,
                Name = name,
                ParentId = parentId
            });

            // 递归获取子部门
            await FetchDepartmentsRecursive(token, subDeptId, result);
        }
    }

    private async Task<List<DingTalkUserDto>> FetchDepartmentUsers(string token, long deptId)
    {
        var users = new List<DingTalkUserDto>();
        var client = _httpClientFactory.CreateClient();
        long cursor = 0;
        const int size = 100;
        bool hasMore = true;

        while (hasMore)
        {
            var body = new { dept_id = deptId, cursor, size };
            var response = await client.PostAsJsonAsync(
                $"https://oapi.dingtalk.com/topapi/v2/user/list?access_token={token}",
                body, _jsonOptions);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            if (json.TryGetProperty("errcode", out var code) && code.GetInt32() != 0)
            {
                var errorMsg = json.TryGetProperty("errmsg", out var msg) ? msg.GetString() : "未知错误";
                _logger.LogError("获取钉钉部门用户失败: DeptId={DeptId}, ErrorCode={Code}, Error={Msg}",
                    deptId, code.GetInt32(), errorMsg);
                throw new InvalidOperationException($"钉钉API错误({code.GetInt32()}): {errorMsg}");
            }

            if (!json.TryGetProperty("result", out var resultNode)) break;

            if (resultNode.TryGetProperty("list", out var list))
            {
                foreach (var item in list.EnumerateArray())
                {
                    var user = new DingTalkUserDto
                    {
                        UserId = item.GetProperty("userid").GetString() ?? string.Empty,
                        Name = item.GetProperty("name").GetString() ?? string.Empty,
                        Mobile = item.TryGetProperty("mobile", out var m) ? m.GetString() : null,
                        Email = item.TryGetProperty("email", out var e) ? e.GetString() : null,
                        Title = item.TryGetProperty("title", out var t) ? t.GetString() : null,
                        JobNumber = item.TryGetProperty("job_number", out var j) ? j.GetString() : null,
                        DeptIdList = item.TryGetProperty("dept_id_list", out var d)
                            ? d.EnumerateArray().Select(x => x.GetInt64()).ToArray()
                            : null
                    };
                    users.Add(user);
                }
            }

            hasMore = resultNode.TryGetProperty("has_more", out var hm) && hm.GetBoolean();
            if (hasMore && resultNode.TryGetProperty("next_cursor", out var nc))
            {
                cursor = nc.GetInt64();
            }
            else
            {
                hasMore = false;
            }
        }

        return users;
    }

    private async Task SyncDepartments(List<DingTalkDepartmentDto> departments, SyncResultDto syncResult, Func<int, int, Task>? progressCallback = null)
    {
        var total = departments.Count;
        var processed = 0;
        foreach (var dept in departments)
        {
            try
            {
                var deptIdStr = dept.DeptId.ToString();
                var existing = await _context.Set<SysOrganization>()
                    .AsTracking()
                    .FirstOrDefaultAsync(o => o.FDingTalkDeptId == deptIdStr);
    
                if (existing != null)
                {
                    // 更新已绑定的组织
                    existing.FDingTalkDeptName = dept.Name;
                    existing.FUpdateTime = DateTime.Now;
                    syncResult.SkipCount++;
                }
                else
                {
                    // 创建新组织
                    var org = new SysOrganization
                    {
                        FUID = Guid.NewGuid().ToString("N"),
                        FName = dept.Name,
                        FCode = $"DT_{dept.DeptId}",
                        FParentId = 0, // 后续根据钉钉父部门映射
                        FTypeId = 5, // 默认为部门
                        FDingTalkDeptId = deptIdStr,
                        FDingTalkDeptName = dept.Name,
                        FDingTalkBindStatus = 1
                    };
    
                    // 尝试找到父部门的本地映射
                    if (dept.ParentId > 1)
                    {
                        var parentDeptIdStr = dept.ParentId.ToString();
                        var parentOrg = await _context.Set<SysOrganization>()
                            .FirstOrDefaultAsync(o => o.FDingTalkDeptId == parentDeptIdStr);
                        if (parentOrg != null)
                        {
                            org.FParentId = parentOrg.FID;
                        }
                    }
    
                    await _context.Set<SysOrganization>().AddAsync(org);
                    syncResult.SuccessCount++;
                }
    
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "同步部门失败: DeptId={DeptId}", dept.DeptId);
                syncResult.FailCount++;
                syncResult.Errors?.Add($"部门 {dept.Name}({dept.DeptId}) 同步失败: {ex.Message}");
            }
    
            processed++;
            if (progressCallback != null)
            {
                await progressCallback(processed, total);
            }
        }

        // 同步完成后，将 FParentId=0 的组织挂载到根节点（FID=1）下
        // 钉钉中 ParentId=1 表示根部门，同步时被设置为 FParentId=0，需要修正
        // 注意：由于全局配置了 NoTracking，需要使用 AsTracking() 才能正确更新
        var orphanOrgs = await _context.Set<SysOrganization>()
            .AsTracking()
            .Where(o => o.FParentId == 0 && o.FID != 1)
            .ToListAsync();
        
        if (orphanOrgs.Count > 0)
        {
            _logger.LogInformation("发现 {Count} 个顶级组织（FParentId=0），将挂载到根节点（FID=1）", orphanOrgs.Count);
            foreach (var org in orphanOrgs)
            {
                org.FParentId = 1;
                org.FUpdateTime = DateTime.Now;
            }
            await _context.SaveChangesAsync();
        }
    }

    private async Task SyncUsers(List<DingTalkUserDto> users, SyncResultDto syncResult, Func<int, int, Task>? progressCallback = null)
    {
        var total = users.Count;
        var processed = 0;
        foreach (var dtUser in users)
        {
            try
            {
                var existing = await _context.Set<SysUser>()
                    .AsTracking()
                    .FirstOrDefaultAsync(u => u.FDingTalkUserId == dtUser.UserId);
    
                if (existing != null)
                {
                    // 更新已绑定的用户
                    existing.FDingTalkUserName = dtUser.Name;
                    existing.FUpdateTime = DateTime.Now;

                    // 更新组织任职关系：先删除钉钉来源的任职记录，再根据最新数据重建
                    var existingUserOrgs = await _context.Set<SysUserOrganization>()
                        .AsTracking()
                        .Where(uo => uo.FUserId == existing.FID)
                        .ToListAsync();

                    // 筛选出钉钉来源的任职关系（关联组织有 FDingTalkDeptId）
                    var dingTalkOrgIds = await _context.Set<SysOrganization>()
                        .Where(o => o.FDingTalkDeptId != null && o.FDingTalkDeptId != "")
                        .Select(o => o.FID)
                        .ToListAsync();

                    var dingTalkUserOrgs = existingUserOrgs
                        .Where(uo => dingTalkOrgIds.Contains(uo.FOrgId))
                        .ToList();

                    if (dingTalkUserOrgs.Count > 0)
                    {
                        _context.Set<SysUserOrganization>().RemoveRange(dingTalkUserOrgs);
                    }

                    // 根据钉钉最新部门数据重建任职关系
                    if (dtUser.DeptIdList != null)
                    {
                        bool isPrimary = true;
                        foreach (var deptId in dtUser.DeptIdList)
                        {
                            var deptIdStr = deptId.ToString();
                            var org = await _context.Set<SysOrganization>()
                                .FirstOrDefaultAsync(o => o.FDingTalkDeptId == deptIdStr);
                            if (org != null)
                            {
                                var uo = new SysUserOrganization
                                {
                                    FUserId = existing.FID,
                                    FOrgId = org.FID,
                                    FIsPrimaryOrg = isPrimary ? 1 : 0,
                                    FPosition = dtUser.Title
                                };
                                await _context.Set<SysUserOrganization>().AddAsync(uo);
                                isPrimary = false;
                            }
                        }
                    }

                    syncResult.SkipCount++;
                }
                else
                {
                    // 创建新用户
                    var user = new SysUser
                    {
                        FUID = Guid.NewGuid().ToString("N"),
                        FName = dtUser.Name,
                        FAccount = dtUser.UserId,
                        FPhone = dtUser.Mobile,
                        FEmail = dtUser.Email,
                        FPasswordHash = string.Empty, // 钉钉用户无需密码
                        FDingTalkUserId = dtUser.UserId,
                        FDingTalkUserName = dtUser.Name,
                        FDingTalkBindStatus = 1
                    };
                    await _context.Set<SysUser>().AddAsync(user);
                    await _context.SaveChangesAsync();
    
                    // 创建用户与部门的关联
                    if (dtUser.DeptIdList != null)
                    {
                        bool isPrimary = true;
                        foreach (var deptId in dtUser.DeptIdList)
                        {
                            var deptIdStr = deptId.ToString();
                            var org = await _context.Set<SysOrganization>()
                                .FirstOrDefaultAsync(o => o.FDingTalkDeptId == deptIdStr);
                            if (org != null)
                            {
                                var uo = new SysUserOrganization
                                {
                                    FUserId = user.FID,
                                    FOrgId = org.FID,
                                    FIsPrimaryOrg = isPrimary ? 1 : 0,
                                    FPosition = dtUser.Title
                                };
                                await _context.Set<SysUserOrganization>().AddAsync(uo);
                                isPrimary = false;
                            }
                        }
                    }
    
                    syncResult.SuccessCount++;
                }
    
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "同步用户失败: UserId={UserId}", dtUser.UserId);
                syncResult.FailCount++;
                syncResult.Errors?.Add($"用户 {dtUser.Name}({dtUser.UserId}) 同步失败: {ex.Message}");
            }
    
            processed++;
            if (progressCallback != null)
            {
                await progressCallback(processed, total);
            }
        }
    }

    private async Task SyncPositions(List<DingTalkPositionDto> positions, SyncResultDto syncResult, Func<int, int, Task>? progressCallback = null)
    {
        var total = positions.Count;
        var processed = 0;
        foreach (var dtPos in positions)
        {
            try
            {
                // 先按 DingTalkPositionId 匹配，再按名称匹配
                var existing = await _context.Set<SysPosition>()
                    .AsTracking()
                    .FirstOrDefaultAsync(p => p.FDingTalkPositionId == dtPos.PositionId)
                    ?? await _context.Set<SysPosition>()
                    .AsTracking()
                    .FirstOrDefaultAsync(p => p.FName == dtPos.Name);
    
                if (existing != null)
                {
                    existing.FName = dtPos.Name;
                    existing.FDingTalkPositionId = dtPos.PositionId;
                    existing.FUpdateTime = DateTime.Now;
                    syncResult.SkipCount++;
                }
                else
                {
                    var position = new SysPosition
                    {
                        FUID = Guid.NewGuid().ToString("N"),
                        FName = dtPos.Name,
                        FCode = $"DT_{dtPos.PositionId}",
                        FDingTalkPositionId = dtPos.PositionId,
                        FDingTalkBindStatus = 1
                    };
                    await _context.Set<SysPosition>().AddAsync(position);
                    syncResult.SuccessCount++;
                }
    
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "同步职位失败: PositionId={PositionId}", dtPos.PositionId);
                syncResult.FailCount++;
                syncResult.Errors?.Add($"职位 {dtPos.Name}({dtPos.PositionId}) 同步失败: {ex.Message}");
            }
    
            processed++;
            if (progressCallback != null)
            {
                await progressCallback(processed, total);
            }
        }
    }

    #endregion
}
