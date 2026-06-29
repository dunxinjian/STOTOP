namespace STOTOP.Module.Express.Services.Billing;

/// <summary>
/// 计费异常
/// </summary>
public class BillingException : Exception
{
    public string ErrorCode { get; }

    public BillingException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }
}
