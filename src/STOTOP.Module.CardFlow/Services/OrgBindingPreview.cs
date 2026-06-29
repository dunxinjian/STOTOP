namespace STOTOP.Module.CardFlow.Services.Import;

/// <summary>组织绑定预检结果</summary>
public class OrgBindingPreview
{
    public long TargetOrgId { get; set; }
    public string TargetOrgName { get; set; } = string.Empty;
    public long? ResolvedAccountSetId { get; set; }
    public string? ResolvedAccountSetName { get; set; }
    public string[] Warnings { get; set; } = Array.Empty<string>();
}

/// <summary>组织绑定预检请求</summary>
public class OrgBindingPreviewRequest
{
    public long TargetOrgId { get; set; }
}
