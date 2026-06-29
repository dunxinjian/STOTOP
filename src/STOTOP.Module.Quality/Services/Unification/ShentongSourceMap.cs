namespace STOTOP.Module.Quality.Services.Unification;

/// <summary>
/// 归一目标种类：事件类（一行一质量事件，写 QL申通_承运商质量事件）/
/// 网点指标类（写 QL申通_网点日质量指标）/ 员工指标类（写 QL申通_员工日质量指标）。
/// 三类目标均已落地（事件类 / 网点指标类 / 员工指标类）。
/// </summary>
public enum UnifyTargetKind
{
    /// <summary>事件类：源每行 → 一条质量事件。</summary>
    Event,
    /// <summary>网点指标类：源每行 → 一条网点日指标 upsert。</summary>
    NetworkMetric,
    /// <summary>员工指标类：源每行 → 一条员工日指标 upsert。</summary>
    EmployeeMetric,
}

/// <summary>
/// 描述「某 STG 源表 → 质量域 / 目标种类 / 关键列」的映射。
/// 关键列名都是 STG 表里的列名（用于反射取值与主数据匹配），允许为空（该源无此维度）。
/// 后续 C1/C2/C3 增量加源时，只需向 <see cref="ShentongSourceMap.All"/> 追加一条 <see cref="ShentongSourceDescriptor"/>，
/// 归一服务按 <see cref="UnifyTargetKind"/> 分发处理，无需改服务主干。
/// </summary>
/// <param name="StgTableName">来源 STG 表名（也是事件 F来源STG表 的取值）。</param>
/// <param name="QualityDomain">质量域（写入字典/事件的 F质量域）。</param>
/// <param name="TargetKind">归一目标种类。</param>
/// <param name="NetworkCodeColumn">网点编码列名（无则 null）。</param>
/// <param name="NetworkNameColumn">网点名称列名（无则 null）。</param>
/// <param name="EmployeeNoColumn">员工工号列名（无则 null）。</param>
/// <param name="EmployeeNameColumn">员工姓名列名（无则 null）。</param>
/// <param name="ProblemTypeColumn">问题类型来源列名（事件类必填；无则 null）。</param>
/// <param name="DateColumn">业务日期列名（无则 null）。</param>
/// <param name="WaybillColumn">运单号列名（无则 null）。</param>
/// <param name="PlatformColumn">电商平台列名（无则 null）。</param>
/// <param name="ProblemCodePrefix">字典自建时问题类型编码前缀（如 "LOGI"）。</param>
/// <param name="OnlyFilterColumn">「仅X」过滤列名（事件类用，无则 null）。与 <see cref="OnlyFilterEquals"/> 配对：
///   WHERE 该列 = <see cref="OnlyFilterEquals"/>。二者任一为 null 则不过滤、整源入事件。</param>
/// <param name="OnlyFilterEquals">「仅X」过滤值（与 <see cref="OnlyFilterColumn"/> 配对）。</param>
/// <param name="ProblemTypeConstant">问题类型常量（与 <see cref="ProblemTypeColumn"/> 二选一：列优先，列为 null 用常量）。</param>
/// <param name="AmountColumn">金额列名（→ 事件 F考核金额，TryDecimal；null 不填）。</param>
/// <param name="KeySnapshotColumns">写入 F关键字段JSON 的列集（null/空则取该源全部声明的关键业务列）。</param>
public record ShentongSourceDescriptor(
    string StgTableName,
    string QualityDomain,
    UnifyTargetKind TargetKind,
    string? NetworkCodeColumn,
    string? NetworkNameColumn,
    string? EmployeeNoColumn,
    string? EmployeeNameColumn,
    string? ProblemTypeColumn,
    string? DateColumn,
    string? WaybillColumn,
    string? PlatformColumn,
    string ProblemCodePrefix,
    string? OnlyFilterColumn = null,
    string? OnlyFilterEquals = null,
    string? ProblemTypeConstant = null,
    string? AmountColumn = null,
    string[]? KeySnapshotColumns = null);

/// <summary>
/// 申通各 STG 源 → 归一目标的静态映射表。扩展点：增量加条目即接新源。
/// 现已覆盖全部 29 源（事件类 / 网点指标类 / 员工指标类，C0~C3 累计填充）。
/// </summary>
public static class ShentongSourceMap
{
    /// <summary>STG申通_物流完整性明细 表名常量。</summary>
    public const string LogisticsCompletenessTable = "STG申通_物流完整性明细";

    /// <summary>STG申通_物流及时准确明细 表名常量（C3 通用事件源 worked example）。</summary>
    public const string LogisticsTimelinessTable = "STG申通_物流及时准确明细";

    /// <summary>STG申通_小件员履约指标 表名常量（C1 员工日指标源）。</summary>
    public const string CourierFulfillTable = "STG申通_小件员履约指标";

    /// <summary>STG申通_积压监控汇总 表名常量（C2 网点日指标源，积压与遗失子集）。</summary>
    public const string BacklogMonitorTable = "STG申通_积压监控汇总";

    // ── C3 批次1：5 个事件源（全走通用事件路径 UnifyGenericEventAsync，仅加描述符）──
    /// <summary>STG申通_揽收分析明细 表名常量（C3 批次1，揽收时效，仅「未及时」过滤入事件）。</summary>
    public const string PickupAnalysisTable = "STG申通_揽收分析明细";

    /// <summary>STG申通_未出仓监控明细 表名常量（C3 批次1，出仓时效，整源即未出仓）。</summary>
    public const string OutboundMonitorTable = "STG申通_未出仓监控明细";

    /// <summary>STG申通_交货滞留明细 表名常量（C3 批次1，交货滞留，仅「滞留」过滤入事件）。</summary>
    public const string HandoverDelayTable = "STG申通_交货滞留明细";

    /// <summary>STG申通_末端派送考核明细 表名常量（C3 批次1，派送签收时效，整源入）。</summary>
    public const string DeliveryAssessTable = "STG申通_末端派送考核明细";

    /// <summary>STG申通_签收未达标明细 表名常量（C3 批次1，派送签收时效，整源即未达标）。</summary>
    public const string SignSubstandardTable = "STG申通_签收未达标明细";

    // ── C3 批次2：4 个事件源（全走通用事件路径 UnifyGenericEventAsync，仅加描述符）──
    /// <summary>STG申通_积压明细 表名常量（C3 批次2，积压与遗失，整源即积压问题件，用问题类型列）。</summary>
    public const string BacklogTable = "STG申通_积压明细";

    /// <summary>STG申通_疑似遗失明细 表名常量（C3 批次2，积压与遗失，整源即疑似遗失，用问题类型列）。</summary>
    public const string SuspectedLossTable = "STG申通_疑似遗失明细";

    /// <summary>STG申通_进港投诉明细 表名常量（C3 批次2，投诉与赔付，整源即投诉，用投诉类型列）。</summary>
    public const string InboundComplaintTable = "STG申通_进港投诉明细";

    /// <summary>STG申通_应拦截明细 表名常量（C3 批次2，拦截渗透，仅拦截失败入事件，带预计考核金额）。</summary>
    public const string InterceptDetailTable = "STG申通_应拦截明细";

    // ── C3 批次3：6 个事件源（最后一批事件，全走通用事件路径 UnifyGenericEventAsync，仅加描述符）──
    /// <summary>STG申通_投诉账单明细 表名常量（C3 批次3，投诉与赔付，<b>无员工维度→状态9</b>，带金额 F金额，整源入）。</summary>
    public const string ComplaintBillTable = "STG申通_投诉账单明细";

