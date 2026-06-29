using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;

namespace STOTOP.Module.Task.Services;

public interface IProjectService
{
    Task<ApiResult<PagedResult<ProjectListDto>>> GetPagedListAsync(ProjectPagedRequest query, long orgId);
    Task<ApiResult<ProjectDetailDto>> GetByIdAsync(long id);
    Task<ApiResult<ProjectDetailDto>> CreateAsync(CreateProjectRequest request, long orgId, long creatorId);
    Task<ApiResult<ProjectDetailDto>> UpdateAsync(long id, UpdateProjectRequest request);
    Task<ApiResult<List<ProjectMemberDto>>> GetMembersAsync(long projectId);
    Task<ApiResult<ProjectMemberDto>> AddMemberAsync(long projectId, AddProjectMemberRequest request);
    Task<ApiResult<bool>> RemoveMemberAsync(long projectId, long userId);
}
