namespace STOTOP.Module.Finance.Dtos;

public class AccountPeriodDto
{
    public long Id { get; set; }
    public int Year { get; set; }
    public int PeriodNo { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
    public int Status { get; set; }
}
