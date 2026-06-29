using System.Text.Json.Serialization;

namespace STOTOP.Core.Models;

/// <summary>
/// 卡片消息载荷结构 - 通用的对话卡片数据模型
/// </summary>
public class CardPayload
{
    public string CardType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<CardSection> Sections { get; set; } = new();
    public List<CardAction> Actions { get; set; } = new();
    public CardMetadata? Metadata { get; set; }
}

/// <summary>
/// 卡片区块（扁平字段结构，已分离包装层）
/// </summary>
public class CardSection
{
    /// <summary>
    /// 区块类型 - fact-set / form / timeline / attachment
    /// </summary>
    public string Type { get; set; } = string.Empty;

    public string? Title { get; set; }
    public string? Content { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FactItem>? Facts { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FormField>? Fields { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TimelineItem>? Items { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResultType { get; set; }
}

/// <summary>
/// 卡片操作按钮
/// </summary>
public class CardAction
{
    /// <summary>
    /// 操作类型 - approve / reject / submit / cancel 等
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 按钮显示文本
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 按钮样式 - primary / danger / default / link
    /// </summary>
    public string? Style { get; set; }

    /// <summary>
    /// 是否禁用
    /// </summary>
    public bool? Disabled { get; set; }

    /// <summary>
    /// 附加数据
    /// </summary>
    public Dictionary<string, object>? Data { get; set; }
}

/// <summary>
/// 卡片元数据
/// </summary>
public class CardMetadata
{
    public long? ProcessConfigId { get; set; }
    public long? ProcessInstanceId { get; set; }
    public long? TaskId { get; set; }
    public long? SelectedProcessId { get; set; }
    public string? BookmarkId { get; set; }
}

#region 区块数据类型

/// <summary>
/// 事实集合数据
/// </summary>
public class FactSetData
{
    public List<FactItem> Facts { get; set; } = new();
}

/// <summary>
/// 事实项
/// </summary>
public class FactItem
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// 表单数据
/// </summary>
public class FormData
{
    public List<FormField> Fields { get; set; } = new();
}

/// <summary>
/// 表单字段
/// </summary>
public class FormField
{
    /// <summary>
    /// 字段键名（用于表单提交时的 key）
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 字段标签（显示名称）
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// 字段类型 - text / number / select / date / file / textarea
    /// </summary>
    public string Type { get; set; } = "text";

    /// <summary>
    /// 是否必填
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// 占位符文本
    /// </summary>
    public string? Placeholder { get; set; }

    /// <summary>
    /// 默认值
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// 下拉选项（仅 type=select 时使用）
    /// </summary>
    public List<SelectOption>? Options { get; set; }

    /// <summary>
    /// 最小值（仅 type=number 时使用）
    /// </summary>
    public decimal? Min { get; set; }

    /// <summary>
    /// 最大值（仅 type=number 时使用）
    /// </summary>
    public decimal? Max { get; set; }

    /// <summary>
    /// 验证规则
    /// </summary>
    public string? Validation { get; set; }
}

/// <summary>
/// 下拉选项
/// </summary>
public class SelectOption
{
    public string Label { get; set; } = string.Empty;
    public object Value { get; set; } = string.Empty;
}

/// <summary>
/// 时间线数据
/// </summary>
public class TimelineData
{
    public List<TimelineItem> Items { get; set; } = new();
}

/// <summary>
/// 时间线项
/// </summary>
public class TimelineItem
{
    /// <summary>
    /// 节点标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 状态 - pending / processing / completed / rejected / skipped
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// 审批人
    /// </summary>
    public string? Assignee { get; set; }

    /// <summary>
    /// 处理时间（字符串格式）
    /// </summary>
    public string? Time { get; set; }

    /// <summary>
    /// 审批意见
    /// </summary>
    public string? Comment { get; set; }
}

/// <summary>
/// 附件数据
/// </summary>
public class AttachmentData
{
    public List<AttachmentItem> Attachments { get; set; } = new();
}

/// <summary>
/// 附件项
/// </summary>
public class AttachmentItem
{
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? FileType { get; set; }
}

#endregion

#region 卡片操作请求/响应

/// <summary>
/// 卡片操作请求
/// </summary>
public class CardActionRequest
{
    /// <summary>
    /// 操作类型 - approve / reject / return / transfer / submit
    /// </summary>
    public string Action { get; set; } = string.Empty;

    public long? TaskId { get; set; }

    /// <summary>
    /// 审批意见
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// 表单数据（当操作需要填写表单时使用）
    /// </summary>
    public Dictionary<string, object>? FormData { get; set; }
}

/// <summary>
/// 卡片操作响应
/// </summary>
public class CardActionResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? NextAction { get; set; }
    public CardPayload? NextCard { get; set; }
}

#endregion
