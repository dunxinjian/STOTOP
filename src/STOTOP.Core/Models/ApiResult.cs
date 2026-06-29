namespace STOTOP.Core.Models;

public class ApiResult<T>
{
    public int Code { get; set; }
    public string Message { get; set; } = "";
    public T? Data { get; set; }
    
    public static ApiResult<T> Success(T data, string message = "操作成功") => new() { Code = 200, Message = message, Data = data };
    public static ApiResult<T> Fail(string message, int code = 400) => new() { Code = code, Message = message };
}

public class ApiResult : ApiResult<object>
{
    public static ApiResult Ok(string message = "操作成功") => new() { Code = 200, Message = message };
    public static new ApiResult Fail(string message, int code = 400) => new() { Code = code, Message = message };
}
