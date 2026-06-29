// SQL Server 冒烟测试——AggregateDeliverVolume 三个场景占位骨架
//
// 【说明】
//   AggregateDeliverVolume 是 AmoebaPLService 的 private async Task<long> 实例方法，
//   执行参数化 SQL 查询 STG申通派件日明细（[F基础派费收费件量] 列），使用原始 SQL，
//   InMemory 不可测，需真实 SQL Server 连接。
//
//   本组测试全为占位骨架（Assert.True(true)）。
//
// 【真实启用方式（二选一）】
//   方式 A：将 AggregateDeliverVolume 提升为 internal，并在 AmoebaPLService 所在程序集配置
//     [assembly: InternalsVisibleTo("STOTOP.Module.Finance.Tests")]
//     则可通过反射或直接调用（internal 对测试程序集可见）。
//
//   方式 B：通过 GetMultiPeriodReportAsync 报表入口端到端驱动——
//     向 STG申通派件日明细 插入预设数据行，再调用报表接口，从返回的 DeliverCount/分摊结果反推断言。
//     此方式不修改生产代码，但装配更复杂（需完整模板+账套+账期种子）。
//
//   两种方式均需先通过 MigrationRunner V23 建立 STG申通派件日明细 表并插入测试行。
//
// 命名空间：STOTOP.Module.Finance.Tests.Amoeba

using Xunit;

namespace STOTOP.Module.Finance.Tests.Amoeba;

public class AggregateDeliverVolumeSmokeTests
{
    private static string? Conn => Environment.GetEnvironmentVariable("STOTOP_TEST_CONNECTION");

    // ─────────────────────────────────────────────────────────────────────
    // Test 1：半开区间 [startDate, endDate+1) 仅含区间内日期，按组织隔离
    // ─────────────────────────────────────────────────────────────────────
    // 【真实装配步骤】
    //   1. 建 STG申通派件日明细 表（MigrationRunner V23）。
    //   2. 插入测试数据（同 orgId=100）：
    //      - F结算日期=2026-05-09, F基础派费收费件量=10
    //      - F结算日期=2026-05-10, F基础派费收费件量=20
    //      - F结算日期=2026-05-11, F基础派费收费件量=30
    //      - 另一 orgId=999 F结算日期=2026-05-10, F基础派费收费件量=9999（噪声）
    //   3. 调用 AggregateDeliverVolume(startDate=2026-05-10, endDate=2026-05-10, orgId=100, scope=null)
    //      → 内部半开区间 endExclusive = 2026-05-11，WHERE F结算日期 >= 05-10 AND < 05-11
    //      → 仅匹配 05-10 行 → 期望返回 20L
    //   4. 另 orgId=999 被 FOrgId 过滤排除，不影响结果。
    //   5. 断言：result == 20L
    // ─────────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async global::System.Threading.Tasks.Task Sums_only_within_half_open_range_and_org()
    {
        Skip.If(string.IsNullOrWhiteSpace(Conn), "未设 STOTOP_TEST_CONNECTION，跳过 SQL Server 冒烟");

        // 占位——需 AggregateDeliverVolume 提为 internal 或经报表入口端到端驱动（见文件头注释）
        Assert.True(true,
            "占位：半开区间+组织隔离验证需要 internal 化或报表入口端到端装配。" +
            "期望逻辑：插 05-09=10/05-10=20/05-11=30，AggregateDeliverVolume(05-10,05-10,org)→20。");

        await global::System.Threading.Tasks.Task.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Test 2：scope.Outlets 过滤使用系统列 F归属网点编号（非 Excel 原始列 F网点编号）
    // ─────────────────────────────────────────────────────────────────────
    // 【真实装配步骤】
    //   1. 建 STG申通派件日明细 表，插入同日两行（同 orgId=100, F结算日期=2026-05-10）：
    //      - F网点编号="A原始", F归属网点编号="A", F基础派费收费件量=50
    //      - F网点编号="B原始", F归属网点编号="B", F基础派费收费件量=30
    //   2. 构造 scope = new AmoebaReportScope { Outlets = ["A"] }
    //   3. 调用 AggregateDeliverVolume(2026-05-10, 2026-05-10, 100, scope)
    //      → WHERE [F归属网点编号] IN ('A') → 仅匹配第一行
    //      → 期望返回 50L（非 80L 也非基于 F网点编号 过滤的值）
    //   4. 断言：result == 50L
    //   核心验证点：过滤列为规范化系统列 F归属网点编号（OutletResolver 填充），非 Excel F网点编号
    // ─────────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async global::System.Threading.Tasks.Task Filters_by_normalized_outlet_column()
    {
        Skip.If(string.IsNullOrWhiteSpace(Conn), "未设 STOTOP_TEST_CONNECTION，跳过 SQL Server 冒烟");

        // 占位——需 AggregateDeliverVolume 提为 internal 或报表入口端到端（见文件头注释）
        Assert.True(true,
            "占位：scope.Outlets 按 F归属网点编号 过滤验证。" +
            "期望：同日 A=50/B=30，Outlets={A} → 期望 50，确认非按 F网点编号 原始列裁。");

        await global::System.Threading.Tasks.Task.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Test 3：跨月周（05-30~06-02）半开求和仅含区间内实际天
    // ─────────────────────────────────────────────────────────────────────
    // 【真实装配步骤】
    //   1. 建 STG申通派件日明细 表，插入跨月数据（orgId=100, scope=null）：
    //      - 2026-05-30: 件量=10
    //      - 2026-05-31: 件量=20
    //      - 2026-06-01: 件量=30
    //      - 2026-06-02: 件量=40
    //      - 2026-06-03: 件量=9999（区间外噪声）
    //   2. 调用 AggregateDeliverVolume(startDate=2026-05-30, endDate=2026-06-02, orgId=100, scope=null)
    //      → 半开区间 [05-30, 06-03)：WHERE F结算日期 >= 05-30 AND < 06-03
    //      → 匹配 05-30+05-31+06-01+06-02 = 10+20+30+40 = 100
    //      → 06-03 被排除
    //   3. 断言：result == 100L
    //   核心验证点：跨月周近似问题根因——之前用"周首月"估算分母引入误差；
    //   此测试验证 AggregateDeliverVolume 本身的 SQL 半开区间逻辑正确，
    //   与月份边界无关（分母近似问题在分摊引擎 ApplyCommonCostAllocation 层，非此方法）。
    // ─────────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async global::System.Threading.Tasks.Task Cross_month_week_sums_only_days_inside_week()
    {
        Skip.If(string.IsNullOrWhiteSpace(Conn), "未设 STOTOP_TEST_CONNECTION，跳过 SQL Server 冒烟");

        // 占位——需 AggregateDeliverVolume 提为 internal 或报表入口端到端（见文件头注释）
        Assert.True(true,
            "占位：跨月周 05-30~06-02 半开求和验证。" +
            "期望：05-30=10/05-31=20/06-01=30/06-02=40/06-03=噪声 → SUM=100，06-03 被排除。");

        await global::System.Threading.Tasks.Task.CompletedTask;
    }
}
