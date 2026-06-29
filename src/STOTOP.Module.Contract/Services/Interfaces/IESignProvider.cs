using STOTOP.Module.Contract.Dtos;

namespace STOTOP.Module.Contract.Services.Interfaces;

/// <summary>
/// 电子签提供者抽象接口（预留第三方平台对接）
/// </summary>
public interface IESignProvider
{
    /// <summary>
    /// 发起签署
    /// </summary>
    Task<ESignRecordDto> InitiateSignAsync(long contractId, CreateESignRecordRequest request);

    /// <summary>
    /// 查询签署状态
    /// </summary>
    Task<int> GetSignStatusAsync(long recordId);

    /// <summary>
    /// 手动完成签署（上传签署文件）
    /// </summary>
    Task<ESignRecordDto?> CompleteSignAsync(long recordId, ManualSignRequest request);

    /// <summary>
    /// 拒签
    /// </summary>
    Task<bool> RejectSignAsync(long recordId);

    /// <summary>
    /// 下载签署文件
    /// </summary>
    Task<string?> GetSignedFilePathAsync(long recordId);
}
