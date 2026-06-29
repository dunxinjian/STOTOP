using Microsoft.Data.SqlClient;
using STOTOP.Module.Quality.Services.Unification;

namespace STOTOP.Module.CardFlow.Tests;

// STOTOP.Module.Task / STOTOP.Module.System 会遮蔽 BCL Task 命名空间，恢复别名
using Task = global::System.Threading.Tasks.Task;

/// <summary>
/// 申通真库测试 STG/QL 残留清理器 + Collection 级一次性基线重置 fixture（根治「跨批次去重假阴性」）。
///
/// 背景（根因）：开发测试库 stotop 的 [STG申通_*] 暂存表里会累积<b>孤儿残留行</b>——
/// 来自被 Ctrl-C / agent 中断的测试或导入进程（跳过了 try/finally 清理）。CardFlow 导入有<b>跨批次去重</b>：
/// STG 已存在相同去重键（如 F运单号+F问题类型）的行时，新导入被整批跳过（插件返回「导入未产生新数据：跳过N行」soft-fail）。
/// 于是依赖「首次导入即拿到数据」的既有真库测试（LogisticsCompletenessImportIntegrationTests /
/// ShentongQualityBatchE2ETests / UnifyLogisticsCompletenessTests / ShentongBatch2·3EventSourcesTests 等）
/// 首跑就拿不到行、Assert 失败——<b>非代码缺陷</b>，纯属脏库基线。
///
/// 弹性机制（组合 A+B，本类提供两者）：
///  - <b>(B) Collection 级一次性基线重置</b>：<see cref="ShentongStgResidueResetFixture"/> 作为
///    StotopRealDb collection 的 <c>ICollectionFixture</c>，构造时（每轮测试开始、任何 StotopRealDb 测试运行前）
///    一次性清空 org=192 的全部 [STG申通_*]（枚举 <see cref="ShentongSourceMap.All"/> 的 StgTableName）
///    与 4 张 [QL申通_*] 残留 → 跨轮自愈，即使上轮被中断留下孤儿残留，下一轮开跑前也回到干净基线。
///  - <b>(A) 共享残留预清 helper</b>：<see cref="CleanAsync(string)"/> 静态方法供个别导入/归一测试在 arrange
///    （首次导入前）直接调用，清掉本测试要用源表的同轮残留（防同轮其它测试落下的行影响）。批次1/4/5
///    已各自实现了按网点编码/名称的窄域预清，本 helper 是按 org 的全域版，二者互补。
///
/// 安全边界：清理<b>严格限定</b> [STG申通_*] / [QL申通_*] 且 <c>FOrgId=192</c>（测试组织）。
/// stotop 是开发测试库，org192 的申通 STG/QL 全部是测试产物，清理安全；绝不触碰其它模块/组织数据。
/// 所有 DELETE 都先 <c>IF OBJECT_ID(...) IS NOT NULL</c> 守卫，未建的表静默跳过（建表由各测试 setup 负责）。
/// </summary>
public static class ShentongStgResidueReset
{
    /// <summary>测试组织（江苏太仓申通）。仅清此 org。</summary>
    public const long TestOrgId = 192;

    /// <summary>归一目标 4 张 QL 表（均含 FOrgId）。STG 表清单从 <see cref="ShentongSourceMap.All"/> 动态枚举。</summary>
    private static readonly string[] QlTables =
    {
        "QL申通_质量问题字典",
        "QL申通_承运商质量事件",
        "QL申通_员工日质量指标",
        "QL申通_网点日质量指标",
    };

    /// <summary>
    /// 测试合成的「业务员脏名别名」具体名单（org=192）。仅这些<b>测试专属脏名</b>会被跨轮重置——
    /// 用「具体名单」而非「by org 全表」，避免误删真实业务员名称映射主数据。
    /// 来源：UnifyCourierFulfillTests（城区吴健304 / 操作部吴健999）、ShentongUnifyBatchE2ETests（城区吴健304 / 物流完整性脏签收员名）。
    /// </summary>
    private static readonly string[] TestSyntheticAliasNames =
    {
        "城区吴健304",
        "操作部吴健999",
        "城区029申亚楠17638932823",
    };

    /// <summary>
    /// 测试合成的「业务员工号」具体名单。3209999 是 UnifyCourierFulfillTests 合成工号；ETEST_ 前缀是 D1 合成工号。
    /// 用 ETEST[_]% 前缀 + 具体值，绝不泛删真实工号（如 3202885246/3202880036）。
    /// </summary>
    private static readonly string[] TestSyntheticEmpNos =
    {
        "3209999",
    };