    /// <summary>STG申通_虚签投诉明细 表名常量（C3 批次3，虚假签收履约，带金额 F预估考核金额，整源入）。</summary>
    public const string FakeSignComplaintTable = "STG申通_虚签投诉明细";

    /// <summary>STG申通_虚假签收明细 表名常量（C3 批次3，虚假签收履约，整源入）。</summary>
    public const string FakeSignTable = "STG申通_虚假签收明细";

    /// <summary>STG申通_照片质检明细 表名常量（C3 批次3，虚假签收履约，<b>仅不合格(F是否质检合格=否)入事件</b>）。</summary>
    public const string PhotoQcTable = "STG申通_照片质检明细";

    /// <summary>STG申通_履约率明细 表名常量（C3 批次3，虚假签收履约，整源即履约失败，问题类型用 F履约状态 列）。</summary>
    public const string FulfillRateTable = "STG申通_履约率明细";

    /// <summary>STG申通_送货上门明细 表名常量（C3 批次3，虚假签收履约，<b>仅履约失败(F履约情况=履约失败)入事件</b>）。</summary>
    public const string HomeDeliveryTable = "STG申通_送货上门明细";

    // ── C3 批次4：6 个网点指标源（NetworkMetric，各填互不相交的字段子集，按 网点×日 合并 upsert，typed 方法）──
    /// <summary>STG申通_物流信息及时汇总 表名常量（C3 批次4，子集：揽收/派件/签收上传不及时率；无网点编码列仅名称）。</summary>
    public const string InfoIndexTimelyTable = "STG申通_物流信息及时汇总";

    /// <summary>STG申通_物流信息完整汇总 表名常量（C3 批次4，子集：揽收/派件/到件缺失率；无网点编码列仅名称）。</summary>
    public const string InfoIndexCompleteTable = "STG申通_物流信息完整汇总";

    /// <summary>STG申通_物流信息准确汇总 表名常量（C3 批次4，子集：不准确率/到件不准确率；无网点编码列仅名称）。</summary>
    public const string InfoIndexAccurateTable = "STG申通_物流信息准确汇总";

    /// <summary>STG申通_揽收考核汇总 表名常量（C3 批次4，子集：及时揽收率/未及时揽收量；有网点编码 F揽收所属网点编码）。</summary>
    public const string PickupAssessTable = "STG申通_揽收考核汇总";

    /// <summary>STG申通_出仓考核汇总 表名常量（C3 批次4，子集：一频次出仓及时率/未及时出仓量/出仓预估考核金额；有网点编码 F所属网点编码）。</summary>
    public const string OutboundAssessTable = "STG申通_出仓考核汇总";

    /// <summary>STG申通_交货滞留汇总 表名常量（C3 批次4，子集：滞留率/考核滞留量/滞留预估考核金额；有网点编码 F揽收所属网点编码）。</summary>
    public const string HandoverSummaryTable = "STG申通_交货滞留汇总";

    // ── C3 批次5：4 个网点指标源（NetworkMetric，最后一批源，各填互不相交字段子集，按 网点×日 合并 upsert，typed 方法）──
    /// <summary>STG申通_末端派送网点汇总 表名常量（C3 批次5，签收域子集：一/二阶段/当天及时签收率 + 派送预估考核金额/有偿派费/预计返款；无网点编码列仅名称 F应签所属网点）。</summary>
    public const string DeliveryNetSummaryTable = "STG申通_末端派送网点汇总";

    /// <summary>STG申通_签收率考核汇总 表名常量（C3 批次5，签收域子集：<b>仅 F签收率考核金额</b>，与末端派送汇总互不相交；有网点编码 F网点编号）。</summary>
    public const string SignRateAssessTable = "STG申通_签收率考核汇总";

    /// <summary>STG申通_拦截汇总 表名常量（C3 批次5，拦截子集：应拦截量/拦截成功率/及时转出率；无网点编码列仅名称 F所属网点）。</summary>
    public const string InterceptSummaryTable = "STG申通_拦截汇总";

    /// <summary>STG申通_渗透建站考核 表名常量（C3 批次5，渗透子集：自建渗透率/渗透率目标/建站待完成/喵柜激活格口数；有网点编码 F网点编号；日期列 F统计周期 为「YYYY-第MM月」非标准日期，typed 方法取月首日）。</summary>
    public const string PenetrationTable = "STG申通_渗透建站考核";

