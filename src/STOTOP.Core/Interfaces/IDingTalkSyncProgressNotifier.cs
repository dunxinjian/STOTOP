namespace STOTOP.Core.Interfaces;

/// <summary>
/// 钉钉同步进度通知器接口
/// </summary>
public interface IDingTalkSyncProgressNotifier
{
    /// <summary>
    /// 发送钉钉同步进度通知
    /// </summary>
    /// <param name="stage">阶段: "departments" / "users" / "positions" / "completed" / "error"</param>
    /// <param name="message">进度消息</param>
    /// <param name="current">当前已处理数</param>
    /// <param name="total">总数</param>
    /// <param name="percent">进度百分比 (0-100)</param>
    /// <param name="result">同步结果（仅 completed 阶段携带）</param>
    Task NotifyProgressAsync(string stage, string message, int current, int total, int percent, object? result = null);
}
