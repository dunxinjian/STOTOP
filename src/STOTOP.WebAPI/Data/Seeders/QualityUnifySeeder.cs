using STOTOP.Infrastructure.Data;

namespace STOTOP.WebAPI.Data.Seeders;

/// <summary>
/// Plan2 统一质控模型建表（测试自足用）。
///
/// 生产侧这些表（QL申通_* / EXP快递业务员名称映射）由应用启动
/// DatabaseSeederAdapter.CreateMissingTables + CreateRelationalArtifacts 按 EF 模型自动建表/建索引，
/// 本 Seeder 不参与应用启动；仅供集成测试 setup 在不启动应用的情况下自足建表。
///
/// 列名与类型逐字对齐各实体 Configuration（HasColumnName / HasColumnType）；
/// 唯一索引名与 Configuration 的 HasDatabaseName 一致。建表/建索引均 IF NOT EXISTS，幂等。
/// </summary>
public static class QualityUnifySeeder
{
    public static void EnsureTables(STOTOPDbContext ctx)
    {
        // 仅 SQL Server 走 DDL；InMemory / 其它 provider 直接返回（仿 CardFlowSeeder 守卫）
        if (!SeederHelper.IsSqlServer(ctx)) return;

        EnsureProblemDict(ctx);
        EnsureQualityEvent(ctx);
        EnsureEmployeeDailyMetric(ctx);
        EnsureNetworkDailyMetric(ctx);
        EnsureSalesmanAlias(ctx);

        // 建表后预置「固定常量问题类型」字典（一期 org=192 基线，幂等）。
        SeedConstantProblemDict(ctx, 192);
    }

    /// <summary>
    /// D2：预置「<b>固定常量问题类型</b>」字典（来自各 STG 源代码里写死的 <c>ProblemTypeConstant</c>，必然出现的高频类型）。
    /// 给它们可读英文编码 + 合理默认，避免归一服务（QualityUnificationService.GetOrCreateProblemDictAsync）
    /// 用前缀_短哈希自动建这批高频类型（短哈希码不可读）。
    ///
    /// 命中口径：唯一/查找键 = (FOrgId, F承运商, F质量域, F来源问题类型原文)。
    /// 此处 <b>F质量域 + F来源问题类型原文 与 ShentongSourceMap 各源的 QualityDomain + ProblemTypeConstant 逐字一致</b>，
    /// 归一查到种子即用、查不到仍自建（不破坏自动建逻辑，种子只是预置）。
    /// 默认：严重度合理默认（一期不用于 KPI）、是否考核=否（与自动建口径一致）、可归责到人=是、状态=1。
    ///
    /// 注意「<b>列值型问题类型</b>」（投诉类型/拦截类型/不合格类型/问题件类型/履约状态…，值多变且源有 ProblemTypeColumn）<b>不在此种子</b>，
    /// 仍靠归一按列值自动建。其中 疑似遗失/虚签投诉/虚假签收/履约失败 四源虽各有 ProblemTypeColumn（列优先），
    /// 但抽样显示该列值恒等于本常量（如履约率源 F履约状态 全为「履约失败」），故仍预置常量原文（列值命中同一条种子）。
    ///
    /// 组织口径：字典 org 隔离。一期 stotop 为开发测试库、申通组织=192，按 192 落基线（幂等 IF NOT EXISTS）。
    /// 生产多组织由归一自动建兜底，或后续按需扩展（不在此引入多组织枚举，保持轻量）。
    /// 幂等：按四元唯一键 WHERE NOT EXISTS 判断，重复执行不新增、不报错。
    /// 全部为固定字面量（org/域/原文/编码/严重度均代码写死，非用户输入），无 SQL 注入风险。
    /// </summary>
    public static void SeedConstantProblemDict(STOTOPDbContext ctx, long orgId)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // (质量域, 来源问题类型原文=名称, 编码, 默认严重度, 是否考核, 是否可归责到人)
        // 域/原文与 ShentongSourceMap 各源 QualityDomain/ProblemTypeConstant 逐字一致（命中归一查找键）。
        var rows = new (string Domain, string Raw, string Code, int Severity, bool Assessed, bool Attributable)[]
        {
            ("揽收时效",     "揽收不及时",  "PICK_LATE",            1, false, true), // STG申通_揽收分析明细
            ("出仓时效",     "未及时出仓",  "OUTB_LATE",            1, false, true), // STG申通_未出仓监控明细
            ("交货滞留",     "交货滞留",    "HANDOVER_DELAY",       1, false, true), // STG申通_交货滞留明细
            ("派送签收时效", "签收未达标",  "SIGN_SUBSTD",          1, false, true), // STG申通_签收未达标明细
            ("积压与遗失",   "疑似遗失",    "SUSPECT_LOSS",         2, false, true), // STG申通_疑似遗失明细
            ("虚假签收履约", "虚签投诉",    "FAKE_SIGN_COMPLAINT",  2, false, true), // STG申通_虚签投诉明细
            ("虚假签收履约", "虚假签收",    "FAKE_SIGN",            2, false, true), // STG申通_虚假签收明细
            ("虚假签收履约", "履约失败",    "FULFILL_FAIL",         1, false, true), // STG申通_履约率明细(F履约状态 抽样恒「履约失败」)
        };

