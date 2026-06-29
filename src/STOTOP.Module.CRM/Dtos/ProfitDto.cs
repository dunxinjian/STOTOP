namespace STOTOP.Module.CRM.Dtos;

public class CustomerProfitDto
{
    public long Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public long? OrgId { get; set; }
    public string Period { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit { get; set; }
    public decimal ProfitRate { get; set; }
    public int DataSource { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
}

public class CreateProfitRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public long? OrgId { get; set; }
    public string Period { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
}

public class ProfitQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? CustomerId { get; set; }
    public long? OrgId { get; set; }
    public string? Period { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class ProfitSummaryDto
{
    public long? OrgId { get; set; }
    public string? Period { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalProfit { get; set; }
    public decimal AvgProfitRate { get; set; }
    public int CustomerCount { get; set; }
}

public class ProfitRankingDto
{
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public decimal TotalProfit { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AvgProfitRate { get; set; }
}