    /// <summary>
    /// 清空 org=192 的全部 [STG申通_*]（<see cref="ShentongSourceMap.All"/> 枚举）+ 4 张 [QL申通_*] 残留行。
    /// 每张表 DELETE 前 <c>IF OBJECT_ID IS NOT NULL</c> 守卫；连接不可达时静默吞掉（与 IsAvailable 跳过语义一致）。
    /// </summary>
    public static async Task CleanAsync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        // STG 表：从源映射动态枚举（加源即覆盖，无需手维护清单）。
        var stgTables = ShentongSourceMap.All.Values
            .Select(d => d.StgTableName)
            .Distinct()
            .ToArray();

        try
        {
            await using var c = new SqlConnection(connectionString);
            await c.OpenAsync();

            foreach (var table in stgTables.Concat(QlTables))
            {
                await using var cmd = c.CreateCommand();
                // 表名来自代码常量（非用户输入），但仍用 OBJECT_ID 守卫避免对未建表报错。
                cmd.CommandText =
                    $"IF OBJECT_ID(N'[{table}]', N'U') IS NOT NULL DELETE FROM [{table}] WHERE [FOrgId] = @org;";
                cmd.Parameters.AddWithValue("@org", TestOrgId);
                await cmd.ExecuteNonQueryAsync();
            }

            // ── EXP 测试合成别名/业务员跨轮重置（具体名单，绝不泛删 org 全表，避免误删真实主数据）──
            // 根治「Express 别名种子残留」跨类隔离假阴性：上轮被中断留下 城区吴健304→某工号 别名残留时，
            // 下轮 UnifyCourierFulfillTests 旧逻辑会被污染。此处跨轮把测试脏名别名 + 合成工号清回干净基线。
            await CleanTestSyntheticExpAsync(c);
        }
        catch
        {
            // 连接不可达 / 库未就绪：与「无可用连接则 Skip」一致，不让基线重置阻断测试运行。
            // 真正需要清的测试若库可达，CleanAsync 会成功；不可达时该测试本就会 Skip。
        }
    }

    /// <summary>
    /// 跨轮清掉测试合成的 EXP 别名/业务员（具体名单 + ETEST_ 合成工号前缀），不触碰真实主数据。
    /// 用已打开的连接执行；表存在性用 OBJECT_ID 守卫。
    /// </summary>
    private static async Task CleanTestSyntheticExpAsync(SqlConnection c)
    {
        // 别名表：按「具体测试脏名」+ org，再按「ETEST_ 合成工号」清。
        {
            await using var cmd = c.CreateCommand();
            var nameParams = string.Join(",", TestSyntheticAliasNames.Select((_, i) => "@an" + i));
            cmd.CommandText =
                "IF OBJECT_ID(N'[EXP快递业务员名称映射]', N'U') IS NOT NULL " +
                "DELETE FROM [EXP快递业务员名称映射] WHERE [F组织ID] = @org " +
                $"AND ([F名称] IN ({nameParams}) OR [F员工编号] LIKE N'ETEST[_]%');";
            cmd.Parameters.AddWithValue("@org", TestOrgId);
            for (int i = 0; i < TestSyntheticAliasNames.Length; i++)
                cmd.Parameters.AddWithValue("@an" + i, TestSyntheticAliasNames[i]);
            await cmd.ExecuteNonQueryAsync();
        }

        // 业务员表：按「ETEST_ 合成工号前缀」+「具体合成工号名单」清（无 FOrgId 列，工号本身即测试专属）。
        {
            await using var cmd = c.CreateCommand();
            var noParams = string.Join(",", TestSyntheticEmpNos.Select((_, i) => "@en" + i));
            cmd.CommandText =
                "IF OBJECT_ID(N'[EXP业务员]', N'U') IS NOT NULL " +
                $"DELETE FROM [EXP业务员] WHERE [F工号] LIKE N'ETEST[_]%' OR [F工号] IN ({noParams});";
            for (int i = 0; i < TestSyntheticEmpNos.Length; i++)
                cmd.Parameters.AddWithValue("@en" + i, TestSyntheticEmpNos[i]);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}

/// <summary>
/// StotopRealDb collection 的一次性基线重置 fixture（机制 B）。
/// xUnit 在该 collection 的<b>任何测试运行前</b>构造一次本 fixture（构造同步、内部同步等待异步清理完成），
/// 把 org=192 的申通 STG/QL 残留清空 → 每轮测试从干净基线开跑，跨轮自愈。
/// </summary>
public sealed class ShentongStgResidueResetFixture
{
    public ShentongStgResidueResetFixture()
    {
        var conn = TestSqlConnection.GetConnectionString();
        if (string.IsNullOrWhiteSpace(conn)) return; // 无连接：测试都会 Skip，无需清

        // fixture 构造器同步：阻塞等待一次性清理完成（远程库握手 + 几十张表 DELETE，秒级）。
        ShentongStgResidueReset.CleanAsync(conn).GetAwaiter().GetResult();
    }
}
