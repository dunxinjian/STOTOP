namespace STOTOP.Module.System.Entities;

/// <summary>
/// 组织类型（SYS组织类型表实体）
/// </summary>
public class SysOrgType
{
    /// <summary>手动指定主键，与种子数据一致（1-9）</summary>
    public long FID { get; set; }

    /// <summary>类型编码，如 GROUP / SUBSIDIARY / CENTER / BRANCH / DEPT / TEAM / NETWORK_POINT / FRANCHISE_AREA / LAST_MILE_STATION</summary>
    public string FCode { get; set; } = "";

    /// <summary>类型名称，如 集团 / 子公司 / 中心 / 分公司 / 部门 / 团组</summary>
    public string FName { get; set; } = "";

    /// <summary>所属层级（1=集团，2=子公司/中心，3=分公司/部门，4=团组）</summary>
    public int FLevel { get; set; }

    /// <summary>是否可关联账套</summary>
    public bool FCanBindAccountSet { get; set; }

    /// <summary>是否可切换（主页切换器）</summary>
    public bool FCanSwitch { get; set; }

    /// <summary>图标名称</summary>
    public string? FIcon { get; set; }

    /// <summary>排序号</summary>
    public int FSortOrder { get; set; }

    /// <summary>说明</summary>
    public string? FDescription { get; set; }

    /// <summary>是否启用</summary>
    public bool FIsEnabled { get; set; } = true;

    /// <summary>创建时间</summary>
    public DateTime FCreateTime { get; set; } = DateTime.Now;
}
