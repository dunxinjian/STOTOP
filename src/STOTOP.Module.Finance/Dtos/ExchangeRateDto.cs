namespace STOTOP.Module.Finance.Dtos;

public class ExchangeRateDto
{
    public long Id { get; set; }
    public long AccountSetId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string CurrencyName { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime CreateTime { get; set; }
}

public class SaveExchangeRateRequest
{
    public long? Id { get; set; }
    public long AccountSetId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string CurrencyName { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public DateTime EffectiveDate { get; set; }
}

public class CurrencyDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
