namespace STOTOP.Module.System.Dtos;

/// <summary>
/// 企业信息DTO
/// </summary>
public class EnterpriseInfoDto
{
    /// <summary>
    /// 企业全称
    /// </summary>
    public string Name { get; set; } = "MDSTO";

    /// <summary>
    /// 企业简称
    /// </summary>
    public string ShortName { get; set; } = "MDSTO";

    /// <summary>
    /// Logo图片URL
    /// </summary>
    public string? LogoUrl { get; set; }
}

/// <summary>
/// 企业信息更新请求
/// </summary>
public class EnterpriseInfoUpdateDto
{
    /// <summary>
    /// 企业全称
    /// </summary>
    public string Name { get; set; } = "MDSTO";

    /// <summary>
    /// 企业简称
    /// </summary>
    public string ShortName { get; set; } = "MDSTO";

    /// <summary>
    /// Logo图片URL
    /// </summary>
    public string? LogoUrl { get; set; }
}
