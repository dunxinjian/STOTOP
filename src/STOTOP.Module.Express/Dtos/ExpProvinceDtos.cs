namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 省份详情
/// </summary>
public class ProvinceDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string? Region { get; set; }
    public bool IsRemote { get; set; }
}

/// <summary>
/// 省份列表项
/// </summary>
public class ProvinceListItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string? Region { get; set; }
    public bool IsRemote { get; set; }
}

/// <summary>
/// 创建省份请求
/// </summary>
public class CreateProvinceRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string? Region { get; set; }
    public bool IsRemote { get; set; }
}

/// <summary>
/// 更新省份请求
/// </summary>
public class UpdateProvinceRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string? Region { get; set; }
    public bool IsRemote { get; set; }
}

/// <summary>
/// 大区重命名请求
/// </summary>
public class RenameRegionRequest
{
    public string OldName { get; set; } = string.Empty;
    public string NewName { get; set; } = string.Empty;
}
