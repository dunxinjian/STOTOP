using STOTOP.Core.Models;

namespace STOTOP.Module.Insurance.Entities;

public class InsCompany : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public long FOrgId { get; set; }
    public string FCompanyName { get; set; } = string.Empty;       // 公司名称
    public string FCompanyCode { get; set; } = string.Empty;       // 公司编码
    public int FCompanyType { get; set; } = 1;                     // 1=财产险, 2=人寿险, 3=综合
    public string? FContactPerson { get; set; }                    // 联系人
    public string? FContactPhone { get; set; }                     // 联系电话
    public string? FAddress { get; set; }                          // 地址
    public string? FRemark { get; set; }                           // 备注
    public int FStatus { get; set; } = 1;                          // 1=启用, 0=禁用
    public long? FCreatorId { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
    // 导航属性
    public List<InsPolicy> Policies { get; set; } = new();
}
