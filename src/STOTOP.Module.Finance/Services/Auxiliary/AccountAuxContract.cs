using STOTOP.Module.Finance.Constants;

namespace STOTOP.Module.Finance.Services.Auxiliary;

/// <summary>
/// 科目辅助核算契约:科目表 <c>FinAccount.FAuxiliary</c>(逗号分隔英文码)声明了
/// "该科目的凭证分录必须带哪些辅助核算维度"。阿米巴方案B 据此:
/// ① 校验导入/手工凭证分录是否带齐维度(EntryAuxValidator, 批次2);
/// ② 驱动自动凭证引擎给分录打全辅助核算(AutoVoucherAuxiliaryResolver, 批次2)。
/// 纯函数,无 DB 依赖(FAuxiliary 字符串由调用方传入)。
/// </summary>
public static class AccountAuxContract
{
    private static readonly IReadOnlySet<string> EmptySet = new HashSet<string>();

    /// <summary>
    /// 解析科目声明的辅助核算维度(英文码集合)。兼容历史中文名(自动翻译)。
    /// 空/null/无可识别项 → 空集(表示该科目无辅助核算)。
    /// </summary>
    public static IReadOnlySet<string> GetDeclaredAuxTypes(string? fAuxiliary)
    {
        if (string.IsNullOrWhiteSpace(fAuxiliary)) return EmptySet;

        var set = new HashSet<string>();
        foreach (var part in fAuxiliary.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var code = AuxTypes.Normalize(part);
            if (AuxTypes.IsValid(code)) set.Add(code);
        }
        return set;
    }

    /// <summary>
    /// 校验分录实带的辅助核算键是否覆盖科目声明的全部维度,返回缺失的维度码(空=合规)。
    /// </summary>
    public static IReadOnlyList<string> GetMissingAuxTypes(string? fAuxiliary, IReadOnlyCollection<string> entryAuxKeys)
    {
        var declared = GetDeclaredAuxTypes(fAuxiliary);
        if (declared.Count == 0) return Array.Empty<string>();

        var present = new HashSet<string>(entryAuxKeys);
        return declared.Where(d => !present.Contains(d)).ToList();
    }
}
