using STOTOP.Module.System.Dtos;

namespace STOTOP.Module.System.Services.Interfaces;

public interface IDingTalkService
{
    // 从钉钉拉取（预览）
    Task<List<DingTalkDepartmentDto>> PullDepartmentsAsync();
    Task<List<DingTalkUserDto>> PullUsersAsync();
    Task<List<DingTalkPositionDto>> PullPositionsAsync();

    // 全量同步
    Task<SyncResultDto> FullSyncFromDingTalkAsync();

    /// <summary>同步指定钉钉用户</summary>
    Task<SyncResultDto> SyncSpecificUsersAsync(List<string> dingTalkUserIds);

    // 绑定/解绑
    Task BindOrganizationAsync(BindOrganizationRequest request);
    Task UnbindOrganizationAsync(long orgId);
    Task BindUserAsync(BindUserRequest request);
    Task UnbindUserAsync(long userId);
    Task BindPositionAsync(BindPositionRequest request);
    Task UnbindPositionAsync(long positionId);

    // 推送到钉钉（供其他 Service 调用）
    Task<bool> PushDepartmentAsync(long orgId);
    Task<bool> PushUserAsync(long userId);
    Task<bool> PushPositionAsync(long positionId);

    /// <summary>获取全局配置的AccessToken（供OA模块调用）</summary>
    Task<string> GetAccessTokenAsync();
}
