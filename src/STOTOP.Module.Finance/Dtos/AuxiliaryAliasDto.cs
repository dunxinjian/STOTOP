namespace STOTOP.Module.Finance.Dtos;

public class AuxiliaryAliasDto
{
    public Guid Id { get; set; }
    public long AuxiliaryItemId { get; set; }
    public string AuxiliaryItemName { get; set; } = "";
    public string AuxiliaryItemCode { get; set; } = "";
    public string Alias { get; set; } = "";
    public string AuxType { get; set; } = "";
    public Guid? OrganizationId { get; set; }
}
