namespace STOTOP.Module.Finance.Dtos;

/// <summary>
/// 模板列表项
/// </summary>
public class AccountTemplateDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPreset { get; set; }
    public int EnableStatus { get; set; }
    public int ItemCount { get; set; }
}

/// <summary>
/// 模板详情（含科目树）
/// </summary>
public class AccountTemplateDetailDto : AccountTemplateDto
{
    public List<AccountTemplateItemTreeDto> Items { get; set; } = new();
}

/// <summary>
/// 模板科目项
/// </summary>
public class AccountTemplateItemDto
{
    public long Id { get; set; }
    public long TemplateId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string BalanceDirection { get; set; } = string.Empty;
    public int Level { get; set; }
    public long ParentId { get; set; }
    public bool IsLeaf { get; set; }
    public string? Auxiliary { get; set; }
    public string? Currency { get; set; }
    public string? Unit { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// 树形科目项
/// </summary>
public class AccountTemplateItemTreeDto : AccountTemplateItemDto
{
    public List<AccountTemplateItemTreeDto> Children { get; set; } = new();
}

/// <summary>
/// 创建模板请求
/// </summary>
public class CreateAccountTemplateRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// 更新模板请求
/// </summary>
public class UpdateAccountTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// 新增科目项请求
/// </summary>
public class CreateTemplateItemRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string BalanceDirection { get; set; } = string.Empty;
    public long ParentId { get; set; }
    public string? Auxiliary { get; set; }
    public string? Currency { get; set; }
    public string? Unit { get; set; }
}

/// <summary>
/// 修改科目项请求
/// </summary>
public class UpdateTemplateItemRequest
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string BalanceDirection { get; set; } = string.Empty;
    public string? Auxiliary { get; set; }
    public string? Currency { get; set; }
    public string? Unit { get; set; }
}
