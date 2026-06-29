namespace STOTOP.Module.CardFlow.Services;

/// <summary>节流式进度上报器</summary>
public class ThrottledProgressReporter : IProgressReporter
{
    private readonly long _batchId;
    private readonly IBatchNotifier _notifier;
    private readonly int _rowInterval;
    private readonly TimeSpan _timeInterval;
    private int _lastReportedCount;
    private DateTime _lastReportedTime = DateTime.MinValue;

    /// <param name="batchId">批次ID</param>
    /// <param name="notifier">推送器</param>
    /// <param name="rowInterval">每隔多少行推送一次（默认1000）</param>
    /// <param name="timeInterval">最小推送间隔（默认1秒）</param>
    public ThrottledProgressReporter(long batchId, IBatchNotifier notifier, int rowInterval = 1000, TimeSpan? timeInterval = null)
    {
        _batchId = batchId;
        _notifier = notifier;
        _rowInterval = rowInterval;
        _timeInterval = timeInterval ?? TimeSpan.FromSeconds(1);
    }

    public async Task ReportAsync(int processed, int total)
    {
        var now = DateTime.Now;
        var rowDelta = processed - _lastReportedCount;
        var timeDelta = now - _lastReportedTime;

        // 满足任一条件则推送：行数间隔达标 OR 时间间隔达标 OR 已完成
        if (rowDelta >= _rowInterval || timeDelta >= _timeInterval || processed >= total)
        {
            _lastReportedCount = processed;
            _lastReportedTime = now;
            await _notifier.ProgressUpdateAsync(_batchId, processed, total);
        }
    }
}