        foreach (var r in rows)
        {
            // 全字面量；中文原文/编码无单引号，按 SQL 字符串字面量直接拼（仍按惯例双写单引号防御）。
            var domain = r.Domain.Replace("'", "''");
            var raw = r.Raw.Replace("'", "''");
            var code = r.Code.Replace("'", "''");
            var name = raw; // 名称=原文
            var assessed = r.Assessed ? 1 : 0;
            var attributable = r.Attributable ? 1 : 0;

            SeederHelper.ExecuteRawSql(ctx, $@"
            IF NOT EXISTS (
                SELECT 1 FROM [QL申通_质量问题字典]
                WHERE [FOrgId] = {orgId}
                  AND [F承运商] = N'申通'
                  AND [F质量域] = N'{domain}'
                  AND [F来源问题类型原文] = N'{raw}'
            )
            INSERT INTO [QL申通_质量问题字典]
                ([FOrgId],[F承运商],[F质量域],[F来源问题类型原文],[F问题类型编码],[F问题类型名称],[F默认严重度],[F是否考核],[F是否可归责到人],[F状态])
            VALUES
                ({orgId}, N'申通', N'{domain}', N'{raw}', N'{code}', N'{name}', {r.Severity}, {assessed}, {attributable}, 1);
            ");
        }
    }