    /// <summary>
    /// 全部源描述（按 STG 表名索引）。后续加源 = 这里追加一条。
    /// </summary>
    public static readonly IReadOnlyDictionary<string, ShentongSourceDescriptor> All =
        new Dictionary<string, ShentongSourceDescriptor>
        {
            [LogisticsCompletenessTable] = new ShentongSourceDescriptor(
                StgTableName: LogisticsCompletenessTable,
                QualityDomain: "物流信息",
                TargetKind: UnifyTargetKind.Event,
                NetworkCodeColumn: null,            // 本源无网点编码列，仅有名称
                NetworkNameColumn: "F网点名称",
                EmployeeNoColumn: "F签收员编号",
                EmployeeNameColumn: "F签收员名称",
                ProblemTypeColumn: "F问题类型",
                DateColumn: "F统计日期",
                WaybillColumn: "F运单号",
                PlatformColumn: "F订单平台",
                ProblemCodePrefix: "LOGI"),

            // C1：小件员履约指标 → 员工日质量指标（员工级，1 行/网点/小件员）。
            // 网点仅有名称（无编码列），员工仅有脏名（无工号列），源无日期列（业务日期取批次创建日，详见服务内 TODO）。
            [CourierFulfillTable] = new ShentongSourceDescriptor(
                StgTableName: CourierFulfillTable,
                QualityDomain: "员工综合",
                TargetKind: UnifyTargetKind.EmployeeMetric,
                NetworkCodeColumn: null,            // 本源无网点编码列，仅有名称
                NetworkNameColumn: "F所属网点",
                EmployeeNoColumn: null,             // 本源无工号列，仅有脏名
                EmployeeNameColumn: "F所属小件员",
                ProblemTypeColumn: null,            // 指标类无问题类型列
                DateColumn: null,                   // 本源无日期列，取批次日（TODO：申通补日期列后改读源列）
                WaybillColumn: null,
                PlatformColumn: null,
                ProblemCodePrefix: "CRFL"),

            // C2：积压监控汇总 → 网点日质量指标（网点级，1 行/网点/日期）。本源<b>有网点编码</b>。
            // 网点指标由多个 NetworkMetric 源各填子集、按 网点×日 合并 upsert；本源只填「积压与遗失」子集
            // （积压倍数/超N天积压量/遗失率ppm/遗失量/进港投诉/虚签投诉等），其它源字段（出仓/滞留/签收等）留 null 待 C3 填。
            [BacklogMonitorTable] = new ShentongSourceDescriptor(
                StgTableName: BacklogMonitorTable,
                QualityDomain: "积压与遗失",
                TargetKind: UnifyTargetKind.NetworkMetric,
                NetworkCodeColumn: "F网点编码",   // 本源有网点编码列
                NetworkNameColumn: "F网点名称",
                EmployeeNoColumn: null,            // 网点级，无员工维度
                EmployeeNameColumn: null,
                ProblemTypeColumn: null,           // 指标类无问题类型列
                DateColumn: "F统计日期",
                WaybillColumn: null,
                PlatformColumn: null,
                ProblemCodePrefix: "BKLG"),

            // C3 worked example：物流及时准确明细 → 质量事件（通用事件路径，第一个非 typed 事件源）。
            // 三个源文件（到件晚于签收/派件晚于签收/揽收上传不及时）同结构，靠「F问题类型」列区分事件类型。
            // 网点仅有名称（无编码列），员工取派件员（编号+名称），问题类型来自列 F问题类型，无金额列、无「仅X」过滤（整源入事件）。
            // 走通用路径 UnifyGenericEventAsync：列名全部来自本描述符，行用 StgRawReader（raw-SQL 按列名读）取。
            [LogisticsTimelinessTable] = new ShentongSourceDescriptor(
                StgTableName: LogisticsTimelinessTable,
                QualityDomain: "物流信息",
                TargetKind: UnifyTargetKind.Event,
                NetworkCodeColumn: null,            // 本源无网点编码列，仅有名称
                NetworkNameColumn: "F网点名称",
                EmployeeNoColumn: "F派件员编号",
                EmployeeNameColumn: "F派件员名称",
                ProblemTypeColumn: "F问题类型",
                DateColumn: "F统计日期",
                WaybillColumn: "F运单号",
                PlatformColumn: "F订单平台",
                ProblemCodePrefix: "LOGT",
                // 无「仅X」过滤（OnlyFilter* 留 null）、无问题类型常量（用列）、无金额列。
                // 关键字段快照列：日期/运单/网点/问题类型/扫描时间/派件员，便于审计回溯。
                KeySnapshotColumns: new[] { "F统计日期", "F运单号", "F网点名称", "F所属网点名称", "F问题类型", "F扫描时间", "F扫描员", "F扫描员编号", "F订单平台", "F派件员编号", "F派件员名称" }),

            // ─────────────────────────────────────────────────────────────────────
            // C3 批次1：5 个事件源（全走通用事件路径，仅加描述符；列名已逐源核对实体确认）。
            // ─────────────────────────────────────────────────────────────────────

            // ① 揽收分析明细 → 质量事件（域=揽收时效）。网点仅有名称（F揽收所属网点），员工=F收件员（仅脏名，无工号列）。
            //    问题类型用常量「揽收不及时」（源无问题类型列）。运单列是 F运单编号（非 F运单号）；本源无电商平台映射列（有 F电商平台但与其它源平台口径一致，纳入快照不作平台维度）。
            //    「仅未及时」过滤：F揽收及时标识 = 否（抽样确认：标识取值为「是/否」，未及时=「否」；本样例 455 行全为「否」即全部未及时，过滤选中全部）。
            [PickupAnalysisTable] = new ShentongSourceDescriptor(
                StgTableName: PickupAnalysisTable,
                QualityDomain: "揽收时效",
                TargetKind: UnifyTargetKind.Event,
                NetworkCodeColumn: null,             // 本源无网点编码列，仅有名称
                NetworkNameColumn: "F揽收所属网点",
                EmployeeNoColumn: null,              // 本源无工号列，仅 F收件员 脏名
                EmployeeNameColumn: "F收件员",
                ProblemTypeColumn: null,             // 无问题类型列 → 用常量
                DateColumn: "F统计日期",
                WaybillColumn: "F运单编号",          // 本源运单列是「运单编号」
                PlatformColumn: "F电商平台",
                ProblemCodePrefix: "PKUP",
                OnlyFilterColumn: "F揽收及时标识",   // 仅未及时入事件
                OnlyFilterEquals: "否",              // 抽样确认：未及时=「否」（是/否标识）
                ProblemTypeConstant: "揽收不及时",
                KeySnapshotColumns: new[] { "F统计日期", "F运单编号", "F揽收所属网点", "F收件员", "F揽收及时标识", "F揽收时间", "F揽收截止时间", "F订单揽收用时h", "F电商平台" }),

            // ② 未出仓监控明细 → 质量事件（域=出仓时效）。整源即「未出仓」，无过滤、问题类型用常量「未及时出仓」。
            //    网点有名称 F应签所属网点（也有编码列 F应签所属网点编码，但与其它源统一按名称匹配，编码纳入快照）；员工=F派件员（仅脏名）。
            //    本源无电商平台列。
            [OutboundMonitorTable] = new ShentongSourceDescriptor(
                StgTableName: OutboundMonitorTable,
                QualityDomain: "出仓时效",
                TargetKind: UnifyTargetKind.Event,
                NetworkCodeColumn: null,             // 统一按名称匹配；编码列入快照
                NetworkNameColumn: "F应签所属网点",
                EmployeeNoColumn: null,
                EmployeeNameColumn: "F派件员",
                ProblemTypeColumn: null,             // 无问题类型列 → 用常量
                DateColumn: "F统计日期",
                WaybillColumn: "F运单号",
                PlatformColumn: null,                // 本源无电商平台列
                ProblemCodePrefix: "OUTB",
                // 整源入：实体无明确「是否未出仓」单列，样例亦无正常行混入（导出本身=未出仓实时监控）→ 不过滤。
                ProblemTypeConstant: "未及时出仓",
                KeySnapshotColumns: new[] { "F统计日期", "F运单号", "F应签所属网点", "F应签所属网点编码", "F派件员", "F实际出仓时间", "F理论应出仓时间", "F理论应出仓日期", "F出仓距离", "F三段码" }),

            // ③ 交货滞留明细 → 质量事件（域=交货滞留）。网点仅有名称 F揽收所属网点；员工=F揽收小件员名称（脏名，带「揽收」前缀，匹配器启发式处理）。
            //    问题类型常量「交货滞留」（源无问题类型列）。
            //    「仅滞留」过滤：F交货滞留标识 = 是（抽样确认：是/否标识，滞留=「是」；本样例 380 行全为「是」即全部滞留，过滤选中全部）。
            [HandoverDelayTable] = new ShentongSourceDescriptor(
                StgTableName: HandoverDelayTable,
                QualityDomain: "交货滞留",
                TargetKind: UnifyTargetKind.Event,
                NetworkCodeColumn: null,             // 本源无网点编码列，仅有名称
                NetworkNameColumn: "F揽收所属网点",
                EmployeeNoColumn: null,              // 仅脏名，无工号列
                EmployeeNameColumn: "F揽收小件员名称",
                ProblemTypeColumn: null,             // 无问题类型列 → 用常量
                DateColumn: "F业务日期",             // 本源日期列是「业务日期」
                WaybillColumn: "F运单号",
                PlatformColumn: "F电商平台",
                ProblemCodePrefix: "HDOV",
                OnlyFilterColumn: "F交货滞留标识",   // 仅滞留入事件
                OnlyFilterEquals: "是",              // 抽样确认：滞留=「是」（是/否标识）
                ProblemTypeConstant: "交货滞留",
                KeySnapshotColumns: new[] { "F业务日期", "F运单号", "F揽收所属网点", "F揽收小件员名称", "F交货滞留标识", "F交货时间", "F交货截止时间", "F网点装车时间", "F当前交货状态", "F电商平台" }),

            // ④ 末端派送考核明细 → 质量事件（域=派送签收时效）。网点仅有名称 F应签收所属网点名称；员工=F派件员姓名（脏名）。
            //    问题类型用列 F问题件类型名称（无值=空 → 落「(未分类)」；源无该列时回退常量「签收延迟」，本源有该列故用列）。
            //    【一期口径抉择 — 整源入，不过滤】依据：该源延迟判定分散在多个独立标识列（T0/T1/T2/T3 延迟、当天签收延迟0-24h/24-48h/超48h、一/二阶段内签收 等），
            //      单列等值过滤覆盖不全。抽样：F未签收有问题件标识=是 仅 46/345 行（恰=F问题件类型名称 非空的 46 行），
            //      若以它过滤会漏掉其余 299 行（这些行 F已签收标识=否、F当天签收标识=否，仍属签收时效问题）。
            //      该导出本身=末端派送考核（未签收/延迟全集，F已签收标识 全为「否」），无清晰单列「问题/正常」判定 → 一期不过滤、整源入，
            //      用 F问题件类型名称 当问题类型（46 行有具体类型，299 行无类型落「(未分类)」），宁可全量入、勿漏，噪声二期检测规则再收。
            [DeliveryAssessTable] = new ShentongSourceDescriptor(
                StgTableName: DeliveryAssessTable,
                QualityDomain: "派送签收时效",
                TargetKind: UnifyTargetKind.Event,
                NetworkCodeColumn: null,             // 本源无网点编码列，仅有名称
                NetworkNameColumn: "F应签收所属网点名称",
                EmployeeNoColumn: null,              // 仅脏名，无工号列
                EmployeeNameColumn: "F派件员姓名",
                ProblemTypeColumn: "F问题件类型名称", // 列优先（有该列）；空单元格落「(未分类)」
                DateColumn: "F统计日期",
                WaybillColumn: "F运单号",
                PlatformColumn: "F电商平台",
                ProblemCodePrefix: "DLVA",
                // 一期不过滤（OnlyFilter* 留 null，整源入）；常量仅在列为 null 时兜底（本源列存在，不触发）。
                ProblemTypeConstant: "签收延迟",
                KeySnapshotColumns: new[] { "F统计日期", "F运单号", "F应签收所属网点名称", "F派件员姓名", "F问题件类型名称", "F问题件原因", "F已签收标识", "F未签收有问题件标识", "F当天签收标识", "F派件时间", "F签收时间", "F电商平台" }),

            // ⑤ 签收未达标明细 → 质量事件（域=派送签收时效）。网点仅有名称 F应签网点；员工=F业务员（脏名）。
            //    问题类型常量「签收未达标」（源无问题类型列）。日期列 F应签日期（带「-」分隔，ParseUtil.TryDate 兼容）。
            //    整源入：实体无「未达标」单一标识列，导出本身=签收未达标全集（抽样 F当日签收标识/F是否已签收 全为「否」，无正常行混入）→ 不过滤。
            [SignSubstandardTable] = new ShentongSourceDescriptor(
                StgTableName: SignSubstandardTable,
                QualityDomain: "派送签收时效",
                TargetKind: UnifyTargetKind.Event,
                NetworkCodeColumn: null,             // 本源无网点编码列，仅有名称
                NetworkNameColumn: "F应签网点",
                EmployeeNoColumn: null,              // 仅脏名，无工号列
                EmployeeNameColumn: "F业务员",
                ProblemTypeColumn: null,             // 无问题类型列 → 用常量
                DateColumn: "F应签日期",
                WaybillColumn: "F运单号",
                PlatformColumn: null,                // 本源无电商平台列
                ProblemCodePrefix: "SIGN",
                // 整源入（OnlyFilter* 留 null）；抽样确认 F当日签收标识=否、F是否已签收=否 全量，无正常行 → 不过滤。
                ProblemTypeConstant: "签收未达标",
                KeySnapshotColumns: new[] { "F应签日期", "F运单号", "F应签网点", "F业务员", "F当日签收标识", "F是否已签收", "F是否未签收有问题件", "F签收时间", "F派件网点", "F签收网点" }),

            // ─────────────────────────────────────────────────────────────────────
            // C3 批次2：4 个事件源（全走通用事件路径，仅加描述符；列名/过滤口径已逐源抽样实体确认）。
            // ─────────────────────────────────────────────────────────────────────

            // ① 积压明细 → 质量事件（域=积压与遗失）。网点仅有名称 F所属网点名称（也有编码列 F所属网点编码，统一按名称匹配，编码入快照）；
            //    员工=F扫描员（最后扫描操作人；本源另有 F业务员 列但抽样全空，扫描员为可靠责任人，故取扫描员脏名，无工号列）。
            //    问题类型用列 F问题件一级类型（空单元格 → 「(未分类)」；本样例 5 行均有值：信息有误/拒收，无空）。
            //    【一期口径抉择 — 整源入，不过滤】依据：该导出每行即「末端时效积压问题件」，本身即问题，恒带 F问题件一/二级类型。
            //      候选剔除列 F是否积压剔除标识 抽样 5/5 全为「否」（无被剔除/正常行混入），F退回件标识/F是否实时签收 亦全「否」，
            //      无清晰单列把「正常/非问题」行分出来 → 整源入、用问题类型列，宁可全量勿漏，噪声二期检测规则再收。
            [BacklogTable] = new ShentongSourceDescriptor(
                StgTableName: BacklogTable,
                QualityDomain: "积压与遗失",
                TargetKind: UnifyTargetKind.Event,
                NetworkCodeColumn: null,             // 统一按名称匹配；编码列入快照
                NetworkNameColumn: "F所属网点名称",
                EmployeeNoColumn: null,              // 仅脏名，无工号列（F扫描员编码 为申通工号但口径不一致，按姓名启发式匹配）
                EmployeeNameColumn: "F扫描员",       // 业务员抽样全空 → 取扫描员（最后扫描责任人）
                ProblemTypeColumn: "F问题件一级类型", // 列优先（该源有问题类型列）；空单元格落「(未分类)」
                DateColumn: "F业务日期",
                WaybillColumn: "F运单号",
                PlatformColumn: null,                // 本源无电商平台列
                ProblemCodePrefix: "BKLD",
                // 一期不过滤（OnlyFilter* 留 null，整源入）；常量仅在列为 null 时兜底（本源列存在，不触发）。
                ProblemTypeConstant: "积压问题件",
                KeySnapshotColumns: new[] { "F业务日期", "F运单号", "F所属网点名称", "F所属网点编码", "F扫描员", "F扫描员编码", "F问题件一级类型", "F问题件二级类型", "F最后扫描时间", "F最后扫描类型", "F是否积压剔除标识", "F超过3天标识" }),

            // ② 疑似遗失明细 → 质量事件（域=积压与遗失）。整源即「疑似遗失」（3日轨迹中断触发），无过滤。
            //    网点取 F扫描站点（轨迹中断触发的扫描操作站点＝质量归属网点，抽样命中种子网点；另有 F订单网点=源单网点，口径不同，纳入快照不作匹配维度）；
            //    员工=F扫描操作人（脏名，抽样有值；F业务员 抽样空）。
            //    问题类型用列 F问题件类型（空 → 「(未分类)」；本样例 1 行值「拒收/客户拒收」），列为 null 时回退常量「疑似遗失」（本源列存在，不触发）。
            //    日期列 F3日轨迹中断触发时间 是 datetime 文本（如「2026-06-14 13:17:38.0」）；ParseUtil.TryDate 走 DateTime.TryParse 解析（保留时分秒，F统计年月 取 yyyyMM 正确）。
            [SuspectedLossTable] = new ShentongSourceDescriptor(
                StgTableName: SuspectedLossTable,
                QualityDomain: "积压与遗失",
                TargetKind: UnifyTargetKind.Event,
                NetworkCodeColumn: null,             // 本源无网点编码列，仅有站点/网点名称
                NetworkNameColumn: "F扫描站点",      // 轨迹中断触发的扫描站点＝质量归属网点（订单网点入快照，不作匹配维度）
                EmployeeNoColumn: null,              // 仅脏名，无工号列
                EmployeeNameColumn: "F扫描操作人",   // 业务员抽样空 → 取扫描操作人
                ProblemTypeColumn: "F问题件类型",    // 列优先（该源有问题类型列）；空单元格落「(未分类)」
                DateColumn: "F3日轨迹中断触发时间",  // datetime 文本，TryDate 解析（保留时分秒，年月取 yyyyMM）
                WaybillColumn: "F运单号",
                PlatformColumn: null,                // 本源无电商平台列（有 F订单来源 但非平台口径）
                ProblemCodePrefix: "SLOS",
                // 整源入（OnlyFilter* 留 null）：导出本身=网点疑似遗失明细全集，无「正常」行混入 → 不过滤。
                ProblemTypeConstant: "疑似遗失",     // 仅在列为 null 时兜底（本源列存在，不触发）
                KeySnapshotColumns: new[] { "F3日轨迹中断触发时间", "F运单号", "F扫描站点", "F订单网点", "F揽收网点", "F扫描操作人", "F问题件类型", "F3日轨迹中断触发类型", "F是否找回", "F最后扫描时间", "F实际金额" }),

            // ③ 进港投诉明细 → 质量事件（域=投诉与赔付）。整源即「进港投诉」，无过滤。
            //    网点仅有名称 F所属网点名称（也有编码列，统一按名称匹配，编码入快照）；员工=F小件员编码(工号)+F小件员名称（抽样工号为 32028xxxxx 真工号、名称脏名俱全）。
            //    问题类型用列 F投诉类型（空单元格 → 「(未分类)」；本样例 9 行有 1 行空，落「(未分类)」）。
            [InboundComplaintTable] = new ShentongSourceDescriptor(
                StgTableName: InboundComplaintTable,
                QualityDomain: "投诉与赔付",
                TargetKind: UnifyTargetKind.Event,
                NetworkCodeColumn: null,             // 统一按名称匹配；编码列入快照
                NetworkNameColumn: "F所属网点名称",
                EmployeeNoColumn: "F小件员编码",     // 本源有真工号列（抽样 3202885246 等）
                EmployeeNameColumn: "F小件员名称",
                ProblemTypeColumn: "F投诉类型",      // 列优先；空单元格落「(未分类)」（本样例有 1 行空）
                DateColumn: "F统计日期",
                WaybillColumn: "F运单号",
                PlatformColumn: null,                // 本源无电商平台列
                ProblemCodePrefix: "INBC",
                // 整源入（OnlyFilter* 留 null）：导出本身=进港投诉明细全集 → 不过滤。
                ProblemTypeConstant: "进港投诉",     // 仅在列为 null 时兜底（本源列存在，不触发）
                KeySnapshotColumns: new[] { "F统计日期", "F运单号", "F投诉类型", "F所属网点名称", "F所属网点编码", "F小件员编码", "F小件员名称", "F工单内容", "F工单创建时间", "F签收时间", "F进港出港" }),

            // ④ 应拦截明细 → 质量事件（域=拦截渗透）。网点仅有名称 F所属网点（另有 F应拦截网点，纳入快照，统一按所属网点名称匹配）；员工=F派件小件员（脏名，无工号列）。
            //    问题类型用列 F拦截类型（空单元格 → 「(未分类)」；本样例 退回/改地址/空，空落「(未分类)」）。
            //    金额列 F预计考核金额（实体真名，非「预估」）→ AmountColumn → F考核金额（ParseUtil.TryDecimal，本样例值 0/3）。
            //    【一期口径抉择 — 仅拦截失败入事件，过滤 F是否拦截成功=否】依据：该导出=应拦截运单全集（抽样 是=52/否=10），
            //      F是否拦截成功 是清晰单列二值标识（是/否），语义明确——拦截失败（否）才是被考核的问题件。
            //      强证据：抽样 F是否拦截成功=否 的 10 行 F预计考核金额 全为 3.0（成功行多为 0），即「未成功＝有考核金额＝问题」一一对应 → 过滤生效后事件即考核问题件，金额落 3。
            [InterceptDetailTable] = new ShentongSourceDescriptor(
                StgTableName: InterceptDetailTable,
                QualityDomain: "拦截渗透",
                TargetKind: UnifyTargetKind.Event,
                NetworkCodeColumn: null,             // 本源无网点编码列，仅有名称
                NetworkNameColumn: "F所属网点",
                EmployeeNoColumn: null,              // 仅脏名，无工号列
                EmployeeNameColumn: "F派件小件员",
                ProblemTypeColumn: "F拦截类型",      // 列优先；空单元格落「(未分类)」
                DateColumn: "F统计日期",
                WaybillColumn: "F运单号",
                PlatformColumn: null,                // 本源无电商平台列
                ProblemCodePrefix: "ITCP",
                OnlyFilterColumn: "F是否拦截成功",   // 仅拦截失败入事件
                OnlyFilterEquals: "否",              // 抽样确认：未成功=「否」（是/否标识），未成功行金额全为 3 ＝考核问题件
                ProblemTypeConstant: "拦截不成功",   // 仅在列为 null 时兜底（本源列存在，不触发）
                AmountColumn: "F预计考核金额",       // → F考核金额（TryDecimal；本样例失败行均 3）
                KeySnapshotColumns: new[] { "F统计日期", "F运单号", "F拦截类型", "F拦截来源", "F所属网点", "F应拦截网点", "F派件小件员", "F是否拦截成功", "F是否转出", "F是否及时转出", "F预计考核金额", "F到件时间" }),

            // ─────────────────────────────────────────────────────────────────────
            // C3 批次3：6 个事件源（最后一批事件，全走通用事件路径，仅加描述符；列名/过滤/金额已逐源抽样实体确认）。
            // ─────────────────────────────────────────────────────────────────────

            // ① 投诉账单明细 → 质量事件（域=投诉与赔付）。【无员工维度 → 两员工列设 null → 通用路径自动置 F员工匹配状态=9（不适用）】。
            //    网点用受款方（F受款方网点编号 真编码 + F受款方网点名称），即「账单受款/被投诉方」——抽样均为外部网点（转运中心/外省公司，无 320288），
            //    本网点（太仓 org=192）是收到账单方，故事件网点按受款方解析，<b>不会命中本地种子网点（状态0）</b>，集成测试不断言网点匹配。
            //    （另有 F投诉网点 列与受款方多数同义，纳入快照；系统列 F归属网点编号 抽样全 NULL，导入未填，不作匹配维度。）
            //    问题类型用列 F账单二级类型（抽样：协商赔付/应赔款金额，无空）。金额列 F金额（抽样 3/50/68/7.89… 纯数值，TryDecimal）→ F考核金额。
            //    日期列 F账单生成时间（datetime 文本，TryDate）。整源入：导出本身=收到的投诉账单全集，无过滤。
            [ComplaintBillTable] = new ShentongSourceDescriptor(
                StgTableName: ComplaintBillTable,
                QualityDomain: "投诉与赔付",
                TargetKind: UnifyTargetKind.Event,
                NetworkCodeColumn: "F受款方网点编号",   // 受款方真编码（按编码解析；抽样均外部网点，不命中本地种子）
                NetworkNameColumn: "F受款方网点名称",
                EmployeeNoColumn: null,              // 无员工维度 → 通用路径置状态9（工号/ID/姓名全空、不计未匹配）
                EmployeeNameColumn: null,            // 无员工维度
                ProblemTypeColumn: "F账单二级类型",   // 列优先（抽样：协商赔付/应赔款金额）；空单元格落「(未分类)」
                DateColumn: "F账单生成时间",          // datetime 文本，TryDate
                WaybillColumn: "F运单号",
                PlatformColumn: null,                // 本源无电商平台列
                ProblemCodePrefix: "CPBL",
                // 整源入（OnlyFilter* 留 null）：导出=收到的投诉账单全集 → 不过滤。
                ProblemTypeConstant: "投诉账单",     // 仅在列为 null 时兜底（本源列存在，不触发）
                AmountColumn: "F金额",               // → F考核金额（TryDecimal；抽样 3/50/68/7.89…）
                KeySnapshotColumns: new[] { "F账单生成时间", "F运单号", "F账单一级类型", "F账单二级类型", "F金额", "F理赔来源", "F投诉网点", "F被投诉方1", "F受款方网点编号", "F受款方网点名称", "F受款方应受款金额", "F处理结果" }),

            // ② 虚签投诉明细 → 质量事件（域=虚假签收履约）。网点用被投诉方（F被投诉网点编号 真编码=320288 + 名称），即责任网点（本地种子命中，状态1）；
            //    员工=F派件业务员id(工号) + F派件业务员名称。问题类型用列 F投诉类型（抽样：签收未收到/虚假签收/要求送货上门/追究遗失，无空）。
            //    金额列 F预估考核金额（抽样全 50，TryDecimal）→ F考核金额。日期列 F投诉日期（TryDate）。整源入：导出=虚签投诉全集，无过滤。
            [FakeSignComplaintTable] = new ShentongSourceDescriptor(
                StgTableName: FakeSignComplaintTable,
                QualityDomain: "虚假签收履约",
                TargetKind: UnifyTargetKind.Event,
                NetworkCodeColumn: "F被投诉网点编号",   // 被投诉=责任网点真编码（抽样全 320288，命中本地种子）
                NetworkNameColumn: "F被投诉网点名称",
                EmployeeNoColumn: "F派件业务员id",     // 派件业务员工号
                EmployeeNameColumn: "F派件业务员名称",
                ProblemTypeColumn: "F投诉类型",        // 列优先（抽样：签收未收到/虚假签收/要求送货上门/追究遗失）
                DateColumn: "F投诉日期",
                WaybillColumn: "F运单号",
                PlatformColumn: null,                // 本源无电商平台列
                ProblemCodePrefix: "FSCP",
                // 整源入（OnlyFilter* 留 null）：导出=虚签投诉明细全集 → 不过滤。
                ProblemTypeConstant: "虚签投诉",     // 仅在列为 null 时兜底（本源列存在，不触发）
                AmountColumn: "F预估考核金额",        // → F考核金额（TryDecimal；抽样全 50）
                KeySnapshotColumns: new[] { "F投诉日期", "F运单号", "F投诉类型", "F投诉理由", "F工单号", "F被投诉网点编号", "F被投诉网点名称", "F派件业务员id", "F派件业务员名称", "F预估考核金额", "F签收类型", "F签收时间" }),

            // ③ 虚假签收明细 → 质量事件（域=虚假签收履约）。结构同虚签投诉（同一批投诉的另一导出视角）：网点=被投诉方真编码（320288，命中种子）；
            //    员工=F派件业务员id + F派件业务员名称。问题类型用列 F投诉类型（无则常量「虚假签收」；本源列存在，用列）。日期 F投诉日期。<b>无金额列</b>。整源入。
            [FakeSignTable] = new ShentongSourceDescriptor(
                StgTableName: FakeSignTable,
                QualityDomain: "虚假签收履约",
                TargetKind: UnifyTargetKind.Event,
                NetworkCodeColumn: "F被投诉网点编号",   // 被投诉=责任网点真编码（抽样全 320288，命中本地种子）
                NetworkNameColumn: "F被投诉网点名称",
                EmployeeNoColumn: "F派件业务员id",
                EmployeeNameColumn: "F派件业务员名称",
                ProblemTypeColumn: "F投诉类型",        // 列优先（抽样：签收未收到/虚假签收/要求送货上门/追究遗失）
                DateColumn: "F投诉日期",
                WaybillColumn: "F运单号",
                PlatformColumn: null,                // 本源无电商平台列
                ProblemCodePrefix: "FSGN",
                // 整源入（OnlyFilter* 留 null）：导出=虚假签收明细全集 → 不过滤；无金额列。
                ProblemTypeConstant: "虚假签收",     // 仅在列为 null 时兜底（本源列存在，不触发）
                KeySnapshotColumns: new[] { "F投诉日期", "F运单号", "F投诉类型", "F投诉理由", "F工单号", "F被投诉网点编号", "F被投诉网点名称", "F派件业务员id", "F派件业务员名称", "F签收类型", "F签收时间" }),

            // ④ 照片质检明细 → 质量事件（域=虚假签收履约）。网点=F网点名称（+ F网点编码 入快照，统一按名称匹配）；员工=F小件员编码(工号) + F小件员名称。
            //    问题类型用列 F不合格类型（抽样不合格子集：无明显环境信息/没有包裹/有人脸…；过滤后非空）。日期用 <b>F投诉时间</b>（附录写的 F分区是错的——F分区是片区码非日期；本源无纯日期列，用投诉时间 datetime 文本，TryDate）。运单列是 F单号（非 F运单号）。
            //    【仅不合格过滤】F是否质检合格 = 否（抽样三值：- 929 / 是 891 / 否 31，「否」=不合格=被考核 → 31 行，且 31 行网点全为 江苏太仓市城区公司 命中种子）。
            [PhotoQcTable] = new ShentongSourceDescriptor(
                StgTableName: PhotoQcTable,
                QualityDomain: "虚假签收履约",
                TargetKind: UnifyTargetKind.Event,
                NetworkCodeColumn: null,             // 统一按名称匹配；编码列入快照
                NetworkNameColumn: "F网点名称",
                EmployeeNoColumn: "F小件员编码",      // 真工号
                EmployeeNameColumn: "F小件员名称",
                ProblemTypeColumn: "F不合格类型",     // 列优先（不合格子集有值）；空单元格落「(未分类)」
                DateColumn: "F投诉时间",              // 用投诉时间（附录 F分区是错的；F分区=片区码非日期）；datetime 文本 TryDate
                WaybillColumn: "F单号",               // 本源运单列是「单号」（非「运单号」）
                PlatformColumn: null,                // 本源无电商平台列
                ProblemCodePrefix: "PHQC",
                OnlyFilterColumn: "F是否质检合格",     // 仅不合格入事件
                OnlyFilterEquals: "否",               // 抽样确认：质检不合格=「否」（三值 -/是/否，否=31 行=被考核，且全为太仓网点）
                ProblemTypeConstant: "照片质检不合格", // 仅在列为 null 时兜底（本源列存在，不触发）
                KeySnapshotColumns: new[] { "F单号", "F业务类型", "F是否质检合格", "F不合格类型", "F签收人", "F小件员编码", "F小件员名称", "F网点编码", "F网点名称", "F投诉类型", "F投诉内容", "F投诉时间" }),

            // ⑤ 履约率明细 → 质量事件（域=虚假签收履约）。【无网点列：源无订单网点列，系统列 F归属网点编号 抽样全 NULL（导入未填）→ 网点不匹配（状态0），集成测试不断言网点匹配】。
            //    员工=F小件员工号(真工号，抽样 32028xxxx) + F小件员名称。问题类型用列 F履约状态（更细；抽样全「履约失败」）。日期列 F日期（TryDate）。
            //    【整源入，不过滤】抽样 F履约状态 distinct 仅「履约失败」一值（8/8）——导出本身=客户声音履约失败全集，无「成功/正常」行混入，无清晰单列分正常/失败 → 整源入（用 F履约状态 当问题类型，不另设过滤，避免 expectedEvents==stgRows 的「过滤未生效」假象）。
            [FulfillRateTable] = new ShentongSourceDescriptor(
                StgTableName: FulfillRateTable,
                QualityDomain: "虚假签收履约",
                TargetKind: UnifyTargetKind.Event,
                NetworkCodeColumn: "F归属网点编号",    // 源唯一网点列（系统列，抽样全 NULL→不匹配，状态0）；保留以备导入侧补填后自动命中
                NetworkNameColumn: null,             // 源无网点名称列
                EmployeeNoColumn: "F小件员工号",       // 真工号
                EmployeeNameColumn: "F小件员名称",
                ProblemTypeColumn: "F履约状态",        // 列优先（抽样全「履约失败」，更细于常量）；空单元格落「(未分类)」
                DateColumn: "F日期",
                WaybillColumn: "F运单号",
                PlatformColumn: null,                // 本源无电商平台列
                ProblemCodePrefix: "FFRT",
                // 整源入（OnlyFilter* 留 null）：抽样 F履约状态 仅「履约失败」一值，导出=履约失败全集，无正常行 → 不过滤（设过滤会全选中＝过滤无效）。
                ProblemTypeConstant: "履约失败",     // 仅在列为 null 时兜底（本源列存在，不触发）
                KeySnapshotColumns: new[] { "F日期", "F运单号", "F履约要求", "F履约状态", "F是否虚假上门", "F小件员工号", "F小件员名称", "F签收时间", "F首次签收类型", "F签收人", "F服务要求" }),

            // ⑥ 送货上门明细 → 质量事件（域=虚假签收履约）。网点=F承包区名称（抽样全 江苏太仓市城区公司，命中种子）+ F承包区编号 入快照；员工=F业务员工号(真工号) + F派送小件员名称。
            //    问题类型用列 F工单判罚类型（抽样本样例全 NULL → 落「(未分类)」；判罚/违规列只在被判罚行有值，本样例未判罚故空，如实落未分类，宁可入勿漏）；列为 null 时兜底常量「送货上门违规」（本源列存在，不触发）。日期列 F统计日期。
            //    【仅履约失败过滤】F履约情况 = 履约失败（抽样二值：履约失败 7 / 已履约 6，清晰单列二值判定，履约失败=被考核问题件 → 7 行）。
            [HomeDeliveryTable] = new ShentongSourceDescriptor(
                StgTableName: HomeDeliveryTable,
                QualityDomain: "虚假签收履约",
                TargetKind: UnifyTargetKind.Event,
                NetworkCodeColumn: null,             // 统一按名称匹配（承包区名称＝网点名）；承包区编号入快照
                NetworkNameColumn: "F承包区名称",
                EmployeeNoColumn: "F业务员工号",       // 真工号
                EmployeeNameColumn: "F派送小件员名称",
                ProblemTypeColumn: "F工单判罚类型",    // 列优先（被判罚行有值；本样例未判罚全空 → 落「(未分类)」）
                DateColumn: "F统计日期",
                WaybillColumn: "F运单号",
                PlatformColumn: null,                // 本源无电商平台列
                ProblemCodePrefix: "HMDV",
                OnlyFilterColumn: "F履约情况",         // 仅履约失败入事件
                OnlyFilterEquals: "履约失败",          // 抽样确认：二值 履约失败 7 / 已履约 6，履约失败=被考核问题件
                ProblemTypeConstant: "送货上门违规",   // 仅在列为 null 时兜底（本源列存在，不触发）
                KeySnapshotColumns: new[] { "F统计日期", "F运单号", "F运单状态", "F承包区编号", "F承包区名称", "F业务员工号", "F派送小件员名称", "F回执情况", "F履约情况", "F违规行为二级内容", "F工单判罚类型", "F签收日期" }),

            // ─────────────────────────────────────────────────────────────────────
            // C3 批次4：6 个网点指标源（NetworkMetric，typed 方法逐源填互不相交字段子集，按 网点×日 合并 upsert）。
            // 走 DispatchNetworkMetricAsync 按表名 switch 到各自 UnifyXxxAsync；typed 方法直接投影 STG 实体，
            // 故下方描述符的列名字段仅作文档（实际列名以各 typed 方法内的实体投影为准）。
            // 6 源字段子集互不相交，核实无抢占同一 QL 字段（见各 typed 方法注释）。
            // ─────────────────────────────────────────────────────────────────────

            // ① 物流信息及时汇总 → 子集：F揽收/派件/签收上传不及时率。无网点编码列，仅 F网点名称（按名称匹配，未匹配回退名称原文）。
            [InfoIndexTimelyTable] = new ShentongSourceDescriptor(
                StgTableName: InfoIndexTimelyTable,
                QualityDomain: "物流信息",
                TargetKind: UnifyTargetKind.NetworkMetric,
                NetworkCodeColumn: null,             // 无网点编码列，仅名称
                NetworkNameColumn: "F网点名称",
                EmployeeNoColumn: null, EmployeeNameColumn: null,
                ProblemTypeColumn: null,
                DateColumn: "F统计日期",
                WaybillColumn: null, PlatformColumn: null,
                ProblemCodePrefix: "INFT"),

            // ② 物流信息完整汇总 → 子集：F揽收/派件/到件缺失率。无网点编码列，仅 F网点名称。
            [InfoIndexCompleteTable] = new ShentongSourceDescriptor(
                StgTableName: InfoIndexCompleteTable,
                QualityDomain: "物流信息",
                TargetKind: UnifyTargetKind.NetworkMetric,
                NetworkCodeColumn: null,
                NetworkNameColumn: "F网点名称",
                EmployeeNoColumn: null, EmployeeNameColumn: null,
                ProblemTypeColumn: null,
                DateColumn: "F统计日期",
                WaybillColumn: null, PlatformColumn: null,
                ProblemCodePrefix: "INFC"),

            // ③ 物流信息准确汇总 → 子集：F不准确率/F到件不准确率。无网点编码列，仅 F网点名称。
            [InfoIndexAccurateTable] = new ShentongSourceDescriptor(
                StgTableName: InfoIndexAccurateTable,
                QualityDomain: "物流信息",
                TargetKind: UnifyTargetKind.NetworkMetric,
                NetworkCodeColumn: null,
                NetworkNameColumn: "F网点名称",
                EmployeeNoColumn: null, EmployeeNameColumn: null,
                ProblemTypeColumn: null,
                DateColumn: "F统计日期",
                WaybillColumn: null, PlatformColumn: null,
                ProblemCodePrefix: "INFA"),

            // ④ 揽收考核汇总 → 子集：F及时揽收率/F未及时揽收量。有网点编码 F揽收所属网点编码（按编码匹配，名称 F揽收所属网点 兜底）。
            [PickupAssessTable] = new ShentongSourceDescriptor(
                StgTableName: PickupAssessTable,
                QualityDomain: "揽收时效",
                TargetKind: UnifyTargetKind.NetworkMetric,
                NetworkCodeColumn: "F揽收所属网点编码",
                NetworkNameColumn: "F揽收所属网点",
                EmployeeNoColumn: null, EmployeeNameColumn: null,
                ProblemTypeColumn: null,
                DateColumn: "F统计日期",
                WaybillColumn: null, PlatformColumn: null,
                ProblemCodePrefix: "PKAS"),

            // ⑤ 出仓考核汇总 → 子集：F一频次出仓及时率/F未及时出仓量/F出仓预估考核金额。有网点编码 F所属网点编码。
            [OutboundAssessTable] = new ShentongSourceDescriptor(
                StgTableName: OutboundAssessTable,
                QualityDomain: "出仓时效",
                TargetKind: UnifyTargetKind.NetworkMetric,
                NetworkCodeColumn: "F所属网点编码",
                NetworkNameColumn: "F所属网点",
                EmployeeNoColumn: null, EmployeeNameColumn: null,
                ProblemTypeColumn: null,
                DateColumn: "F统计日期",
                WaybillColumn: null, PlatformColumn: null,
                ProblemCodePrefix: "OBAS"),

            // ⑥ 交货滞留汇总 → 子集：F滞留率/F考核滞留量/F滞留预估考核金额（STG真名 F滞留预估考核日，导出列「滞留预估考核-日」）。
            //    有网点编码 F揽收所属网点编码（F揽收网点编码 抽样空，用「所属」编码），名称 F揽收网点所属网点。
            [HandoverSummaryTable] = new ShentongSourceDescriptor(
                StgTableName: HandoverSummaryTable,
                QualityDomain: "交货滞留",
                TargetKind: UnifyTargetKind.NetworkMetric,
                NetworkCodeColumn: "F揽收所属网点编码",
                NetworkNameColumn: "F揽收网点所属网点",
                EmployeeNoColumn: null, EmployeeNameColumn: null,
                ProblemTypeColumn: null,
                DateColumn: "F统计日期",
                WaybillColumn: null, PlatformColumn: null,
                ProblemCodePrefix: "HDSM"),

            // ─────────────────────────────────────────────────────────────────────
            // C3 批次5：4 个网点指标源（NetworkMetric，最后一批源；typed 方法逐源填互不相交字段子集，按 网点×日 合并 upsert）。
            // 走 DispatchNetworkMetricAsync 按表名 switch 到各自 UnifyXxxAsync；typed 方法直接投影 STG 实体，
            // 故下方描述符的列名字段仅作文档（实际列名以各 typed 方法内的实体投影为准）。
            // 4 源字段子集互不相交（核实无抢占同一 QL 字段，见各 typed 方法注释）；
            // 关键：签收域两源（末端派送汇总 / 签收率考核汇总）字段子集严格不相交——
            //   末端派送汇总 填 一/二阶段/当天及时签收率 + 派送预估考核金额/有偿派费/预计返款；
            //   签收率考核汇总 只填 F签收率考核金额。落到同一 (网点×日) 行时互不清空。
            // ─────────────────────────────────────────────────────────────────────

            // ① 末端派送网点汇总 → 签收域子集：一/二阶段/当天及时签收率 + 派送预估考核金额(←F预计考核金额)/有偿派费金额/预计返款金额。
            //    无网点编码列，仅 F应签所属网点（按名称匹配，未匹配回退名称原文）。日期列 F统计日期（yyyyMMdd，TryDate 兼容）。
            //    率源以小数分数落（如 0.8903＝89.03%），TryDecimal 原样解析（无尾缀×1）。
            [DeliveryNetSummaryTable] = new ShentongSourceDescriptor(
                StgTableName: DeliveryNetSummaryTable,
                QualityDomain: "派送签收时效",
                TargetKind: UnifyTargetKind.NetworkMetric,
                NetworkCodeColumn: null,             // 无网点编码列，仅名称
                NetworkNameColumn: "F应签所属网点",
                EmployeeNoColumn: null, EmployeeNameColumn: null,
                ProblemTypeColumn: null,
                DateColumn: "F统计日期",
                WaybillColumn: null, PlatformColumn: null,
                ProblemCodePrefix: "DLNS"),

            // ② 签收率考核汇总 → 签收域子集：<b>仅 F签收率考核金额</b>（← F总金额=总考核金额，与末端派送汇总互不相交）。
            //    有网点编码 F网点编号（名称 F网点名称 兜底）。日期列 F日期（yyyyMMdd，TryDate 兼容）。
            //    草案的 F48h签收率 该源无对应列（退化表头分时段明细未逐列建模）→ 留 null。
            [SignRateAssessTable] = new ShentongSourceDescriptor(
                StgTableName: SignRateAssessTable,
                QualityDomain: "派送签收时效",
                TargetKind: UnifyTargetKind.NetworkMetric,
                NetworkCodeColumn: "F网点编号",
                NetworkNameColumn: "F网点名称",
                EmployeeNoColumn: null, EmployeeNameColumn: null,
                ProblemTypeColumn: null,
                DateColumn: "F日期",
                WaybillColumn: null, PlatformColumn: null,
                ProblemCodePrefix: "SRAS"),

            // ③ 拦截汇总 → 拦截子集：应拦截量(I)/拦截成功率(D)/及时转出率(D)。
            //    无网点编码列，仅 F所属网点（按名称匹配，未匹配回退名称原文）。日期列 F统计日期（2026-06-15 带分隔，TryDate 兼容）。
            //    率源带 %（如 83.87%），TryDecimal 去符号不除100。
            [InterceptSummaryTable] = new ShentongSourceDescriptor(
                StgTableName: InterceptSummaryTable,
                QualityDomain: "拦截渗透",
                TargetKind: UnifyTargetKind.NetworkMetric,
                NetworkCodeColumn: null,             // 无网点编码列，仅名称
                NetworkNameColumn: "F所属网点",
                EmployeeNoColumn: null, EmployeeNameColumn: null,
                ProblemTypeColumn: null,
                DateColumn: "F统计日期",
                WaybillColumn: null, PlatformColumn: null,
                ProblemCodePrefix: "ITSM"),

            // ④ 渗透建站考核 → 渗透子集：自建渗透率(D←F已认证自建渗透率)/渗透率目标(D←F自建渗透率当月目标)/建站待完成(I)/喵柜激活格口数(I)。
            //    有网点编码 F网点编号（名称 F网点名称 兜底）。
            //    日期列 F统计周期 为「YYYY-第MM月」（如「2026-第06月」），<b>非标准日期，TryDate 解析不出</b>→
            //    typed 方法内用专用周期解析取<b>月首日</b>（2026-第06月 → 2026-06-01），口径＝该月网点级月度汇总落月首日。
            //    率源带 %（如 11.61%/15.00%），TryDecimal 去符号不除100。
            [PenetrationTable] = new ShentongSourceDescriptor(
                StgTableName: PenetrationTable,
                QualityDomain: "拦截渗透",
                TargetKind: UnifyTargetKind.NetworkMetric,
                NetworkCodeColumn: "F网点编号",
                NetworkNameColumn: "F网点名称",
                EmployeeNoColumn: null, EmployeeNameColumn: null,
                ProblemTypeColumn: null,
                DateColumn: "F统计周期",              // 「YYYY-第MM月」，typed 方法取月首日
                WaybillColumn: null, PlatformColumn: null,
                ProblemCodePrefix: "PNTR"),
        };
}
