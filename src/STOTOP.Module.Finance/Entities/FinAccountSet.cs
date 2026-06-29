using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinAccountSet : BaseEntity
{
    public string FName { get; set; } = string.Empty;       // 账套名称，如"太仓美申账套"
    public string FCode { get; set; } = string.Empty;       // 编码
    public string FCompanyName { get; set; } = string.Empty; // 所属法人名称
    public string? FDescription { get; set; }                // 说明
    public bool FIsDefault { get; set; }                     // 是否默认账套
    public int FStatus { get; set; } = 1;                    // 0=禁用, 1=启用
    public int FSortOrder { get; set; }
    public int FStartYear { get; set; }                      // 起始年份
    public int FStartMonth { get; set; }                     // 起始月份
    public long FOrgId { get; set; }  // 组织ID
    public DateTime FCreatedTime { get; set; }
    public DateTime FUpdatedTime { get; set; }
}
