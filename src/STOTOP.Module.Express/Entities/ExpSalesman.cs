namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 业务员（主键为 F工号，不继承 BaseEntity）
/// </summary>
public class ExpSalesman
{
    /// <summary>工号（主键）</summary>
    public string FEmployeeNo { get; set; } = string.Empty;
    /// <summary>网点编号</summary>
    public string FNetworkPointCode { get; set; } = string.Empty;
    /// <summary>员工ID</summary>
    public long FEmployeeId { get; set; }
    /// <summary>姓名</summary>
    public string FName { get; set; } = string.Empty;
    /// <summary>联系电话</summary>
    public string? FPhone { get; set; }
    /// <summary>所属部门</summary>
    public string? FDepartment { get; set; }
    /// <summary>入职日期</summary>
    public DateOnly? FHireDate { get; set; }
    /// <summary>状态 1启用 0停用</summary>
    public int FStatus { get; set; } = 1;
    /// <summary>备注</summary>
    public string? FRemark { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    /// <summary>更新时间</summary>
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
}
