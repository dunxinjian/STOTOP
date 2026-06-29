namespace STOTOP.Module.Finance.Dtos;

public class GrantAccountSetRequest
{
    public long UserId { get; set; }
    public long AccountSetId { get; set; }
    public long AccountSetRoleId { get; set; }
}

public class UpdateAccountSetRoleRequest
{
    public long AccountSetRoleId { get; set; }
}
