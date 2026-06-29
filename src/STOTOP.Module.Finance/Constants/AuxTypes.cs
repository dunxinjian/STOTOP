namespace STOTOP.Module.Finance.Constants;

/// <summary>
/// 辅助核算类型编码(英文码)。三处统一使用这些码:
/// ① 科目 FinAccount.FAuxiliary 声明(逗号分隔);
/// ② 损益项 V2 F关联科目JSON 的 filters.auxType;
/// ③ 凭证分录辅助核算 AuxValues 的键。
/// 阿米巴方案B(业务方向一等公民)依赖此封闭枚举做维度过滤与校验。
/// </summary>
public static class AuxTypes
{
    public const string Outlet = "outlet";                        // 网点
    public const string BusinessDirection = "business_direction"; // 业务方向(出港/进港/综合, 取值见 BusinessDirection)
    public const string ExpressBrand = "express_brand";           // 快递品牌
    public const string BusinessUnit = "business_unit";           // 经营单元
    public const string Project = "project";                      // 项目(如裹裹)
    public const string Department = "department";                // 部门
    public const string Customer = "customer";                    // 客户
    public const string Supplier = "supplier";                    // 供应商
    public const string Employee = "employee";                    // 员工

    /// <summary>全部合法 auxType 编码(封闭枚举)</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Outlet, BusinessDirection, ExpressBrand, BusinessUnit, Project,
        Department, Customer, Supplier, Employee
    };

    /// <summary>中文名 → 英文码(科目表/损益项映射 CSV 落库时翻译用)</summary>
    public static readonly IReadOnlyDictionary<string, string> CnToCode = new Dictionary<string, string>
    {
        ["网点"] = Outlet,
        ["业务方向"] = BusinessDirection,
        ["快递品牌"] = ExpressBrand,
        ["经营单元"] = BusinessUnit,
        ["项目"] = Project,
        ["部门"] = Department,
        ["客户"] = Customer,
        ["供应商"] = Supplier,
        ["员工"] = Employee,
    };

    public static bool IsValid(string? code) => code != null && All.Contains(code);

    /// <summary>把中文名或英文码统一规整为英文码;无法识别则原样返回。</summary>
    public static string Normalize(string raw)
    {
        var t = raw.Trim();
        return CnToCode.TryGetValue(t, out var code) ? code : t;
    }
}
