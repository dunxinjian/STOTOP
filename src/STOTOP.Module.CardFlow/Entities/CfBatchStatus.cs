namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// CfBatch 状态常量
/// 对应 CfBatch.FStatus 的 int 值 0-8
/// </summary>
public static class CfBatchStatus
{
    /// <summary>解析中</summary>
    public const int Parsing = 0;
    /// <summary>已暂存</summary>
    public const int Staged = 1;
    /// <summary>质检中</summary>
    public const int QualityChecking = 2;
    /// <summary>已创建卡片</summary>
    public const int CardCreated = 3;
    /// <summary>处理中</summary>
    public const int Processing = 4;
    /// <summary>已完成</summary>
    public const int Completed = 5;
    /// <summary>失败</summary>
    public const int Failed = 6;
    /// <summary>部分完成</summary>
    public const int PartiallyCompleted = 7;
    /// <summary>已撤销</summary>
    public const int Revoked = 8;
}
