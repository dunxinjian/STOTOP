namespace STOTOP.Module.CardFlow.Models;

public class SystemServiceAccountOptions
{
    public const string SectionName = "SystemServiceAccount";
    public long UserId { get; set; } = 1;
    public long OrgId { get; set; } = 1;
}
