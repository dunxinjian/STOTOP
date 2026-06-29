using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;

namespace STOTOP.Module.Task.Services;

public interface IAttachmentService
{
    /// <summary>
    /// 获取附件列表（多态：0任务/1评论/2进度上报/3复盘/4知识）
    /// </summary>
    Task<ApiResult<List<AttachmentListDto>>> GetListAsync(int relationType, long relationId);

    /// <summary>
    /// 上传附件（保存记录+路径信息，实际文件IO由Controller处理）
    /// </summary>
    Task<ApiResult<AttachmentListDto>> UploadAsync(UploadAttachmentRequest request, string originalFileName, string storagePath, long fileSize, string fileType, long operatorId);

    /// <summary>
    /// 删除附件（删除记录）
    /// </summary>
    Task<ApiResult<bool>> DeleteAsync(long id);

    /// <summary>
    /// 获取下载信息（返回文件路径和文件名）
    /// </summary>
    Task<ApiResult<AttachmentDownloadInfoDto>> GetDownloadInfoAsync(long id);
}