    // ── 1. QL申通_质量问题字典 ──
    private static void EnsureProblemDict(STOTOPDbContext ctx)
    {
        SeederHelper.ExecuteRawSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'QL申通_质量问题字典')
        CREATE TABLE [QL申通_质量问题字典] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [FOrgId] BIGINT NOT NULL,
            [F承运商] NVARCHAR(50) NOT NULL,
            [F质量域] NVARCHAR(50) NOT NULL,
            [F来源问题类型原文] NVARCHAR(200) NOT NULL,
            [F问题类型编码] NVARCHAR(50) NOT NULL,
            [F问题类型名称] NVARCHAR(200) NOT NULL,
            [F问题大类] NVARCHAR(100) NULL,
            [F问题小类] NVARCHAR(100) NULL,
            [F默认严重度] INT NOT NULL DEFAULT 0,
            [F是否考核] BIT NOT NULL DEFAULT 0,
            [F是否可归责到人] BIT NOT NULL DEFAULT 0,
            [F默认整改时限小时] INT NULL,
            [F状态] INT NOT NULL DEFAULT 1,
            [F备注] NVARCHAR(500) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_QL申通_质量问题字典_来源原文' AND object_id = OBJECT_ID(N'QL申通_质量问题字典'))
        CREATE UNIQUE INDEX [UX_QL申通_质量问题字典_来源原文] ON [QL申通_质量问题字典]([FOrgId],[F承运商],[F质量域],[F来源问题类型原文]);
        ");
    }

    // ── 2. QL申通_承运商质量事件 ──
    private static void EnsureQualityEvent(STOTOPDbContext ctx)
    {
        SeederHelper.ExecuteRawSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'QL申通_承运商质量事件')
        CREATE TABLE [QL申通_承运商质量事件] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [FOrgId] BIGINT NOT NULL,
            [F承运商] NVARCHAR(50) NOT NULL,
            [F业务日期] DATETIME NULL,
            [F统计年月] NVARCHAR(20) NULL,
            [F运单号] NVARCHAR(200) NULL,
            [F网点编码] NVARCHAR(50) NULL,
            [F网点名称] NVARCHAR(200) NULL,
            [F员工工号] NVARCHAR(50) NULL,
            [F员工姓名原文] NVARCHAR(200) NULL,
            [F员工ID] BIGINT NULL,
            [F电商平台] NVARCHAR(100) NULL,
            [F质量域] NVARCHAR(50) NOT NULL,
            [F问题类型编码] NVARCHAR(50) NULL,
            [F问题类型名称] NVARCHAR(200) NULL,
            [F严重度] INT NOT NULL DEFAULT 0,
            [F是否考核件] BIT NOT NULL DEFAULT 0,
            [F考核金额] DECIMAL(18,2) NULL,
            [F责任方] NVARCHAR(100) NULL,
            [F来源STG表] NVARCHAR(200) NOT NULL,
            [F来源行ID] BIGINT NOT NULL,
            [F来源批次ID] BIGINT NULL,
            [F关键字段JSON] NVARCHAR(MAX) NULL,
            [F网点匹配状态] INT NOT NULL DEFAULT 0,
            [F员工匹配状态] INT NOT NULL DEFAULT 0,
            [F是否已提升异常单] BIT NOT NULL DEFAULT 0,
            [F关联异常单ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE()
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_QL申通_质量事件_源行' AND object_id = OBJECT_ID(N'QL申通_承运商质量事件'))
        CREATE UNIQUE INDEX [UX_QL申通_质量事件_源行] ON [QL申通_承运商质量事件]([FOrgId],[F承运商],[F来源STG表],[F来源行ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QL申通_质量事件_承运商_日期_网点' AND object_id = OBJECT_ID(N'QL申通_承运商质量事件'))
        CREATE INDEX [IX_QL申通_质量事件_承运商_日期_网点] ON [QL申通_承运商质量事件]([F承运商],[F业务日期],[F网点编码]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QL申通_质量事件_员工_日期' AND object_id = OBJECT_ID(N'QL申通_承运商质量事件'))
        CREATE INDEX [IX_QL申通_质量事件_员工_日期] ON [QL申通_承运商质量事件]([F员工工号],[F业务日期]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QL申通_质量事件_运单号' AND object_id = OBJECT_ID(N'QL申通_承运商质量事件'))
        CREATE INDEX [IX_QL申通_质量事件_运单号] ON [QL申通_承运商质量事件]([F运单号]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QL申通_质量事件_质量域_问题编码' AND object_id = OBJECT_ID(N'QL申通_承运商质量事件'))
        CREATE INDEX [IX_QL申通_质量事件_质量域_问题编码] ON [QL申通_承运商质量事件]([F质量域],[F问题类型编码]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QL申通_质量事件_网点匹配状态' AND object_id = OBJECT_ID(N'QL申通_承运商质量事件'))
        CREATE INDEX [IX_QL申通_质量事件_网点匹配状态] ON [QL申通_承运商质量事件]([F网点匹配状态]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QL申通_质量事件_员工匹配状态' AND object_id = OBJECT_ID(N'QL申通_承运商质量事件'))
        CREATE INDEX [IX_QL申通_质量事件_员工匹配状态] ON [QL申通_承运商质量事件]([F员工匹配状态]);
        ");
    }

    // ── 3. QL申通_员工日质量指标 ──
    private static void EnsureEmployeeDailyMetric(STOTOPDbContext ctx)
    {
        SeederHelper.ExecuteRawSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'QL申通_员工日质量指标')
        CREATE TABLE [QL申通_员工日质量指标] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [FOrgId] BIGINT NOT NULL,
            [F承运商] NVARCHAR(50) NOT NULL,
            [F业务日期] DATETIME NOT NULL,
            [F统计年月] NVARCHAR(20) NULL,
            [F网点编码] NVARCHAR(50) NOT NULL,
            [F员工工号] NVARCHAR(50) NOT NULL,
            [F员工姓名原文] NVARCHAR(200) NULL,
            [F员工ID] BIGINT NULL,
            [F派件量] INT NULL,
            [F当日派签量] INT NULL,
            [F当日派签率] DECIMAL(9,4) NULL,
            [F应上门量] INT NULL,
            [F未上门量] INT NULL,
            [F按需上门率] DECIMAL(9,4) NULL,
            [F客诉发起量] INT NULL,
            [F工单定责量] INT NULL,
            [F客诉发起率] DECIMAL(9,4) NULL,
            [F虚假签收数] INT NULL,
            [F照片质检不合格数] INT NULL,
            [F派送超时T0数] INT NULL,
            [F派送超时T1数] INT NULL,
            [F派送超时T2数] INT NULL,
            [F派送超时T3数] INT NULL,
            [F揽收不及时数] INT NULL,
            [F上传不及时数] INT NULL,
            [F问题件数] INT NULL,
            [F违规虚假电联] INT NULL,
            [F违规无效电联] INT NULL,
            [F违规双签] INT NULL,
            [F违规照片定位虚假] INT NULL,
            [F违规签收文本不规范] INT NULL,
            [F违规引导代收] INT NULL,
            [F回访真实率] DECIMAL(9,4) NULL,
            [F考核金额合计] DECIMAL(18,2) NULL,
            [F来源批次ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE()
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_QL申通_员工日质量指标_日期网点员工' AND object_id = OBJECT_ID(N'QL申通_员工日质量指标'))
        CREATE UNIQUE INDEX [UX_QL申通_员工日质量指标_日期网点员工] ON [QL申通_员工日质量指标]([FOrgId],[F承运商],[F业务日期],[F网点编码],[F员工工号]);
        ");
    }

    // ── 4. QL申通_网点日质量指标 ──
    private static void EnsureNetworkDailyMetric(STOTOPDbContext ctx)
    {
        SeederHelper.ExecuteRawSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'QL申通_网点日质量指标')
        CREATE TABLE [QL申通_网点日质量指标] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [FOrgId] BIGINT NOT NULL,
            [F承运商] NVARCHAR(50) NOT NULL,
            [F业务日期] DATETIME NOT NULL,
            [F统计年月] NVARCHAR(20) NULL,
            [F网点编码] NVARCHAR(50) NOT NULL,
            [F网点名称] NVARCHAR(200) NULL,
            [F片区] NVARCHAR(100) NULL,
            [F省区] NVARCHAR(100) NULL,
            [F揽收上传不及时率] DECIMAL(9,4) NULL,
            [F派件上传不及时率] DECIMAL(9,4) NULL,
            [F签收上传不及时率] DECIMAL(9,4) NULL,
            [F揽收缺失率] DECIMAL(9,4) NULL,
            [F派件缺失率] DECIMAL(9,4) NULL,
            [F到件缺失率] DECIMAL(9,4) NULL,
            [F不准确率] DECIMAL(9,4) NULL,
            [F到件不准确率] DECIMAL(9,4) NULL,
            [F及时揽收率] DECIMAL(9,4) NULL,
            [F未及时揽收量] INT NULL,
            [F一频次出仓及时率] DECIMAL(9,4) NULL,
            [F未及时出仓量] INT NULL,
            [F出仓预估考核金额] DECIMAL(18,2) NULL,
            [F滞留率] DECIMAL(9,4) NULL,
            [F考核滞留量] INT NULL,
            [F滞留预估考核金额] DECIMAL(18,2) NULL,
            [F一阶段及时签收率] DECIMAL(9,4) NULL,
            [F二阶段及时签收率] DECIMAL(9,4) NULL,
            [F当天及时签收率] DECIMAL(9,4) NULL,
            [F派送预估考核金额] DECIMAL(18,2) NULL,
            [F有偿派费金额] DECIMAL(18,2) NULL,
            [F预计返款金额] DECIMAL(18,2) NULL,
            [F48h签收率] DECIMAL(9,4) NULL,
            [F签收率考核金额] DECIMAL(18,2) NULL,
            [F日均出港量] INT NULL,
            [F日均进港量] INT NULL,
            [F积压倍数] DECIMAL(9,4) NULL,
            [F超3天积压量] INT NULL,
            [F超5天积压量] INT NULL,
            [F超7天积压量] INT NULL,
            [F遗失率ppm] DECIMAL(18,2) NULL,
            [F遗失量] INT NULL,
            [F进港投诉量] INT NULL,
            [F进港投诉率] DECIMAL(9,4) NULL,
            [F虚签投诉率] DECIMAL(9,4) NULL,
            [F7日虚签投诉量] INT NULL,
            [F应拦截量] INT NULL,
            [F拦截成功率] DECIMAL(9,4) NULL,
            [F及时转出率] DECIMAL(9,4) NULL,
            [F自建渗透率] DECIMAL(9,4) NULL,
            [F渗透率目标] DECIMAL(9,4) NULL,
            [F建站待完成] INT NULL,
            [F喵柜激活格口数] INT NULL,
            [F考核金额合计] DECIMAL(18,2) NULL,
            [F来源批次ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE()
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_QL申通_网点日质量指标_日期网点' AND object_id = OBJECT_ID(N'QL申通_网点日质量指标'))
        CREATE UNIQUE INDEX [UX_QL申通_网点日质量指标_日期网点] ON [QL申通_网点日质量指标]([FOrgId],[F承运商],[F业务日期],[F网点编码]);
        ");
    }

    // ── 5. EXP快递业务员名称映射（Express ExpSalesmanAlias）──
    private static void EnsureSalesmanAlias(STOTOPDbContext ctx)
    {
        SeederHelper.ExecuteRawSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'EXP快递业务员名称映射')
        CREATE TABLE [EXP快递业务员名称映射] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F名称] NVARCHAR(100) NOT NULL,
            [F员工编号] NVARCHAR(50) NOT NULL,
            [F组织ID] BIGINT NOT NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_快递业务员名称映射_员工编号' AND object_id = OBJECT_ID(N'EXP快递业务员名称映射'))
        CREATE INDEX [IX_快递业务员名称映射_员工编号] ON [EXP快递业务员名称映射]([F员工编号]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_快递业务员名称映射_名称组织' AND object_id = OBJECT_ID(N'EXP快递业务员名称映射'))
        CREATE UNIQUE INDEX [UQ_快递业务员名称映射_名称组织] ON [EXP快递业务员名称映射]([F名称],[F组织ID]);
        ");
    }
}
