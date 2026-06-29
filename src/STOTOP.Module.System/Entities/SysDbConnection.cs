using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

public class SysDbConnection : BaseEntity
{
    public string FConnectionName { get; set; } = string.Empty;
    public string FDatabaseType { get; set; } = string.Empty;
    public string? FServer { get; set; }
    public int? FPort { get; set; }
    public string? FDatabaseName { get; set; }
    public string? FUsername { get; set; }
    public string? FPassword { get; set; }
    public string? FFilePath { get; set; }
    public int FWindowsAuth { get; set; }
    public int FTrustServerCertificate { get; set; }
    public string? FConnectionString { get; set; }
    public string? FDescription { get; set; }
    /// <summary>说明</summary>
    public string? F说明 { get; set; }
    public int FStatus { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime? FUpdateTime { get; set; }
}
