namespace STOTOP.Core.Models;

public abstract class BaseEntity
{
    public long FID { get; set; }
}

public abstract class BaseGuidEntity
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
}
