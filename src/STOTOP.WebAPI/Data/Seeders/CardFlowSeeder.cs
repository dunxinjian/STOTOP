using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;

namespace STOTOP.WebAPI.Data.Seeders;

public static class CardFlowSeeder
{
    private const string Module = "CardFlow";

    /// <summary>
    /// 统一走 SeederHelper.ExecuteRawSql（纯 ADO，不经 EF 的 String.Format），
    /// SQL 字面量中的大括号（JSON、模板占位符）无需转义。
    /// </summary>
    private static void ExecSql(STOTOPDbContext ctx, string sql)
    {
        SeederHelper.ExecuteRawSql(ctx, sql);
    }

    public static void Migrate(STOTOPDbContext ctx)
    {
        var steps = new List<MigrationStep>
        {
            new(1, "数据库基线 (2026-05-26)", MigrateV1),
            new(2, "列重命名：FAgent→FAutoPlugin / CfPluginDef / CfStageDefinition / CfQualityIssueType (2026-05-26)", MigrateV2),
            new(3, "FanOut插件描述更新 (2026-05-26)", MigrateV3),
            new(4, "CfPluginRule + CfStageDefinition + FMatchPattern 补充 (2026-05-26)", MigrateV4),
            new(5, "清理残留废弃列 (2026-05-26)", MigrateV5),
            new(6, "BatchSummary插件 + AutoVoucher规则 + 申通总部交易明细缺失节点 (2026-05-26)", MigrateV6),
            new(7, "修正版本2258节点：批次汇总重命名 + 补充确认通知 (2026-05-26)", MigrateV7),
            new(8, "为版本2258预置卡片SchemaJSON (2026-05-26)", MigrateV8),
            new(9, "创建STG_出港运费表 + 补充版本2254后续节点(Pricing+BatchSummary) (2026-05-26)", MigrateV9),
            new(10, "为节点5008补充PricingAgent配置JSON + 修正brandCode (2026-05-26)", MigrateV10),
            new(11, "修正节点5008 brandCode: STO→ST (2026-05-27)", MigrateV11),
            new(12, "修正版本2254成本计算节点F类型/F处理粒度 (2026-06-03)", MigrateV12),
            new(13, "补齐版本2254缺失的成本计算节点并顺延批次汇总 (2026-06-05)", MigrateV13),
            new(14, "修正版本2254价格/成本节点排序 (2026-06-06)", MigrateV14),
            new(15, "完善费用报销流程可用配置 (2026-06-07)", MigrateV15),
            new(16, "费用资金类流程升级为 CardFlow 2.0 参考模板 (2026-06-07)", MigrateV16),
            new(17, "费用资金类参考模板补齐条件路由、动态审批和组件展示 (2026-06-09)", MigrateV17),
            new(18, "新增导入计算验证工作台权限 (2026-06-10)", MigrateV18),
            new(19, "补齐历史流程节点稳定键 (2026-06-12)", MigrateV19),
            new(20, "流程节点重复键兜底清理（循环收敛） (2026-06-12)", MigrateV20),
            new(21, "CF卡片流程新增 F是否模板 列 (2026-06-16)", MigrateV21),
            new(22, "费用报销 FOrgId=0 全局模板种子 (2026-06-16)", MigrateV22),
            new(23, "创建 STG申通派件日明细 表 + 派件导入 rule/流程三件套 (2026-06-17)", MigrateV23),
            new(24, "网点质控：接入 STG申通_物流及时准确明细（建表 + 规则3102 + 流程2302 + 首节点5102）(2026-06-17)", MigrateV24),
            new(25, "网点质控：接入 STG申通_揽收分析明细（建表 + 规则3103 + 流程2303 + 首节点5103）(2026-06-17)", MigrateV25),
            new(26, "网点质控：接入 STG申通_未出仓监控明细（建表 + 规则3104 + 流程2304 + 首节点5104）(2026-06-17)", MigrateV26),
            new(27, "网点质控：接入 STG申通_交货滞留明细（建表 + 规则3105 + 流程2305 + 首节点5105）(2026-06-17)", MigrateV27),
            new(28, "网点质控：接入 STG申通_末端派送考核明细（建表 + 规则3106 + 流程2306 + 首节点5106）(2026-06-18)", MigrateV28),
            new(29, "网点质控：接入 STG申通_签收未达标明细（建表 + 规则3107 + 流程2307 + 首节点5107）(2026-06-18)", MigrateV29),
            new(30, "网点质控：接入 STG申通_积压明细（建表 + 规则3108 + 流程2308 + 首节点5108）(2026-06-18)", MigrateV30),
            new(31, "网点质控：接入 STG申通_疑似遗失明细（建表 + 规则3109 + 流程2309 + 首节点5109）(2026-06-18)", MigrateV31),
            new(32, "网点质控：接入 STG申通_进港投诉明细（建表 + 规则3110 + 流程2310 + 首节点5110）(2026-06-18)", MigrateV32),
            new(33, "网点质控：接入 STG申通_投诉账单明细（双行表头 + 建表 + 规则3111 + 流程2311 + 首节点5111）(2026-06-18)", MigrateV33),
            new(34, "网点质控：接入 STG申通_虚签投诉明细（建表 + 规则3112 + 流程2312 + 首节点5112）(2026-06-18)", MigrateV34),
            new(35, "网点质控：接入 STG申通_虚假签收明细（建表 + 规则3113 + 流程2313 + 首节点5113）(2026-06-18)", MigrateV35),
            new(36, "网点质控：接入 STG申通_照片质检明细（建表 + 规则3114 + 流程2314 + 首节点5114）(2026-06-18)", MigrateV36),
            new(37, "网点质控：接入 STG申通_履约率明细（建表 + 规则3115 + 流程2315 + 首节点5115）(2026-06-18)", MigrateV37),
            new(38, "网点质控：接入 STG申通_送货上门明细（建表 + 规则3116 + 流程2316 + 首节点5116）(2026-06-18)", MigrateV38),
            new(39, "网点质控：接入 STG申通_应拦截明细（建表 + 规则3117 + 流程2317 + 首节点5117）(2026-06-18)", MigrateV39),
            new(40, "网点质控：接入 STG申通_渗透建站考核（建表 + 规则3118 + 流程2318 + 首节点5118）(2026-06-18)", MigrateV40),
            new(41, "网点质控：接入 STG申通_物流信息(及时/完整/准确)三汇总表（多sheet，建 3 表 + 规则3119-3121 + 流程2319-2321 + 首节点5119-5121）(2026-06-18)", MigrateV41),
            new(42, "网点质控：接入 STG申通_签收率考核汇总（退化表头，建表 + 规则3122 + 流程2322 + 首节点5122）(2026-06-18)", MigrateV42),
            new(43, "网点质控：接入 STG申通_出仓考核汇总（含全角括号列，建表 + 规则3123 + 流程2323 + 首节点5123）(2026-06-18)", MigrateV43),
            new(44, "网点质控：接入 STG申通_小件员履约指标（双行表头/员工级，建表 + 规则3124 + 流程2324 + 首节点5124）(2026-06-18)", MigrateV44),
            new(45, "网点质控：接入 STG申通_末端派送网点汇总（单行表头55列，建表 + 规则3125 + 流程2325 + 首节点5125）(2026-06-18)", MigrateV45),
            new(46, "网点质控：接入 STG申通_积压监控汇总（单行表头64列含括号/连字符列，建表 + 规则3126 + 流程2326 + 首节点5126）(2026-06-18)", MigrateV46),
            new(47, "网点质控：接入 STG申通_交货滞留汇总（单行表头30列含括号/连字符/'&'列，建表 + 规则3127 + 流程2327 + 首节点5127）(2026-06-18)", MigrateV47),
            new(48, "网点质控：接入 STG申通_拦截汇总（单行表头10列，建表 + 规则3128 + 流程2328 + 首节点5128）(2026-06-18)", MigrateV48),
            new(49, "网点质控：接入 STG申通_揽收考核汇总（单行表头19列含末尾空格列，建表 + 规则3129 + 流程2329 + 首节点5129）(2026-06-18)", MigrateV49),
            new(50, "网点质控：接入 STG申通_物流完整性明细（建表 + 规则3101 + 流程2301 + 首节点5101）(2026-06-17，原 V23，因 master 的派件量 V23 占用而改 50)", MigrateV50),
            new(51, "派件量补丁：存量库补建 STG申通派件日明细+规则3006+流程2330（V23在存量库被网点质控占用未执行，复用幂等 MigrateV23）(2026-06-19)", MigrateV23),
            new(52, "凭证规则21按品牌版科目重配（建/追平 FID=21 + 备份V1）(2026-06-19)", MigrateV52),
            new(53, "createDraft占位科目注入：规则21 config 加 draftPlaceholderAccountId=700044(1901待处理财产损溢) (2026-06-19)", MigrateV53),
        };
        MigrationRunner.RunMigrations(ctx, Module, steps);
    }

    private static void MigrateV1(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // CF卡片流程
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF卡片流程] ON;
        
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 1352)
        
        INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则]) VALUES (1352, NULL, 0, N'2026-05-22 18:15:32.826', NULL, NULL, N'2026-05-22 18:15:32.826', N'出港发件费结算-{月份}', N'出港发件费结算', NULL, N'CGFJF', N'published', 192, N'CGFJF-{yyyy}{MM}-{seq}', NULL, NULL, NULL);
        
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 1353)
        
        INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则]) VALUES (1353, NULL, 0, N'2026-05-22 18:15:32.830', NULL, NULL, N'2026-05-22 18:15:32.830', N'代收货款结算-{月份}', N'代收货款结算', NULL, N'DSHK', N'published', 192, N'DSHK-{yyyy}{MM}-{seq}', NULL, NULL, NULL);
        
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 1354)
        
        INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则]) VALUES (1354, NULL, 0, N'2026-05-22 18:15:32.833', NULL, NULL, N'2026-05-22 18:15:32.833', N'{发起人}-费用报销-{金额}元', N'费用报销', NULL, N'FYBS', N'published', 192, N'FYBS-{yyyy}{MM}{dd}-{seq}', NULL, NULL, NULL);
        
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 1355)
        
        INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则]) VALUES (1355, NULL, 0, N'2026-05-22 18:15:32.840', NULL, NULL, N'2026-05-22 18:15:32.840', N'{发起人}-费用付款-{供应商}', N'费用付款', NULL, N'FYFK', N'published', 192, N'FYFK-{yyyy}{MM}{dd}-{seq}', NULL, NULL, NULL);
        
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 1356)
        
        INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则]) VALUES (1356, NULL, 0, N'2026-05-22 18:15:32.843', NULL, NULL, N'2026-05-22 18:15:32.843', N'进港到达费结算-{月份}', N'进港到达费结算', NULL, N'JGDDF', N'published', 192, N'JGDDF-{yyyy}{MM}-{seq}', NULL, NULL, NULL);
        
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 1357)
        
        INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则]) VALUES (1357, NULL, 0, N'2026-05-22 18:15:32.846', NULL, NULL, N'2026-05-22 18:15:32.846', N'{发起人}-请款申请-{金额}元', N'请款申请', NULL, N'QKSQ', N'published', 192, N'QKSQ-{yyyy}{MM}{dd}-{seq}', NULL, NULL, NULL);
        
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 1358)
        
        INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则]) VALUES (1358, NULL, 0, N'2026-05-22 18:15:32.850', NULL, NULL, N'2026-05-22 18:15:32.850', N'月度承包费结算-{月份}', N'月度承包费结算', NULL, N'YDCBF', N'published', 192, N'YDCBF-{yyyy}{MM}-{seq}', NULL, NULL, NULL);
        
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2255)
        
        INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则]) VALUES (2255, NULL, 0, N'2026-05-22 18:54:02.293', NULL, NULL, N'2026-05-24 09:51:17.203', N'{发起人}-费用报销付款-{金额}元', N'太仓费用报销付款数据导入流程', NULL, N'TCFYBSFK', N'published', 192, N'TCFYBSFK-{yyyy}{MM}{dd}-{seq}', N'{""triggers"":[{""type"":""fileUpload"",""enabled"":true,""config"":{""fileTypes"":["".xlsx"","".xls""]}}]}', NULL, NULL);
        
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2256)
        
        INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则]) VALUES (2256, NULL, 1, N'2026-05-25 14:48:57.125', NULL, N'迁移自 DC管道(FID=9)：Excel导入 → 质量分析 → 出港运单价格计算 → 出港运单成本计算', NULL, NULL, N'申通出港运单导入', NULL, N'PL_ST_OUTBOUND_WAYBILL', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, NULL);
        
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2257)
        
        INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则]) VALUES (2257, NULL, 1, N'2026-05-25 14:48:57.125', NULL, N'迁移自 DC管道(FID=10)：Excel导入 → 质量分析 → 自动凭证', NULL, NULL, N'申通总部交易明细导入', NULL, N'PL_ST_HQ_TRANSACTION', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, NULL);
        
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2258)
        
        INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则]) VALUES (2258, NULL, 1, N'2026-05-25 14:48:57.125', NULL, N'迁移自 DC管道(FID=11)：Excel导入 → 质量分析 → 自动凭证生成-费用支出凭证规则', NULL, NULL, N'费用支出导入', NULL, N'PL_EXPENSE_REIMBURSE', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, NULL);
        
        SET IDENTITY_INSERT [CF卡片流程] OFF;
        
        ");

        // CF流程版本
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF流程版本] ON;
        
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 1345)
        
        INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态]) VALUES (1345, 0, N'2026-05-22 18:15:32.826', N'[{""key"":""period"",""label"":""结算月份"",""type"":""text"",""required"":true},{""key"":""amount"",""label"":""结算金额"",""type"":""amount"",""required"":true},{""key"":""itemCount"",""label"":""明细笔数"",""type"":""number"",""required"":true},{""key"":""description"",""label"":""结算说明"",""type"":""textarea"",""required"":false},{""key"":""attachments"",""label"":""附件"",""type"":""attachment"",""required"":false}]', N'2026-05-22 18:15:32.826', NULL, 1, 1352, NULL, 1, N'published');
        
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 1346)
        
        INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态]) VALUES (1346, 0, N'2026-05-22 18:15:32.830', N'[{""key"":""period"",""label"":""结算月份"",""type"":""text"",""required"":true},{""key"":""amount"",""label"":""结算金额"",""type"":""amount"",""required"":true},{""key"":""itemCount"",""label"":""明细笔数"",""type"":""number"",""required"":true},{""key"":""description"",""label"":""结算说明"",""type"":""textarea"",""required"":false},{""key"":""attachments"",""label"":""附件"",""type"":""attachment"",""required"":false}]', N'2026-05-22 18:15:32.830', NULL, 1, 1353, NULL, 1, N'published');
        
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 1347)
        
        INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态]) VALUES (1347, 0, N'2026-05-22 18:15:32.836', N'[{""key"":""applicant"",""label"":""申请人"",""type"":""user"",""required"":true},{""key"":""department"",""label"":""部门"",""type"":""department"",""required"":true},{""key"":""amount"",""label"":""报销金额"",""type"":""amount"",""required"":true,""source"":""detailSum""},{""key"":""category"",""label"":""费用类别"",""type"":""select"",""required"":true,""options"":[""差旅费"",""办公费"",""招待费"",""交通费"",""通讯费"",""其他""]},{""key"":""description"",""label"":""报销说明"",""type"":""textarea"",""required"":true},{""key"":""attachments"",""label"":""附件"",""type"":""attachment"",""required"":false}]', N'2026-05-22 18:15:32.836', N'[{""key"":""expenseDate"",""label"":""费用日期"",""type"":""date"",""required"":true},{""key"":""expenseType"",""label"":""费用类型"",""type"":""select"",""required"":true,""options"":[""差旅费"",""办公费"",""招待费"",""交通费"",""通讯费"",""住宿费"",""餐费"",""其他""]},{""key"":""description"",""label"":""费用说明"",""type"":""text"",""required"":true},{""key"":""amount"",""label"":""金额"",""type"":""amount"",""required"":true},{""key"":""invoiceNo"",""label"":""发票号"",""type"":""text"",""required"":false}]', 1, 1354, NULL, 1, N'published');
        
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 1348)
        
        INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态]) VALUES (1348, 0, N'2026-05-22 18:15:32.840', N'[{""key"":""applicant"",""label"":""申请人"",""type"":""user"",""required"":true},{""key"":""amount"",""label"":""付款金额"",""type"":""amount"",""required"":true},{""key"":""supplier"",""label"":""供应商"",""type"":""text"",""required"":true},{""key"":""invoiceNo"",""label"":""发票号"",""type"":""text"",""required"":false},{""key"":""contractNo"",""label"":""合同编号"",""type"":""text"",""required"":false},{""key"":""description"",""label"":""付款说明"",""type"":""textarea"",""required"":true}]', N'2026-05-22 18:15:32.840', NULL, 1, 1355, NULL, 1, N'published');
        
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 1349)
        
        INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态]) VALUES (1349, 0, N'2026-05-22 18:15:32.843', N'[{""key"":""period"",""label"":""结算月份"",""type"":""text"",""required"":true},{""key"":""amount"",""label"":""结算金额"",""type"":""amount"",""required"":true},{""key"":""itemCount"",""label"":""明细笔数"",""type"":""number"",""required"":true},{""key"":""description"",""label"":""结算说明"",""type"":""textarea"",""required"":false},{""key"":""attachments"",""label"":""附件"",""type"":""attachment"",""required"":false}]', N'2026-05-22 18:15:32.843', NULL, 1, 1356, NULL, 1, N'published');
        
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 1350)
        
        INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态]) VALUES (1350, 0, N'2026-05-22 18:15:32.846', N'[{""key"":""applicant"",""label"":""申请人"",""type"":""user"",""required"":true},{""key"":""department"",""label"":""部门"",""type"":""department"",""required"":true},{""key"":""amount"",""label"":""请款金额"",""type"":""amount"",""required"":true},{""key"":""payee"",""label"":""收款方"",""type"":""text"",""required"":true},{""key"":""bankAccount"",""label"":""收款账号"",""type"":""text"",""required"":true},{""key"":""purpose"",""label"":""请款用途"",""type"":""textarea"",""required"":true},{""key"":""expectedDate"",""label"":""期望到款日"",""type"":""date"",""required"":false}]', N'2026-05-22 18:15:32.846', NULL, 1, 1357, NULL, 1, N'published');
        
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 1351)
        
        INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态]) VALUES (1351, 0, N'2026-05-22 18:15:32.850', N'[{""key"":""period"",""label"":""结算月份"",""type"":""text"",""required"":true},{""key"":""amount"",""label"":""结算金额"",""type"":""amount"",""required"":true},{""key"":""itemCount"",""label"":""明细笔数"",""type"":""number"",""required"":true},{""key"":""description"",""label"":""结算说明"",""type"":""textarea"",""required"":false},{""key"":""attachments"",""label"":""附件"",""type"":""attachment"",""required"":false}]', N'2026-05-22 18:15:32.850', NULL, 1, 1358, NULL, 1, N'published');
        
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2250)
        
        INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态]) VALUES (2250, 0, N'2026-05-22 18:54:02.293', N'[{""key"":""flowType"",""label"":""流程类型"",""type"":""text"",""required"":true,""readonly"":true},{""key"":""expenseCategory"",""label"":""费用类别"",""type"":""text"",""required"":true,""readonly"":true},{""key"":""expenseSummary"",""label"":""费用摘要"",""type"":""textarea"",""required"":true,""readonly"":true},{""key"":""amount"",""label"":""支出金额"",""type"":""money"",""required"":true,""readonly"":true},{""key"":""businessDate"",""label"":""业务日期"",""type"":""date"",""required"":false,""readonly"":true},{""key"":""payee"",""label"":""收款方"",""type"":""text"",""required"":false,""readonly"":true},{""key"":""costCenter"",""label"":""成本中心"",""type"":""text"",""required"":false,""readonly"":true},{""key"":""approvalNo"",""label"":""审批编号"",""type"":""text"",""required"":true,""readonly"":true},{""key"":""applicantName"",""label"":""申请人"",""type"":""text"",""required"":true,""readonly"":true},{""key"":""applicantDepartment"",""label"":""申请人部门"",""type"":""text"",""required"":false,""readonly"":true},{""key"":""approvalResult"",""label"":""审批结果"",""type"":""text"",""required"":false,""readonly"":true},{""key"":""completionDate"",""label"":""完成时间"",""type"":""date"",""required"":false,""readonly"":true},{""key"":""account"",""label"":""会计科目"",""type"":""account"",""required"":true},{""key"":""auxEmployee"",""label"":""报销员工"",""type"":""auxiliary"",""auxType"":""employee"",""required"":false},{""key"":""auxDepartment"",""label"":""部门"",""type"":""auxiliary"",""auxType"":""department"",""required"":false},{""key"":""paymentDate"",""label"":""付款日期"",""type"":""date"",""required"":true},{""key"":""paymentAccount"",""label"":""付款银行账户"",""type"":""bankAccount"",""required"":true},{""key"":""voucher1Ref"",""label"":""报销凭证"",""type"":""voucherRef"",""readonly"":true},{""key"":""voucher2Ref"",""label"":""付款凭证"",""type"":""voucherRef"",""readonly"":true}]', N'2026-05-22 18:54:02.293', NULL, 1, 2255, NULL, 1, N'published');
        
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2252)
        
        INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态]) VALUES (2252, 0, N'2026-05-24 15:24:15.970', N'[{""key"":""flowType"",""label"":""流程类型"",""type"":""text"",""required"":true,""readonly"":true},{""key"":""expenseCategory"",""label"":""费用类别"",""type"":""text"",""required"":true,""readonly"":true},{""key"":""expenseSummary"",""label"":""费用摘要"",""type"":""textarea"",""required"":true,""readonly"":true},{""key"":""amount"",""label"":""支出金额"",""type"":""money"",""required"":true,""readonly"":true},{""key"":""businessDate"",""label"":""业务日期"",""type"":""date"",""required"":false,""readonly"":true},{""key"":""payee"",""label"":""收款方"",""type"":""text"",""required"":false,""readonly"":true},{""key"":""costCenter"",""label"":""成本中心"",""type"":""text"",""required"":false,""readonly"":true},{""key"":""approvalNo"",""label"":""审批编号"",""type"":""text"",""required"":true,""readonly"":true},{""key"":""applicantName"",""label"":""申请人"",""type"":""text"",""required"":true,""readonly"":true},{""key"":""applicantDepartment"",""label"":""申请人部门"",""type"":""text"",""required"":false,""readonly"":true},{""key"":""approvalResult"",""label"":""审批结果"",""type"":""text"",""required"":false,""readonly"":true},{""key"":""completionDate"",""label"":""完成时间"",""type"":""date"",""required"":false,""readonly"":true},{""key"":""account"",""label"":""会计科目"",""type"":""account"",""required"":true},{""key"":""auxEmployee"",""label"":""报销员工"",""type"":""auxiliary"",""auxType"":""employee"",""required"":false},{""key"":""auxDepartment"",""label"":""部门"",""type"":""auxiliary"",""auxType"":""department"",""required"":false},{""key"":""paymentDate"",""label"":""付款日期"",""type"":""date"",""required"":true},{""key"":""paymentAccount"",""label"":""付款银行账户"",""type"":""bankAccount"",""required"":true},{""key"":""voucher1Ref"",""label"":""报销凭证"",""type"":""voucherRef"",""readonly"":true},{""key"":""voucher2Ref"",""label"":""付款凭证"",""type"":""voucherRef"",""readonly"":true}]', NULL, NULL, 0, 2255, NULL, 2, N'draft');
        
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2253)
        
        INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态]) VALUES (2253, 0, N'2026-05-24 17:52:15.361', N'[{""key"":""applicant"",""label"":""申请人"",""type"":""user"",""required"":true},{""key"":""department"",""label"":""部门"",""type"":""department"",""required"":true},{""key"":""amount"",""label"":""报销金额"",""type"":""amount"",""required"":true,""source"":""detailSum""},{""key"":""category"",""label"":""费用类别"",""type"":""select"",""required"":true,""options"":[""差旅费"",""办公费"",""招待费"",""交通费"",""通讯费"",""其他""]},{""key"":""description"",""label"":""报销说明"",""type"":""textarea"",""required"":true},{""key"":""attachments"",""label"":""附件"",""type"":""attachment"",""required"":false}]', NULL, N'[{""key"":""expenseDate"",""label"":""费用日期"",""type"":""date"",""required"":true},{""key"":""expenseType"",""label"":""费用类型"",""type"":""select"",""required"":true,""options"":[""差旅费"",""办公费"",""招待费"",""交通费"",""通讯费"",""住宿费"",""餐费"",""其他""]},{""key"":""description"",""label"":""费用说明"",""type"":""text"",""required"":true},{""key"":""amount"",""label"":""金额"",""type"":""amount"",""required"":true},{""key"":""invoiceNo"",""label"":""发票号"",""type"":""text"",""required"":false}]', 0, 1354, NULL, 2, N'draft');
        
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2254)
        
        INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态]) VALUES (2254, 1, N'2026-05-25 14:48:57.125', NULL, N'2026-05-25 14:48:57.125', NULL, 1, 2256, N'{""source"":""DcPipelineMigration"",""sourcePipelineId"":9}', 1, N'published');
        
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2255)
        
        INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态]) VALUES (2255, 1, N'2026-05-25 14:48:57.125', NULL, N'2026-05-25 14:48:57.125', NULL, 1, 2257, N'{""source"":""DcPipelineMigration"",""sourcePipelineId"":10}', 1, N'published');
        
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2256)
        
        INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态]) VALUES (2256, 1, N'2026-05-25 14:48:57.125', NULL, N'2026-05-25 14:48:57.125', NULL, 1, 2258, N'{""source"":""DcPipelineMigration"",""sourcePipelineId"":11}', 1, N'published');
        
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2257)
        
        INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态]) VALUES (2257, 1, N'2026-05-25 22:51:55.128', NULL, NULL, NULL, 0, 2256, N'{""source"":""DcPipelineMigration"",""sourcePipelineId"":9}', 2, N'draft');
        
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2258)
        
        INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态]) VALUES (2258, 1, N'2026-05-26 06:08:45.310', NULL, NULL, NULL, 0, 2257, N'{""source"":""DcPipelineMigration"",""sourcePipelineId"":10}', 2, N'draft');
        
        SET IDENTITY_INSERT [CF流程版本] OFF;
        
        ");

        // CF编排模板
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF编排模板] ON;
        
        IF NOT EXISTS (SELECT 1 FROM [CF编排模板] WHERE [FID] = 1)
        
        INSERT INTO [CF编排模板] ([FID], [F乐观锁], [F修改时间], [F创建人ID], [F创建时间], [F名称], [F描述], [F最大触发次数], [F状态], [F组织ID], [F编码], [F节点JSON], [F边JSON]) VALUES (1, NULL, N'2026-05-22 21:47:58.226', 0, N'2026-05-22 21:47:58.226', N'费用报销付款编排', N'费用报销 → (费用付款 + 凭证生成) → 汇聚 系统预置编排模板', 50, N'published', 0, N'FYBS_FYFK_CHAIN', N'{""nodes"":[{""id"":""start"",""type"":""start"",""name"":""开始""},{""id"":""fybs"",""type"":""cardflow"",""name"":""费用报销"",""flowCode"":""FYBS"",""completionMode"":""single""},{""id"":""fyfk"",""type"":""cardflow"",""name"":""费用付款"",""flowCode"":""FYFK"",""completionMode"":""single""},{""id"":""voucher"",""type"":""cardflow"",""name"":""凭证生成"",""flowCode"":""FYBS_VOUCHER"",""completionMode"":""single""},{""id"":""join1"",""type"":""join"",""name"":""汇聚"",""joinMode"":""all""},{""id"":""end"",""type"":""end"",""name"":""结束""}]}', N'{""edges"":[{""id"":""e1"",""from"":""start"",""to"":""fybs"",""dataProtocol"":{""level"":""signal""}},{""id"":""e2"",""from"":""fybs"",""to"":""fyfk"",""condition"":{""field"":""endStatus"",""op"":""=="",""value"":""completed""},""dataProtocol"":{""level"":""inline"",""mapping"":{""amount"":""$.amount"",""applicant"":""$.applicantName""}}},{""id"":""e3"",""from"":""fybs"",""to"":""voucher"",""condition"":{""field"":""endStatus"",""op"":""=="",""value"":""completed""},""dataProtocol"":{""level"":""ref"",""ref"":{""table"":""CF卡片"",""filterExpr"":""FID = ${sourceCardId}""}}},{""id"":""e4"",""from"":""fyfk"",""to"":""join1""},{""id"":""e5"",""from"":""voucher"",""to"":""join1""},{""id"":""e6"",""from"":""join1"",""to"":""end""}]}');
        
        SET IDENTITY_INSERT [CF编排模板] OFF;
        
        ");

        // CF自动插件注册
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件注册] ON;
        
        IF NOT EXISTS (SELECT 1 FROM [CF自动插件注册] WHERE [FID] = 1)
        
        INSERT INTO [CF自动插件注册] ([FID], [F处理粒度], [F插件名称], [F插件类型], [F插件编码], [F状态], [F说明], [F默认配置JSON]) VALUES (1, N'batch', N'Excel导入解析', N'Processing', N'ExcelInput', 1, N'读取上传的Excel文件，解析并写入STG暂存表', NULL);
        
        IF NOT EXISTS (SELECT 1 FROM [CF自动插件注册] WHERE [FID] = 2)
        
        INSERT INTO [CF自动插件注册] ([FID], [F处理粒度], [F插件名称], [F插件类型], [F插件编码], [F状态], [F说明], [F默认配置JSON]) VALUES (2, N'card', N'安全校验', N'Processing', N'SecurityCheck', 1, N'对导入数据执行安全规则校验', NULL);
        
        IF NOT EXISTS (SELECT 1 FROM [CF自动插件注册] WHERE [FID] = 3)
        
        INSERT INTO [CF自动插件注册] ([FID], [F处理粒度], [F插件名称], [F插件类型], [F插件编码], [F状态], [F说明], [F默认配置JSON]) VALUES (3, N'batch', N'质量分析', N'Processing', N'QualityAnalysis', 1, N'对批次数据执行质量规则分析，标记异常行', NULL);
        
        IF NOT EXISTS (SELECT 1 FROM [CF自动插件注册] WHERE [FID] = 4)
        
        INSERT INTO [CF自动插件注册] ([FID], [F处理粒度], [F插件名称], [F插件类型], [F插件编码], [F状态], [F说明], [F默认配置JSON]) VALUES (4, N'card', N'费用分类', N'Processing', N'Classification', 1, N'按规则对数据行进行费用科目分类', NULL);
        
        IF NOT EXISTS (SELECT 1 FROM [CF自动插件注册] WHERE [FID] = 5)
        
        INSERT INTO [CF自动插件注册] ([FID], [F处理粒度], [F插件名称], [F插件类型], [F插件编码], [F状态], [F说明], [F默认配置JSON]) VALUES (5, N'batch', N'自动凭证', N'Processing', N'AutoVoucher', 1, N'根据分类结果自动生成财务凭证', NULL);
        
        IF NOT EXISTS (SELECT 1 FROM [CF自动插件注册] WHERE [FID] = 7)
        
        INSERT INTO [CF自动插件注册] ([FID], [F处理粒度], [F插件名称], [F插件类型], [F插件编码], [F状态], [F说明], [F默认配置JSON]) VALUES (7, N'card', N'工作任务', N'Notification', N'WorkTask', 1, N'创建工作任务派发给指定人员', NULL);
        
        IF NOT EXISTS (SELECT 1 FROM [CF自动插件注册] WHERE [FID] = 8)
        
        INSERT INTO [CF自动插件注册] ([FID], [F处理粒度], [F插件名称], [F插件类型], [F插件编码], [F状态], [F说明], [F默认配置JSON]) VALUES (8, N'card', N'预警通知', N'Notification', N'AlertNotify', 1, N'发送预警通知（钉钉/站内信）', NULL);
        
        IF NOT EXISTS (SELECT 1 FROM [CF自动插件注册] WHERE [FID] = 9)
        
        INSERT INTO [CF自动插件注册] ([FID], [F处理粒度], [F插件名称], [F插件类型], [F插件编码], [F状态], [F说明], [F默认配置JSON]) VALUES (9, N'card', N'信息记录', N'Notification', N'InfoRecord', 1, N'记录流程执行信息日志', NULL);
        
        IF NOT EXISTS (SELECT 1 FROM [CF自动插件注册] WHERE [FID] = 10)
        
        INSERT INTO [CF自动插件注册] ([FID], [F处理粒度], [F插件名称], [F插件类型], [F插件编码], [F状态], [F说明], [F默认配置JSON]) VALUES (10, N'batch', N'凭证迁移', N'Processing', N'VoucherMigration', 1, N'从外部系统迁移历史凭证数据', NULL);
        
        IF NOT EXISTS (SELECT 1 FROM [CF自动插件注册] WHERE [FID] = 11)
        
        INSERT INTO [CF自动插件注册] ([FID], [F处理粒度], [F插件名称], [F插件类型], [F插件编码], [F状态], [F说明], [F默认配置JSON]) VALUES (11, N'batch', N'计价计算', N'Processing', N'Pricing', 1, N'按规则计算运单收入/成本（出港运单价格计算等）', NULL);
        
        IF NOT EXISTS (SELECT 1 FROM [CF自动插件注册] WHERE [FID] = 12)
        
        INSERT INTO [CF自动插件注册] ([FID], [F处理粒度], [F插件名称], [F插件类型], [F插件编码], [F状态], [F说明], [F默认配置JSON]) VALUES (12, N'batch', N'成本计算', N'Processing', N'Cost', 1, N'按成本方案计算运单/批次成本', NULL);
        
        IF NOT EXISTS (SELECT 1 FROM [CF自动插件注册] WHERE [FID] = 13)
        
        INSERT INTO [CF自动插件注册] ([FID], [F处理粒度], [F插件名称], [F插件类型], [F插件编码], [F状态], [F说明], [F默认配置JSON]) VALUES (13, N'batch', N'扇出分发', N'Processing', N'FanOut', 1, N'将批次明细行展开为独立卡片', NULL);
        
        SET IDENTITY_INSERT [CF自动插件注册] OFF;
        
        ");

        // 唯一索引: IX_CF卡片流程_编码_组织
        ExecSql(ctx, @"
        -- 先删除旧唯一索引（如果存在）
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CF卡片流程_编码' AND object_id = OBJECT_ID('CF卡片流程'))
        DROP INDEX [IX_CF卡片流程_编码] ON [CF卡片流程];
        
        -- 创建新的复合唯一索引
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CF卡片流程_编码_组织' AND object_id = OBJECT_ID('CF卡片流程'))
        CREATE UNIQUE INDEX [IX_CF卡片流程_编码_组织] ON [CF卡片流程] ([F流程编码], [F组织ID]);
        ");

    }

    private static void MigrateV2(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // CF自动插件_执行记录：FAgent名称 → FAutoPlugin名称，FAgent索引 → FAutoPlugin索引
        ExecSql(ctx, @"
        IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CF自动插件_执行记录]') AND name = N'FAgent名称')
            AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CF自动插件_执行记录]') AND name = N'FAutoPlugin名称')
            EXEC sp_rename N'[dbo].[CF自动插件_执行记录].[FAgent名称]', N'FAutoPlugin名称', N'COLUMN';

        IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CF自动插件_执行记录]') AND name = N'FAgent索引')
            AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CF自动插件_执行记录]') AND name = N'FAutoPlugin索引')
            EXEC sp_rename N'[dbo].[CF自动插件_执行记录].[FAgent索引]', N'FAutoPlugin索引', N'COLUMN';
        ");

        // CF批次快照：FAgent序号 → FAutoPlugin序号，FAgent名称 → FAutoPlugin名称
        ExecSql(ctx, @"
        IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CF批次快照]') AND name = N'FAgent序号')
            AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CF批次快照]') AND name = N'FAutoPlugin序号')
            EXEC sp_rename N'[dbo].[CF批次快照].[FAgent序号]', N'FAutoPlugin序号', N'COLUMN';

        IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CF批次快照]') AND name = N'FAgent名称')
            AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CF批次快照]') AND name = N'FAutoPlugin名称')
            EXEC sp_rename N'[dbo].[CF批次快照].[FAgent名称]', N'FAutoPlugin名称', N'COLUMN';
        ");

        // CF自动插件（CfPluginDef）：FAgent名称 → F插件名称，FAgent类型 → F插件类型，FAgent实现类型 → F插件实现类型
        ExecSql(ctx, @"
        IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CF自动插件]') AND name = N'FAgent名称')
            AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CF自动插件]') AND name = N'F插件名称')
            EXEC sp_rename N'[dbo].[CF自动插件].[FAgent名称]', N'F插件名称', N'COLUMN';

        IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CF自动插件]') AND name = N'FAgent类型')
            AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CF自动插件]') AND name = N'F插件类型')
            EXEC sp_rename N'[dbo].[CF自动插件].[FAgent类型]', N'F插件类型', N'COLUMN';

        IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CF自动插件]') AND name = N'FAgent实现类型')
            AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CF自动插件]') AND name = N'F插件实现类型')
            EXEC sp_rename N'[dbo].[CF自动插件].[FAgent实现类型]', N'F插件实现类型', N'COLUMN';
        ");

        // CF流程节点（CfStageDefinition）：F自动Agent名称 → F自动AutoPlugin名称，FAgent配置JSON → FAutoPlugin配置JSON
        ExecSql(ctx, @"
        IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CF流程节点]') AND name = N'F自动Agent名称')
            AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CF流程节点]') AND name = N'F自动AutoPlugin名称')
            EXEC sp_rename N'[dbo].[CF流程节点].[F自动Agent名称]', N'F自动AutoPlugin名称', N'COLUMN';

        IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CF流程节点]') AND name = N'FAgent配置JSON')
            AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CF流程节点]') AND name = N'FAutoPlugin配置JSON')
            EXEC sp_rename N'[dbo].[CF流程节点].[FAgent配置JSON]', N'FAutoPlugin配置JSON', N'COLUMN';
        ");

        // CF质量问题类型（CfQualityIssueType）：FSourceAgent → FSourceAutoPlugin
        ExecSql(ctx, @"
        IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CF质量问题类型]') AND name = N'FSourceAgent')
            AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CF质量问题类型]') AND name = N'FSourceAutoPlugin')
            EXEC sp_rename N'[dbo].[CF质量问题类型].[FSourceAgent]', N'FSourceAutoPlugin', N'COLUMN';
        ");
    }

    private static void MigrateV3(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // FanOut 插件注册描述更新
        ExecSql(ctx, @"
        UPDATE [CF自动插件注册] SET [F说明] = N'将批次明细行展开为独立卡片' WHERE [FID] = 13 AND [F说明] = N'批次拆分为子批次';
        ");
    }

    private static void MigrateV4(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ CfPluginRule: ExcelInput 规则（使文件上传自动匹配功能生效） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        -- 申通交易明细导入规则
        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3001)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3001, 192, N'excelInput', N'申通交易明细导入规则',
        N'{""targetTable"":""STG_申通交易明细"",""outputMode"":""stg"",""headerRow"":1,""dataStartRow"":2,""columnIdentifier"":""业务日期,记账日期,网点编号,费用名称,发生额(收入),发生额(支出),费用科目编码"",""fullColumnIdentifier"":""业务摘要,业务日期,业务类型,余额,关联单号,发生额(支出),发生额(收入),报表业务日期,网点名称,网点编号,联系方式,记账日期,账单类型,费用名称,费用科目收付类型,费用科目编码,银行账号"",""columnMapping"":[{""excelColumn"":""业务日期"",""dbColumn"":""F业务日期""},{""excelColumn"":""记账日期"",""dbColumn"":""F记账日期""},{""excelColumn"":""网点编号"",""dbColumn"":""F网点编号""},{""excelColumn"":""网点名称"",""dbColumn"":""F网点名称""},{""excelColumn"":""业务类型"",""dbColumn"":""F业务类型""},{""excelColumn"":""业务摘要"",""dbColumn"":""F业务摘要""},{""excelColumn"":""费用名称"",""dbColumn"":""F费用名称""},{""excelColumn"":""发生额(收入)"",""dbColumn"":""F收入""},{""excelColumn"":""发生额(支出)"",""dbColumn"":""F支出""},{""excelColumn"":""余额"",""dbColumn"":""F余额""},{""excelColumn"":""费用科目编码"",""dbColumn"":""F费用科目编码""},{""excelColumn"":""关联单号"",""dbColumn"":""F关联单号""},{""excelColumn"":""账单类型"",""dbColumn"":""F账单类型""}],""decimalFields"":[""发生额(收入)"",""发生额(支出)"",""余额""],""keyFields"":[""业务日期"",""网点编号"",""关联单号""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""transformRules"":[],""crossBatchDedupEnabled"":false,""crossBatchDedupFields"":[],""batchSplit"":{""enabled"":false}}',
        1, N'申通总部交易明细Excel导入配置', REPLACE(NEWID(),'-',''), GETDATE());

        -- 出港发件费结算明细导入规则
        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3002)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3002, 192, N'excelInput', N'出港发件费结算明细导入规则',
        N'{""targetTable"":""STG_出港运费"",""outputMode"":""stg"",""headerRow"":1,""dataStartRow"":2,""columnIdentifier"":""运单号,结算类型,结算对象,应收金额,中转费/运费,结算重量,目的省份"",""fullColumnIdentifier"":""业务时间,中转费/运费,加收费,应收金额,所属网点,目的城市,目的省份,结算对象,结算类型,结算重量,运单号,附加费"",""columnMapping"":[{""excelColumn"":""运单号"",""dbColumn"":""F运单号""},{""excelColumn"":""业务时间"",""dbColumn"":""F业务时间""},{""excelColumn"":""所属网点"",""dbColumn"":""F所属网点""},{""excelColumn"":""结算类型"",""dbColumn"":""F结算类型""},{""excelColumn"":""结算对象"",""dbColumn"":""F结算对象""},{""excelColumn"":""应收金额"",""dbColumn"":""F应收金额""},{""excelColumn"":""中转费/运费"",""dbColumn"":""F中转费""},{""excelColumn"":""附加费"",""dbColumn"":""F附加费""},{""excelColumn"":""加收费"",""dbColumn"":""F加收费""},{""excelColumn"":""结算重量"",""dbColumn"":""F结算重量""},{""excelColumn"":""目的省份"",""dbColumn"":""F目的省份""},{""excelColumn"":""目的城市"",""dbColumn"":""F目的城市""}],""decimalFields"":[""应收金额"",""中转费/运费"",""附加费"",""加收费"",""结算重量""],""keyFields"":[""运单号"",""业务时间""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""transformRules"":[],""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""运单号""],""batchSplit"":{""enabled"":false}}',
        1, N'出港发件费结算明细Excel导入配置', REPLACE(NEWID(),'-',''), GETDATE());

        -- 费用支出导入规则
        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3003)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3003, 192, N'excelInput', N'费用支出导入规则',
        N'{""targetTable"":""STG_费用支出"",""outputMode"":""stg"",""headerRow"":1,""dataStartRow"":2,""columnIdentifier"":""费用发生日期,付款事由,付款金额（元）,付款对象,费用归属单元,收款账户,审批编号"",""fullColumnIdentifier"":""付款事由,付款对象,付款金额（元）,付款金额（元）（大写）,创建人,创建人部门,创建时间,历史审批人,完成时间,审批单标题,审批状态,审批结果,审批编号,审批记录,序号,当前负责人,报销总额（元）,报销总额（元）（大写）,报销明细,收款账户,数据id,更新时间,耗时(时:分:秒),请选择流程类型,费用发生日期,费用归属单元,附件"",""columnMapping"":[{""excelColumn"":""数据id"",""dbColumn"":""F数据id""},{""excelColumn"":""请选择流程类型"",""dbColumn"":""F流程类型""},{""excelColumn"":""报销明细"",""dbColumn"":""F报销明细""},{""excelColumn"":""报销总额（元）"",""dbColumn"":""F报销总额""},{""excelColumn"":""费用发生日期"",""dbColumn"":""F费用发生日期""},{""excelColumn"":""付款事由"",""dbColumn"":""F付款事由""},{""excelColumn"":""付款金额（元）"",""dbColumn"":""F付款金额""},{""excelColumn"":""付款对象"",""dbColumn"":""F付款对象""},{""excelColumn"":""费用归属单元"",""dbColumn"":""F费用归属单元""},{""excelColumn"":""收款账户"",""dbColumn"":""F收款账户""},{""excelColumn"":""审批编号"",""dbColumn"":""F审批编号""},{""excelColumn"":""创建人"",""dbColumn"":""F创建人""},{""excelColumn"":""创建人部门"",""dbColumn"":""F创建人部门""},{""excelColumn"":""审批结果"",""dbColumn"":""F审批结果""},{""excelColumn"":""审批状态"",""dbColumn"":""F审批状态""},{""excelColumn"":""完成时间"",""dbColumn"":""F完成时间""},{""excelColumn"":""审批单标题"",""dbColumn"":""F审批单标题""}],""decimalFields"":[""报销总额（元）"",""付款金额（元）""],""keyFields"":[""审批编号"",""费用发生日期""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""transformRules"":[],""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""审批编号""],""batchSplit"":{""enabled"":false}}',
        1, N'费用报销付款Excel导入配置', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ CfStageDefinition: 流程首节点（ExcelInput 批次级自动节点） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF流程节点] ON;

        -- 申通交易明细流程 首节点（流程版本ID=2255）
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5001)
        INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
        VALUES (5001, 2255, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3001);

        -- 出港运单流程 首节点（流程版本ID=2254）
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5002)
        INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
        VALUES (5002, 2254, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3002);

        -- 费用支出流程 首节点（流程版本ID=2256）
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5003)
        INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
        VALUES (5003, 2256, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3003);

        SET IDENTITY_INSERT [CF流程节点] OFF;
        ");

        // ═══ CfFlowDefinition: 更新 FMatchPattern（文件名回退匹配） ═══
        ExecSql(ctx, @"
        UPDATE [CF卡片流程] SET [F匹配规则] = N'{""fileNamePattern"":""交易明细*""}' WHERE [FID] = 2257;
        UPDATE [CF卡片流程] SET [F匹配规则] = N'{""fileNamePattern"":""出港发件费结算明细*""}' WHERE [FID] = 2256;
        UPDATE [CF卡片流程] SET [F匹配规则] = N'{""fileNamePattern"":""费用报销付款*""}' WHERE [FID] = 2258;
        ");
    }

    private static void MigrateV5(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // [CF流程节点]: 清除 FAgent配置JSON, F自动Agent名称（先删默认约束再删列）
        SeederHelper.DropColumnSafe(ctx, "CF流程节点", "FAgent配置JSON");
        SeederHelper.DropColumnSafe(ctx, "CF流程节点", "F自动Agent名称");

        // [CF批次]: 清除多个残留列（先删默认约束再删列）
        SeederHelper.DropColumnSafe(ctx, "CF批次", "F当前Agent名称");
        SeederHelper.DropColumnSafe(ctx, "CF批次", "F当前Agent序号");
        SeederHelper.DropColumnSafe(ctx, "CF批次", "F当前步骤名称");
        SeederHelper.DropColumnSafe(ctx, "CF批次", "F当前步骤序号");
        SeederHelper.DropColumnSafe(ctx, "CF批次", "F步骤总数");
        SeederHelper.DropColumnSafe(ctx, "CF批次", "F流程类型");
        SeederHelper.DropColumnSafe(ctx, "CF批次", "F管道ID");
        SeederHelper.DropColumnSafe(ctx, "CF批次", "F管道阶段");

        // [CF批次快照]: 清除 FAgent名称, FAgent序号（先删默认约束再删列）
        SeederHelper.DropColumnSafe(ctx, "CF批次快照", "FAgent名称");
        SeederHelper.DropColumnSafe(ctx, "CF批次快照", "FAgent序号");

        // [CF质量规则]: 清除 F管道ID（先删默认约束再删列）
        SeederHelper.DropColumnSafe(ctx, "CF质量规则", "F管道ID");

        // [CF质量问题类型]: 清除 FSourceAgent（先删默认约束再删列）
        SeederHelper.DropColumnSafe(ctx, "CF质量问题类型", "FSourceAgent");

        // [CF自动插件]: 清除 FAgent名称, FAgent实现类型, FAgent类型, F管道ID（先删默认约束再删列）
        SeederHelper.DropColumnSafe(ctx, "CF自动插件", "FAgent名称");
        SeederHelper.DropColumnSafe(ctx, "CF自动插件", "FAgent实现类型");
        SeederHelper.DropColumnSafe(ctx, "CF自动插件", "FAgent类型");
        SeederHelper.DropColumnSafe(ctx, "CF自动插件", "F管道ID");

        // [CF自动插件_执行记录]: 清除 FAgent名称, FAgent索引（先删默认约束再删列）
        SeederHelper.DropColumnSafe(ctx, "CF自动插件_执行记录", "FAgent名称");
        SeederHelper.DropColumnSafe(ctx, "CF自动插件_执行记录", "FAgent索引");
    }

    private static void MigrateV6(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // 1. 注册 BatchSummary 插件
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件注册] ON;
        IF NOT EXISTS (SELECT 1 FROM [CF自动插件注册] WHERE [FID] = 14)
        INSERT INTO [CF自动插件注册] ([FID],[F处理粒度],[F插件名称],[F插件类型],[F插件编码],[F状态],[F说明],[F默认配置JSON])
        VALUES (14, N'batch', N'批次汇总', N'Processing', N'BatchSummary', 1, N'批次级链结束后创建一张汇总卡片', NULL);
        SET IDENTITY_INSERT [CF自动插件注册] OFF;
        ");

        // 修正 AutoVoucher 插件处理粒度：card → batch（AutoVoucherPlugin 继承 BatchPluginBase）
        ExecSql(ctx, @"
UPDATE [CF自动插件注册] SET [F处理粒度] = N'batch' WHERE [FID] = 5 AND [F处理粒度] = N'card';
");

        // 2. 创建 AutoVoucher 规则（申通交易明细凭证规则）
        //    优先复用数据库中旧DC规则(FID=21)；若不存在则创建新规则(FID=3004)
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;
        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3004)
           AND NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 21 AND [F类型编码] = N'autoVoucher')
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3004, 192, N'autoVoucher', N'申通交易明细凭证规则',
        N'{""mode"":""rulesBased"",""dateField"":""F记账日期"",""ruleGroups"":[],""unmatchedAction"":""skip""}',
        1, N'申通总部交易明细自动凭证规则（需通过凭证向导配置ruleGroups）', REPLACE(NEWID(),'-',''), GETDATE());
        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // 3. 补充 申通总部交易明细 (FlowVersion=2255) 缺失节点
        //    AutoVoucher规则ID：优先用旧规则21，否则用新建的3004
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF流程节点] ON;

        DECLARE @autoVoucherRuleId BIGINT;
        IF EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 21 AND [F类型编码] = N'autoVoucher')
            SET @autoVoucherRuleId = 21;
        ELSE
            SET @autoVoucherRuleId = 3004;

        -- 质量分析 (sort=2)
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5004)
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID])
        VALUES (5004, 2255, 2, N'质量分析', N'auto', N'batch', N'single', 3, NULL);

        -- 自动凭证 (sort=3)
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5005)
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID])
        VALUES (5005, 2255, 3, N'自动凭证', N'auto', N'batch', N'single', 5, @autoVoucherRuleId);

        -- 批次汇总 (sort=4)
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5006)
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID])
        VALUES (5006, 2255, 4, N'批次汇总', N'auto', N'batch', N'single', 14, NULL);

        -- 确认通知 (sort=5, human card-level, 钉钉推送)
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5007)
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID],
            [F处理人策略],[F处理人配置JSON])
        VALUES (5007, 2255, 5, N'确认通知', N'human', N'card', N'single', NULL, NULL,
            N'fixed', N'{""users"":[{""userId"":1,""userName"":""管理员""}],""pushChannel"":""dingtalk""}');

        SET IDENTITY_INSERT [CF流程节点] OFF;
        ");
    }

    private static void MigrateV7(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // 1. 修正版本2258中 FID=6627 节点名称：自动节点 → 批次汇总
        ExecSql(ctx, @"
        UPDATE [CF流程节点] SET [F节点名称] = N'批次汇总'
        WHERE [FID] = 6627 AND [F节点名称] = N'自动节点';
        ");

        // 2. 在版本2258中补充"确认通知"节点（Sort=5, human/card级, 钉钉推送）
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [F流程版本ID] = 2258 AND [F节点名称] = N'确认通知')
        INSERT INTO [CF流程节点] ([F流程版本ID],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID],
            [F处理人策略],[F处理人配置JSON])
        VALUES (2258, 5, N'确认通知', N'human', N'card', N'single', NULL, NULL,
            N'fixed', N'{""users"":[{""userId"":1,""userName"":""管理员""}],""pushChannel"":""dingtalk""}');
        ");

        // 3. 清理版本2255中的重复节点（6585/6586/6587 与 5001/5004/5005 同排序号冲突）
        ExecSql(ctx, @"
        DELETE FROM [CF流程节点] WHERE [FID] IN (6585, 6586, 6587) AND [F流程版本ID] = 2255;
        ");
    }

    private static void MigrateV8(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // 为版本2258（费用支出导入）预置卡片SchemaJSON（导入结果展示字段）
        ExecSql(ctx, @"
        UPDATE [CF流程版本]
        SET [F卡片SchemaJSON] = N'[{""key"":""totalRows"",""label"":""导入总行数"",""type"":""text"",""required"":false,""readonly"":true},{""key"":""successRows"",""label"":""成功行数"",""type"":""text"",""required"":false,""readonly"":true},{""key"":""failedRows"",""label"":""失败行数"",""type"":""text"",""required"":false,""readonly"":true},{""key"":""voucherCount"",""label"":""凭证数量"",""type"":""text"",""required"":false,""readonly"":true},{""key"":""importTime"",""label"":""导入时间"",""type"":""date"",""required"":false,""readonly"":true}]'
        WHERE FID = 2258 AND ([F卡片SchemaJSON] IS NULL OR [F卡片SchemaJSON] = N'');
        ");
    }

    private static void MigrateV9(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // 1. 创建 STG_出港运费 暂存表
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG_出港运费')
        CREATE TABLE [STG_出港运费] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3002 columnMapping）
            [F运单号] NVARCHAR(200) NULL,
            [F业务时间] NVARCHAR(100) NULL,
            [F所属网点] NVARCHAR(200) NULL,
            [F结算类型] NVARCHAR(100) NULL,
            [F结算对象] NVARCHAR(200) NULL,
            [F应收金额] DECIMAL(18,2) NULL,
            [F中转费] DECIMAL(18,2) NULL,
            [F附加费] DECIMAL(18,2) NULL,
            [F加收费] DECIMAL(18,2) NULL,
            [F结算重量] DECIMAL(18,4) NULL,
            [F目的省份] NVARCHAR(50) NULL,
            [F目的城市] NVARCHAR(50) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL,
            [F计算状态] INT NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG_出港运费_F批次ID' AND object_id = OBJECT_ID(N'STG_出港运费'))
        CREATE INDEX [IX_STG_出港运费_F批次ID] ON [STG_出港运费]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG_出港运费_FDataScopeId' AND object_id = OBJECT_ID(N'STG_出港运费'))
        CREATE INDEX [IX_STG_出港运费_FDataScopeId] ON [STG_出港运费]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;
        ");

        // 2. 清理版本2254中配置错误的节点（保留FID=5002首节点），然后补充正确的后续节点
        ExecSql(ctx, @"
        -- 先删除版本2254中除首节点(FID=5002)外的所有后续节点（那些配置错误的ExcelInput节点）
        DELETE FROM [CF流程节点] WHERE [F流程版本ID] = 2254 AND [FID] != 5002 AND [F排序号] > 1;

        SET IDENTITY_INSERT [CF流程节点] ON;

        -- 出港运单流程 价格计算节点（sort=2, Pricing 插件 FID=11）
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5008)
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID],[FAutoPlugin配置JSON])
        VALUES (5008, 2254, 2, N'出港运单价格计算', N'auto', N'batch', N'single', 11, NULL, N'{""sourceTable"":""STG申通出港运单数据"",""brandCode"":""STO"",""resultTable"":""EXP出港运单_计费结果"",""columnMapping"":{""waybillNo"":""F运单编号"",""shopName"":""F店铺账号"",""waybillDate"":""F业务日期"",""billingStatus"":""F计算状态"",""clientAlias"":""F共享别名"",""destinationProvince"":""F目的省份"",""destinationCity"":""F目的城市"",""settlementWeight"":""F结算重量"",""pickupWeight"":""F揽收重量"",""transitWeight"":""F中转重量"",""deliveryWeight"":""F到件重量"",""bundleWeight"":""F集包重量"",""volumeWeight"":""F计泡重量"",""hqWeight"":""F总部重量"",""declarationValue"":""F声明价值"",""networkPointName"":""F所属网点""}}');

        -- 出港运单流程 批次汇总节点（sort=3, BatchSummary 插件 FID=14）
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5009)
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID])
        VALUES (5009, 2254, 3, N'批次汇总', N'auto', N'batch', N'single', 14, NULL);

        SET IDENTITY_INSERT [CF流程节点] OFF;
        ");
    }

    private static void MigrateV10(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // 为节点5008补充 FAutoPlugin配置JSON（MigrateV9创建时遗漏）
        ExecSql(ctx, @"
        UPDATE [CF流程节点]
        SET [FAutoPlugin配置JSON] = N'{""sourceTable"":""STG申通出港运单数据"",""brandCode"":""ST"",""resultTable"":""EXP出港运单_计费结果"",""columnMapping"":{""waybillNo"":""F运单编号"",""shopName"":""F店铺账号"",""waybillDate"":""F业务日期"",""billingStatus"":""F计算状态"",""clientAlias"":""F共享别名"",""destinationProvince"":""F目的省份"",""destinationCity"":""F目的城市"",""settlementWeight"":""F结算重量"",""pickupWeight"":""F揽收重量"",""transitWeight"":""F中转重量"",""deliveryWeight"":""F到件重量"",""bundleWeight"":""F集包重量"",""volumeWeight"":""F计泡重量"",""hqWeight"":""F总部重量"",""declarationValue"":""F声明价值"",""networkPointName"":""F所属网点""}}'
        WHERE [FID] = 5008;
        ");
    }

    private static void MigrateV11(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // 修正 brandCode: "STO" → "ST"（EXP品牌表编码为 NCHAR(2)，申通 = "ST"）
        ExecSql(ctx, @"
        UPDATE [CF流程节点]
        SET [FAutoPlugin配置JSON] = REPLACE([FAutoPlugin配置JSON], '""brandCode"":""STO""', '""brandCode"":""ST""')
        WHERE [FID] = 5008 AND [FAutoPlugin配置JSON] LIKE N'%brandCode%STO%';
        ");
    }

    private static void MigrateV12(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // 修正版本2254（出港运单流程）中成本计算节点配置：F类型=auto, F处理粒度=batch
        ExecSql(ctx, @"
        UPDATE [CF流程节点] SET [F类型] = N'auto', [F处理粒度] = N'batch'
        WHERE [F流程版本ID] = 2254
          AND [F插件注册ID] = (SELECT TOP 1 [FID] FROM [CF自动插件注册] WHERE [F插件编码] = N'Cost')
          AND ([F类型] IS NULL OR [F类型] != N'auto' OR [F处理粒度] IS NULL OR [F处理粒度] != N'batch');
        ");
    }

    private static void MigrateV13(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // 修复版本2254：MigrateV9 重建后续节点时遗漏了 Cost 节点，
        // 导致批次会在 Pricing + BatchSummary 后正常结束，但不会产生成本明细。
        ExecSql(ctx, @"
        DECLARE @costPluginId BIGINT = (
            SELECT TOP 1 [FID]
            FROM [CF自动插件注册]
            WHERE [F插件编码] = N'Cost'
        );

        IF @costPluginId IS NOT NULL
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;

            IF NOT EXISTS (
                SELECT 1
                FROM [CF流程节点]
                WHERE [F流程版本ID] = 2254
                  AND [F插件注册ID] = @costPluginId
            )
            AND NOT EXISTS (
                SELECT 1
                FROM [CF流程节点]
                WHERE [FID] = 5010
            )
            INSERT INTO [CF流程节点] (
                [FID],[F流程版本ID],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],
                [F插件注册ID],[F插件规则ID],[FAutoPlugin配置JSON]
            )
            VALUES (
                5010, 2254, 4, N'出港运单成本计算', N'auto', N'batch', N'single',
                @costPluginId, NULL,
                N'{""sourceTable"":""STG申通出港运单数据"",""resultTable"":""EXP出港运单_计费结果""}'
            );

            SET IDENTITY_INSERT [CF流程节点] OFF;

            UPDATE [CF流程节点]
            SET [F排序号] = 4,
                [F节点名称] = N'出港运单成本计算',
                [F类型] = N'auto',
                [F处理粒度] = N'batch',
                [F审批模式] = N'single',
                [FAutoPlugin配置JSON] = N'{""sourceTable"":""STG申通出港运单数据"",""resultTable"":""EXP出港运单_计费结果""}'
            WHERE [F流程版本ID] = 2254
              AND [F插件注册ID] = @costPluginId;
        END;

        UPDATE [CF流程节点]
        SET [F排序号] = 5
        WHERE [F流程版本ID] = 2254
          AND [FID] = 5009
          AND [F排序号] < 5;
        ");
    }

    private static void MigrateV14(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        ExecSql(ctx, @"
        DECLARE @pricingPluginId BIGINT = (
            SELECT TOP 1 [FID]
            FROM [CF自动插件注册]
            WHERE [F插件编码] = N'Pricing'
        );
        DECLARE @costPluginId BIGINT = (
            SELECT TOP 1 [FID]
            FROM [CF自动插件注册]
            WHERE [F插件编码] = N'Cost'
        );

        IF @pricingPluginId IS NOT NULL
        BEGIN
            UPDATE [CF流程节点]
            SET [F排序号] = 3
            WHERE [F流程版本ID] = 2254
              AND [F插件注册ID] = @pricingPluginId;
        END;

        IF @costPluginId IS NOT NULL
        BEGIN
            UPDATE [CF流程节点]
            SET [F排序号] = 4,
                [F类型] = N'auto',
                [F处理粒度] = N'batch',
                [F审批模式] = N'single',
                [F节点名称] = N'出港运单成本计算'
            WHERE [F流程版本ID] = 2254
              AND [F插件注册ID] = @costPluginId;
        END;

        UPDATE [CF流程节点]
        SET [F排序号] = 5
        WHERE [F流程版本ID] = 2254
          AND [F节点名称] = N'批次汇总'
          AND [F排序号] <= 4;
        ");
    }

    private static void MigrateV15(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // 1. 将费用报销/费用付款手工流程 Schema 规范到当前 SchemaRenderer 支持的字段类型。
        ExecSql(ctx, @"
        UPDATE [CF流程版本]
        SET [F卡片SchemaJSON] = N'[{""key"":""applicant"",""label"":""申请人"",""type"":""user"",""required"":true,""readonly"":true,""dataSource"":""auto"",""autoSource"":""currentUser""},{""key"":""department"",""label"":""部门"",""type"":""org"",""required"":true,""readonly"":true,""dataSource"":""auto"",""autoSource"":""currentUserDepartment""},{""key"":""amount"",""label"":""报销金额"",""type"":""money"",""required"":true,""source"":""detailSum"",""readonly"":true},{""key"":""category"",""label"":""费用类别"",""type"":""enum"",""required"":true,""options"":[""差旅费"",""办公费"",""招待费"",""交通费"",""通讯费"",""其他""]},{""key"":""description"",""label"":""报销说明"",""type"":""text"",""required"":true},{""key"":""attachments"",""label"":""附件"",""type"":""file"",""required"":false}]',
            [F明细SchemaJSON] = N'[{""key"":""expenseDate"",""label"":""费用日期"",""type"":""date"",""required"":true},{""key"":""expenseType"",""label"":""费用类型"",""type"":""enum"",""required"":true,""options"":[""差旅费"",""办公费"",""招待费"",""交通费"",""通讯费"",""住宿费"",""餐费"",""其他""]},{""key"":""description"",""label"":""费用说明"",""type"":""text"",""required"":true},{""key"":""amount"",""label"":""金额"",""type"":""money"",""required"":true},{""key"":""invoiceNo"",""label"":""发票号"",""type"":""text"",""required"":false}]'
        WHERE FID = 1347;

        UPDATE [CF流程版本]
        SET [F卡片SchemaJSON] = N'[{""key"":""applicant"",""label"":""申请人"",""type"":""user"",""required"":true},{""key"":""amount"",""label"":""付款金额"",""type"":""money"",""required"":true},{""key"":""supplier"",""label"":""供应商"",""type"":""text"",""required"":true},{""key"":""invoiceNo"",""label"":""发票号"",""type"":""text"",""required"":false},{""key"":""contractNo"",""label"":""合同编号"",""type"":""text"",""required"":false},{""key"":""description"",""label"":""付款说明"",""type"":""text"",""required"":true}]'
        WHERE FID = 1348;
        ");

        // 2. 补齐编排模板引用的凭证生成流程 FYBS_VOUCHER。
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF卡片流程] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [F流程编码] = N'FYBS_VOUCHER' AND [F组织ID] = 192)
           AND NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2260)
        INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
        VALUES (2260, NULL, 0, GETDATE(), NULL, N'费用报销完成后生成或确认财务凭证', GETDATE(), N'{发起人}-凭证生成-{金额}元', N'凭证生成', NULL, N'FYBS_VOUCHER', N'published', 192, N'FYBSV-{yyyy}{MM}{dd}-{seq}', NULL, NULL, NULL);

        SET IDENTITY_INSERT [CF卡片流程] OFF;
        ");

        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF流程版本] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2260)
           AND EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2260 AND [F流程编码] = N'FYBS_VOUCHER')
        INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
        VALUES (2260, 0, GETDATE(), N'[{""key"":""sourceCardNo"",""label"":""来源报销单"",""type"":""text"",""required"":false},{""key"":""applicant"",""label"":""申请人"",""type"":""user"",""required"":true},{""key"":""amount"",""label"":""凭证金额"",""type"":""money"",""required"":true},{""key"":""description"",""label"":""凭证摘要"",""type"":""text"",""required"":true},{""key"":""voucherRef"",""label"":""凭证引用"",""type"":""voucherRef"",""required"":false}]', GETDATE(), NULL, 1, 2260, NULL, 1, N'published');

        SET IDENTITY_INSERT [CF流程版本] OFF;
        ");

        // 3. 给手工流程补默认人工节点，确保发起、提交、审批可以闭环。
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF流程节点] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5011)
           AND NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [F流程版本ID] = 1347 AND [F节点名称] = N'费用报销审批')
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID],[F处理人策略],[F处理人配置JSON])
        VALUES (5011, 1347, 1, N'费用报销审批', N'human', N'card', N'single', NULL, NULL, N'fixed', N'{""users"":[{""userId"":1,""userName"":""管理员""}]}');

        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5012)
           AND NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [F流程版本ID] = 1348 AND [F节点名称] = N'费用付款确认')
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID],[F处理人策略],[F处理人配置JSON])
        VALUES (5012, 1348, 1, N'费用付款确认', N'human', N'card', N'single', NULL, NULL, N'fixed', N'{""users"":[{""userId"":1,""userName"":""管理员""}]}');

        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5013)
           AND NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [F流程版本ID] = 2260 AND [F节点名称] = N'凭证生成确认')
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID],[F处理人策略],[F处理人配置JSON])
        VALUES (5013, 2260, 1, N'凭证生成确认', N'human', N'card', N'single', NULL, NULL, N'fixed', N'{""users"":[{""userId"":1,""userName"":""管理员""}]}');

        SET IDENTITY_INSERT [CF流程节点] OFF;
        ");

        // 4. 修正费用支出导入规则：目标表与列名要匹配 STG费用支出记录 实体表。
        ExecSql(ctx, @"
        UPDATE [CF自动插件_规则]
        SET [F规则配置JSON] = N'{""targetTable"":""STG费用支出记录"",""outputMode"":""stg"",""headerRow"":1,""dataStartRow"":2,""columnIdentifier"":""费用发生日期,付款事由,付款金额（元）,付款对象,费用归属单元,收款账户,审批编号"",""fullColumnIdentifier"":""付款事由,付款对象,付款金额（元）,付款金额（元）（大写）,创建人,创建人部门,创建时间,历史审批人,完成时间,审批单标题,审批状态,审批结果,审批编号,审批记录,序号,当前负责人,报销总额（元）,报销总额（元）（大写）,报销明细,收款账户,数据id,更新时间,耗时(时:分:秒),请选择流程类型,费用发生日期,费用归属单元,附件"",""columnMapping"":[{""excelColumn"":""数据id"",""dbColumn"":""F数据ID""},{""excelColumn"":""请选择流程类型"",""dbColumn"":""F流程类型""},{""excelColumn"":""请选择流程类型"",""dbColumn"":""F费用类别""},{""excelColumn"":""付款事由"",""dbColumn"":""F费用摘要""},{""excelColumn"":""付款金额（元）"",""dbColumn"":""F支出金额""},{""excelColumn"":""费用发生日期"",""dbColumn"":""F业务日期""},{""excelColumn"":""付款对象"",""dbColumn"":""F收款方""},{""excelColumn"":""费用归属单元"",""dbColumn"":""F成本中心""},{""excelColumn"":""审批编号"",""dbColumn"":""F审批编号""},{""excelColumn"":""创建人"",""dbColumn"":""F申请人""},{""excelColumn"":""创建人部门"",""dbColumn"":""F申请人部门""},{""excelColumn"":""审批结果"",""dbColumn"":""F审批结果""},{""excelColumn"":""完成时间"",""dbColumn"":""F完成时间""}],""decimalFields"":[""付款金额（元）""],""dateFields"":[""费用发生日期"",""完成时间""],""keyFields"":[""审批编号""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""transformRules"":[],""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""审批编号""],""batchSplit"":{""enabled"":false}}',
            [F说明] = N'费用报销付款Excel导入配置'
        WHERE [FID] = 3003;
        ");

        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3005)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3005, 192, N'autoVoucher', N'费用支出自动凭证规则',
            N'{""mode"":""rulesBased"",""targetTable"":""STG费用支出记录"",""dateField"":""F业务日期"",""ruleGroups"":[],""unmatchedAction"":""skip""}',
            1, N'费用支出自动凭证规则（需通过凭证向导配置ruleGroups）', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // 5. 将费用支出导入后续节点补到当前发布版本 2256，而不是旧迁移误用的 2258。
        ExecSql(ctx, @"
        UPDATE [CF流程版本]
        SET [F卡片SchemaJSON] = N'[{""key"":""totalRows"",""label"":""导入总行数"",""type"":""text"",""required"":false,""readonly"":true},{""key"":""successRows"",""label"":""成功行数"",""type"":""text"",""required"":false,""readonly"":true},{""key"":""failedRows"",""label"":""失败行数"",""type"":""text"",""required"":false,""readonly"":true},{""key"":""voucherCount"",""label"":""凭证数量"",""type"":""text"",""required"":false,""readonly"":true},{""key"":""targetTable"",""label"":""暂存表"",""type"":""text"",""required"":false,""readonly"":true},{""key"":""importTime"",""label"":""导入时间"",""type"":""date"",""required"":false,""readonly"":true}]'
        WHERE FID = 2256 AND ([F卡片SchemaJSON] IS NULL OR [F卡片SchemaJSON] = N'');
        ");

        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF流程节点] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5014)
           AND NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [F流程版本ID] = 2256 AND [F插件注册ID] = 3)
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID])
        VALUES (5014, 2256, 2, N'费用支出质量分析', N'auto', N'batch', N'single', 3, NULL);

        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5015)
           AND NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [F流程版本ID] = 2256 AND [F插件注册ID] = 5)
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID])
        VALUES (5015, 2256, 3, N'费用支出自动凭证', N'auto', N'batch', N'single', 5, 3005);

        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5016)
           AND NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [F流程版本ID] = 2256 AND [F节点名称] = N'批次汇总')
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID])
        VALUES (5016, 2256, 4, N'批次汇总', N'auto', N'batch', N'single', 14, NULL);

        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5017)
           AND NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [F流程版本ID] = 2256 AND [F节点名称] = N'确认通知')
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID],[F处理人策略],[F处理人配置JSON])
        VALUES (5017, 2256, 5, N'确认通知', N'human', N'card', N'single', NULL, NULL, N'fixed', N'{""users"":[{""userId"":1,""userName"":""管理员""}],""pushChannel"":""dingtalk""}');

        SET IDENTITY_INSERT [CF流程节点] OFF;
        ");
    }

    private static void MigrateV16(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // 资金类手工流程升级：费用申请/请款(QKSQ) → 借款(JKSQ) → 费用报销(FYBS) 联动。
        ExecSql(ctx, @"
        UPDATE [CF卡片流程]
        SET [F描述] = N'支持引用费用申请、冲抵借款/备用金，并按岗位节点差异化展示卡片内容',
            [F标题模板] = N'{发起人}-费用报销-{金额}元',
            [F更新时间] = GETDATE()
        WHERE [FID] = 1354;

        UPDATE [CF卡片流程]
        SET [F流程名称] = N'费用申请/请款',
            [F描述] = N'费用发生前的预算、付款和请款申请，可作为费用报销的前置引用来源',
            [F标题模板] = N'{发起人}-费用申请-{金额}元',
            [F更新时间] = GETDATE()
        WHERE [FID] = 1357;
        ");

        ExecSql(ctx, @"
        UPDATE [CF流程版本]
        SET [F卡片SchemaJSON] = N'[{""key"":""applicant"",""label"":""申请人"",""type"":""user"",""required"":true,""readonly"":true,""dataSource"":""auto"",""autoSource"":""currentUser""},{""key"":""department"",""label"":""申请部门"",""type"":""org"",""required"":true,""readonly"":true,""dataSource"":""auto"",""autoSource"":""currentUserDepartment""},{""key"":""category"",""label"":""报销场景"",""type"":""enum"",""required"":true,""options"":[""日常费用"",""差旅报销"",""项目报销"",""费用申请核销"",""借款冲抵"",""其他""]},{""key"":""requestRef"",""label"":""引用请款单"",""type"":""cardRef"",""required"":false,""targetFlowCode"":""QKSQ"",""displayFields"":[""cardNumber"",""title"",""amount"",""availableAmount"",""status""]},{""key"":""loanRef"",""label"":""引用借款/备用金"",""type"":""cardRef"",""required"":false,""targetFlowCode"":""JKSQ"",""displayFields"":[""cardNumber"",""title"",""loanAmount"",""outstandingBalance"",""status""]},{""key"":""amount"",""label"":""报销金额"",""type"":""money"",""required"":true,""source"":""detailSum"",""readonly"":true},{""key"":""offsetLoanAmount"",""label"":""冲抵借款金额"",""type"":""money"",""required"":false,""defaultValue"":0},{""key"":""actualPayAmount"",""label"":""实付金额"",""type"":""money"",""required"":false,""readonly"":true},{""key"":""paymentMethod"",""label"":""收款方式"",""type"":""enum"",""required"":true,""options"":[""员工收款"",""供应商收款"",""冲抵借款"",""不付款""]},{""key"":""payeeName"",""label"":""收款人名称"",""type"":""text"",""required"":true},{""key"":""payeeAccountNo"",""label"":""收款人账号"",""type"":""text"",""required"":false},{""key"":""payeeBankName"",""label"":""收款人开户行"",""type"":""text"",""required"":false},{""key"":""paymentAccount"",""label"":""付款银行账户"",""type"":""bankAccount"",""required"":false},{""key"":""remarks"",""label"":""备注"",""type"":""text"",""required"":false},{""key"":""attachments"",""label"":""票据附件"",""type"":""file"",""required"":true,""accept"":"".pdf,.jpg,.jpeg,.png,.xlsx,.xls"",""maxSize"":50}]',
            [F明细SchemaJSON] = N'[{""key"":""expenseDate"",""label"":""费用日期"",""type"":""date"",""required"":true},{""key"":""expenseAccount"",""label"":""费用科目"",""type"":""account"",""required"":true},{""key"":""auxDepartment"",""label"":""费用归属部门"",""type"":""auxiliary"",""auxType"":""department"",""required"":false},{""key"":""project"",""label"":""项目"",""type"":""auxiliary"",""auxType"":""project"",""required"":false},{""key"":""expenseType"",""label"":""费用类型"",""type"":""enum"",""required"":true,""options"":[""差旅费"",""办公费"",""招待费"",""交通费"",""通讯费"",""住宿费"",""餐费"",""其他""]},{""key"":""description"",""label"":""费用说明"",""type"":""text"",""required"":true},{""key"":""invoiceDate"",""label"":""发票日期"",""type"":""date"",""required"":false},{""key"":""invoiceNo"",""label"":""发票号"",""type"":""text"",""required"":false},{""key"":""invoiceAmount"",""label"":""发票金额"",""type"":""money"",""required"":false},{""key"":""amount"",""label"":""报销金额"",""type"":""money"",""required"":true}]',
            [F流程设置JSON] = N'{""version"":2,""flowFamily"":""financeExpense"",""linkedFlows"":{""expenseRequest"":""QKSQ"",""loan"":""JKSQ"",""voucher"":""FYBS_VOUCHER""}}'
        WHERE FID = 1347;

        UPDATE [CF流程版本]
        SET [F卡片SchemaJSON] = N'[{""key"":""applicant"",""label"":""申请人"",""type"":""user"",""required"":true,""readonly"":true,""dataSource"":""auto"",""autoSource"":""currentUser""},{""key"":""department"",""label"":""申请部门"",""type"":""org"",""required"":true,""readonly"":true,""dataSource"":""auto"",""autoSource"":""currentUserDepartment""},{""key"":""expenseType"",""label"":""费用类型"",""type"":""enum"",""required"":true,""options"":[""日常费用"",""差旅"",""项目费用"",""供应商付款"",""备用金"",""其他""]},{""key"":""reason"",""label"":""申请事由"",""type"":""text"",""required"":true},{""key"":""amount"",""label"":""申请金额"",""type"":""money"",""required"":true},{""key"":""referencedAmount"",""label"":""已报销引用"",""type"":""money"",""readonly"":true,""defaultValue"":0},{""key"":""availableAmount"",""label"":""可报销余额"",""type"":""money"",""readonly"":true},{""key"":""paymentMethod"",""label"":""付款方式"",""type"":""enum"",""required"":true,""options"":[""员工收款"",""供应商收款"",""备用金"",""不付款""]},{""key"":""payeeName"",""label"":""收款人名称"",""type"":""text"",""required"":false},{""key"":""payeeAccountNo"",""label"":""收款账号"",""type"":""text"",""required"":false},{""key"":""payeeBankName"",""label"":""开户行"",""type"":""text"",""required"":false},{""key"":""expectedPayDate"",""label"":""期望付款日期"",""type"":""date"",""required"":false},{""key"":""paymentAccount"",""label"":""付款银行账户"",""type"":""bankAccount"",""required"":false},{""key"":""attachments"",""label"":""申请附件"",""type"":""file"",""required"":false,""accept"":"".pdf,.jpg,.jpeg,.png,.xlsx,.xls"",""maxSize"":50},{""key"":""remarks"",""label"":""备注"",""type"":""text"",""required"":false}]',
            [F明细SchemaJSON] = NULL,
            [F流程设置JSON] = N'{""version"":2,""flowFamily"":""financeExpenseRequest"",""downstreamFlows"":[""FYBS""]}'
        WHERE FID = 1350;
        ");

        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF卡片流程] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2261)
           AND NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [F流程编码] = N'JKSQ' AND [F组织ID] = 192)
        INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
        VALUES (2261, NULL, 0, GETDATE(), NULL, N'员工或部门借款、备用金申请，可被费用报销引用并冲抵', GETDATE(), N'{发起人}-借款申请-{金额}元', N'借款申请', NULL, N'JKSQ', N'published', 192, N'JKSQ-{yyyy}{MM}{dd}-{seq}', NULL, NULL, NULL);

        SET IDENTITY_INSERT [CF卡片流程] OFF;
        ");

        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF流程版本] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2261)
           AND EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2261 AND [F流程编码] = N'JKSQ')
        INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
        VALUES (2261, 0, GETDATE(), N'[{""key"":""applicant"",""label"":""申请人"",""type"":""user"",""required"":true,""readonly"":true,""dataSource"":""auto"",""autoSource"":""currentUser""},{""key"":""department"",""label"":""申请部门"",""type"":""org"",""required"":true,""readonly"":true,""dataSource"":""auto"",""autoSource"":""currentUserDepartment""},{""key"":""loanAmount"",""label"":""借款金额"",""type"":""money"",""required"":true},{""key"":""loanReason"",""label"":""借款事由"",""type"":""text"",""required"":true},{""key"":""expectedReturnDate"",""label"":""预计归还日期"",""type"":""date"",""required"":false},{""key"":""paymentMethod"",""label"":""放款方式"",""type"":""enum"",""required"":true,""options"":[""员工收款"",""备用金"",""其他""]},{""key"":""payeeName"",""label"":""收款人名称"",""type"":""text"",""required"":true},{""key"":""payeeAccountNo"",""label"":""收款账号"",""type"":""text"",""required"":false},{""key"":""payeeBankName"",""label"":""开户行"",""type"":""text"",""required"":false},{""key"":""paymentAccount"",""label"":""放款银行账户"",""type"":""bankAccount"",""required"":false},{""key"":""reimburseOffsetAmount"",""label"":""已报销冲抵"",""type"":""money"",""readonly"":true,""defaultValue"":0},{""key"":""repaidAmount"",""label"":""已还款"",""type"":""money"",""readonly"":true,""defaultValue"":0},{""key"":""outstandingBalance"",""label"":""未还余额"",""type"":""money"",""readonly"":true},{""key"":""attachments"",""label"":""借款附件"",""type"":""file"",""required"":false,""accept"":"".pdf,.jpg,.jpeg,.png,.xlsx,.xls"",""maxSize"":50},{""key"":""remarks"",""label"":""备注"",""type"":""text"",""required"":false}]', GETDATE(), NULL, 1, 2261, N'{""version"":2,""flowFamily"":""financeLoan"",""downstreamFlows"":[""FYBS""]}', 1, N'published');

        SET IDENTITY_INSERT [CF流程版本] OFF;
        ");

        ExecSql(ctx, @"
        UPDATE [CF流程节点]
        SET [F节点名称] = N'主管审批',
            [F类型] = N'human',
            [F处理粒度] = N'card',
            [F审批模式] = N'single',
            [F处理人策略] = N'fixedUsers',
            [F处理人配置JSON] = N'{""users"":[{""userId"":1,""userName"":""管理员""}],""fallback"":{""mode"":""fixedUsers"",""users"":[{""userId"":1,""userName"":""管理员""}]}}',
            [F补充字段JSON] = N'{""version"":2,""inputFields"":[],""viewProfile"":{""profileName"":""主管审批"",""sections"":[{""key"":""main"",""title"":""报销概要"",""type"":""fields"",""fields"":[{""fieldKey"":""applicant""},{""fieldKey"":""department""},{""fieldKey"":""category""},{""fieldKey"":""amount""},{""fieldKey"":""requestRef""},{""fieldKey"":""loanRef""}]},{""key"":""payee"",""title"":""收款信息"",""type"":""fields"",""fields"":[{""fieldKey"":""paymentMethod""},{""fieldKey"":""payeeName""},{""fieldKey"":""payeeAccountNo""},{""fieldKey"":""payeeBankName""}]},{""key"":""details"",""title"":""报销明细"",""type"":""detailTable""}],""fieldAccess"":{""payeeAccountNo"":{""access"":""masked""},""paymentAccount"":{""access"":""hidden""}},""detailAccess"":{""default.expenseAccount"":{""access"":""readonly""}},""actions"":[""approve"",""reject"",""returnToStage"",""addSignBefore"",""addSignAfter"",""transfer"",""cc""]},""actionPolicy"":{""allowedActions"":[""approve"",""reject"",""returnToStage"",""addSignBefore"",""addSignAfter"",""transfer"",""cc""]}}'
        WHERE [FID] = 5011 OR ([F流程版本ID] = 1347 AND [F排序号] = 1);
        ");

        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF流程节点] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5018)
           AND NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [F流程版本ID] = 1347 AND [F节点名称] = N'财务复核')
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID],[F处理人策略],[F处理人配置JSON],[F补充字段JSON])
        VALUES (5018, 1347, 2, N'财务复核', N'human', N'card', N'single', NULL, NULL, N'fixedUsers', N'{""users"":[{""userId"":1,""userName"":""管理员""}],""fallback"":{""mode"":""fixedUsers"",""users"":[{""userId"":1,""userName"":""管理员""}]}}', N'{""version"":2,""inputFields"":[""paymentAccount""],""viewProfile"":{""profileName"":""财务复核"",""sections"":[{""key"":""finance"",""title"":""财务信息"",""type"":""fields"",""fields"":[{""fieldKey"":""amount""},{""fieldKey"":""offsetLoanAmount""},{""fieldKey"":""actualPayAmount""},{""fieldKey"":""paymentAccount""}]},{""key"":""payee"",""title"":""收款信息"",""type"":""fields"",""fields"":[{""fieldKey"":""paymentMethod""},{""fieldKey"":""payeeName""},{""fieldKey"":""payeeAccountNo""},{""fieldKey"":""payeeBankName""}]},{""key"":""details"",""title"":""会计核算明细"",""type"":""detailTable""}],""detailAccess"":{""default.expenseAccount"":{""access"":""required"",""required"":true},""default.auxDepartment"":{""access"":""editable""},""default.project"":{""access"":""editable""}},""fieldAccess"":{""paymentAccount"":{""access"":""required"",""required"":true},""actualPayAmount"":{""access"":""readonly""},""payeeAccountNo"":{""access"":""readonly""}},""actions"":[""approve"",""reject"",""returnToStage"",""transfer"",""cc""]},""actionPolicy"":{""allowedActions"":[""approve"",""reject"",""returnToStage"",""transfer"",""cc""]}}');

        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5019)
           AND NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [F流程版本ID] = 1347 AND [F节点名称] = N'付款确认')
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID],[F处理人策略],[F处理人配置JSON],[F补充字段JSON])
        VALUES (5019, 1347, 3, N'付款确认', N'human', N'card', N'single', NULL, NULL, N'fixedUsers', N'{""users"":[{""userId"":1,""userName"":""管理员""}],""fallback"":{""mode"":""fixedUsers"",""users"":[{""userId"":1,""userName"":""管理员""}]}}', N'{""version"":2,""inputFields"":[],""viewProfile"":{""profileName"":""付款确认"",""sections"":[{""key"":""payment"",""title"":""付款确认"",""type"":""fields"",""fields"":[{""fieldKey"":""paymentAccount""},{""fieldKey"":""paymentMethod""},{""fieldKey"":""payeeName""},{""fieldKey"":""payeeAccountNo""},{""fieldKey"":""payeeBankName""},{""fieldKey"":""actualPayAmount""}]},{""key"":""refs"",""title"":""关联单据"",""type"":""fields"",""fields"":[{""fieldKey"":""requestRef""},{""fieldKey"":""loanRef""}]}],""fieldAccess"":{""paymentAccount"":{""access"":""readonly""},""payeeAccountNo"":{""access"":""readonly""},""actualPayAmount"":{""access"":""readonly""}},""actions"":[""approve"",""reject"",""returnToStage"",""cc""]},""actionPolicy"":{""allowedActions"":[""approve"",""reject"",""returnToStage"",""cc""]}}');

        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5020)
           AND NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [F流程版本ID] = 1350 AND [F节点名称] = N'费用申请审批')
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID],[F处理人策略],[F处理人配置JSON],[F补充字段JSON])
        VALUES (5020, 1350, 1, N'费用申请审批', N'human', N'card', N'single', NULL, NULL, N'fixedUsers', N'{""users"":[{""userId"":1,""userName"":""管理员""}],""fallback"":{""mode"":""fixedUsers"",""users"":[{""userId"":1,""userName"":""管理员""}]}}', N'{""version"":2,""inputFields"":[],""viewProfile"":{""profileName"":""费用申请审批"",""sections"":[{""key"":""request"",""title"":""申请信息"",""type"":""fields"",""fields"":[{""fieldKey"":""applicant""},{""fieldKey"":""department""},{""fieldKey"":""expenseType""},{""fieldKey"":""amount""},{""fieldKey"":""reason""},{""fieldKey"":""expectedPayDate""}]},{""key"":""payee"",""title"":""收款信息"",""type"":""fields"",""fields"":[{""fieldKey"":""paymentMethod""},{""fieldKey"":""payeeName""},{""fieldKey"":""payeeAccountNo""},{""fieldKey"":""payeeBankName""}]}],""fieldAccess"":{""payeeAccountNo"":{""access"":""masked""},""paymentAccount"":{""access"":""hidden""},""availableAmount"":{""access"":""hidden""}},""actions"":[""approve"",""reject"",""returnToStage"",""addSignBefore"",""addSignAfter"",""transfer"",""cc""]},""actionPolicy"":{""allowedActions"":[""approve"",""reject"",""returnToStage"",""addSignBefore"",""addSignAfter"",""transfer"",""cc""]}}');

        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5021)
           AND NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [F流程版本ID] = 1350 AND [F节点名称] = N'财务预算确认')
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID],[F处理人策略],[F处理人配置JSON],[F补充字段JSON])
        VALUES (5021, 1350, 2, N'财务预算确认', N'human', N'card', N'single', NULL, NULL, N'fixedUsers', N'{""users"":[{""userId"":1,""userName"":""管理员""}],""fallback"":{""mode"":""fixedUsers"",""users"":[{""userId"":1,""userName"":""管理员""}]}}', N'{""version"":2,""inputFields"":[""paymentAccount""],""viewProfile"":{""profileName"":""财务预算确认"",""sections"":[{""key"":""budget"",""title"":""预算与付款"",""type"":""fields"",""fields"":[{""fieldKey"":""amount""},{""fieldKey"":""referencedAmount""},{""fieldKey"":""availableAmount""},{""fieldKey"":""paymentAccount""}]},{""key"":""payee"",""title"":""收款信息"",""type"":""fields"",""fields"":[{""fieldKey"":""payeeName""},{""fieldKey"":""payeeAccountNo""},{""fieldKey"":""payeeBankName""}]}],""fieldAccess"":{""availableAmount"":{""access"":""readonly""},""paymentAccount"":{""access"":""required"",""required"":true},""payeeAccountNo"":{""access"":""readonly""}},""actions"":[""approve"",""reject"",""returnToStage"",""transfer"",""cc""]},""actionPolicy"":{""allowedActions"":[""approve"",""reject"",""returnToStage"",""transfer"",""cc""]}}');

        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5022)
           AND NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [F流程版本ID] = 2261 AND [F节点名称] = N'借款审批')
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID],[F处理人策略],[F处理人配置JSON],[F补充字段JSON])
        VALUES (5022, 2261, 1, N'借款审批', N'human', N'card', N'single', NULL, NULL, N'fixedUsers', N'{""users"":[{""userId"":1,""userName"":""管理员""}],""fallback"":{""mode"":""fixedUsers"",""users"":[{""userId"":1,""userName"":""管理员""}]}}', N'{""version"":2,""inputFields"":[],""viewProfile"":{""profileName"":""借款审批"",""sections"":[{""key"":""loan"",""title"":""借款信息"",""type"":""fields"",""fields"":[{""fieldKey"":""applicant""},{""fieldKey"":""department""},{""fieldKey"":""loanAmount""},{""fieldKey"":""loanReason""},{""fieldKey"":""expectedReturnDate""}]},{""key"":""payee"",""title"":""收款信息"",""type"":""fields"",""fields"":[{""fieldKey"":""paymentMethod""},{""fieldKey"":""payeeName""},{""fieldKey"":""payeeAccountNo""},{""fieldKey"":""payeeBankName""}]}],""fieldAccess"":{""payeeAccountNo"":{""access"":""masked""},""paymentAccount"":{""access"":""hidden""},""outstandingBalance"":{""access"":""hidden""}},""actions"":[""approve"",""reject"",""returnToStage"",""addSignBefore"",""addSignAfter"",""transfer"",""cc""]},""actionPolicy"":{""allowedActions"":[""approve"",""reject"",""returnToStage"",""addSignBefore"",""addSignAfter"",""transfer"",""cc""]}}');

        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5023)
           AND NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [F流程版本ID] = 2261 AND [F节点名称] = N'财务放款确认')
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID],[F处理人策略],[F处理人配置JSON],[F补充字段JSON])
        VALUES (5023, 2261, 2, N'财务放款确认', N'human', N'card', N'single', NULL, NULL, N'fixedUsers', N'{""users"":[{""userId"":1,""userName"":""管理员""}],""fallback"":{""mode"":""fixedUsers"",""users"":[{""userId"":1,""userName"":""管理员""}]}}', N'{""version"":2,""inputFields"":[""paymentAccount""],""viewProfile"":{""profileName"":""财务放款确认"",""sections"":[{""key"":""payment"",""title"":""放款确认"",""type"":""fields"",""fields"":[{""fieldKey"":""loanAmount""},{""fieldKey"":""paymentAccount""},{""fieldKey"":""payeeName""},{""fieldKey"":""payeeAccountNo""},{""fieldKey"":""payeeBankName""}]},{""key"":""balance"",""title"":""借款余额"",""type"":""fields"",""fields"":[{""fieldKey"":""reimburseOffsetAmount""},{""fieldKey"":""repaidAmount""},{""fieldKey"":""outstandingBalance""}]}],""fieldAccess"":{""paymentAccount"":{""access"":""required"",""required"":true},""outstandingBalance"":{""access"":""readonly""},""payeeAccountNo"":{""access"":""readonly""}},""actions"":[""approve"",""reject"",""returnToStage"",""cc""]},""actionPolicy"":{""allowedActions"":[""approve"",""reject"",""returnToStage"",""cc""]}}');

        SET IDENTITY_INSERT [CF流程节点] OFF;
        ");
    }

    private static void MigrateV17(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // P4 财务参考模板：补齐稳定节点键、组件化卡片 schema、可预演样例与流程管理员兜底。
        ExecSql(ctx, @"
        UPDATE [CF流程版本]
        SET [F流程设置JSON] = N'{""version"":2,""flowFamily"":""financeExpenseRequest"",""downstreamFlows"":[""FYBS""],""approvalAdminUserIds"":[1],""ruleGuardrails"":{""requireDefaultRoute"":true,""maxDynamicInsertCount"":20,""disableLoopInsert"":true}}',
            [F卡片SchemaJSON] = N'{""version"":2,""fields"":[{""key"":""applicant"",""label"":""申请人"",""type"":""user"",""required"":true,""readonly"":true,""dataSource"":{""type"":""auto"",""source"":""currentUser""}},{""key"":""department"",""label"":""申请部门"",""type"":""org"",""required"":true,""readonly"":true,""dataSource"":{""type"":""auto"",""source"":""currentUserDepartment""}},{""key"":""expenseType"",""label"":""费用类型"",""type"":""enum"",""required"":true,""options"":[{""label"":""日常费用"",""value"":""daily""},{""label"":""差旅"",""value"":""travel""},{""label"":""项目费用"",""value"":""project""},{""label"":""供应商付款"",""value"":""supplier""},{""label"":""备用金"",""value"":""pettyCash""},{""label"":""其他"",""value"":""other""}]},{""key"":""reason"",""label"":""申请事由"",""type"":""text"",""required"":true},{""key"":""amount"",""label"":""申请金额"",""type"":""money"",""required"":true},{""key"":""referencedAmount"",""label"":""已报销引用"",""type"":""money"",""readonly"":true,""defaultValue"":0},{""key"":""availableAmount"",""label"":""可报销余额"",""type"":""money"",""readonly"":true},{""key"":""paymentMethod"",""label"":""付款方式"",""type"":""enum"",""required"":true,""options"":[{""label"":""员工收款"",""value"":""employee""},{""label"":""供应商收款"",""value"":""supplier""},{""label"":""备用金"",""value"":""pettyCash""},{""label"":""不付款"",""value"":""none""}]},{""key"":""payeeName"",""label"":""收款人名称"",""type"":""text"",""required"":false},{""key"":""payeeAccountNo"",""label"":""收款账号"",""type"":""text"",""required"":false},{""key"":""payeeBankName"",""label"":""开户行"",""type"":""text"",""required"":false},{""key"":""expectedPayDate"",""label"":""期望付款日期"",""type"":""date"",""required"":false},{""key"":""paymentAccount"",""label"":""付款银行账户"",""type"":""bankAccount"",""required"":false},{""key"":""attachments"",""label"":""申请附件"",""type"":""file"",""required"":false,""accept"":"".pdf,.jpg,.jpeg,.png,.xlsx,.xls"",""maxSize"":50},{""key"":""remarks"",""label"":""备注"",""type"":""text"",""required"":false}],""components"":[{""id"":""qksq_amount_summary"",""type"":""amountSummary"",""title"":""申请金额"",""binding"":{""source"":""cardField"",""fieldKey"":""amount""},""statisticKey"":""finance.qksq.amount""},{""id"":""qksq_budget_status"",""type"":""budgetStatus"",""title"":""预算状态"",""binding"":{""source"":""cardField"",""fieldKey"":""availableAmount""},""props"":{""statusText"":""等待财务预算确认"",""visibleOnStage"":""expense_request_budget""},""statisticKey"":""finance.qksq.budget""},{""id"":""qksq_route_decision"",""type"":""routeDecision"",""title"":""流转说明"",""binding"":{""source"":""snapshot"",""snapshotType"":""routeDecision""},""statisticKey"":""finance.qksq.routeDecision""}],""previewSamples"":[{""name"":""小额日常费用"",""cardData"":{""flowCode"":""QKSQ"",""amount"":3000,""expenseType"":""daily""},""expectedPath"":[""发起人"",""费用申请审批"",""财务预算确认""]},{""name"":""大额项目费用"",""cardData"":{""flowCode"":""QKSQ"",""amount"":18000,""expenseType"":""project""},""expectedPath"":[""发起人"",""费用申请审批"",""区域负责人"",""财务预算确认""]}]}'
        WHERE FID = 1350;

        UPDATE [CF流程版本]
        SET [F流程设置JSON] = N'{""version"":2,""flowFamily"":""financeLoan"",""downstreamFlows"":[""FYBS""],""approvalAdminUserIds"":[1],""ruleGuardrails"":{""requireDefaultRoute"":true,""maxDynamicInsertCount"":20,""disableLoopInsert"":true}}',
            [F卡片SchemaJSON] = N'{""version"":2,""fields"":[{""key"":""applicant"",""label"":""申请人"",""type"":""user"",""required"":true,""readonly"":true,""dataSource"":{""type"":""auto"",""source"":""currentUser""}},{""key"":""department"",""label"":""申请部门"",""type"":""org"",""required"":true,""readonly"":true,""dataSource"":{""type"":""auto"",""source"":""currentUserDepartment""}},{""key"":""loanAmount"",""label"":""借款金额"",""type"":""money"",""required"":true},{""key"":""isPettyCash"",""label"":""是否备用金"",""type"":""boolean"",""required"":false,""defaultValue"":false},{""key"":""loanReason"",""label"":""借款事由"",""type"":""text"",""required"":true},{""key"":""expectedReturnDate"",""label"":""预计归还日期"",""type"":""date"",""required"":false},{""key"":""paymentMethod"",""label"":""放款方式"",""type"":""enum"",""required"":true,""options"":[{""label"":""员工收款"",""value"":""employee""},{""label"":""备用金"",""value"":""pettyCash""},{""label"":""其他"",""value"":""other""}]},{""key"":""payeeName"",""label"":""收款人名称"",""type"":""text"",""required"":true},{""key"":""payeeAccountNo"",""label"":""收款账号"",""type"":""text"",""required"":false},{""key"":""payeeBankName"",""label"":""开户行"",""type"":""text"",""required"":false},{""key"":""paymentAccount"",""label"":""放款银行账户"",""type"":""bankAccount"",""required"":false},{""key"":""reimburseOffsetAmount"",""label"":""已报销冲抵"",""type"":""money"",""readonly"":true,""defaultValue"":0},{""key"":""repaidAmount"",""label"":""已还款"",""type"":""money"",""readonly"":true,""defaultValue"":0},{""key"":""outstandingBalance"",""label"":""未还余额"",""type"":""money"",""readonly"":true},{""key"":""attachments"",""label"":""借款附件"",""type"":""file"",""required"":false,""accept"":"".pdf,.jpg,.jpeg,.png,.xlsx,.xls"",""maxSize"":50},{""key"":""remarks"",""label"":""备注"",""type"":""text"",""required"":false}],""components"":[{""id"":""jksq_loan_balance"",""type"":""loanOffset"",""title"":""借款余额"",""binding"":{""source"":""cardField"",""fieldKey"":""outstandingBalance""},""props"":{""statusText"":""未还余额待财务确认""},""statisticKey"":""finance.jksq.loanOffset""},{""id"":""jksq_repayment_plan"",""type"":""loanOffset"",""title"":""还款计划"",""binding"":{""source"":""cardField"",""fieldKey"":""expectedReturnDate""},""props"":{""variant"":""repaymentPlan"",""statusText"":""按预计归还日跟踪""},""statisticKey"":""repaymentPlan""},{""id"":""jksq_route_decision"",""type"":""routeDecision"",""title"":""流转说明"",""binding"":{""source"":""snapshot"",""snapshotType"":""routeDecision""},""statisticKey"":""finance.jksq.routeDecision""}],""previewSamples"":[{""name"":""普通借款"",""cardData"":{""flowCode"":""JKSQ"",""loanAmount"":5000,""outstandingBalance"":0,""isPettyCash"":false},""expectedPath"":[""发起人"",""借款审批"",""财务放款确认""]},{""name"":""大额备用金"",""cardData"":{""flowCode"":""JKSQ"",""loanAmount"":30000,""outstandingBalance"":0,""isPettyCash"":true},""expectedPath"":[""发起人"",""借款审批"",""财务经理复核"",""财务放款确认""]}]}'
        WHERE FID = 2261;

        UPDATE [CF流程版本]
        SET [F流程设置JSON] = N'{""version"":2,""flowFamily"":""financeExpense"",""linkedFlows"":{""expenseRequest"":""QKSQ"",""loan"":""JKSQ"",""voucher"":""FYBS_VOUCHER""},""approvalAdminUserIds"":[1],""ruleGuardrails"":{""requireDefaultRoute"":true,""maxDynamicInsertCount"":20,""disableLoopInsert"":true}}',
            [F卡片SchemaJSON] = N'{""version"":2,""fields"":[{""key"":""applicant"",""label"":""申请人"",""type"":""user"",""required"":true,""readonly"":true,""dataSource"":{""type"":""auto"",""source"":""currentUser""}},{""key"":""department"",""label"":""申请部门"",""type"":""org"",""required"":true,""readonly"":true,""dataSource"":{""type"":""auto"",""source"":""currentUserDepartment""}},{""key"":""category"",""label"":""报销场景"",""type"":""enum"",""required"":true,""options"":[{""label"":""日常费用"",""value"":""daily""},{""label"":""差旅报销"",""value"":""travel""},{""label"":""项目报销"",""value"":""project""},{""label"":""费用申请核销"",""value"":""requestWriteOff""},{""label"":""借款冲抵"",""value"":""loanOffset""},{""label"":""其他"",""value"":""other""}]},{""key"":""requestRef"",""label"":""引用请款单"",""type"":""cardRef"",""required"":false,""targetFlowCode"":""QKSQ"",""displayFields"":[""cardNumber"",""title"",""amount"",""availableAmount"",""status""]},{""key"":""loanRef"",""label"":""引用借款/备用金"",""type"":""cardRef"",""required"":false,""targetFlowCode"":""JKSQ"",""displayFields"":[""cardNumber"",""title"",""loanAmount"",""outstandingBalance"",""status""]},{""key"":""amount"",""label"":""报销金额"",""type"":""money"",""required"":true,""source"":""detailSum"",""readonly"":true},{""key"":""offsetLoanAmount"",""label"":""冲抵借款金额"",""type"":""money"",""required"":false,""defaultValue"":0},{""key"":""actualPayAmount"",""label"":""实付金额"",""type"":""money"",""required"":false,""readonly"":true},{""key"":""paymentMethod"",""label"":""收款方式"",""type"":""enum"",""required"":true,""options"":[{""label"":""员工收款"",""value"":""employee""},{""label"":""供应商收款"",""value"":""supplier""},{""label"":""冲抵借款"",""value"":""loanOffset""},{""label"":""不付款"",""value"":""none""}]},{""key"":""payeeName"",""label"":""收款人名称"",""type"":""text"",""required"":true},{""key"":""payeeAccountNo"",""label"":""收款人账号"",""type"":""text"",""required"":false},{""key"":""payeeBankName"",""label"":""收款人开户行"",""type"":""text"",""required"":false},{""key"":""paymentAccount"",""label"":""付款银行账户"",""type"":""bankAccount"",""required"":false},{""key"":""remarks"",""label"":""备注"",""type"":""text"",""required"":false},{""key"":""attachments"",""label"":""票据附件"",""type"":""file"",""required"":true,""accept"":"".pdf,.jpg,.jpeg,.png,.xlsx,.xls"",""maxSize"":50}],""components"":[{""id"":""fybs_amount_summary"",""type"":""amountSummary"",""title"":""报销金额"",""binding"":{""source"":""cardField"",""fieldKey"":""amount""},""statisticKey"":""finance.fybs.amount""},{""id"":""fybs_invoice_status"",""type"":""invoiceStatus"",""title"":""发票状态"",""binding"":{""source"":""detailSummary"",""summaryKey"":""invoiceAmount""},""props"":{""statusText"":""按明细发票金额汇总""},""statisticKey"":""invoiceStatus""},{""id"":""fybs_budget_status"",""type"":""budgetStatus"",""title"":""预算核销"",""binding"":{""source"":""relation"",""relationType"":""expenseRequest""},""props"":{""linkedFlowCode"":""QKSQ""},""statisticKey"":""budgetStatus""},{""id"":""fybs_loan_offset"",""type"":""loanOffset"",""title"":""借款冲抵"",""binding"":{""source"":""cardField"",""fieldKey"":""offsetLoanAmount""},""props"":{""linkedFlowCode"":""JKSQ""},""statisticKey"":""loanOffset""},{""id"":""fybs_payment_info"",""type"":""paymentInfo"",""title"":""付款信息"",""binding"":{""source"":""cardField"",""fieldKey"":""actualPayAmount""},""statisticKey"":""paymentInfo""},{""id"":""fybs_risk_alert"",""type"":""riskAlert"",""title"":""风险提示"",""binding"":{""source"":""snapshot"",""snapshotType"":""ruleWarning""},""props"":{""rules"":[""amountOverBudget"",""missingInvoice"",""loanOffsetMismatch""]},""statisticKey"":""riskAlert""},{""id"":""fybs_route_decision"",""type"":""routeDecision"",""title"":""流转说明"",""binding"":{""source"":""snapshot"",""snapshotType"":""routeDecision""},""statisticKey"":""routeDecision""},{""id"":""fybs_dynamic_approver"",""type"":""dynamicApprover"",""title"":""动态审批人"",""binding"":{""source"":""snapshot"",""snapshotType"":""dynamicApprover""},""statisticKey"":""dynamicApprover""}],""previewSamples"":[{""name"":""小额实付"",""cardData"":{""flowCode"":""FYBS"",""amount"":3000,""actualPayAmount"":3000,""offsetLoanAmount"":0},""expectedPath"":[""发起人"",""主管审批"",""财务复核"",""付款确认""]},{""name"":""大额报销"",""cardData"":{""flowCode"":""FYBS"",""amount"":8000,""actualPayAmount"":8000,""offsetLoanAmount"":0},""expectedPath"":[""发起人"",""主管审批"",""总经理审批"",""财务复核"",""付款确认""]},{""name"":""实付为零"",""cardData"":{""flowCode"":""FYBS"",""amount"":1000,""actualPayAmount"":0,""requestRef"":""QKSQ-001""},""expectedPath"":[""发起人"",""主管审批"",""财务复核"",""核销确认""]}]}',
            [F明细SchemaJSON] = N'{""version"":2,""tables"":[{""detailTableKey"":""default"",""label"":""费用明细"",""columns"":[{""key"":""expenseDate"",""label"":""费用日期"",""type"":""date"",""required"":true},{""key"":""expenseAccount"",""label"":""费用科目"",""type"":""account"",""required"":true},{""key"":""auxDepartment"",""label"":""费用归属部门"",""type"":""auxiliary"",""auxType"":""department"",""required"":false},{""key"":""project"",""label"":""项目"",""type"":""auxiliary"",""auxType"":""project"",""required"":false},{""key"":""expenseType"",""label"":""费用类型"",""type"":""enum"",""required"":true,""options"":[{""label"":""差旅费"",""value"":""travel""},{""label"":""办公费"",""value"":""office""},{""label"":""招待费"",""value"":""businessMeal""},{""label"":""交通费"",""value"":""traffic""},{""label"":""通讯费"",""value"":""communication""},{""label"":""住宿费"",""value"":""hotel""},{""label"":""餐费"",""value"":""meal""},{""label"":""其他"",""value"":""other""}]},{""key"":""description"",""label"":""费用说明"",""type"":""text"",""required"":true},{""key"":""invoiceDate"",""label"":""发票日期"",""type"":""date"",""required"":false},{""key"":""invoiceNo"",""label"":""发票号"",""type"":""text"",""required"":false},{""key"":""invoiceAmount"",""label"":""发票金额"",""type"":""money"",""required"":false},{""key"":""amount"",""label"":""报销金额"",""type"":""money"",""required"":true}]}]}'
        WHERE FID = 1347;
        ");

        ExecSql(ctx, @"
        UPDATE [CF流程节点] SET [F节点键] = N'expense_supervisor', [F排序号] = 1 WHERE [FID] = 5011 OR ([F流程版本ID] = 1347 AND [F排序号] = 1);
        UPDATE [CF流程节点] SET [F节点键] = N'expense_finance', [F排序号] = 3 WHERE [FID] = 5018 OR ([F流程版本ID] = 1347 AND [F节点名称] = N'财务复核');
        UPDATE [CF流程节点] SET [F节点键] = N'expense_payment', [F排序号] = 5 WHERE [FID] = 5019 OR ([F流程版本ID] = 1347 AND [F节点名称] = N'付款确认');
        UPDATE [CF流程节点] SET [F节点键] = N'expense_request_approval', [F排序号] = 1 WHERE [FID] = 5020 OR ([F流程版本ID] = 1350 AND [F节点名称] = N'费用申请审批');
        UPDATE [CF流程节点] SET [F节点键] = N'expense_request_budget', [F排序号] = 4 WHERE [FID] = 5021 OR ([F流程版本ID] = 1350 AND [F节点名称] = N'财务预算确认');
        UPDATE [CF流程节点] SET [F节点键] = N'loan_approval', [F排序号] = 1 WHERE [FID] = 5022 OR ([F流程版本ID] = 2261 AND [F节点名称] = N'借款审批');
        UPDATE [CF流程节点] SET [F节点键] = N'loan_payment', [F排序号] = 4 WHERE [FID] = 5023 OR ([F流程版本ID] = 2261 AND [F节点名称] = N'财务放款确认');

        SET IDENTITY_INSERT [CF流程节点] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5024)
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F节点键],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID],[F处理人策略],[F处理人配置JSON],[F补充字段JSON])
        VALUES (5024, 1350, N'expense_request_dept', 2, N'部门负责人审批', N'human', N'card', N'single', NULL, NULL, N'fixedUsers', N'{""users"":[{""userId"":1,""userName"":""管理员""}],""fallback"":{""type"":""flowAdmin""}}', N'{""version"":2,""inputFields"":[],""viewProfile"":{""profileName"":""部门负责人审批"",""fieldAccess"":{""payeeAccountNo"":{""access"":""masked""}},""actions"":[""approve"",""reject"",""returnToStage"",""transfer"",""cc""]},""actionPolicy"":{""allowedActions"":[""approve"",""reject"",""returnToStage"",""transfer"",""cc""]}}');

        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5025)
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F节点键],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID],[F处理人策略],[F处理人配置JSON],[F补充字段JSON])
        VALUES (5025, 1350, N'expense_request_region', 3, N'区域负责人审批', N'human', N'card', N'single', NULL, NULL, N'fixedUsers', N'{""users"":[{""userId"":1,""userName"":""管理员""}],""fallback"":{""type"":""flowAdmin""}}', N'{""version"":2,""inputFields"":[],""viewProfile"":{""profileName"":""区域负责人审批"",""fieldAccess"":{""payeeAccountNo"":{""access"":""masked""}},""actions"":[""approve"",""reject"",""returnToStage"",""transfer"",""cc""]},""actionPolicy"":{""allowedActions"":[""approve"",""reject"",""returnToStage"",""transfer"",""cc""]}}');

        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5026)
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F节点键],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID],[F处理人策略],[F处理人配置JSON],[F补充字段JSON])
        VALUES (5026, 2261, N'loan_upper_manager', 2, N'上级负责人审批', N'human', N'card', N'single', NULL, NULL, N'fixedUsers', N'{""users"":[{""userId"":1,""userName"":""管理员""}],""fallback"":{""type"":""flowAdmin""}}', N'{""version"":2,""inputFields"":[],""viewProfile"":{""profileName"":""上级负责人审批"",""fieldAccess"":{""payeeAccountNo"":{""access"":""masked""}},""actions"":[""approve"",""reject"",""returnToStage"",""transfer"",""cc""]},""actionPolicy"":{""allowedActions"":[""approve"",""reject"",""returnToStage"",""transfer"",""cc""]}}');

        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5027)
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F节点键],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID],[F处理人策略],[F处理人配置JSON],[F补充字段JSON])
        VALUES (5027, 2261, N'loan_finance_manager', 3, N'财务经理复核', N'human', N'card', N'single', NULL, NULL, N'fixedUsers', N'{""users"":[{""userId"":1,""userName"":""管理员""}],""fallback"":{""type"":""flowAdmin""}}', N'{""version"":2,""inputFields"":[],""viewProfile"":{""profileName"":""财务经理复核"",""componentAccess"":{""jksq_loan_balance"":{""access"":""readonly""},""jksq_repayment_plan"":{""access"":""readonly""}},""actions"":[""approve"",""reject"",""returnToStage"",""transfer"",""cc""]},""actionPolicy"":{""allowedActions"":[""approve"",""reject"",""returnToStage"",""transfer"",""cc""]}}');

        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5028)
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F节点键],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID],[F处理人策略],[F处理人配置JSON],[F补充字段JSON])
        VALUES (5028, 1347, N'expense_gm', 2, N'总经理审批', N'human', N'card', N'single', NULL, NULL, N'fixedUsers', N'{""users"":[{""userId"":1,""userName"":""管理员""}],""fallback"":{""type"":""flowAdmin""}}', N'{""version"":2,""inputFields"":[],""viewProfile"":{""profileName"":""总经理审批"",""fieldAccess"":{""payeeAccountNo"":{""access"":""masked""},""paymentAccount"":{""access"":""hidden""}},""actions"":[""approve"",""reject"",""returnToStage"",""transfer"",""cc""]},""actionPolicy"":{""allowedActions"":[""approve"",""reject"",""returnToStage"",""transfer"",""cc""]}}');

        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5029)
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F节点键],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID],[F处理人策略],[F处理人配置JSON],[F补充字段JSON])
        VALUES (5029, 1347, N'expense_loan_offset', 4, N'借款冲抵确认', N'human', N'card', N'single', NULL, NULL, N'fixedUsers', N'{""users"":[{""userId"":1,""userName"":""管理员""}],""fallback"":{""type"":""flowAdmin""}}', N'{""version"":2,""inputFields"":[],""viewProfile"":{""profileName"":""借款冲抵确认"",""componentAccess"":{""fybs_loan_offset"":{""access"":""readonly""},""fybs_payment_info"":{""access"":""readonly""}},""actions"":[""approve"",""reject"",""returnToStage"",""transfer"",""cc""]},""actionPolicy"":{""allowedActions"":[""approve"",""reject"",""returnToStage"",""transfer"",""cc""]}}');

        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5030)
        INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F节点键],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID],[F处理人策略],[F处理人配置JSON],[F补充字段JSON])
        VALUES (5030, 1347, N'expense_reconcile', 6, N'核销确认', N'human', N'card', N'single', NULL, NULL, N'fixedUsers', N'{""users"":[{""userId"":1,""userName"":""管理员""}],""fallback"":{""type"":""flowAdmin""}}', N'{""version"":2,""inputFields"":[],""viewProfile"":{""profileName"":""核销确认"",""componentAccess"":{""fybs_budget_status"":{""access"":""readonly""},""fybs_loan_offset"":{""access"":""readonly""},""fybs_payment_info"":{""access"":""readonly""}},""actions"":[""approve"",""reject"",""returnToStage"",""cc""]},""actionPolicy"":{""allowedActions"":[""approve"",""reject"",""returnToStage"",""cc""]}}');

        SET IDENTITY_INSERT [CF流程节点] OFF;
        ");

        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF节点流转规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF节点流转规则] WHERE [F流程版本ID] = 1350 AND [F边键] = N'edge_qksq_region_amount')
        INSERT INTO [CF节点流转规则] ([FID],[F流程版本ID],[F边键],[F来源节点ID],[F来源节点键],[F目标节点ID],[F目标节点键],[F规则名称],[F条件JSON],[F优先级],[F是否默认分支],[F状态],[F失败兜底JSON])
        VALUES (7001, 1350, N'edge_qksq_region_amount', 5020, N'expense_request_approval', 5025, N'expense_request_region', N'大额或项目费用追加区域负责人', N'{""logic"":""and"",""conditions"":[{""field"":""amount"",""operator"":""gte"",""value"":10000}],""metadata"":{""flowCode"":""QKSQ"",""explanation"":""申请金额大于等于 10000，流转到区域负责人审批""}}', 1, 0, N'active', N'{""fallback"":""defaultRoute"",""flowCode"":""QKSQ""}');

        IF NOT EXISTS (SELECT 1 FROM [CF节点流转规则] WHERE [F流程版本ID] = 1350 AND [F边键] = N'edge_qksq_dept_amount')
        INSERT INTO [CF节点流转规则] ([FID],[F流程版本ID],[F边键],[F来源节点ID],[F来源节点键],[F目标节点ID],[F目标节点键],[F规则名称],[F条件JSON],[F优先级],[F是否默认分支],[F状态],[F失败兜底JSON])
        VALUES (7002, 1350, N'edge_qksq_dept_amount', 5020, N'expense_request_approval', 5024, N'expense_request_dept', N'中额费用追加部门负责人', N'{""logic"":""and"",""conditions"":[{""field"":""amount"",""operator"":""gte"",""value"":5000}],""metadata"":{""flowCode"":""QKSQ"",""explanation"":""申请金额大于等于 5000，流转到部门负责人审批""}}', 2, 0, N'active', N'{""fallback"":""defaultRoute"",""flowCode"":""QKSQ""}');

        IF NOT EXISTS (SELECT 1 FROM [CF节点流转规则] WHERE [F流程版本ID] = 1350 AND [F边键] = N'edge_qksq_default_budget')
        INSERT INTO [CF节点流转规则] ([FID],[F流程版本ID],[F边键],[F来源节点ID],[F来源节点键],[F目标节点ID],[F目标节点键],[F规则名称],[F条件JSON],[F优先级],[F是否默认分支],[F状态],[F失败兜底JSON])
        VALUES (7003, 1350, N'edge_qksq_default_budget', 5020, N'expense_request_approval', 5021, N'expense_request_budget', N'其他情况进入财务预算确认', N'{""metadata"":{""flowCode"":""QKSQ"",""explanation"":""其他情况进入财务预算确认""}}', 99, 1, N'active', N'{""fallback"":""defaultRoute"",""flowCode"":""QKSQ""}');

        IF NOT EXISTS (SELECT 1 FROM [CF节点流转规则] WHERE [F流程版本ID] = 1350 AND [F边键] = N'edge_qksq_dept_to_budget')
        INSERT INTO [CF节点流转规则] ([FID],[F流程版本ID],[F边键],[F来源节点ID],[F来源节点键],[F目标节点ID],[F目标节点键],[F规则名称],[F条件JSON],[F优先级],[F是否默认分支],[F状态],[F失败兜底JSON])
        VALUES (7004, 1350, N'edge_qksq_dept_to_budget', 5024, N'expense_request_dept', 5021, N'expense_request_budget', N'部门负责人完成后进入预算确认', N'{""metadata"":{""flowCode"":""QKSQ"",""explanation"":""部门负责人完成后进入财务预算确认""}}', 99, 1, N'active', N'{""fallback"":""end"",""flowCode"":""QKSQ""}');

        IF NOT EXISTS (SELECT 1 FROM [CF节点流转规则] WHERE [F流程版本ID] = 1350 AND [F边键] = N'edge_qksq_region_to_budget')
        INSERT INTO [CF节点流转规则] ([FID],[F流程版本ID],[F边键],[F来源节点ID],[F来源节点键],[F目标节点ID],[F目标节点键],[F规则名称],[F条件JSON],[F优先级],[F是否默认分支],[F状态],[F失败兜底JSON])
        VALUES (7005, 1350, N'edge_qksq_region_to_budget', 5025, N'expense_request_region', 5021, N'expense_request_budget', N'区域负责人完成后进入预算确认', N'{""metadata"":{""flowCode"":""QKSQ"",""explanation"":""区域负责人完成后进入财务预算确认""}}', 99, 1, N'active', N'{""fallback"":""end"",""flowCode"":""QKSQ""}');

        IF NOT EXISTS (SELECT 1 FROM [CF节点流转规则] WHERE [F流程版本ID] = 2261 AND [F边键] = N'edge_jksq_finance_manager_amount')
        INSERT INTO [CF节点流转规则] ([FID],[F流程版本ID],[F边键],[F来源节点ID],[F来源节点键],[F目标节点ID],[F目标节点键],[F规则名称],[F条件JSON],[F优先级],[F是否默认分支],[F状态],[F失败兜底JSON])
        VALUES (7011, 2261, N'edge_jksq_finance_manager_amount', 5022, N'loan_approval', 5027, N'loan_finance_manager', N'大额借款进入财务经理复核', N'{""logic"":""and"",""conditions"":[{""field"":""loanAmount"",""operator"":""gte"",""value"":20000}],""metadata"":{""flowCode"":""JKSQ"",""explanation"":""借款金额大于等于 20000，流转到财务经理复核""}}', 1, 0, N'active', N'{""fallback"":""defaultRoute"",""flowCode"":""JKSQ""}');

        IF NOT EXISTS (SELECT 1 FROM [CF节点流转规则] WHERE [F流程版本ID] = 2261 AND [F边键] = N'edge_jksq_upper_manager_balance')
        INSERT INTO [CF节点流转规则] ([FID],[F流程版本ID],[F边键],[F来源节点ID],[F来源节点键],[F目标节点ID],[F目标节点键],[F规则名称],[F条件JSON],[F优先级],[F是否默认分支],[F状态],[F失败兜底JSON])
        VALUES (7012, 2261, N'edge_jksq_upper_manager_balance', 5022, N'loan_approval', 5026, N'loan_upper_manager', N'存在未还余额追加上级负责人', N'{""logic"":""and"",""conditions"":[{""field"":""outstandingBalance"",""operator"":""gt"",""value"":0}],""metadata"":{""flowCode"":""JKSQ"",""explanation"":""存在未还余额，流转到上级负责人审批""}}', 2, 0, N'active', N'{""fallback"":""defaultRoute"",""flowCode"":""JKSQ""}');

        IF NOT EXISTS (SELECT 1 FROM [CF节点流转规则] WHERE [F流程版本ID] = 2261 AND [F边键] = N'edge_jksq_petty_cash_finance')
        INSERT INTO [CF节点流转规则] ([FID],[F流程版本ID],[F边键],[F来源节点ID],[F来源节点键],[F目标节点ID],[F目标节点键],[F规则名称],[F条件JSON],[F优先级],[F是否默认分支],[F状态],[F失败兜底JSON])
        VALUES (7013, 2261, N'edge_jksq_petty_cash_finance', 5022, N'loan_approval', 5027, N'loan_finance_manager', N'备用金借款进入财务经理复核', N'{""logic"":""and"",""conditions"":[{""field"":""isPettyCash"",""operator"":""eq"",""value"":true}],""metadata"":{""flowCode"":""JKSQ"",""explanation"":""备用金借款流转到财务经理复核""}}', 3, 0, N'active', N'{""fallback"":""defaultRoute"",""flowCode"":""JKSQ""}');

        IF NOT EXISTS (SELECT 1 FROM [CF节点流转规则] WHERE [F流程版本ID] = 2261 AND [F边键] = N'edge_jksq_default_payment')
        INSERT INTO [CF节点流转规则] ([FID],[F流程版本ID],[F边键],[F来源节点ID],[F来源节点键],[F目标节点ID],[F目标节点键],[F规则名称],[F条件JSON],[F优先级],[F是否默认分支],[F状态],[F失败兜底JSON])
        VALUES (7014, 2261, N'edge_jksq_default_payment', 5022, N'loan_approval', 5023, N'loan_payment', N'其他情况进入财务放款确认', N'{""metadata"":{""flowCode"":""JKSQ"",""explanation"":""其他情况进入财务放款确认""}}', 99, 1, N'active', N'{""fallback"":""defaultRoute"",""flowCode"":""JKSQ""}');

        IF NOT EXISTS (SELECT 1 FROM [CF节点流转规则] WHERE [F流程版本ID] = 2261 AND [F边键] = N'edge_jksq_upper_to_payment')
        INSERT INTO [CF节点流转规则] ([FID],[F流程版本ID],[F边键],[F来源节点ID],[F来源节点键],[F目标节点ID],[F目标节点键],[F规则名称],[F条件JSON],[F优先级],[F是否默认分支],[F状态],[F失败兜底JSON])
        VALUES (7015, 2261, N'edge_jksq_upper_to_payment', 5026, N'loan_upper_manager', 5023, N'loan_payment', N'上级负责人完成后进入放款确认', N'{""metadata"":{""flowCode"":""JKSQ"",""explanation"":""上级负责人完成后进入财务放款确认""}}', 99, 1, N'active', N'{""fallback"":""end"",""flowCode"":""JKSQ""}');

        IF NOT EXISTS (SELECT 1 FROM [CF节点流转规则] WHERE [F流程版本ID] = 2261 AND [F边键] = N'edge_jksq_finance_to_payment')
        INSERT INTO [CF节点流转规则] ([FID],[F流程版本ID],[F边键],[F来源节点ID],[F来源节点键],[F目标节点ID],[F目标节点键],[F规则名称],[F条件JSON],[F优先级],[F是否默认分支],[F状态],[F失败兜底JSON])
        VALUES (7016, 2261, N'edge_jksq_finance_to_payment', 5027, N'loan_finance_manager', 5023, N'loan_payment', N'财务经理完成后进入放款确认', N'{""metadata"":{""flowCode"":""JKSQ"",""explanation"":""财务经理完成后进入财务放款确认""}}', 99, 1, N'active', N'{""fallback"":""end"",""flowCode"":""JKSQ""}');

        IF NOT EXISTS (SELECT 1 FROM [CF节点流转规则] WHERE [F流程版本ID] = 1347 AND [F边键] = N'edge_fybs_large_amount_to_finance')
        INSERT INTO [CF节点流转规则] ([FID],[F流程版本ID],[F边键],[F来源节点ID],[F来源节点键],[F目标节点ID],[F目标节点键],[F规则名称],[F条件JSON],[F优先级],[F是否默认分支],[F状态],[F失败兜底JSON])
        VALUES (7021, 1347, N'edge_fybs_large_amount_to_finance', 5011, N'expense_supervisor', 5018, N'expense_finance', N'大额报销触发动态总经理审批', N'{""logic"":""and"",""conditions"":[{""field"":""amount"",""operator"":""gte"",""value"":5000}],""metadata"":{""flowCode"":""FYBS"",""explanation"":""报销金额大于等于 5000，先插入总经理审批再进入财务复核""}}', 1, 0, N'active', N'{""fallback"":""defaultRoute"",""flowCode"":""FYBS""}');

        IF NOT EXISTS (SELECT 1 FROM [CF节点流转规则] WHERE [F流程版本ID] = 1347 AND [F边键] = N'edge_fybs_default_finance')
        INSERT INTO [CF节点流转规则] ([FID],[F流程版本ID],[F边键],[F来源节点ID],[F来源节点键],[F目标节点ID],[F目标节点键],[F规则名称],[F条件JSON],[F优先级],[F是否默认分支],[F状态],[F失败兜底JSON])
        VALUES (7022, 1347, N'edge_fybs_default_finance', 5011, N'expense_supervisor', 5018, N'expense_finance', N'其他情况进入财务复核', N'{""metadata"":{""flowCode"":""FYBS"",""explanation"":""其他情况进入财务复核""}}', 99, 1, N'active', N'{""fallback"":""defaultRoute"",""flowCode"":""FYBS""}');

        IF NOT EXISTS (SELECT 1 FROM [CF节点流转规则] WHERE [F流程版本ID] = 1347 AND [F边键] = N'edge_fybs_zero_pay_skip_payment')
        INSERT INTO [CF节点流转规则] ([FID],[F流程版本ID],[F边键],[F来源节点ID],[F来源节点键],[F目标节点ID],[F目标节点键],[F规则名称],[F条件JSON],[F优先级],[F是否默认分支],[F状态],[F失败兜底JSON])
        VALUES (7023, 1347, N'edge_fybs_zero_pay_skip_payment', 5018, N'expense_finance', 5030, N'expense_reconcile', N'实付为零跳过付款确认', N'{""logic"":""and"",""conditions"":[{""field"":""actualPayAmount"",""operator"":""eq"",""value"":0}],""metadata"":{""flowCode"":""FYBS"",""explanation"":""实付金额为 0，跳过付款确认并进入核销确认""}}', 1, 0, N'active', N'{""fallback"":""defaultRoute"",""flowCode"":""FYBS""}');

        IF NOT EXISTS (SELECT 1 FROM [CF节点流转规则] WHERE [F流程版本ID] = 1347 AND [F边键] = N'edge_fybs_loan_offset_confirm')
        INSERT INTO [CF节点流转规则] ([FID],[F流程版本ID],[F边键],[F来源节点ID],[F来源节点键],[F目标节点ID],[F目标节点键],[F规则名称],[F条件JSON],[F优先级],[F是否默认分支],[F状态],[F失败兜底JSON])
        VALUES (7024, 1347, N'edge_fybs_loan_offset_confirm', 5018, N'expense_finance', 5029, N'expense_loan_offset', N'引用借款且有冲抵金额进入冲抵确认', N'{""logic"":""and"",""conditions"":[{""field"":""loanRef"",""operator"":""notEmpty""},{""field"":""offsetLoanAmount"",""operator"":""gt"",""value"":0}],""metadata"":{""flowCode"":""FYBS"",""explanation"":""引用借款且冲抵借款金额大于 0，进入借款冲抵确认""}}', 2, 0, N'active', N'{""fallback"":""defaultRoute"",""flowCode"":""FYBS""}');

        IF NOT EXISTS (SELECT 1 FROM [CF节点流转规则] WHERE [F流程版本ID] = 1347 AND [F边键] = N'edge_fybs_request_reconcile')
        INSERT INTO [CF节点流转规则] ([FID],[F流程版本ID],[F边键],[F来源节点ID],[F来源节点键],[F目标节点ID],[F目标节点键],[F规则名称],[F条件JSON],[F优先级],[F是否默认分支],[F状态],[F失败兜底JSON])
        VALUES (7025, 1347, N'edge_fybs_request_reconcile', 5018, N'expense_finance', 5030, N'expense_reconcile', N'引用请款单进入核销确认', N'{""logic"":""and"",""conditions"":[{""field"":""requestRef"",""operator"":""notEmpty""}],""metadata"":{""flowCode"":""FYBS"",""explanation"":""引用费用申请，请进入核销确认""}}', 3, 0, N'active', N'{""fallback"":""defaultRoute"",""flowCode"":""FYBS""}');

        IF NOT EXISTS (SELECT 1 FROM [CF节点流转规则] WHERE [F流程版本ID] = 1347 AND [F边键] = N'edge_fybs_finance_default_payment')
        INSERT INTO [CF节点流转规则] ([FID],[F流程版本ID],[F边键],[F来源节点ID],[F来源节点键],[F目标节点ID],[F目标节点键],[F规则名称],[F条件JSON],[F优先级],[F是否默认分支],[F状态],[F失败兜底JSON])
        VALUES (7026, 1347, N'edge_fybs_finance_default_payment', 5018, N'expense_finance', 5019, N'expense_payment', N'其他情况进入付款确认', N'{""metadata"":{""flowCode"":""FYBS"",""explanation"":""其他情况进入付款确认""}}', 99, 1, N'active', N'{""fallback"":""defaultRoute"",""flowCode"":""FYBS""}');

        IF NOT EXISTS (SELECT 1 FROM [CF节点流转规则] WHERE [F流程版本ID] = 1347 AND [F边键] = N'edge_fybs_loan_offset_to_payment')
        INSERT INTO [CF节点流转规则] ([FID],[F流程版本ID],[F边键],[F来源节点ID],[F来源节点键],[F目标节点ID],[F目标节点键],[F规则名称],[F条件JSON],[F优先级],[F是否默认分支],[F状态],[F失败兜底JSON])
        VALUES (7027, 1347, N'edge_fybs_loan_offset_to_payment', 5029, N'expense_loan_offset', 5019, N'expense_payment', N'借款冲抵确认后进入付款确认', N'{""metadata"":{""flowCode"":""FYBS"",""explanation"":""借款冲抵确认后进入付款确认""}}', 99, 1, N'active', N'{""fallback"":""end"",""flowCode"":""FYBS""}');

        SET IDENTITY_INSERT [CF节点流转规则] OFF;
        ");

        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF动态审批策略] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF动态审批策略] WHERE [F流程版本ID] = 1350 AND [F策略键] = N'qksq_amount_matrix')
        INSERT INTO [CF动态审批策略] ([FID],[F流程版本ID],[F策略键],[F触发节点ID],[F触发节点键],[F策略名称],[F策略类型],[F策略配置JSON],[F条件JSON],[F触发时机],[F插入位置],[F续接节点键],[F优先级],[F最大插入数量],[F兜底JSON],[F状态])
        VALUES (8001, 1350, N'qksq_amount_matrix', 5020, N'expense_request_approval', N'费用申请金额矩阵审批', N'amountMatrix', N'{""flowCode"":""QKSQ"",""amountField"":""amount"",""ranges"":[{""min"":5000,""max"":9999,""userIds"":[1],""label"":""部门负责人""},{""min"":10000,""max"":49999,""userIds"":[1],""label"":""区域负责人""},{""min"":50000,""userIds"":[1],""label"":""总部财务负责人""}]}', N'{""logic"":""and"",""conditions"":[{""field"":""amount"",""operator"":""gte"",""value"":5000}],""metadata"":{""flowCode"":""QKSQ"",""explanation"":""金额矩阵命中后运行时插入审批人""}}', N'afterRouteBeforeTarget', N'beforeTarget', N'expense_request_budget', 1, 3, N'{""type"":""flowAdmin"",""flowCode"":""QKSQ""}', N'active');

        IF NOT EXISTS (SELECT 1 FROM [CF动态审批策略] WHERE [F流程版本ID] = 2261 AND [F策略键] = N'jksq_loan_amount_matrix')
        INSERT INTO [CF动态审批策略] ([FID],[F流程版本ID],[F策略键],[F触发节点ID],[F触发节点键],[F策略名称],[F策略类型],[F策略配置JSON],[F条件JSON],[F触发时机],[F插入位置],[F续接节点键],[F优先级],[F最大插入数量],[F兜底JSON],[F状态])
        VALUES (8002, 2261, N'jksq_loan_amount_matrix', 5022, N'loan_approval', N'借款金额矩阵审批', N'amountMatrix', N'{""flowCode"":""JKSQ"",""amountField"":""loanAmount"",""ranges"":[{""min"":0,""max"":9999,""userIds"":[1],""label"":""上级负责人""},{""min"":10000,""userIds"":[1],""label"":""财务经理""}]}', N'{""logic"":""or"",""conditions"":[{""field"":""loanAmount"",""operator"":""gte"",""value"":10000},{""field"":""outstandingBalance"",""operator"":""gt"",""value"":0},{""field"":""isPettyCash"",""operator"":""eq"",""value"":true}],""metadata"":{""flowCode"":""JKSQ"",""explanation"":""借款金额、未还余额或备用金命中后插入审批人""}}', N'afterRouteBeforeTarget', N'beforeTarget', N'loan_payment', 1, 2, N'{""type"":""flowAdmin"",""flowCode"":""JKSQ""}', N'active');

        IF NOT EXISTS (SELECT 1 FROM [CF动态审批策略] WHERE [F流程版本ID] = 1347 AND [F策略键] = N'fybs_large_amount_gm')
        INSERT INTO [CF动态审批策略] ([FID],[F流程版本ID],[F策略键],[F触发节点ID],[F触发节点键],[F策略名称],[F策略类型],[F策略配置JSON],[F条件JSON],[F触发时机],[F插入位置],[F续接节点键],[F优先级],[F最大插入数量],[F兜底JSON],[F状态])
        VALUES (8003, 1347, N'fybs_large_amount_gm', 5011, N'expense_supervisor', N'大额报销总经理审批', N'amountMatrix', N'{""flowCode"":""FYBS"",""amountField"":""amount"",""ranges"":[{""min"":5000,""userIds"":[1],""label"":""总经理审批""}]}', N'{""logic"":""and"",""conditions"":[{""field"":""amount"",""operator"":""gte"",""value"":5000}],""metadata"":{""flowCode"":""FYBS"",""explanation"":""报销金额大于等于 5000 时运行时插入总经理审批""}}', N'afterRouteBeforeTarget', N'beforeTarget', N'expense_finance', 1, 1, N'{""type"":""flowAdmin"",""flowCode"":""FYBS""}', N'active');

        SET IDENTITY_INSERT [CF动态审批策略] OFF;
        ");
    }

    private static void MigrateV18(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        ExecSql(ctx, @"
DECLARE @UploadCenterMenuId BIGINT;
SELECT @UploadCenterMenuId = [FID]
FROM [SYS功能权限]
WHERE [F编码] = N'cardflow:upload-center' AND [F类型] = N'菜单';

IF @UploadCenterMenuId IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码] = N'cardflow:import:validation')
BEGIN
    INSERT INTO [SYS功能权限] ([F名称],[F编码],[F类型],[F父ID],[F排序],[F状态],[F是否可见])
    VALUES (N'验证导入计算',N'cardflow:import:validation',N'按钮',@UploadCenterMenuId,3,1,0);
END

INSERT INTO [SYS角色权限] ([F角色ID], [F权限ID])
SELECT 1, p.FID
FROM [SYS功能权限] p
WHERE p.[F编码] = N'cardflow:import:validation'
AND NOT EXISTS (
    SELECT 1 FROM [SYS角色权限]
    WHERE [F角色ID] = 1 AND [F权限ID] = p.FID
);
	");
    }

    private static void MigrateV19(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        ExecSql(ctx, @"
DECLARE @StageKeyFix TABLE (
    FID BIGINT NOT NULL PRIMARY KEY,
    OldStageKey NVARCHAR(80) NULL,
    NewStageKey NVARCHAR(80) NOT NULL
);

;WITH MissingStageKeys AS (
    SELECT
        [FID],
        [F流程版本ID],
        [F节点键],
        ROW_NUMBER() OVER (PARTITION BY [F流程版本ID] ORDER BY [F排序号], [FID]) AS RowNo
    FROM [dbo].[CF流程节点]
    WHERE NULLIF(LTRIM(RTRIM(ISNULL([F节点键], N''))), N'') IS NULL
)
INSERT INTO @StageKeyFix (FID, OldStageKey, NewStageKey)
SELECT
    [FID],
    [F节点键],
    CONCAT(N'stage_', [F流程版本ID], N'_', RowNo, N'_', [FID])
FROM MissingStageKeys;

UPDATE stage
   SET [F节点键] = fix.NewStageKey
  FROM [dbo].[CF流程节点] stage
  JOIN @StageKeyFix fix ON fix.FID = stage.[FID];

UPDATE route
   SET [F来源节点键] = fix.NewStageKey
  FROM [dbo].[CF节点流转规则] route
  JOIN @StageKeyFix fix ON fix.FID = route.[F来源节点ID]
 WHERE ISNULL(route.[F来源节点键], N'') = ISNULL(fix.OldStageKey, N'');

UPDATE route
   SET [F目标节点键] = fix.NewStageKey
  FROM [dbo].[CF节点流转规则] route
  JOIN @StageKeyFix fix ON fix.FID = route.[F目标节点ID]
 WHERE ISNULL(route.[F目标节点键], N'') = ISNULL(fix.OldStageKey, N'');

UPDATE policy
   SET [F触发节点键] = fix.NewStageKey
  FROM [dbo].[CF动态审批策略] policy
  JOIN @StageKeyFix fix ON fix.FID = policy.[F触发节点ID]
 WHERE ISNULL(policy.[F触发节点键], N'') = ISNULL(fix.OldStageKey, N'');

DELETE FROM @StageKeyFix;

;WITH RankedStageKeys AS (
    SELECT
        [FID],
        [F流程版本ID],
        [F节点键],
        ROW_NUMBER() OVER (
            PARTITION BY [F流程版本ID], [F节点键]
            ORDER BY
                CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM [dbo].[CF节点流转规则] route
                        WHERE route.[F流程版本ID] = stage.[F流程版本ID]
                          AND (
                              (route.[F来源节点ID] = stage.[FID] AND route.[F来源节点键] = stage.[F节点键])
                              OR (route.[F目标节点ID] = stage.[FID] AND route.[F目标节点键] = stage.[F节点键])
                          )
                    )
                    OR EXISTS (
                        SELECT 1
                        FROM [dbo].[CF动态审批策略] policy
                        WHERE policy.[F流程版本ID] = stage.[F流程版本ID]
                          AND policy.[F触发节点ID] = stage.[FID]
                          AND policy.[F触发节点键] = stage.[F节点键]
                    )
                    THEN 0 ELSE 1
                END,
                [FID]
        ) AS RowNo
    FROM [dbo].[CF流程节点] stage
    WHERE NULLIF(LTRIM(RTRIM(ISNULL([F节点键], N''))), N'') IS NOT NULL
)
INSERT INTO @StageKeyFix (FID, OldStageKey, NewStageKey)
SELECT
    [FID],
    [F节点键],
    CONCAT(LEFT([F节点键], 60), N'_', [FID])
FROM RankedStageKeys
WHERE RowNo > 1;

UPDATE stage
   SET [F节点键] = fix.NewStageKey
  FROM [dbo].[CF流程节点] stage
  JOIN @StageKeyFix fix ON fix.FID = stage.[FID];

UPDATE route
   SET [F来源节点键] = fix.NewStageKey
  FROM [dbo].[CF节点流转规则] route
  JOIN @StageKeyFix fix ON fix.FID = route.[F来源节点ID]
 WHERE route.[F来源节点键] = fix.OldStageKey;

UPDATE route
   SET [F目标节点键] = fix.NewStageKey
  FROM [dbo].[CF节点流转规则] route
  JOIN @StageKeyFix fix ON fix.FID = route.[F目标节点ID]
 WHERE route.[F目标节点键] = fix.OldStageKey;

UPDATE policy
   SET [F触发节点键] = fix.NewStageKey
  FROM [dbo].[CF动态审批策略] policy
  JOIN @StageKeyFix fix ON fix.FID = policy.[F触发节点ID]
 WHERE policy.[F触发节点键] = fix.OldStageKey;
");
    }

    /// <summary>
    /// V20: 流程节点重复键兜底清理。
    /// 与 V19 逻辑相同（空键回填 + 重复键重命名 + 同步流转规则/动态审批策略引用），
    /// 但包在收敛循环里：V19 的重命名结果理论上可能与已有键再次碰撞（如已存在键恰为
    /// LEFT(重复键,60)_FID），单轮处理无法保证收敛；本版本每轮先检查是否仍存在
    /// 空键或 (F流程版本ID, F节点键) 重复组，干净即退出，最多 5 轮。
    /// 同时兜底修复 V19 执行后数据被再次弄脏的存量库，确保启动时
    /// IX_CF流程节点_版本节点键 唯一索引可以创建。
    /// </summary>
    private static void MigrateV20(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        ExecSql(ctx, @"
DECLARE @StageKeyFix TABLE (
    FID BIGINT NOT NULL PRIMARY KEY,
    OldStageKey NVARCHAR(80) NULL,
    NewStageKey NVARCHAR(80) NOT NULL
);
DECLARE @Round INT = 0;

WHILE @Round < 5
BEGIN
    SET @Round += 1;

    IF NOT EXISTS (
        SELECT 1
        FROM [dbo].[CF流程节点]
        WHERE NULLIF(LTRIM(RTRIM(ISNULL([F节点键], N''))), N'') IS NULL
    )
    AND NOT EXISTS (
        SELECT 1
        FROM [dbo].[CF流程节点]
        GROUP BY [F流程版本ID], [F节点键]
        HAVING COUNT(*) > 1
    )
        BREAK;

    DELETE FROM @StageKeyFix;

    ;WITH MissingStageKeys AS (
        SELECT
            [FID],
            [F流程版本ID],
            [F节点键],
            ROW_NUMBER() OVER (PARTITION BY [F流程版本ID] ORDER BY [F排序号], [FID]) AS RowNo
        FROM [dbo].[CF流程节点]
        WHERE NULLIF(LTRIM(RTRIM(ISNULL([F节点键], N''))), N'') IS NULL
    )
    INSERT INTO @StageKeyFix (FID, OldStageKey, NewStageKey)
    SELECT
        [FID],
        [F节点键],
        CONCAT(N'stage_', [F流程版本ID], N'_', RowNo, N'_', [FID])
    FROM MissingStageKeys;

    UPDATE stage
       SET [F节点键] = fix.NewStageKey
      FROM [dbo].[CF流程节点] stage
      JOIN @StageKeyFix fix ON fix.FID = stage.[FID];

    UPDATE route
       SET [F来源节点键] = fix.NewStageKey
      FROM [dbo].[CF节点流转规则] route
      JOIN @StageKeyFix fix ON fix.FID = route.[F来源节点ID]
     WHERE ISNULL(route.[F来源节点键], N'') = ISNULL(fix.OldStageKey, N'');

    UPDATE route
       SET [F目标节点键] = fix.NewStageKey
      FROM [dbo].[CF节点流转规则] route
      JOIN @StageKeyFix fix ON fix.FID = route.[F目标节点ID]
     WHERE ISNULL(route.[F目标节点键], N'') = ISNULL(fix.OldStageKey, N'');

    UPDATE policy
       SET [F触发节点键] = fix.NewStageKey
      FROM [dbo].[CF动态审批策略] policy
      JOIN @StageKeyFix fix ON fix.FID = policy.[F触发节点ID]
     WHERE ISNULL(policy.[F触发节点键], N'') = ISNULL(fix.OldStageKey, N'');

    DELETE FROM @StageKeyFix;

    ;WITH RankedStageKeys AS (
        SELECT
            [FID],
            [F流程版本ID],
            [F节点键],
            ROW_NUMBER() OVER (
                PARTITION BY [F流程版本ID], [F节点键]
                ORDER BY
                    CASE
                        WHEN EXISTS (
                            SELECT 1
                            FROM [dbo].[CF节点流转规则] route
                            WHERE route.[F流程版本ID] = stage.[F流程版本ID]
                              AND (
                                  (route.[F来源节点ID] = stage.[FID] AND route.[F来源节点键] = stage.[F节点键])
                                  OR (route.[F目标节点ID] = stage.[FID] AND route.[F目标节点键] = stage.[F节点键])
                              )
                        )
                        OR EXISTS (
                            SELECT 1
                            FROM [dbo].[CF动态审批策略] policy
                            WHERE policy.[F流程版本ID] = stage.[F流程版本ID]
                              AND policy.[F触发节点ID] = stage.[FID]
                              AND policy.[F触发节点键] = stage.[F节点键]
                        )
                        THEN 0 ELSE 1
                    END,
                    [FID]
            ) AS RowNo
        FROM [dbo].[CF流程节点] stage
        WHERE NULLIF(LTRIM(RTRIM(ISNULL([F节点键], N''))), N'') IS NOT NULL
    )
    INSERT INTO @StageKeyFix (FID, OldStageKey, NewStageKey)
    SELECT
        [FID],
        [F节点键],
        CONCAT(LEFT([F节点键], 60), N'_', [FID])
    FROM RankedStageKeys
    WHERE RowNo > 1;

    UPDATE stage
       SET [F节点键] = fix.NewStageKey
      FROM [dbo].[CF流程节点] stage
      JOIN @StageKeyFix fix ON fix.FID = stage.[FID];

    UPDATE route
       SET [F来源节点键] = fix.NewStageKey
      FROM [dbo].[CF节点流转规则] route
      JOIN @StageKeyFix fix ON fix.FID = route.[F来源节点ID]
     WHERE route.[F来源节点键] = fix.OldStageKey;

    UPDATE route
       SET [F目标节点键] = fix.NewStageKey
      FROM [dbo].[CF节点流转规则] route
      JOIN @StageKeyFix fix ON fix.FID = route.[F目标节点ID]
     WHERE route.[F目标节点键] = fix.OldStageKey;

    UPDATE policy
       SET [F触发节点键] = fix.NewStageKey
      FROM [dbo].[CF动态审批策略] policy
      JOIN @StageKeyFix fix ON fix.FID = policy.[F触发节点ID]
     WHERE policy.[F触发节点键] = fix.OldStageKey;
END
");
    }

    private static void MigrateV21(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = N'CF卡片流程' AND COLUMN_NAME = N'F是否模板')
        ALTER TABLE [CF卡片流程] ADD [F是否模板] bit NOT NULL CONSTRAINT [DF_CF卡片流程_F是否模板] DEFAULT 0;
        ");
    }

    private static void MigrateV22(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // 1) 流程定义（FOrgId=0，F是否模板=1，published）
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2262)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID],[F乐观锁],[F创建人ID],[F创建时间],[F可发起角色JSON],[F描述],[F更新时间],[F标题模板],[F流程名称],[F流程组ID],[F流程编码],[F状态],[F组织ID],[F编号模板],[F触发配置JSON],[F账套ID],[F匹配规则],[F是否模板])
            VALUES (2262, NULL, 0, GETDATE(), NULL, N'费用报销审批模板（部门负责人→财务）', GETDATE(), N'{发起人}-费用报销-{金额}元', N'费用报销（模板）', NULL, N'FYBS_TEMPLATE', N'published', 0, N'FYBSTPL-{yyyy}{MM}{dd}-{seq}', NULL, NULL, NULL, 1);
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // 2) 流程版本（current、published，复用 FYBS 扁平 schema）
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2263)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID],[F创建人ID],[F创建时间],[F卡片SchemaJSON],[F发布时间],[F明细SchemaJSON],[F是否当前版本],[F流程定义ID],[F流程设置JSON],[F版本号],[F状态])
            VALUES (2263, 0, GETDATE(), N'[{""key"":""applicant"",""label"":""申请人"",""type"":""user"",""required"":true},{""key"":""department"",""label"":""部门"",""type"":""department"",""required"":true},{""key"":""amount"",""label"":""报销金额"",""type"":""amount"",""required"":true,""source"":""detailSum""},{""key"":""category"",""label"":""费用类别"",""type"":""select"",""required"":true,""options"":[""差旅费"",""办公费"",""招待费"",""交通费"",""通讯费"",""其他""]},{""key"":""description"",""label"":""报销说明"",""type"":""textarea"",""required"":true},{""key"":""attachments"",""label"":""附件"",""type"":""attachment"",""required"":false}]', GETDATE(), N'[{""key"":""expenseDate"",""label"":""费用日期"",""type"":""date"",""required"":true},{""key"":""expenseType"",""label"":""费用类型"",""type"":""select"",""required"":true,""options"":[""差旅费"",""办公费"",""招待费"",""交通费"",""通讯费"",""住宿费"",""餐费"",""其他""]},{""key"":""description"",""label"":""费用说明"",""type"":""text"",""required"":true},{""key"":""amount"",""label"":""金额"",""type"":""amount"",""required"":true},{""key"":""invoiceNo"",""label"":""发票号"",""type"":""text"",""required"":false}]', 1, 2262, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // 3) 部门负责人审批节点（fixedUsers→管理员占位）
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5031)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F节点键],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID],[F处理人策略],[F处理人配置JSON])
            VALUES (5031, 2263, N'tpl_expense_dept', 1, N'部门负责人审批', N'human', N'card', N'single', NULL, NULL, N'fixedUsers', N'{""users"":[{""userId"":1,""userName"":""管理员""}],""fallback"":{""mode"":""fixedUsers"",""users"":[{""userId"":1,""userName"":""管理员""}]}}');
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");

        // 4) 财务审批节点（fixedUsers→管理员占位）
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5032)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID],[F流程版本ID],[F节点键],[F排序号],[F节点名称],[F类型],[F处理粒度],[F审批模式],[F插件注册ID],[F插件规则ID],[F处理人策略],[F处理人配置JSON])
            VALUES (5032, 2263, N'tpl_expense_finance', 2, N'财务审批', N'human', N'card', N'single', NULL, NULL, N'fixedUsers', N'{""users"":[{""userId"":1,""userName"":""管理员""}],""fallback"":{""mode"":""fixedUsers"",""users"":[{""userId"":1,""userName"":""管理员""}]}}');
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }

    private static void MigrateV23(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // 1. 建表 STG申通派件日明细（STG 前缀表 EF 不自动建，必须显式 CREATE）
        //    列名/类型逐字对齐 A1 实体 StgShentongDeliveryDailyConfiguration；件量 INT、金额 DECIMAL(18,4)、F结算日期 DATE
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通派件日明细')
        CREATE TABLE [STG申通派件日明细] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3006 columnMapping）
            [F结算日期] DATE NULL,
            [F网点编号] NVARCHAR(50) NULL,
            [F网点名称] NVARCHAR(200) NULL,
            [F承包区编号] NVARCHAR(50) NULL,
            [F承包区名称] NVARCHAR(200) NULL,
            [F业务员编码] NVARCHAR(50) NULL,
            [F业务员名称] NVARCHAR(200) NULL,
            [F基础派费收费件量] INT NULL,
            [F基础派费收费金额] DECIMAL(18,4) NULL,
            [F基础派费收费调整金额] DECIMAL(18,4) NULL,
            [F正常派件退费收金额] DECIMAL(18,4) NULL,
            [F周期性派费收金额] DECIMAL(18,4) NULL,
            [F大货计重收费金额] DECIMAL(18,4) NULL,
            [F违规重量罚款收件量] INT NULL,
            [F违规重量罚款收金额] DECIMAL(18,4) NULL,
            [F基础派费和时效拦截弃件付费件量合计] INT NULL,
            [F基础派费和时效拦截弃件付费金额合计] DECIMAL(18,4) NULL,
            [F综合KPI奖励派费件量] INT NULL,
            [F综合KPI奖励派费金额] DECIMAL(18,4) NULL,
            [F考核奖励派费金额] DECIMAL(18,4) NULL,
            [F补贴派费付费金额] DECIMAL(18,4) NULL,
            [F周期性派费付费金额] DECIMAL(18,4) NULL,
            [F大货计重付费金额] DECIMAL(18,4) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通派件日明细_F批次ID' AND object_id = OBJECT_ID(N'STG申通派件日明细'))
        CREATE INDEX [IX_STG申通派件日明细_F批次ID] ON [STG申通派件日明细]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通派件日明细_数据作用域' AND object_id = OBJECT_ID(N'STG申通派件日明细'))
        CREATE INDEX [IX_STG申通派件日明细_数据作用域] ON [STG申通派件日明细]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通派件日明细_去重' AND object_id = OBJECT_ID(N'STG申通派件日明细'))
        CREATE UNIQUE INDEX [UX_STG申通派件日明细_去重] ON [STG申通派件日明细]([FOrgId],[F结算日期],[F网点编号],[F业务员编码]) WHERE [FIsRevoked] = 0 AND [F网点编号] IS NOT NULL AND [F业务员编码] IS NOT NULL;

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通派件日明细_取数' AND object_id = OBJECT_ID(N'STG申通派件日明细'))
        CREATE INDEX [IX_STG申通派件日明细_取数] ON [STG申通派件日明细]([FOrgId],[F结算日期],[F归属网点编号]) INCLUDE ([F基础派费收费件量]);
        ");

        // 2. 导入规则 CF自动插件_规则（FID=3006；F组织ID 占位 192，上线前确认派件文件实际上传组织）
        //    headerRow=1 / dataStartRow=4（前3行重复表头）；crossBatchDedupEnabled=false（去重方案B靠唯一索引）；不配 dateFields / serialNumberRule
        //    ⚠ excelColumn 须按真实派件文件 row1 表头逐字核对（当前按 spec §A1 标准名）
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;
        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3006)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3006, 192, N'excelInput', N'申通派件日明细导入规则',
        N'{""targetTable"":""STG申通派件日明细"",""outputMode"":""stg"",""headerRow"":1,""dataStartRow"":4,""columnIdentifier"":""结算日期,网点编号,业务员编码,基础派费收费件量"",""fullColumnIdentifier"":""结算日期,网点编号,网点名称,承包区编号,业务员编码,业务员名称,基础派费收费件量,基础派费收费金额"",""columnMapping"":[{""excelColumn"":""结算日期"",""dbColumn"":""F结算日期""},{""excelColumn"":""网点编号"",""dbColumn"":""F网点编号""},{""excelColumn"":""网点名称"",""dbColumn"":""F网点名称""},{""excelColumn"":""承包区编号"",""dbColumn"":""F承包区编号""},{""excelColumn"":""承包区名称"",""dbColumn"":""F承包区名称""},{""excelColumn"":""业务员编码"",""dbColumn"":""F业务员编码""},{""excelColumn"":""业务员名称"",""dbColumn"":""F业务员名称""},{""excelColumn"":""基础派费收费件量"",""dbColumn"":""F基础派费收费件量""},{""excelColumn"":""基础派费收费金额"",""dbColumn"":""F基础派费收费金额""},{""excelColumn"":""基础派费收费调整金额"",""dbColumn"":""F基础派费收费调整金额""},{""excelColumn"":""正常派件退费收金额"",""dbColumn"":""F正常派件退费收金额""},{""excelColumn"":""周期性派费收金额"",""dbColumn"":""F周期性派费收金额""},{""excelColumn"":""大货计重收费金额"",""dbColumn"":""F大货计重收费金额""},{""excelColumn"":""违规重量罚款收件量"",""dbColumn"":""F违规重量罚款收件量""},{""excelColumn"":""违规重量罚款收金额"",""dbColumn"":""F违规重量罚款收金额""},{""excelColumn"":""基础派费和时效拦截弃件付费件量合计"",""dbColumn"":""F基础派费和时效拦截弃件付费件量合计""},{""excelColumn"":""基础派费和时效拦截弃件付费金额合计"",""dbColumn"":""F基础派费和时效拦截弃件付费金额合计""},{""excelColumn"":""综合KPI奖励派费件量"",""dbColumn"":""F综合KPI奖励派费件量""},{""excelColumn"":""综合KPI奖励派费金额"",""dbColumn"":""F综合KPI奖励派费金额""},{""excelColumn"":""考核奖励派费金额"",""dbColumn"":""F考核奖励派费金额""},{""excelColumn"":""补贴派费付费金额"",""dbColumn"":""F补贴派费付费金额""},{""excelColumn"":""周期性派费付费金额"",""dbColumn"":""F周期性派费付费金额""},{""excelColumn"":""大货计重付费金额"",""dbColumn"":""F大货计重付费金额""}],""decimalFields"":[""基础派费收费金额"",""基础派费收费调整金额"",""正常派件退费收金额"",""周期性派费收金额"",""大货计重收费金额"",""违规重量罚款收金额"",""基础派费和时效拦截弃件付费金额合计"",""综合KPI奖励派费金额"",""考核奖励派费金额"",""补贴派费付费金额"",""周期性派费付费金额"",""大货计重付费金额""],""keyFields"":[""结算日期"",""网点编号"",""业务员编码""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""transformRules"":[],""crossBatchDedupEnabled"":false,""batchSplit"":{""enabled"":false}}',
        1, N'申通派件日明细Excel导入配置（dataStartRow=4 前3行重复表头；去重方案B靠唯一索引，重复导入整批失败）', REPLACE(NEWID(),'-',''), GETDATE());
        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // 3. 流程三件套（CF卡片流程=2264 / CF流程版本=2265 / CF流程节点=5033；F组织ID 占位 192，上线前确认派件文件实际上传组织）
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF卡片流程] ON;
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2264)
        INSERT INTO [CF卡片流程] ([FID],[F乐观锁],[F创建人ID],[F创建时间],[F可发起角色JSON],[F描述],[F更新时间],[F标题模板],[F流程名称],[F流程组ID],[F流程编码],[F状态],[F组织ID],[F编号模板],[F触发配置JSON],[F账套ID],[F匹配规则],[F是否模板])
        VALUES (2264, NULL, 1, GETDATE(), NULL, N'申通派件日明细导入：Excel导入 → STG申通派件日明细（按件量分摊取数 deliver）', GETDATE(), NULL, N'申通派件日明细导入', NULL, N'PL_ST_DELIVERY_DAILY', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""*派件*""}', 0);
        SET IDENTITY_INSERT [CF卡片流程] OFF;

        SET IDENTITY_INSERT [CF流程版本] ON;
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2265)
        INSERT INTO [CF流程版本] ([FID],[F创建人ID],[F创建时间],[F卡片SchemaJSON],[F发布时间],[F明细SchemaJSON],[F是否当前版本],[F流程定义ID],[F流程设置JSON],[F版本号],[F状态])
        VALUES (2265, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2264, NULL, 1, N'published');
        SET IDENTITY_INSERT [CF流程版本] OFF;

        SET IDENTITY_INSERT [CF流程节点] ON;
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5033)
        INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
        VALUES (5033, 2265, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3006);
        SET IDENTITY_INSERT [CF流程节点] OFF;
        ");
    }

    private static void MigrateV50(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_物流完整性明细 暂存表（系统列 + 17 业务列 + 标准字段） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_物流完整性明细')
        CREATE TABLE [STG申通_物流完整性明细] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3101 columnMapping，17 列）
            [F统计日期] NVARCHAR(100) NULL,
            [F运单号] NVARCHAR(200) NULL,
            [F网点名称] NVARCHAR(200) NULL,
            [F所属网点名称] NVARCHAR(200) NULL,
            [F问题类型] NVARCHAR(50) NULL,
            [F订单网点] NVARCHAR(200) NULL,
            [F订单平台] NVARCHAR(100) NULL,
            [F订单时间] NVARCHAR(100) NULL,
            [F揽收时间] NVARCHAR(100) NULL,
            [F揽收网点] NVARCHAR(200) NULL,
            [F派件时间] NVARCHAR(100) NULL,
            [F派件网点] NVARCHAR(200) NULL,
            [F签收时间] NVARCHAR(100) NULL,
            [F签收网点] NVARCHAR(200) NULL,
            [F是否黑土共配] NVARCHAR(20) NULL,
            [F签收员编号] NVARCHAR(100) NULL,
            [F签收员名称] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_物流完整性明细_F批次ID' AND object_id = OBJECT_ID(N'STG申通_物流完整性明细'))
        CREATE INDEX [IX_STG申通_物流完整性明细_F批次ID] ON [STG申通_物流完整性明细]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_物流完整性明细_数据作用域' AND object_id = OBJECT_ID(N'STG申通_物流完整性明细'))
        CREATE INDEX [IX_STG申通_物流完整性明细_数据作用域] ON [STG申通_物流完整性明细]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（运单号 + 问题类型 + 组织，仅未撤销 + 运单号非空）
        -- 物流完整性是问题件清单，按「事件身份」(运单号+问题类型) 去重、忽略导出统计日期；
        -- 去重字段降为 2 个以兼容 ExcelInputPlugin（其跨批去重仅正确支持 ≤2 字段）。
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_物流完整性明细_运单问题日期_未撤销' AND object_id = OBJECT_ID(N'STG申通_物流完整性明细'))
        CREATE UNIQUE INDEX [UX_STG申通_物流完整性明细_运单问题日期_未撤销]
            ON [STG申通_物流完整性明细]([F运单号],[F问题类型],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3101（物流完整性明细导入） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3101)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3101, 192, N'excelInput', N'申通物流完整性明细导入规则',
        N'{""targetTable"":""STG申通_物流完整性明细"",""outputMode"":""stg"",""headerRow"":1,""dataStartRow"":2,""columnIdentifier"":""统计日期,运单号,问题类型,订单网点,签收时间"",""fullColumnIdentifier"":""统计日期,运单号,网点名称,所属网点名称,问题类型,订单网点,订单平台,订单时间,揽收时间,揽收网点,派件时间,派件网点,签收时间,签收网点,是否黑土共配,签收员编号,签收员名称"",""columnMapping"":[{""excelColumn"":""统计日期"",""dbColumn"":""F统计日期""},{""excelColumn"":""运单号"",""dbColumn"":""F运单号""},{""excelColumn"":""网点名称"",""dbColumn"":""F网点名称""},{""excelColumn"":""所属网点名称"",""dbColumn"":""F所属网点名称""},{""excelColumn"":""问题类型"",""dbColumn"":""F问题类型""},{""excelColumn"":""订单网点"",""dbColumn"":""F订单网点""},{""excelColumn"":""订单平台"",""dbColumn"":""F订单平台""},{""excelColumn"":""订单时间"",""dbColumn"":""F订单时间""},{""excelColumn"":""揽收时间"",""dbColumn"":""F揽收时间""},{""excelColumn"":""揽收网点"",""dbColumn"":""F揽收网点""},{""excelColumn"":""派件时间"",""dbColumn"":""F派件时间""},{""excelColumn"":""派件网点"",""dbColumn"":""F派件网点""},{""excelColumn"":""签收时间"",""dbColumn"":""F签收时间""},{""excelColumn"":""签收网点"",""dbColumn"":""F签收网点""},{""excelColumn"":""是否黑土共配"",""dbColumn"":""F是否黑土共配""},{""excelColumn"":""签收员编号"",""dbColumn"":""F签收员编号""},{""excelColumn"":""签收员名称"",""dbColumn"":""F签收员名称""}],""keyFields"":[""运单号"",""问题类型""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F运单号"",""F问题类型""],""batchSplit"":{""enabled"":false}}',
        1, N'申通物流完整性明细（未揽收/未到件/未派件）Excel导入配置', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2301（QC_ST_LOGISTICS_COMPLETENESS） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2301)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2301, NULL, 1, GETDATE(), NULL, N'网点质控：申通物流完整性明细（未揽收/未到件/未派件）导入暂存', GETDATE(), NULL, N'申通物流完整性明细导入', NULL, N'QC_ST_LOGISTICS_COMPLETENESS', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""*未*件*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2301（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2301)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2301, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2301, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5101（ExcelInput 批次级自动节点，插件注册=1，规则=3101） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5101)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5101, 2301, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3101);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }
    private static void MigrateV52(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;
        IF EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 21)
        BEGIN
            UPDATE [CF自动插件_规则]
            SET [F规则配置V1备份] = COALESCE([F规则配置V1备份], [F规则配置JSON]),
                [F规则配置JSON]   = N'{
  ""mode"": ""rulesBased"",
  ""version"": 2,
  ""voucherWord"": ""记"",
  ""dateField"": ""F业务日期"",
  ""stagingTable"": ""STG申通总部交易明细"",
  ""accountSetId"": 2,
  ""keyFields"": [
    ""F科目编码"",
    ""F费用名称"",
    ""F业务摘要""
  ],
  ""unmatchedAction"": ""createDraft"",
  ""filterConditions"": [],
  ""matchingLayers"": {
    ""exactMatchField"": ""F科目编码"",
    ""categoryField"": ""F费用名称"",
    ""summaryField"": ""F业务摘要""
  },
  ""ruleGroups"": [
    {
      ""id"": ""g001"",
      ""name"": ""1122.01 面单服务费"",
      ""order"": 1,
      ""exactCodes"": [
        ""1122.01""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700341,
          ""accountCode"": ""54010101"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g002"",
      ""name"": ""1122.01.05 提现服务费收费"",
      ""order"": 2,
      ""exactCodes"": [
        ""1122.01.05""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700272,
          ""accountCode"": ""560302"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g003"",
      ""name"": ""1122.02 辅料销售"",
      ""order"": 3,
      ""exactCodes"": [
        ""1122.02""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700384,
          ""accountCode"": ""54010801"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g004"",
      ""name"": ""1122.03 基础派费收费"",
      ""order"": 4,
      ""exactCodes"": [
        ""1122.03""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700342,
          ""accountCode"": ""54010201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g005"",
      ""name"": ""1122.03.01 大货计重收费"",
      ""order"": 5,
      ""exactCodes"": [
        ""1122.03.01""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700344,
          ""accountCode"": ""54010203"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g006"",
      ""name"": ""1122.03.02 周期性派费收费"",
      ""order"": 6,
      ""exactCodes"": [
        ""1122.03.02""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700345,
          ""accountCode"": ""54010204"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g007"",
      ""name"": ""1122.03.04 派费预收"",
      ""order"": 7,
      ""exactCodes"": [
        ""1122.03.04""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700342,
          ""accountCode"": ""54010201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g008"",
      ""name"": ""1122.04.02 中转费"",
      ""order"": 8,
      ""exactCodes"": [
        ""1122.04.02""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700354,
          ""accountCode"": ""54010301"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g009"",
      ""name"": ""1122.04.06 全网出港费"",
      ""order"": 9,
      ""exactCodes"": [
        ""1122.04.06""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700357,
          ""accountCode"": ""54010304"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g010"",
      ""name"": ""1122.04.12 出港操作费"",
      ""order"": 10,
      ""exactCodes"": [
        ""1122.04.12""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700362,
          ""accountCode"": ""54010404"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g011"",
      ""name"": ""1122.04.15 退件操作费"",
      ""order"": 11,
      ""exactCodes"": [
        ""1122.04.15""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700361,
          ""accountCode"": ""54010403"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g012"",
      ""name"": ""1122.04.16 退件中转费"",
      ""order"": 12,
      ""exactCodes"": [
        ""1122.04.16""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700358,
          ""accountCode"": ""54010305"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g013"",
      ""name"": ""1122.04.21 操作装卸费"",
      ""order"": 13,
      ""exactCodes"": [
        ""1122.04.21""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700363,
          ""accountCode"": ""54010405"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g014"",
      ""name"": ""1122.04.22 大货操作费"",
      ""order"": 14,
      ""exactCodes"": [
        ""1122.04.22""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700362,
          ""accountCode"": ""54010404"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g015"",
      ""name"": ""1122.04.24 进港集包应收"",
      ""order"": 15,
      ""exactCodes"": [
        ""1122.04.24""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700359,
          ""accountCode"": ""54010401"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g016"",
      ""name"": ""1122.04.26 托管接管服务费"",
      ""order"": 16,
      ""exactCodes"": [
        ""1122.04.26""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700362,
          ""accountCode"": ""54010404"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g017"",
      ""name"": ""1122.04.31 短驳费"",
      ""order"": 17,
      ""exactCodes"": [
        ""1122.04.31""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700358,
          ""accountCode"": ""54010305"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g018"",
      ""name"": ""1122.04.37 保价应收"",
      ""order"": 18,
      ""exactCodes"": [
        ""1122.04.37""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700362,
          ""accountCode"": ""54010404"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g019"",
      ""name"": ""1122.04.40 异形件操作费"",
      ""order"": 19,
      ""exactCodes"": [
        ""1122.04.40""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700362,
          ""accountCode"": ""54010404"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g020"",
      ""name"": ""1122.04.41 批货收费"",
      ""order"": 20,
      ""exactCodes"": [
        ""1122.04.41""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700362,
          ""accountCode"": ""54010404"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g021"",
      ""name"": ""1122.05.01 遗失件赔款"",
      ""order"": 21,
      ""exactCodes"": [
        ""1122.05.01""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700382,
          ""accountCode"": ""54010701"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g022"",
      ""name"": ""1122.05.02 破损件赔款"",
      ""order"": 22,
      ""exactCodes"": [
        ""1122.05.02""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700382,
          ""accountCode"": ""54010701"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g023"",
      ""name"": ""1122.05.03 延误件赔款"",
      ""order"": 23,
      ""exactCodes"": [
        ""1122.05.03""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700382,
          ""accountCode"": ""54010701"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g024"",
      ""name"": ""1122.05.04 超派件赔款"",
      ""order"": 24,
      ""exactCodes"": [
        ""1122.05.04""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700382,
          ""accountCode"": ""54010701"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g025"",
      ""name"": ""1122.05.07 工单服务质量考核"",
      ""order"": 25,
      ""exactCodes"": [
        ""1122.05.07""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700375,
          ""accountCode"": ""54010607"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g026"",
      ""name"": ""1122.05.12 网点违规行为考核"",
      ""order"": 26,
      ""exactCodes"": [
        ""1122.05.12""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700381,
          ""accountCode"": ""54010613"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g027"",
      ""name"": ""1122.05.18 先行理赔"",
      ""order"": 27,
      ""exactCodes"": [
        ""1122.05.18""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700382,
          ""accountCode"": ""54010701"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g028"",
      ""name"": ""1122.05.19 工单考核"",
      ""order"": 28,
      ""exactCodes"": [
        ""1122.05.19""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700375,
          ""accountCode"": ""54010607"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g029"",
      ""name"": ""1122.05.23 客服综合考核"",
      ""order"": 29,
      ""exactCodes"": [
        ""1122.05.23""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700372,
          ""accountCode"": ""54010604"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g030"",
      ""name"": ""1122.05.26 客服服务费"",
      ""order"": 30,
      ""exactCodes"": [
        ""1122.05.26""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700364,
          ""accountCode"": ""54010501"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g031"",
      ""name"": ""1122.05.27 虚假问题件考核"",
      ""order"": 31,
      ""exactCodes"": [
        ""1122.05.27""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700378,
          ""accountCode"": ""54010610"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g032"",
      ""name"": ""1122.06.02 错发件"",
      ""order"": 32,
      ""exactCodes"": [
        ""1122.06.02""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700371,
          ""accountCode"": ""54010603"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g033"",
      ""name"": ""1122.06.03 合包不规范"",
      ""order"": 33,
      ""exactCodes"": [
        ""1122.06.03""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700371,
          ""accountCode"": ""54010603"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g034"",
      ""name"": ""1122.06.04 违禁品"",
      ""order"": 34,
      ""exactCodes"": [
        ""1122.06.04""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700381,
          ""accountCode"": ""54010613"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g035"",
      ""name"": ""1122.06.07 网点违规退回考核"",
      ""order"": 35,
      ""exactCodes"": [
        ""1122.06.07""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700371,
          ""accountCode"": ""54010603"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g036"",
      ""name"": ""1122.06.09 车辆类考核"",
      ""order"": 36,
      ""exactCodes"": [
        ""1122.06.09""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700373,
          ""accountCode"": ""54010605"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g037"",
      ""name"": ""1122.06.12 热敏面单未规范使用"",
      ""order"": 37,
      ""exactCodes"": [
        ""1122.06.12""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700371,
          ""accountCode"": ""54010603"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g038"",
      ""name"": ""1122.06.13 同行封装考核"",
      ""order"": 38,
      ""exactCodes"": [
        ""1122.06.13""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700371,
          ""accountCode"": ""54010603"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g039"",
      ""name"": ""1122.06.17 违规重量考核"",
      ""order"": 39,
      ""exactCodes"": [
        ""1122.06.17""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700371,
          ""accountCode"": ""54010603"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g040"",
      ""name"": ""1122.06.18 物流信息考核"",
      ""order"": 40,
      ""exactCodes"": [
        ""1122.06.18""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700371,
          ""accountCode"": ""54010603"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g041"",
      ""name"": ""1122.06.21 实名制考核收费"",
      ""order"": 41,
      ""exactCodes"": [
        ""1122.06.21""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700381,
          ""accountCode"": ""54010613"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g042"",
      ""name"": ""1122.06.23 进港服务投诉考核"",
      ""order"": 42,
      ""exactCodes"": [
        ""1122.06.23""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700372,
          ""accountCode"": ""54010604"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g043"",
      ""name"": ""1122.06.26 操作综合考核"",
      ""order"": 43,
      ""exactCodes"": [
        ""1122.06.26""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700371,
          ""accountCode"": ""54010603"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g044"",
      ""name"": ""1122.06.27 环保袋使用费"",
      ""order"": 44,
      ""exactCodes"": [
        ""1122.06.27""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700385,
          ""accountCode"": ""54010802"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g045"",
      ""name"": ""1122.06.29 环保袋超时费"",
      ""order"": 45,
      ""exactCodes"": [
        ""1122.06.29""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700385,
          ""accountCode"": ""54010802"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g046"",
      ""name"": ""1122.06.30 环保袋遗失费"",
      ""order"": 46,
      ""exactCodes"": [
        ""1122.06.30""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700385,
          ""accountCode"": ""54010802"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g047"",
      ""name"": ""1122.06.32 交货时效考核"",
      ""order"": 47,
      ""exactCodes"": [
        ""1122.06.32""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700370,
          ""accountCode"": ""54010602"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g048"",
      ""name"": ""1122.06.33 末端考核"",
      ""order"": 48,
      ""exactCodes"": [
        ""1122.06.33""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700379,
          ""accountCode"": ""54010611"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g049"",
      ""name"": ""1122.06.35 环保袋收费"",
      ""order"": 49,
      ""exactCodes"": [
        ""1122.06.35""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700385,
          ""accountCode"": ""54010802"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g050"",
      ""name"": ""1122.06.36 三个100%投诉"",
      ""order"": 50,
      ""exactCodes"": [
        ""1122.06.36""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700372,
          ""accountCode"": ""54010604"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g051"",
      ""name"": ""1122.06.45 网点出仓率考核"",
      ""order"": 51,
      ""exactCodes"": [
        ""1122.06.45""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700374,
          ""accountCode"": ""54010606"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g052"",
      ""name"": ""1122.06.46 非专营网点考核收费"",
      ""order"": 52,
      ""exactCodes"": [
        ""1122.06.46""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700381,
          ""accountCode"": ""54010613"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g053"",
      ""name"": ""1122.06.47 拦截退回不及时考核"",
      ""order"": 53,
      ""exactCodes"": [
        ""1122.06.47""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700370,
          ""accountCode"": ""54010602"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g054"",
      ""name"": ""1122.07 到付业务"",
      ""order"": 54,
      ""exactCodes"": [
        ""1122.07""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700364,
          ""accountCode"": ""54010501"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g055"",
      ""name"": ""1122.07.31 签收率未达标考核"",
      ""order"": 55,
      ""exactCodes"": [
        ""1122.07.31""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700376,
          ""accountCode"": ""54010608"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g056"",
      ""name"": ""1122.09.12 申鲜尊享项目收费"",
      ""order"": 56,
      ""exactCodes"": [
        ""1122.09.12""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700364,
          ""accountCode"": ""54010501"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g057"",
      ""name"": ""1122.09.13 市场客服收费"",
      ""order"": 57,
      ""exactCodes"": [
        ""1122.09.13""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700364,
          ""accountCode"": ""54010501"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g058"",
      ""name"": ""1122.09.20 淘逆业务结算-2小时"",
      ""order"": 58,
      ""exactCodes"": [
        ""1122.09.20""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700362,
          ""accountCode"": ""54010404"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g059"",
      ""name"": ""1122.10.01 乡镇村业务费用"",
      ""order"": 59,
      ""exactCodes"": [
        ""1122.10.01""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700348,
          ""accountCode"": ""54010207"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g060"",
      ""name"": ""1122.11 通讯业务（含短信提醒，电话卡）"",
      ""order"": 60,
      ""exactCodes"": [
        ""1122.11""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700412,
          ""accountCode"": ""56020901"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g061"",
      ""name"": ""1122.13.05 政策返还"",
      ""order"": 61,
      ""exactCodes"": [
        ""1122.13.05""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700364,
          ""accountCode"": ""54010501"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g062"",
      ""name"": ""1122.13.06 一口价结算"",
      ""order"": 62,
      ""exactCodes"": [
        ""1122.13.06""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700364,
          ""accountCode"": ""54010501"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g063"",
      ""name"": ""1122.21 应计利息"",
      ""order"": 63,
      ""exactCodes"": [
        ""1122.21""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700271,
          ""accountCode"": ""560301"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g064"",
      ""name"": ""1122.22 网点租金"",
      ""order"": 64,
      ""exactCodes"": [
        ""1122.22""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700250,
          ""accountCode"": ""560204"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g065"",
      ""name"": ""1122.23 网点水电费"",
      ""order"": 65,
      ""exactCodes"": [
        ""1122.23""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700251,
          ""accountCode"": ""560205"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g066"",
      ""name"": ""1122.25 省区综合考核收费"",
      ""order"": 66,
      ""exactCodes"": [
        ""1122.25""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700380,
          ""accountCode"": ""54010612"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g067"",
      ""name"": ""1122.27 揽收时效考核"",
      ""order"": 67,
      ""exactCodes"": [
        ""1122.27""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700370,
          ""accountCode"": ""54010602"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g068"",
      ""name"": ""1122.31 总部大客户项目收费"",
      ""order"": 68,
      ""exactCodes"": [
        ""1122.31""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700364,
          ""accountCode"": ""54010501"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g069"",
      ""name"": ""1122.32 菜鸟仓配项目收费"",
      ""order"": 69,
      ""exactCodes"": [
        ""1122.32""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700364,
          ""accountCode"": ""54010501"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g070"",
      ""name"": ""1122.41 按需派送业务费用"",
      ""order"": 70,
      ""exactCodes"": [
        ""1122.41""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700347,
          ""accountCode"": ""54010206"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g071"",
      ""name"": ""1122.48 散单履约考核"",
      ""order"": 71,
      ""exactCodes"": [
        ""1122.48""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700370,
          ""accountCode"": ""54010602"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g072"",
      ""name"": ""1122.49 手续费"",
      ""order"": 72,
      ""exactCodes"": [
        ""1122.49""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700272,
          ""accountCode"": ""560302"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g073"",
      ""name"": ""1122.50 个人寄件结算-散件"",
      ""order"": 73,
      ""exactCodes"": [
        ""1122.50""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700364,
          ""accountCode"": ""54010501"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g074"",
      ""name"": ""1122.70 网点管车服务费"",
      ""order"": 74,
      ""exactCodes"": [
        ""1122.70""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700364,
          ""accountCode"": ""54010501"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g075"",
      ""name"": ""1122.71 二三段码考核"",
      ""order"": 75,
      ""exactCodes"": [
        ""1122.71""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700371,
          ""accountCode"": ""54010603"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g076"",
      ""name"": ""1122.99 系统服务费"",
      ""order"": 76,
      ""exactCodes"": [
        ""1122.99""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700368,
          ""accountCode"": ""54010505"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g077"",
      ""name"": ""2202.02.01 基础派费付费"",
      ""order"": 77,
      ""exactCodes"": [
        ""2202.02.01""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700308,
          ""accountCode"": ""50010201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g078"",
      ""name"": ""2202.02.06.01 遗失件赔款返款"",
      ""order"": 78,
      ""exactCodes"": [
        ""2202.02.06.01""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700339,
          ""accountCode"": ""50010701"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g079"",
      ""name"": ""2202.02.06.02 破损件赔款返款"",
      ""order"": 79,
      ""exactCodes"": [
        ""2202.02.06.02""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700339,
          ""accountCode"": ""50010701"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g080"",
      ""name"": ""2202.02.06.06 客服手工账单返款"",
      ""order"": 80,
      ""exactCodes"": [
        ""2202.02.06.06""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700339,
          ""accountCode"": ""50010701"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g081"",
      ""name"": ""2202.02.06.25 客服综合考核"",
      ""order"": 81,
      ""exactCodes"": [
        ""2202.02.06.25""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700327,
          ""accountCode"": ""50010604"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g082"",
      ""name"": ""2202.02.07.12 热敏面单未规范使用"",
      ""order"": 82,
      ""exactCodes"": [
        ""2202.02.07.12""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700326,
          ""accountCode"": ""50010603"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g083"",
      ""name"": ""2202.02.07.23 末端激励"",
      ""order"": 83,
      ""exactCodes"": [
        ""2202.02.07.23""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700334,
          ""accountCode"": ""50010611"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g084"",
      ""name"": ""2202.02.13 到付业务费用"",
      ""order"": 84,
      ""exactCodes"": [
        ""2202.02.13""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700306,
          ""accountCode"": ""50010103"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g085"",
      ""name"": ""2202.02.16 揽收费"",
      ""order"": 85,
      ""exactCodes"": [
        ""2202.02.16""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700304,
          ""accountCode"": ""50010101"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g086"",
      ""name"": ""2202.02.32 散件业务结算"",
      ""order"": 86,
      ""exactCodes"": [
        ""2202.02.32""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700304,
          ""accountCode"": ""50010101"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g087"",
      ""name"": ""2202.02.42 退件操作费"",
      ""order"": 87,
      ""exactCodes"": [
        ""2202.02.42""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700319,
          ""accountCode"": ""50010301"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g088"",
      ""name"": ""2202.02.43 补贴派费"",
      ""order"": 88,
      ""exactCodes"": [
        ""2202.02.43""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700309,
          ""accountCode"": ""50010202"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g089"",
      ""name"": ""2202.02.44 时效&拦截弃件付费"",
      ""order"": 89,
      ""exactCodes"": [
        ""2202.02.44""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700308,
          ""accountCode"": ""50010201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g090"",
      ""name"": ""2202.02.45 大货计重付费"",
      ""order"": 90,
      ""exactCodes"": [
        ""2202.02.45""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700310,
          ""accountCode"": ""50010203"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g091"",
      ""name"": ""2202.02.46 考核派费"",
      ""order"": 91,
      ""exactCodes"": [
        ""2202.02.46""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700316,
          ""accountCode"": ""50010209"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g092"",
      ""name"": ""2202.02.47 周期性派费付费"",
      ""order"": 92,
      ""exactCodes"": [
        ""2202.02.47""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700311,
          ""accountCode"": ""50010204"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g093"",
      ""name"": ""2202.02.50 按需派送业务费用"",
      ""order"": 93,
      ""exactCodes"": [
        ""2202.02.50""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700313,
          ""accountCode"": ""50010206"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g094"",
      ""name"": ""2202.02.55 异形件操作费"",
      ""order"": 94,
      ""exactCodes"": [
        ""2202.02.55""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700320,
          ""accountCode"": ""50010302"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g095"",
      ""name"": ""2202.06.01 乡镇村业务费用"",
      ""order"": 95,
      ""exactCodes"": [
        ""2202.06.01""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700314,
          ""accountCode"": ""50010207"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g096"",
      ""name"": ""2202.08.13 菜鸟仓配项目付费"",
      ""order"": 96,
      ""exactCodes"": [
        ""2202.08.13""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700128,
          ""accountCode"": ""220301"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g097"",
      ""name"": ""2202.08.14 总部大客户项目付费"",
      ""order"": 97,
      ""exactCodes"": [
        ""2202.08.14""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700320,
          ""accountCode"": ""50010302"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g098"",
      ""name"": ""2202.08.16 抖音项目付费"",
      ""order"": 98,
      ""exactCodes"": [
        ""2202.08.16""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700307,
          ""accountCode"": ""50010104"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g099"",
      ""name"": ""2202.08.28 集运直邮补贴"",
      ""order"": 99,
      ""exactCodes"": [
        ""2202.08.28""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700428,
          ""accountCode"": ""50010105"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g100"",
      ""name"": ""2202.10 个人寄件结算"",
      ""order"": 100,
      ""exactCodes"": [
        ""2202.10""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700304,
          ""accountCode"": ""50010101"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g101"",
      ""name"": ""2202.13 个人寄件结算-散件"",
      ""order"": 101,
      ""exactCodes"": [
        ""2202.13""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700304,
          ""accountCode"": ""50010101"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    }
  ]
}',
                [F更新时间]       = GETDATE()
            WHERE [FID] = 21;
        END
        ELSE
        BEGIN
            INSERT INTO [CF自动插件_规则]
                ([FID],[F组织ID],[F类型编码],[F规则名称],[F规则配置JSON],[F状态],[F说明],[F并发戳],[F创建时间])
            VALUES
                (21, 192, N'autoVoucher', N'Tai申通总部交易明细生成凭证规则',
                 N'{
  ""mode"": ""rulesBased"",
  ""version"": 2,
  ""voucherWord"": ""记"",
  ""dateField"": ""F业务日期"",
  ""stagingTable"": ""STG申通总部交易明细"",
  ""accountSetId"": 2,
  ""keyFields"": [
    ""F科目编码"",
    ""F费用名称"",
    ""F业务摘要""
  ],
  ""unmatchedAction"": ""createDraft"",
  ""filterConditions"": [],
  ""matchingLayers"": {
    ""exactMatchField"": ""F科目编码"",
    ""categoryField"": ""F费用名称"",
    ""summaryField"": ""F业务摘要""
  },
  ""ruleGroups"": [
    {
      ""id"": ""g001"",
      ""name"": ""1122.01 面单服务费"",
      ""order"": 1,
      ""exactCodes"": [
        ""1122.01""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700341,
          ""accountCode"": ""54010101"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g002"",
      ""name"": ""1122.01.05 提现服务费收费"",
      ""order"": 2,
      ""exactCodes"": [
        ""1122.01.05""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700272,
          ""accountCode"": ""560302"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g003"",
      ""name"": ""1122.02 辅料销售"",
      ""order"": 3,
      ""exactCodes"": [
        ""1122.02""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700384,
          ""accountCode"": ""54010801"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g004"",
      ""name"": ""1122.03 基础派费收费"",
      ""order"": 4,
      ""exactCodes"": [
        ""1122.03""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700342,
          ""accountCode"": ""54010201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g005"",
      ""name"": ""1122.03.01 大货计重收费"",
      ""order"": 5,
      ""exactCodes"": [
        ""1122.03.01""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700344,
          ""accountCode"": ""54010203"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g006"",
      ""name"": ""1122.03.02 周期性派费收费"",
      ""order"": 6,
      ""exactCodes"": [
        ""1122.03.02""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700345,
          ""accountCode"": ""54010204"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g007"",
      ""name"": ""1122.03.04 派费预收"",
      ""order"": 7,
      ""exactCodes"": [
        ""1122.03.04""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700342,
          ""accountCode"": ""54010201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g008"",
      ""name"": ""1122.04.02 中转费"",
      ""order"": 8,
      ""exactCodes"": [
        ""1122.04.02""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700354,
          ""accountCode"": ""54010301"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g009"",
      ""name"": ""1122.04.06 全网出港费"",
      ""order"": 9,
      ""exactCodes"": [
        ""1122.04.06""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700357,
          ""accountCode"": ""54010304"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g010"",
      ""name"": ""1122.04.12 出港操作费"",
      ""order"": 10,
      ""exactCodes"": [
        ""1122.04.12""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700362,
          ""accountCode"": ""54010404"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g011"",
      ""name"": ""1122.04.15 退件操作费"",
      ""order"": 11,
      ""exactCodes"": [
        ""1122.04.15""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700361,
          ""accountCode"": ""54010403"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g012"",
      ""name"": ""1122.04.16 退件中转费"",
      ""order"": 12,
      ""exactCodes"": [
        ""1122.04.16""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700358,
          ""accountCode"": ""54010305"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g013"",
      ""name"": ""1122.04.21 操作装卸费"",
      ""order"": 13,
      ""exactCodes"": [
        ""1122.04.21""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700363,
          ""accountCode"": ""54010405"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g014"",
      ""name"": ""1122.04.22 大货操作费"",
      ""order"": 14,
      ""exactCodes"": [
        ""1122.04.22""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700362,
          ""accountCode"": ""54010404"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g015"",
      ""name"": ""1122.04.24 进港集包应收"",
      ""order"": 15,
      ""exactCodes"": [
        ""1122.04.24""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700359,
          ""accountCode"": ""54010401"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g016"",
      ""name"": ""1122.04.26 托管接管服务费"",
      ""order"": 16,
      ""exactCodes"": [
        ""1122.04.26""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700362,
          ""accountCode"": ""54010404"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g017"",
      ""name"": ""1122.04.31 短驳费"",
      ""order"": 17,
      ""exactCodes"": [
        ""1122.04.31""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700358,
          ""accountCode"": ""54010305"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g018"",
      ""name"": ""1122.04.37 保价应收"",
      ""order"": 18,
      ""exactCodes"": [
        ""1122.04.37""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700362,
          ""accountCode"": ""54010404"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g019"",
      ""name"": ""1122.04.40 异形件操作费"",
      ""order"": 19,
      ""exactCodes"": [
        ""1122.04.40""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700362,
          ""accountCode"": ""54010404"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g020"",
      ""name"": ""1122.04.41 批货收费"",
      ""order"": 20,
      ""exactCodes"": [
        ""1122.04.41""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700362,
          ""accountCode"": ""54010404"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g021"",
      ""name"": ""1122.05.01 遗失件赔款"",
      ""order"": 21,
      ""exactCodes"": [
        ""1122.05.01""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700382,
          ""accountCode"": ""54010701"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g022"",
      ""name"": ""1122.05.02 破损件赔款"",
      ""order"": 22,
      ""exactCodes"": [
        ""1122.05.02""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700382,
          ""accountCode"": ""54010701"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g023"",
      ""name"": ""1122.05.03 延误件赔款"",
      ""order"": 23,
      ""exactCodes"": [
        ""1122.05.03""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700382,
          ""accountCode"": ""54010701"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g024"",
      ""name"": ""1122.05.04 超派件赔款"",
      ""order"": 24,
      ""exactCodes"": [
        ""1122.05.04""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700382,
          ""accountCode"": ""54010701"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g025"",
      ""name"": ""1122.05.07 工单服务质量考核"",
      ""order"": 25,
      ""exactCodes"": [
        ""1122.05.07""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700375,
          ""accountCode"": ""54010607"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g026"",
      ""name"": ""1122.05.12 网点违规行为考核"",
      ""order"": 26,
      ""exactCodes"": [
        ""1122.05.12""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700381,
          ""accountCode"": ""54010613"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g027"",
      ""name"": ""1122.05.18 先行理赔"",
      ""order"": 27,
      ""exactCodes"": [
        ""1122.05.18""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700382,
          ""accountCode"": ""54010701"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g028"",
      ""name"": ""1122.05.19 工单考核"",
      ""order"": 28,
      ""exactCodes"": [
        ""1122.05.19""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700375,
          ""accountCode"": ""54010607"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g029"",
      ""name"": ""1122.05.23 客服综合考核"",
      ""order"": 29,
      ""exactCodes"": [
        ""1122.05.23""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700372,
          ""accountCode"": ""54010604"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g030"",
      ""name"": ""1122.05.26 客服服务费"",
      ""order"": 30,
      ""exactCodes"": [
        ""1122.05.26""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700364,
          ""accountCode"": ""54010501"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g031"",
      ""name"": ""1122.05.27 虚假问题件考核"",
      ""order"": 31,
      ""exactCodes"": [
        ""1122.05.27""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700378,
          ""accountCode"": ""54010610"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g032"",
      ""name"": ""1122.06.02 错发件"",
      ""order"": 32,
      ""exactCodes"": [
        ""1122.06.02""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700371,
          ""accountCode"": ""54010603"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g033"",
      ""name"": ""1122.06.03 合包不规范"",
      ""order"": 33,
      ""exactCodes"": [
        ""1122.06.03""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700371,
          ""accountCode"": ""54010603"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g034"",
      ""name"": ""1122.06.04 违禁品"",
      ""order"": 34,
      ""exactCodes"": [
        ""1122.06.04""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700381,
          ""accountCode"": ""54010613"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g035"",
      ""name"": ""1122.06.07 网点违规退回考核"",
      ""order"": 35,
      ""exactCodes"": [
        ""1122.06.07""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700371,
          ""accountCode"": ""54010603"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g036"",
      ""name"": ""1122.06.09 车辆类考核"",
      ""order"": 36,
      ""exactCodes"": [
        ""1122.06.09""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700373,
          ""accountCode"": ""54010605"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g037"",
      ""name"": ""1122.06.12 热敏面单未规范使用"",
      ""order"": 37,
      ""exactCodes"": [
        ""1122.06.12""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700371,
          ""accountCode"": ""54010603"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g038"",
      ""name"": ""1122.06.13 同行封装考核"",
      ""order"": 38,
      ""exactCodes"": [
        ""1122.06.13""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700371,
          ""accountCode"": ""54010603"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g039"",
      ""name"": ""1122.06.17 违规重量考核"",
      ""order"": 39,
      ""exactCodes"": [
        ""1122.06.17""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700371,
          ""accountCode"": ""54010603"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g040"",
      ""name"": ""1122.06.18 物流信息考核"",
      ""order"": 40,
      ""exactCodes"": [
        ""1122.06.18""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700371,
          ""accountCode"": ""54010603"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g041"",
      ""name"": ""1122.06.21 实名制考核收费"",
      ""order"": 41,
      ""exactCodes"": [
        ""1122.06.21""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700381,
          ""accountCode"": ""54010613"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g042"",
      ""name"": ""1122.06.23 进港服务投诉考核"",
      ""order"": 42,
      ""exactCodes"": [
        ""1122.06.23""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700372,
          ""accountCode"": ""54010604"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g043"",
      ""name"": ""1122.06.26 操作综合考核"",
      ""order"": 43,
      ""exactCodes"": [
        ""1122.06.26""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700371,
          ""accountCode"": ""54010603"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g044"",
      ""name"": ""1122.06.27 环保袋使用费"",
      ""order"": 44,
      ""exactCodes"": [
        ""1122.06.27""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700385,
          ""accountCode"": ""54010802"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g045"",
      ""name"": ""1122.06.29 环保袋超时费"",
      ""order"": 45,
      ""exactCodes"": [
        ""1122.06.29""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700385,
          ""accountCode"": ""54010802"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g046"",
      ""name"": ""1122.06.30 环保袋遗失费"",
      ""order"": 46,
      ""exactCodes"": [
        ""1122.06.30""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700385,
          ""accountCode"": ""54010802"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g047"",
      ""name"": ""1122.06.32 交货时效考核"",
      ""order"": 47,
      ""exactCodes"": [
        ""1122.06.32""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700370,
          ""accountCode"": ""54010602"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g048"",
      ""name"": ""1122.06.33 末端考核"",
      ""order"": 48,
      ""exactCodes"": [
        ""1122.06.33""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700379,
          ""accountCode"": ""54010611"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g049"",
      ""name"": ""1122.06.35 环保袋收费"",
      ""order"": 49,
      ""exactCodes"": [
        ""1122.06.35""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700385,
          ""accountCode"": ""54010802"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g050"",
      ""name"": ""1122.06.36 三个100%投诉"",
      ""order"": 50,
      ""exactCodes"": [
        ""1122.06.36""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700372,
          ""accountCode"": ""54010604"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g051"",
      ""name"": ""1122.06.45 网点出仓率考核"",
      ""order"": 51,
      ""exactCodes"": [
        ""1122.06.45""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700374,
          ""accountCode"": ""54010606"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g052"",
      ""name"": ""1122.06.46 非专营网点考核收费"",
      ""order"": 52,
      ""exactCodes"": [
        ""1122.06.46""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700381,
          ""accountCode"": ""54010613"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g053"",
      ""name"": ""1122.06.47 拦截退回不及时考核"",
      ""order"": 53,
      ""exactCodes"": [
        ""1122.06.47""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700370,
          ""accountCode"": ""54010602"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g054"",
      ""name"": ""1122.07 到付业务"",
      ""order"": 54,
      ""exactCodes"": [
        ""1122.07""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700364,
          ""accountCode"": ""54010501"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g055"",
      ""name"": ""1122.07.31 签收率未达标考核"",
      ""order"": 55,
      ""exactCodes"": [
        ""1122.07.31""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700376,
          ""accountCode"": ""54010608"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g056"",
      ""name"": ""1122.09.12 申鲜尊享项目收费"",
      ""order"": 56,
      ""exactCodes"": [
        ""1122.09.12""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700364,
          ""accountCode"": ""54010501"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g057"",
      ""name"": ""1122.09.13 市场客服收费"",
      ""order"": 57,
      ""exactCodes"": [
        ""1122.09.13""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700364,
          ""accountCode"": ""54010501"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g058"",
      ""name"": ""1122.09.20 淘逆业务结算-2小时"",
      ""order"": 58,
      ""exactCodes"": [
        ""1122.09.20""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700362,
          ""accountCode"": ""54010404"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g059"",
      ""name"": ""1122.10.01 乡镇村业务费用"",
      ""order"": 59,
      ""exactCodes"": [
        ""1122.10.01""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700348,
          ""accountCode"": ""54010207"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g060"",
      ""name"": ""1122.11 通讯业务（含短信提醒，电话卡）"",
      ""order"": 60,
      ""exactCodes"": [
        ""1122.11""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700412,
          ""accountCode"": ""56020901"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g061"",
      ""name"": ""1122.13.05 政策返还"",
      ""order"": 61,
      ""exactCodes"": [
        ""1122.13.05""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700364,
          ""accountCode"": ""54010501"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g062"",
      ""name"": ""1122.13.06 一口价结算"",
      ""order"": 62,
      ""exactCodes"": [
        ""1122.13.06""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700364,
          ""accountCode"": ""54010501"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g063"",
      ""name"": ""1122.21 应计利息"",
      ""order"": 63,
      ""exactCodes"": [
        ""1122.21""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700271,
          ""accountCode"": ""560301"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g064"",
      ""name"": ""1122.22 网点租金"",
      ""order"": 64,
      ""exactCodes"": [
        ""1122.22""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700250,
          ""accountCode"": ""560204"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g065"",
      ""name"": ""1122.23 网点水电费"",
      ""order"": 65,
      ""exactCodes"": [
        ""1122.23""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700251,
          ""accountCode"": ""560205"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g066"",
      ""name"": ""1122.25 省区综合考核收费"",
      ""order"": 66,
      ""exactCodes"": [
        ""1122.25""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700380,
          ""accountCode"": ""54010612"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g067"",
      ""name"": ""1122.27 揽收时效考核"",
      ""order"": 67,
      ""exactCodes"": [
        ""1122.27""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700370,
          ""accountCode"": ""54010602"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g068"",
      ""name"": ""1122.31 总部大客户项目收费"",
      ""order"": 68,
      ""exactCodes"": [
        ""1122.31""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700364,
          ""accountCode"": ""54010501"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g069"",
      ""name"": ""1122.32 菜鸟仓配项目收费"",
      ""order"": 69,
      ""exactCodes"": [
        ""1122.32""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700364,
          ""accountCode"": ""54010501"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g070"",
      ""name"": ""1122.41 按需派送业务费用"",
      ""order"": 70,
      ""exactCodes"": [
        ""1122.41""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700347,
          ""accountCode"": ""54010206"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g071"",
      ""name"": ""1122.48 散单履约考核"",
      ""order"": 71,
      ""exactCodes"": [
        ""1122.48""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700370,
          ""accountCode"": ""54010602"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g072"",
      ""name"": ""1122.49 手续费"",
      ""order"": 72,
      ""exactCodes"": [
        ""1122.49""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700272,
          ""accountCode"": ""560302"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g073"",
      ""name"": ""1122.50 个人寄件结算-散件"",
      ""order"": 73,
      ""exactCodes"": [
        ""1122.50""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700364,
          ""accountCode"": ""54010501"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g074"",
      ""name"": ""1122.70 网点管车服务费"",
      ""order"": 74,
      ""exactCodes"": [
        ""1122.70""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700364,
          ""accountCode"": ""54010501"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g075"",
      ""name"": ""1122.71 二三段码考核"",
      ""order"": 75,
      ""exactCodes"": [
        ""1122.71""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700371,
          ""accountCode"": ""54010603"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g076"",
      ""name"": ""1122.99 系统服务费"",
      ""order"": 76,
      ""exactCodes"": [
        ""1122.99""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""借"",
          ""accountId"": 700368,
          ""accountCode"": ""54010505"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""贷"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额支出"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g077"",
      ""name"": ""2202.02.01 基础派费付费"",
      ""order"": 77,
      ""exactCodes"": [
        ""2202.02.01""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700308,
          ""accountCode"": ""50010201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g078"",
      ""name"": ""2202.02.06.01 遗失件赔款返款"",
      ""order"": 78,
      ""exactCodes"": [
        ""2202.02.06.01""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700339,
          ""accountCode"": ""50010701"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g079"",
      ""name"": ""2202.02.06.02 破损件赔款返款"",
      ""order"": 79,
      ""exactCodes"": [
        ""2202.02.06.02""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700339,
          ""accountCode"": ""50010701"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g080"",
      ""name"": ""2202.02.06.06 客服手工账单返款"",
      ""order"": 80,
      ""exactCodes"": [
        ""2202.02.06.06""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700339,
          ""accountCode"": ""50010701"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g081"",
      ""name"": ""2202.02.06.25 客服综合考核"",
      ""order"": 81,
      ""exactCodes"": [
        ""2202.02.06.25""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700327,
          ""accountCode"": ""50010604"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g082"",
      ""name"": ""2202.02.07.12 热敏面单未规范使用"",
      ""order"": 82,
      ""exactCodes"": [
        ""2202.02.07.12""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700326,
          ""accountCode"": ""50010603"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g083"",
      ""name"": ""2202.02.07.23 末端激励"",
      ""order"": 83,
      ""exactCodes"": [
        ""2202.02.07.23""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700334,
          ""accountCode"": ""50010611"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g084"",
      ""name"": ""2202.02.13 到付业务费用"",
      ""order"": 84,
      ""exactCodes"": [
        ""2202.02.13""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700306,
          ""accountCode"": ""50010103"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g085"",
      ""name"": ""2202.02.16 揽收费"",
      ""order"": 85,
      ""exactCodes"": [
        ""2202.02.16""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700304,
          ""accountCode"": ""50010101"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g086"",
      ""name"": ""2202.02.32 散件业务结算"",
      ""order"": 86,
      ""exactCodes"": [
        ""2202.02.32""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700304,
          ""accountCode"": ""50010101"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g087"",
      ""name"": ""2202.02.42 退件操作费"",
      ""order"": 87,
      ""exactCodes"": [
        ""2202.02.42""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700319,
          ""accountCode"": ""50010301"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g088"",
      ""name"": ""2202.02.43 补贴派费"",
      ""order"": 88,
      ""exactCodes"": [
        ""2202.02.43""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700309,
          ""accountCode"": ""50010202"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g089"",
      ""name"": ""2202.02.44 时效&拦截弃件付费"",
      ""order"": 89,
      ""exactCodes"": [
        ""2202.02.44""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700308,
          ""accountCode"": ""50010201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g090"",
      ""name"": ""2202.02.45 大货计重付费"",
      ""order"": 90,
      ""exactCodes"": [
        ""2202.02.45""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700310,
          ""accountCode"": ""50010203"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g091"",
      ""name"": ""2202.02.46 考核派费"",
      ""order"": 91,
      ""exactCodes"": [
        ""2202.02.46""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700316,
          ""accountCode"": ""50010209"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g092"",
      ""name"": ""2202.02.47 周期性派费付费"",
      ""order"": 92,
      ""exactCodes"": [
        ""2202.02.47""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700311,
          ""accountCode"": ""50010204"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g093"",
      ""name"": ""2202.02.50 按需派送业务费用"",
      ""order"": 93,
      ""exactCodes"": [
        ""2202.02.50""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700313,
          ""accountCode"": ""50010206"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g094"",
      ""name"": ""2202.02.55 异形件操作费"",
      ""order"": 94,
      ""exactCodes"": [
        ""2202.02.55""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700320,
          ""accountCode"": ""50010302"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g095"",
      ""name"": ""2202.06.01 乡镇村业务费用"",
      ""order"": 95,
      ""exactCodes"": [
        ""2202.06.01""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700314,
          ""accountCode"": ""50010207"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""IN"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g096"",
      ""name"": ""2202.08.13 菜鸟仓配项目付费"",
      ""order"": 96,
      ""exactCodes"": [
        ""2202.08.13""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700128,
          ""accountCode"": ""220301"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""CMB"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g097"",
      ""name"": ""2202.08.14 总部大客户项目付费"",
      ""order"": 97,
      ""exactCodes"": [
        ""2202.08.14""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700320,
          ""accountCode"": ""50010302"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g098"",
      ""name"": ""2202.08.16 抖音项目付费"",
      ""order"": 98,
      ""exactCodes"": [
        ""2202.08.16""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700307,
          ""accountCode"": ""50010104"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g099"",
      ""name"": ""2202.08.28 集运直邮补贴"",
      ""order"": 99,
      ""exactCodes"": [
        ""2202.08.28""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700428,
          ""accountCode"": ""50010105"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g100"",
      ""name"": ""2202.10 个人寄件结算"",
      ""order"": 100,
      ""exactCodes"": [
        ""2202.10""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700304,
          ""accountCode"": ""50010101"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    },
    {
      ""id"": ""g101"",
      ""name"": ""2202.13 个人寄件结算-散件"",
      ""order"": 101,
      ""exactCodes"": [
        ""2202.13""
      ],
      ""exactCategories"": null,
      ""categoryKeywords"": null,
      ""summaryKeywords"": null,
      ""fallthrough"": false,
      ""amountAggregation"": ""ROW"",
      ""lines"": [
        {
          ""lineNo"": 1,
          ""direction"": ""贷"",
          ""accountId"": 700304,
          ""accountCode"": ""50010101"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 1,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""outlet"",
              ""sourceType"": ""dynamic"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": null,
              ""sourceField"": ""F网点编号"",
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            },
            {
              ""auxType"": ""business_direction"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": null,
              ""fixedItemCode"": null,
              ""fixedValue"": ""OUT"",
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        },
        {
          ""lineNo"": 2,
          ""direction"": ""借"",
          ""accountId"": 700125,
          ""accountCode"": ""220201"",
          ""amountField"": ""F发生额收入"",
          ""summaryTemplate"": ""{F业务摘要}"",
          ""status"": 1,
          ""displayOrder"": 2,
          ""auxiliaryConfigs"": [
            {
              ""auxType"": ""express_brand"",
              ""sourceType"": ""fixed"",
              ""fixedItemId"": 16,
              ""fixedItemCode"": ""ST"",
              ""fixedValue"": null,
              ""sourceField"": null,
              ""matchBy"": ""code""
            }
          ]
        }
      ]
    }
  ]
}', 1, N'品牌版科目凭证规则（2026-06-19重配）',
                 REPLACE(NEWID(),'-',''), GETDATE());
        END
        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");
    }
    private static void MigrateV53(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;
        // 规则21 createDraft 待补录草稿占位科目 = 700044(账套2 1901 待处理财产损溢)
        ExecSql(ctx, @"UPDATE [CF自动插件_规则]
            SET [F规则配置JSON] = JSON_MODIFY([F规则配置JSON], '$.draftPlaceholderAccountId', 700044)
            WHERE [FID] = 21 AND ISJSON([F规则配置JSON]) = 1;");
    }
    private static void MigrateV24(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_物流及时准确明细 暂存表（系统列 + 17 业务列 + 标准字段） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_物流及时准确明细')
        CREATE TABLE [STG申通_物流及时准确明细] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3102 columnMapping，17 列）
            [F统计日期] NVARCHAR(100) NULL,
            [F运单号] NVARCHAR(200) NULL,
            [F网点名称] NVARCHAR(200) NULL,
            [F所属网点名称] NVARCHAR(200) NULL,
            [F问题类型] NVARCHAR(50) NULL,
            [F扫描时间] NVARCHAR(100) NULL,
            [F上传时间] NVARCHAR(100) NULL,
            [F入库时间] NVARCHAR(100) NULL,
            [F扫描类型] NVARCHAR(100) NULL,
            [F扫描员] NVARCHAR(200) NULL,
            [F扫描员编号] NVARCHAR(100) NULL,
            [F设备类型] NVARCHAR(100) NULL,
            [F设备ID] NVARCHAR(100) NULL,
            [F是否黑土共配] NVARCHAR(20) NULL,
            [F订单平台] NVARCHAR(100) NULL,
            [F派件员编号] NVARCHAR(100) NULL,
            [F派件员名称] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_物流及时准确明细_F批次ID' AND object_id = OBJECT_ID(N'STG申通_物流及时准确明细'))
        CREATE INDEX [IX_STG申通_物流及时准确明细_F批次ID] ON [STG申通_物流及时准确明细]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_物流及时准确明细_数据作用域' AND object_id = OBJECT_ID(N'STG申通_物流及时准确明细'))
        CREATE INDEX [IX_STG申通_物流及时准确明细_数据作用域] ON [STG申通_物流及时准确明细]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（运单号 + 问题类型 + 组织，仅未撤销 + 运单号非空）
        -- 及时准确明细同样是问题件清单，按「事件身份」(运单号+问题类型) 去重；
        -- 三个源文件（到件晚于签收/派件晚于签收/揽收上传不及时）靠「问题类型」区分。
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_物流及时准确明细_运单问题_未撤销' AND object_id = OBJECT_ID(N'STG申通_物流及时准确明细'))
        CREATE UNIQUE INDEX [UX_STG申通_物流及时准确明细_运单问题_未撤销]
            ON [STG申通_物流及时准确明细]([F运单号],[F问题类型],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3102（物流及时准确明细导入） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3102)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3102, 192, N'excelInput', N'申通物流及时准确明细导入规则',
        N'{""targetTable"":""STG申通_物流及时准确明细"",""outputMode"":""stg"",""headerRow"":1,""dataStartRow"":2,""columnIdentifier"":""统计日期,运单号,问题类型,扫描时间,扫描员"",""fullColumnIdentifier"":""统计日期,运单号,网点名称,所属网点名称,问题类型,扫描时间,上传时间,入库时间,扫描类型,扫描员,扫描员编号,设备类型,设备ID,是否黑土共配,订单平台,派件员编号,派件员名称"",""columnMapping"":[{""excelColumn"":""统计日期"",""dbColumn"":""F统计日期""},{""excelColumn"":""运单号"",""dbColumn"":""F运单号""},{""excelColumn"":""网点名称"",""dbColumn"":""F网点名称""},{""excelColumn"":""所属网点名称"",""dbColumn"":""F所属网点名称""},{""excelColumn"":""问题类型"",""dbColumn"":""F问题类型""},{""excelColumn"":""扫描时间"",""dbColumn"":""F扫描时间""},{""excelColumn"":""上传时间"",""dbColumn"":""F上传时间""},{""excelColumn"":""入库时间"",""dbColumn"":""F入库时间""},{""excelColumn"":""扫描类型"",""dbColumn"":""F扫描类型""},{""excelColumn"":""扫描员"",""dbColumn"":""F扫描员""},{""excelColumn"":""扫描员编号"",""dbColumn"":""F扫描员编号""},{""excelColumn"":""设备类型"",""dbColumn"":""F设备类型""},{""excelColumn"":""设备ID"",""dbColumn"":""F设备ID""},{""excelColumn"":""是否黑土共配"",""dbColumn"":""F是否黑土共配""},{""excelColumn"":""订单平台"",""dbColumn"":""F订单平台""},{""excelColumn"":""派件员编号"",""dbColumn"":""F派件员编号""},{""excelColumn"":""派件员名称"",""dbColumn"":""F派件员名称""}],""keyFields"":[""运单号"",""问题类型""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F运单号"",""F问题类型""],""batchSplit"":{""enabled"":false}}',
        1, N'申通物流及时准确明细（到件晚于签收/派件晚于签收/揽收上传不及时）Excel导入配置', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2302（QC_ST_LOGISTICS_TIMELINESS） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2302)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2302, NULL, 1, GETDATE(), NULL, N'网点质控：申通物流及时准确明细（到件晚于签收/派件晚于签收/揽收上传不及时）导入暂存', GETDATE(), NULL, N'申通物流及时准确明细导入', NULL, N'QC_ST_LOGISTICS_TIMELINESS', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""*晚于*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2302（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2302)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2302, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2302, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5102（ExcelInput 批次级自动节点，插件注册=1，规则=3102） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5102)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5102, 2302, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3102);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }

    private static void MigrateV25(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_揽收分析明细 暂存表（系统列 + 24 业务列 + 标准字段） ═══
        // 注意：「订单揽收用时/h」含非法字符 /，dbColumn 去掉斜杠为 F订单揽收用时h（实体/EF/DDL/映射四处一致）。
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_揽收分析明细')
        CREATE TABLE [STG申通_揽收分析明细] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3103 columnMapping，24 列）
            [F统计日期] NVARCHAR(100) NULL,
            [F电商平台] NVARCHAR(100) NULL,
            [F运单编号] NVARCHAR(200) NULL,
            [F订单编号] NVARCHAR(200) NULL,
            [F时效类型] NVARCHAR(100) NULL,
            [F频次] NVARCHAR(50) NULL,
            [F订单时间] NVARCHAR(100) NULL,
            [F揽收时间] NVARCHAR(100) NULL,
            [F揽收截止时间] NVARCHAR(100) NULL,
            [F订单揽收用时h] NVARCHAR(50) NULL,
            [F揽收标识] NVARCHAR(50) NULL,
            [F揽收及时标识] NVARCHAR(50) NULL,
            [F商家名称] NVARCHAR(200) NULL,
            [F订单网点] NVARCHAR(200) NULL,
            [F订单所属网点] NVARCHAR(200) NULL,
            [F揽收网点] NVARCHAR(200) NULL,
            [F揽收所属网点] NVARCHAR(200) NULL,
            [F收件员] NVARCHAR(200) NULL,
            [F订单始发城市] NVARCHAR(100) NULL,
            [F订单目的城市] NVARCHAR(100) NULL,
            [F仓类型] NVARCHAR(100) NULL,
            [F菜鸟仓编号] NVARCHAR(100) NULL,
            [F菜鸟仓名称] NVARCHAR(200) NULL,
            [F揽收超15天标识] NVARCHAR(50) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_揽收分析明细_F批次ID' AND object_id = OBJECT_ID(N'STG申通_揽收分析明细'))
        CREATE INDEX [IX_STG申通_揽收分析明细_F批次ID] ON [STG申通_揽收分析明细]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_揽收分析明细_数据作用域' AND object_id = OBJECT_ID(N'STG申通_揽收分析明细'))
        CREATE INDEX [IX_STG申通_揽收分析明细_数据作用域] ON [STG申通_揽收分析明细]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（运单编号 + 组织，仅未撤销 + 运单编号非空）
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_揽收分析明细_运单编号_未撤销' AND object_id = OBJECT_ID(N'STG申通_揽收分析明细'))
        CREATE UNIQUE INDEX [UX_STG申通_揽收分析明细_运单编号_未撤销]
            ON [STG申通_揽收分析明细]([F运单编号],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F运单编号] IS NOT NULL AND [F运单编号] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3103（揽收分析明细导入） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3103)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3103, 192, N'excelInput', N'申通揽收分析明细导入规则',
        N'{""targetTable"":""STG申通_揽收分析明细"",""outputMode"":""stg"",""headerRow"":1,""dataStartRow"":2,""columnIdentifier"":""运单编号,订单编号,揽收截止时间,揽收及时标识"",""fullColumnIdentifier"":""统计日期,电商平台,运单编号,订单编号,时效类型,频次,订单时间,揽收时间,揽收截止时间,订单揽收用时/h,揽收标识,揽收及时标识,商家名称,订单网点,订单所属网点,揽收网点,揽收所属网点,收件员,订单始发城市,订单目的城市,仓类型,菜鸟仓编号,菜鸟仓名称,揽收超15天标识"",""columnMapping"":[{""excelColumn"":""统计日期"",""dbColumn"":""F统计日期""},{""excelColumn"":""电商平台"",""dbColumn"":""F电商平台""},{""excelColumn"":""运单编号"",""dbColumn"":""F运单编号""},{""excelColumn"":""订单编号"",""dbColumn"":""F订单编号""},{""excelColumn"":""时效类型"",""dbColumn"":""F时效类型""},{""excelColumn"":""频次"",""dbColumn"":""F频次""},{""excelColumn"":""订单时间"",""dbColumn"":""F订单时间""},{""excelColumn"":""揽收时间"",""dbColumn"":""F揽收时间""},{""excelColumn"":""揽收截止时间"",""dbColumn"":""F揽收截止时间""},{""excelColumn"":""订单揽收用时/h"",""dbColumn"":""F订单揽收用时h""},{""excelColumn"":""揽收标识"",""dbColumn"":""F揽收标识""},{""excelColumn"":""揽收及时标识"",""dbColumn"":""F揽收及时标识""},{""excelColumn"":""商家名称"",""dbColumn"":""F商家名称""},{""excelColumn"":""订单网点"",""dbColumn"":""F订单网点""},{""excelColumn"":""订单所属网点"",""dbColumn"":""F订单所属网点""},{""excelColumn"":""揽收网点"",""dbColumn"":""F揽收网点""},{""excelColumn"":""揽收所属网点"",""dbColumn"":""F揽收所属网点""},{""excelColumn"":""收件员"",""dbColumn"":""F收件员""},{""excelColumn"":""订单始发城市"",""dbColumn"":""F订单始发城市""},{""excelColumn"":""订单目的城市"",""dbColumn"":""F订单目的城市""},{""excelColumn"":""仓类型"",""dbColumn"":""F仓类型""},{""excelColumn"":""菜鸟仓编号"",""dbColumn"":""F菜鸟仓编号""},{""excelColumn"":""菜鸟仓名称"",""dbColumn"":""F菜鸟仓名称""},{""excelColumn"":""揽收超15天标识"",""dbColumn"":""F揽收超15天标识""}],""keyFields"":[""运单编号""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F运单编号""],""batchSplit"":{""enabled"":false}}',
        1, N'申通订单揽收分析明细V3 Excel导入配置', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2303（QC_ST_PICKUP_ANALYSIS） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2303)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2303, NULL, 1, GETDATE(), NULL, N'网点质控：申通订单揽收分析明细V3 导入暂存', GETDATE(), NULL, N'申通揽收分析明细导入', NULL, N'QC_ST_PICKUP_ANALYSIS', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""订单揽收分析明细*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2303（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2303)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2303, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2303, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5103（ExcelInput 批次级自动节点，插件注册=1，规则=3103） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5103)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5103, 2303, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3103);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }

    private static void MigrateV26(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_未出仓监控明细 暂存表（系统列 + 13 业务列 + 标准字段） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_未出仓监控明细')
        CREATE TABLE [STG申通_未出仓监控明细] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3104 columnMapping，13 列）
            [F统计日期] NVARCHAR(100) NULL,
            [F运单号] NVARCHAR(200) NULL,
            [F中转站] NVARCHAR(200) NULL,
            [F应签所属网点] NVARCHAR(200) NULL,
            [F应签所属网点编码] NVARCHAR(100) NULL,
            [F应签站点] NVARCHAR(200) NULL,
            [F应签站点编码] NVARCHAR(100) NULL,
            [F派件员] NVARCHAR(200) NULL,
            [F三段码] NVARCHAR(100) NULL,
            [F出仓距离] NVARCHAR(50) NULL,
            [F实际出仓时间] NVARCHAR(100) NULL,
            [F理论应出仓日期] NVARCHAR(100) NULL,
            [F理论应出仓时间] NVARCHAR(100) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_未出仓监控明细_F批次ID' AND object_id = OBJECT_ID(N'STG申通_未出仓监控明细'))
        CREATE INDEX [IX_STG申通_未出仓监控明细_F批次ID] ON [STG申通_未出仓监控明细]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_未出仓监控明细_数据作用域' AND object_id = OBJECT_ID(N'STG申通_未出仓监控明细'))
        CREATE INDEX [IX_STG申通_未出仓监控明细_数据作用域] ON [STG申通_未出仓监控明细]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（运单号 + 组织，仅未撤销 + 运单号非空）
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_未出仓监控明细_运单号_未撤销' AND object_id = OBJECT_ID(N'STG申通_未出仓监控明细'))
        CREATE UNIQUE INDEX [UX_STG申通_未出仓监控明细_运单号_未撤销]
            ON [STG申通_未出仓监控明细]([F运单号],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3104（未出仓监控明细导入） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3104)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3104, 192, N'excelInput', N'申通未出仓监控明细导入规则',
        N'{""targetTable"":""STG申通_未出仓监控明细"",""outputMode"":""stg"",""headerRow"":1,""dataStartRow"":2,""columnIdentifier"":""运单号,应签所属网点,理论应出仓时间,实际出仓时间"",""fullColumnIdentifier"":""统计日期,运单号,中转站,应签所属网点,应签所属网点编码,应签站点,应签站点编码,派件员,三段码,出仓距离,实际出仓时间,理论应出仓日期,理论应出仓时间"",""columnMapping"":[{""excelColumn"":""统计日期"",""dbColumn"":""F统计日期""},{""excelColumn"":""运单号"",""dbColumn"":""F运单号""},{""excelColumn"":""中转站"",""dbColumn"":""F中转站""},{""excelColumn"":""应签所属网点"",""dbColumn"":""F应签所属网点""},{""excelColumn"":""应签所属网点编码"",""dbColumn"":""F应签所属网点编码""},{""excelColumn"":""应签站点"",""dbColumn"":""F应签站点""},{""excelColumn"":""应签站点编码"",""dbColumn"":""F应签站点编码""},{""excelColumn"":""派件员"",""dbColumn"":""F派件员""},{""excelColumn"":""三段码"",""dbColumn"":""F三段码""},{""excelColumn"":""出仓距离"",""dbColumn"":""F出仓距离""},{""excelColumn"":""实际出仓时间"",""dbColumn"":""F实际出仓时间""},{""excelColumn"":""理论应出仓日期"",""dbColumn"":""F理论应出仓日期""},{""excelColumn"":""理论应出仓时间"",""dbColumn"":""F理论应出仓时间""}],""keyFields"":[""运单号""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F运单号""],""batchSplit"":{""enabled"":false}}',
        1, N'申通未出仓实时监控明细 Excel导入配置', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2304（QC_ST_OUTBOUND_MONITOR） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2304)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2304, NULL, 1, GETDATE(), NULL, N'网点质控：申通未出仓实时监控明细 导入暂存', GETDATE(), NULL, N'申通未出仓监控明细导入', NULL, N'QC_ST_OUTBOUND_MONITOR', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""未出仓实时监控*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2304（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2304)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2304, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2304, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5104（ExcelInput 批次级自动节点，插件注册=1，规则=3104） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5104)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5104, 2304, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3104);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }

    private static void MigrateV27(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_交货滞留明细 暂存表（系统列 + 34 业务列 + 标准字段） ═══
        // 注意：「装车/发件网点」含非法字符 /，dbColumn 去掉斜杠为 F装车发件网点（实体/EF/DDL/映射四处一致）。
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_交货滞留明细')
        CREATE TABLE [STG申通_交货滞留明细] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3105 columnMapping，34 列）
            [F业务日期] NVARCHAR(100) NULL,
            [F运单号] NVARCHAR(200) NULL,
            [F电商平台] NVARCHAR(100) NULL,
            [F客户名称] NVARCHAR(200) NULL,
            [F当前交货状态] NVARCHAR(100) NULL,
            [F揽收网点] NVARCHAR(200) NULL,
            [F揽收所属网点] NVARCHAR(200) NULL,
            [F装车发件网点] NVARCHAR(200) NULL,
            [F任务号] NVARCHAR(100) NULL,
            [F车牌号] NVARCHAR(50) NULL,
            [F计划下一站中心] NVARCHAR(200) NULL,
            [F实际下一站中心] NVARCHAR(200) NULL,
            [F装车用时] NVARCHAR(50) NULL,
            [F在途用时] NVARCHAR(50) NULL,
            [F交货用时] NVARCHAR(50) NULL,
            [F揽收时间] NVARCHAR(100) NULL,
            [F网点装车时间] NVARCHAR(100) NULL,
            [F交货时间] NVARCHAR(100) NULL,
            [F交货截止时间] NVARCHAR(100) NULL,
            [F中心到件时间] NVARCHAR(100) NULL,
            [F考核标识] NVARCHAR(50) NULL,
            [F考核达标标识] NVARCHAR(50) NULL,
            [F错发下一站标识] NVARCHAR(50) NULL,
            [F地区件标识] NVARCHAR(50) NULL,
            [F交货滞留截止时间] NVARCHAR(100) NULL,
            [F交货滞留标识] NVARCHAR(50) NULL,
            [F线路类型] NVARCHAR(100) NULL,
            [F内网揽收时间] NVARCHAR(100) NULL,
            [F外网揽收时间] NVARCHAR(100) NULL,
            [F揽收超48h标识] NVARCHAR(50) NULL,
            [F揽收小件员名称] NVARCHAR(200) NULL,
            [F首中心操作时间] NVARCHAR(100) NULL,
            [F考核滞留且揽收超48小时标识] NVARCHAR(50) NULL,
            [F揽收选取类型] NVARCHAR(100) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_交货滞留明细_F批次ID' AND object_id = OBJECT_ID(N'STG申通_交货滞留明细'))
        CREATE INDEX [IX_STG申通_交货滞留明细_F批次ID] ON [STG申通_交货滞留明细]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_交货滞留明细_数据作用域' AND object_id = OBJECT_ID(N'STG申通_交货滞留明细'))
        CREATE INDEX [IX_STG申通_交货滞留明细_数据作用域] ON [STG申通_交货滞留明细]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（运单号 + 组织，仅未撤销 + 运单号非空）
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_交货滞留明细_运单号_未撤销' AND object_id = OBJECT_ID(N'STG申通_交货滞留明细'))
        CREATE UNIQUE INDEX [UX_STG申通_交货滞留明细_运单号_未撤销]
            ON [STG申通_交货滞留明细]([F运单号],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3105（交货滞留明细导入） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3105)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3105, 192, N'excelInput', N'申通交货滞留明细导入规则',
        N'{""targetTable"":""STG申通_交货滞留明细"",""outputMode"":""stg"",""headerRow"":1,""dataStartRow"":2,""columnIdentifier"":""运单号,当前交货状态,交货滞留标识,揽收超48h标识"",""fullColumnIdentifier"":""业务日期,运单号,电商平台,客户名称,当前交货状态,揽收网点,揽收所属网点,装车/发件网点,任务号,车牌号,计划下一站中心,实际下一站中心,装车用时,在途用时,交货用时,揽收时间,网点装车时间,交货时间,交货截止时间,中心到件时间,考核标识,考核达标标识,错发下一站标识,地区件标识,交货滞留截止时间,交货滞留标识,线路类型,内网揽收时间,外网揽收时间,揽收超48h标识,揽收小件员名称,首中心操作时间,考核滞留且揽收超48小时标识,揽收选取类型"",""columnMapping"":[{""excelColumn"":""业务日期"",""dbColumn"":""F业务日期""},{""excelColumn"":""运单号"",""dbColumn"":""F运单号""},{""excelColumn"":""电商平台"",""dbColumn"":""F电商平台""},{""excelColumn"":""客户名称"",""dbColumn"":""F客户名称""},{""excelColumn"":""当前交货状态"",""dbColumn"":""F当前交货状态""},{""excelColumn"":""揽收网点"",""dbColumn"":""F揽收网点""},{""excelColumn"":""揽收所属网点"",""dbColumn"":""F揽收所属网点""},{""excelColumn"":""装车/发件网点"",""dbColumn"":""F装车发件网点""},{""excelColumn"":""任务号"",""dbColumn"":""F任务号""},{""excelColumn"":""车牌号"",""dbColumn"":""F车牌号""},{""excelColumn"":""计划下一站中心"",""dbColumn"":""F计划下一站中心""},{""excelColumn"":""实际下一站中心"",""dbColumn"":""F实际下一站中心""},{""excelColumn"":""装车用时"",""dbColumn"":""F装车用时""},{""excelColumn"":""在途用时"",""dbColumn"":""F在途用时""},{""excelColumn"":""交货用时"",""dbColumn"":""F交货用时""},{""excelColumn"":""揽收时间"",""dbColumn"":""F揽收时间""},{""excelColumn"":""网点装车时间"",""dbColumn"":""F网点装车时间""},{""excelColumn"":""交货时间"",""dbColumn"":""F交货时间""},{""excelColumn"":""交货截止时间"",""dbColumn"":""F交货截止时间""},{""excelColumn"":""中心到件时间"",""dbColumn"":""F中心到件时间""},{""excelColumn"":""考核标识"",""dbColumn"":""F考核标识""},{""excelColumn"":""考核达标标识"",""dbColumn"":""F考核达标标识""},{""excelColumn"":""错发下一站标识"",""dbColumn"":""F错发下一站标识""},{""excelColumn"":""地区件标识"",""dbColumn"":""F地区件标识""},{""excelColumn"":""交货滞留截止时间"",""dbColumn"":""F交货滞留截止时间""},{""excelColumn"":""交货滞留标识"",""dbColumn"":""F交货滞留标识""},{""excelColumn"":""线路类型"",""dbColumn"":""F线路类型""},{""excelColumn"":""内网揽收时间"",""dbColumn"":""F内网揽收时间""},{""excelColumn"":""外网揽收时间"",""dbColumn"":""F外网揽收时间""},{""excelColumn"":""揽收超48h标识"",""dbColumn"":""F揽收超48h标识""},{""excelColumn"":""揽收小件员名称"",""dbColumn"":""F揽收小件员名称""},{""excelColumn"":""首中心操作时间"",""dbColumn"":""F首中心操作时间""},{""excelColumn"":""考核滞留且揽收超48小时标识"",""dbColumn"":""F考核滞留且揽收超48小时标识""},{""excelColumn"":""揽收选取类型"",""dbColumn"":""F揽收选取类型""}],""keyFields"":[""运单号""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F运单号""],""batchSplit"":{""enabled"":false}}',
        1, N'申通网点交货滞留v3明细 Excel导入配置', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2305（QC_ST_HANDOVER_DELAY） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2305)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2305, NULL, 1, GETDATE(), NULL, N'网点质控：申通网点交货滞留v3明细 导入暂存', GETDATE(), NULL, N'申通交货滞留明细导入', NULL, N'QC_ST_HANDOVER_DELAY', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""网点交货滞留v3明细*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2305（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2305)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2305, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2305, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5105（ExcelInput 批次级自动节点，插件注册=1，规则=3105） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5105)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5105, 2305, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3105);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }

    private static void MigrateV28(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_末端派送考核明细 暂存表（系统列 + 63 业务列 + 标准字段） ═══
        // 注意：「当天签收延迟0-24h标识」「当天签收延迟24-48h标识」含非法字符 -，dbColumn 去掉斜杠为 F当天签收延迟024h标识 / F当天签收延迟2448h标识（实体/EF/DDL/映射四处一致）。
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_末端派送考核明细')
        CREATE TABLE [STG申通_末端派送考核明细] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3106 columnMapping，63 列）
            [F运单号] NVARCHAR(200) NULL,
            [F统计日期] NVARCHAR(200) NULL,
            [F中转站名称] NVARCHAR(200) NULL,
            [F应签收所属网点名称] NVARCHAR(200) NULL,
            [F应签收网点名称] NVARCHAR(200) NULL,
            [F发件频次名称] NVARCHAR(200) NULL,
            [F中转站发件时间] NVARCHAR(200) NULL,
            [F派件时间] NVARCHAR(200) NULL,
            [F签收时间] NVARCHAR(200) NULL,
            [F一阶段签收时限] NVARCHAR(200) NULL,
            [F一阶段内签收标识] NVARCHAR(200) NULL,
            [F二阶段签收时限] NVARCHAR(200) NULL,
            [F二阶段内签收标识] NVARCHAR(200) NULL,
            [F当天签收时限] NVARCHAR(200) NULL,
            [F当天签收标识] NVARCHAR(200) NULL,
            [F频次开始时间] NVARCHAR(200) NULL,
            [F频次截止时间] NVARCHAR(200) NULL,
            [F带货网点名称] NVARCHAR(200) NULL,
            [F派件员姓名] NVARCHAR(200) NULL,
            [F四级区域名称] NVARCHAR(200) NULL,
            [F五级区域名称] NVARCHAR(200) NULL,
            [F派件网点名称] NVARCHAR(200) NULL,
            [F签收网点名称] NVARCHAR(200) NULL,
            [F派次类型名称] NVARCHAR(200) NULL,
            [F签收类型名称] NVARCHAR(200) NULL,
            [F签收时长] NVARCHAR(200) NULL,
            [F网点时效用时] NVARCHAR(200) NULL,
            [F时效配置] NVARCHAR(200) NULL,
            [F发件日期] NVARCHAR(200) NULL,
            [FT0延迟签收标识] NVARCHAR(200) NULL,
            [FT1延迟签收标识] NVARCHAR(200) NULL,
            [FT2延迟签收标识] NVARCHAR(200) NULL,
            [FT3延迟签收标识] NVARCHAR(200) NULL,
            [F当天签收延迟024h标识] NVARCHAR(200) NULL,
            [F当天签收延迟2448h标识] NVARCHAR(200) NULL,
            [F当天签收延迟超48h标识] NVARCHAR(200) NULL,
            [F14点签收时限] NVARCHAR(200) NULL,
            [F14点签收标识] NVARCHAR(200) NULL,
            [F20点签收时限] NVARCHAR(200) NULL,
            [F20点签收标识] NVARCHAR(200) NULL,
            [F已签收标识] NVARCHAR(200) NULL,
            [F未签收有问题件标识] NVARCHAR(200) NULL,
            [F已派未签标识] NVARCHAR(200) NULL,
            [F进村件标识] NVARCHAR(200) NULL,
            [F有进村件配置标识] NVARCHAR(200) NULL,
            [F进村件顺延天数] NVARCHAR(200) NULL,
            [F问题件原因] NVARCHAR(200) NULL,
            [F问题件类型名称] NVARCHAR(200) NULL,
            [F问题件登记时间] NVARCHAR(200) NULL,
            [F退回件原因] NVARCHAR(200) NULL,
            [F退回件扫描时间] NVARCHAR(200) NULL,
            [F是否曾经退回标识] NVARCHAR(200) NULL,
            [F是否曾经问题件标识] NVARCHAR(200) NULL,
            [F时效配置类型名称] NVARCHAR(200) NULL,
            [F未签收退回件标识] NVARCHAR(200) NULL,
            [F包号] NVARCHAR(200) NULL,
            [F预售标识] NVARCHAR(200) NULL,
            [F电商平台] NVARCHAR(200) NULL,
            [F配送类型名称] NVARCHAR(200) NULL,
            [F一阶段考核标识] NVARCHAR(200) NULL,
            [F二阶段考核标识] NVARCHAR(200) NULL,
            [F区域时效件] NVARCHAR(200) NULL,
            [F三段码] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_末端派送考核明细_F批次ID' AND object_id = OBJECT_ID(N'STG申通_末端派送考核明细'))
        CREATE INDEX [IX_STG申通_末端派送考核明细_F批次ID] ON [STG申通_末端派送考核明细]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_末端派送考核明细_数据作用域' AND object_id = OBJECT_ID(N'STG申通_末端派送考核明细'))
        CREATE INDEX [IX_STG申通_末端派送考核明细_数据作用域] ON [STG申通_末端派送考核明细]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（运单号 + 组织，仅未撤销 + 运单号非空）
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_末端派送考核明细_运单号_未撤销' AND object_id = OBJECT_ID(N'STG申通_末端派送考核明细'))
        CREATE UNIQUE INDEX [UX_STG申通_末端派送考核明细_运单号_未撤销]
            ON [STG申通_末端派送考核明细]([F运单号],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3106（末端派送考核明细导入） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3106)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3106, 192, N'excelInput', N'申通末端派送考核明细导入规则',
        N'{""targetTable"":""STG申通_末端派送考核明细"",""outputMode"":""stg"",""headerRow"":1,""dataStartRow"":2,""columnIdentifier"":""运单号,一阶段内签收标识,T0延迟签收标识,当天签收标识"",""fullColumnIdentifier"":""运单号,统计日期,中转站名称,应签收所属网点名称,应签收网点名称,发件频次名称,中转站发件时间,派件时间,签收时间,一阶段签收时限,一阶段内签收标识,二阶段签收时限,二阶段内签收标识,当天签收时限,当天签收标识,频次开始时间,频次截止时间,带货网点名称,派件员姓名,四级区域名称,五级区域名称,派件网点名称,签收网点名称,派次类型名称,签收类型名称,签收时长,网点时效用时,时效配置,发件日期,T0延迟签收标识,T1延迟签收标识,T2延迟签收标识,T3延迟签收标识,当天签收延迟0-24h标识,当天签收延迟24-48h标识,当天签收延迟超48h标识,14点签收时限,14点签收标识,20点签收时限,20点签收标识,已签收标识,未签收有问题件标识,已派未签标识,进村件标识,有进村件配置标识,进村件顺延天数,问题件原因,问题件类型名称,问题件登记时间,退回件原因,退回件扫描时间,是否曾经退回标识,是否曾经问题件标识,时效配置类型名称,未签收退回件标识,包号,预售标识,电商平台,配送类型名称,一阶段考核标识,二阶段考核标识,区域时效件,三段码"",""columnMapping"":[{""excelColumn"":""运单号"",""dbColumn"":""F运单号""},{""excelColumn"":""统计日期"",""dbColumn"":""F统计日期""},{""excelColumn"":""中转站名称"",""dbColumn"":""F中转站名称""},{""excelColumn"":""应签收所属网点名称"",""dbColumn"":""F应签收所属网点名称""},{""excelColumn"":""应签收网点名称"",""dbColumn"":""F应签收网点名称""},{""excelColumn"":""发件频次名称"",""dbColumn"":""F发件频次名称""},{""excelColumn"":""中转站发件时间"",""dbColumn"":""F中转站发件时间""},{""excelColumn"":""派件时间"",""dbColumn"":""F派件时间""},{""excelColumn"":""签收时间"",""dbColumn"":""F签收时间""},{""excelColumn"":""一阶段签收时限"",""dbColumn"":""F一阶段签收时限""},{""excelColumn"":""一阶段内签收标识"",""dbColumn"":""F一阶段内签收标识""},{""excelColumn"":""二阶段签收时限"",""dbColumn"":""F二阶段签收时限""},{""excelColumn"":""二阶段内签收标识"",""dbColumn"":""F二阶段内签收标识""},{""excelColumn"":""当天签收时限"",""dbColumn"":""F当天签收时限""},{""excelColumn"":""当天签收标识"",""dbColumn"":""F当天签收标识""},{""excelColumn"":""频次开始时间"",""dbColumn"":""F频次开始时间""},{""excelColumn"":""频次截止时间"",""dbColumn"":""F频次截止时间""},{""excelColumn"":""带货网点名称"",""dbColumn"":""F带货网点名称""},{""excelColumn"":""派件员姓名"",""dbColumn"":""F派件员姓名""},{""excelColumn"":""四级区域名称"",""dbColumn"":""F四级区域名称""},{""excelColumn"":""五级区域名称"",""dbColumn"":""F五级区域名称""},{""excelColumn"":""派件网点名称"",""dbColumn"":""F派件网点名称""},{""excelColumn"":""签收网点名称"",""dbColumn"":""F签收网点名称""},{""excelColumn"":""派次类型名称"",""dbColumn"":""F派次类型名称""},{""excelColumn"":""签收类型名称"",""dbColumn"":""F签收类型名称""},{""excelColumn"":""签收时长"",""dbColumn"":""F签收时长""},{""excelColumn"":""网点时效用时"",""dbColumn"":""F网点时效用时""},{""excelColumn"":""时效配置"",""dbColumn"":""F时效配置""},{""excelColumn"":""发件日期"",""dbColumn"":""F发件日期""},{""excelColumn"":""T0延迟签收标识"",""dbColumn"":""FT0延迟签收标识""},{""excelColumn"":""T1延迟签收标识"",""dbColumn"":""FT1延迟签收标识""},{""excelColumn"":""T2延迟签收标识"",""dbColumn"":""FT2延迟签收标识""},{""excelColumn"":""T3延迟签收标识"",""dbColumn"":""FT3延迟签收标识""},{""excelColumn"":""当天签收延迟0-24h标识"",""dbColumn"":""F当天签收延迟024h标识""},{""excelColumn"":""当天签收延迟24-48h标识"",""dbColumn"":""F当天签收延迟2448h标识""},{""excelColumn"":""当天签收延迟超48h标识"",""dbColumn"":""F当天签收延迟超48h标识""},{""excelColumn"":""14点签收时限"",""dbColumn"":""F14点签收时限""},{""excelColumn"":""14点签收标识"",""dbColumn"":""F14点签收标识""},{""excelColumn"":""20点签收时限"",""dbColumn"":""F20点签收时限""},{""excelColumn"":""20点签收标识"",""dbColumn"":""F20点签收标识""},{""excelColumn"":""已签收标识"",""dbColumn"":""F已签收标识""},{""excelColumn"":""未签收有问题件标识"",""dbColumn"":""F未签收有问题件标识""},{""excelColumn"":""已派未签标识"",""dbColumn"":""F已派未签标识""},{""excelColumn"":""进村件标识"",""dbColumn"":""F进村件标识""},{""excelColumn"":""有进村件配置标识"",""dbColumn"":""F有进村件配置标识""},{""excelColumn"":""进村件顺延天数"",""dbColumn"":""F进村件顺延天数""},{""excelColumn"":""问题件原因"",""dbColumn"":""F问题件原因""},{""excelColumn"":""问题件类型名称"",""dbColumn"":""F问题件类型名称""},{""excelColumn"":""问题件登记时间"",""dbColumn"":""F问题件登记时间""},{""excelColumn"":""退回件原因"",""dbColumn"":""F退回件原因""},{""excelColumn"":""退回件扫描时间"",""dbColumn"":""F退回件扫描时间""},{""excelColumn"":""是否曾经退回标识"",""dbColumn"":""F是否曾经退回标识""},{""excelColumn"":""是否曾经问题件标识"",""dbColumn"":""F是否曾经问题件标识""},{""excelColumn"":""时效配置类型名称"",""dbColumn"":""F时效配置类型名称""},{""excelColumn"":""未签收退回件标识"",""dbColumn"":""F未签收退回件标识""},{""excelColumn"":""包号"",""dbColumn"":""F包号""},{""excelColumn"":""预售标识"",""dbColumn"":""F预售标识""},{""excelColumn"":""电商平台"",""dbColumn"":""F电商平台""},{""excelColumn"":""配送类型名称"",""dbColumn"":""F配送类型名称""},{""excelColumn"":""一阶段考核标识"",""dbColumn"":""F一阶段考核标识""},{""excelColumn"":""二阶段考核标识"",""dbColumn"":""F二阶段考核标识""},{""excelColumn"":""区域时效件"",""dbColumn"":""F区域时效件""},{""excelColumn"":""三段码"",""dbColumn"":""F三段码""}],""keyFields"":[""运单号""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F运单号""],""batchSplit"":{""enabled"":false}}',
        1, N'申通末端派送考核(新)明细V2 Excel导入配置', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2306（QC_ST_DELIVERY_ASSESS） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2306)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2306, NULL, 1, GETDATE(), NULL, N'网点质控：申通末端派送考核(新)明细V2 导入暂存', GETDATE(), NULL, N'申通末端派送考核明细导入', NULL, N'QC_ST_DELIVERY_ASSESS', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""末端派送考核(新)明细*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2306（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2306)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2306, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2306, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5106（ExcelInput 批次级自动节点，插件注册=1，规则=3106） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5106)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5106, 2306, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3106);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }

    private static void MigrateV29(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_签收未达标明细 暂存表（系统列 + 15 业务列 + 标准字段） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_签收未达标明细')
        CREATE TABLE [STG申通_签收未达标明细] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3107 columnMapping，15 列）
            [F运单号] NVARCHAR(200) NULL,
            [F应签网点] NVARCHAR(200) NULL,
            [F应签网点所属独立网点] NVARCHAR(200) NULL,
            [F应签日期] NVARCHAR(200) NULL,
            [F签收时间] NVARCHAR(200) NULL,
            [F业务员] NVARCHAR(200) NULL,
            [F当日签收标识] NVARCHAR(200) NULL,
            [F派件网点] NVARCHAR(200) NULL,
            [F签收网点] NVARCHAR(200) NULL,
            [F签收网点所属独立网点] NVARCHAR(200) NULL,
            [F是否已签收] NVARCHAR(200) NULL,
            [F是否未签收有问题件] NVARCHAR(200) NULL,
            [F是否曾经退回件] NVARCHAR(200) NULL,
            [F退回扫描时间] NVARCHAR(200) NULL,
            [F是否曾经问题件] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_签收未达标明细_F批次ID' AND object_id = OBJECT_ID(N'STG申通_签收未达标明细'))
        CREATE INDEX [IX_STG申通_签收未达标明细_F批次ID] ON [STG申通_签收未达标明细]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_签收未达标明细_数据作用域' AND object_id = OBJECT_ID(N'STG申通_签收未达标明细'))
        CREATE INDEX [IX_STG申通_签收未达标明细_数据作用域] ON [STG申通_签收未达标明细]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（运单号 + 应签日期 + 组织，仅未撤销 + 运单号非空）
        -- 签收未达标明细按「运单号 + 应签日期」去重；2 字段以兼容 ExcelInputPlugin。
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_签收未达标明细_运单应签日期_未撤销' AND object_id = OBJECT_ID(N'STG申通_签收未达标明细'))
        CREATE UNIQUE INDEX [UX_STG申通_签收未达标明细_运单应签日期_未撤销]
            ON [STG申通_签收未达标明细]([F运单号],[F应签日期],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3107（签收未达标明细导入） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3107)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3107, 192, N'excelInput', N'申通签收未达标明细导入规则',
        N'{""targetTable"":""STG申通_签收未达标明细"",""outputMode"":""stg"",""headerRow"":1,""dataStartRow"":2,""columnIdentifier"":""运单号,应签网点,应签日期,当日签收标识,是否未签收有问题件"",""fullColumnIdentifier"":""运单号,应签网点,应签网点所属独立网点,应签日期,签收时间,业务员,当日签收标识,派件网点,签收网点,签收网点所属独立网点,是否已签收,是否未签收有问题件,是否曾经退回件,退回扫描时间,是否曾经问题件"",""columnMapping"":[{""excelColumn"":""运单号"",""dbColumn"":""F运单号""},{""excelColumn"":""应签网点"",""dbColumn"":""F应签网点""},{""excelColumn"":""应签网点所属独立网点"",""dbColumn"":""F应签网点所属独立网点""},{""excelColumn"":""应签日期"",""dbColumn"":""F应签日期""},{""excelColumn"":""签收时间"",""dbColumn"":""F签收时间""},{""excelColumn"":""业务员"",""dbColumn"":""F业务员""},{""excelColumn"":""当日签收标识"",""dbColumn"":""F当日签收标识""},{""excelColumn"":""派件网点"",""dbColumn"":""F派件网点""},{""excelColumn"":""签收网点"",""dbColumn"":""F签收网点""},{""excelColumn"":""签收网点所属独立网点"",""dbColumn"":""F签收网点所属独立网点""},{""excelColumn"":""是否已签收"",""dbColumn"":""F是否已签收""},{""excelColumn"":""是否未签收有问题件"",""dbColumn"":""F是否未签收有问题件""},{""excelColumn"":""是否曾经退回件"",""dbColumn"":""F是否曾经退回件""},{""excelColumn"":""退回扫描时间"",""dbColumn"":""F退回扫描时间""},{""excelColumn"":""是否曾经问题件"",""dbColumn"":""F是否曾经问题件""}],""keyFields"":[""运单号"",""应签日期""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F运单号"",""F应签日期""],""batchSplit"":{""enabled"":false}}',
        1, N'申通签收未达标明细 Excel导入配置', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2307（QC_ST_SIGN_SUBSTANDARD） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2307)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2307, NULL, 1, GETDATE(), NULL, N'网点质控：申通签收未达标明细 导入暂存', GETDATE(), NULL, N'申通签收未达标明细导入', NULL, N'QC_ST_SIGN_SUBSTANDARD', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""*签收未达标*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2307（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2307)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2307, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2307, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5107（ExcelInput 批次级自动节点，插件注册=1，规则=3107） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5107)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5107, 2307, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3107);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }

    private static void MigrateV30(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_积压明细 暂存表（系统列 + 41 业务列 + 标准字段） ═══
        // 注意：「积压8-15天标识」「积压16-30天标识」「积压31-60天标识」含非法字符 -，dbColumn 去掉斜杠为 F积压815天标识 / F积压1630天标识 / F积压3160天标识（实体/EF/DDL/映射四处一致）。
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_积压明细')
        CREATE TABLE [STG申通_积压明细] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3108 columnMapping，41 列）
            [F业务日期] NVARCHAR(200) NULL,
            [F运单号] NVARCHAR(200) NULL,
            [F大区名称] NVARCHAR(200) NULL,
            [F省区名称] NVARCHAR(200) NULL,
            [F省份名称] NVARCHAR(200) NULL,
            [F所属网点编码] NVARCHAR(200) NULL,
            [F所属网点名称] NVARCHAR(200) NULL,
            [F应签网点编码] NVARCHAR(200) NULL,
            [F应签网点] NVARCHAR(200) NULL,
            [F四级区域编码] NVARCHAR(200) NULL,
            [F四级区域] NVARCHAR(200) NULL,
            [F三段码] NVARCHAR(200) NULL,
            [F最后扫描组织编码] NVARCHAR(200) NULL,
            [F最后扫描组织名称] NVARCHAR(200) NULL,
            [F最后扫描组织父级编码] NVARCHAR(200) NULL,
            [F最后扫描组织父级名称] NVARCHAR(200) NULL,
            [F最后扫描时间] NVARCHAR(200) NULL,
            [F最后扫描类型] NVARCHAR(200) NULL,
            [F最后扫描类型编码] NVARCHAR(200) NULL,
            [F扫描员] NVARCHAR(200) NULL,
            [F扫描员编码] NVARCHAR(200) NULL,
            [F业务员] NVARCHAR(200) NULL,
            [F业务员编码] NVARCHAR(200) NULL,
            [F问题件一级类型] NVARCHAR(200) NULL,
            [F问题件二级类型] NVARCHAR(200) NULL,
            [F退回件标识] NVARCHAR(200) NULL,
            [F积压1天标识] NVARCHAR(200) NULL,
            [F积压2天标识] NVARCHAR(200) NULL,
            [F积压3天标识] NVARCHAR(200) NULL,
            [F积压4天标识] NVARCHAR(200) NULL,
            [F积压5天标识] NVARCHAR(200) NULL,
            [F积压六6天标识] NVARCHAR(200) NULL,
            [F积压7天标识] NVARCHAR(200) NULL,
            [F积压815天标识] NVARCHAR(200) NULL,
            [F积压1630天标识] NVARCHAR(200) NULL,
            [F积压3160天标识] NVARCHAR(200) NULL,
            [F超过3天标识] NVARCHAR(200) NULL,
            [F超过5天标识] NVARCHAR(200) NULL,
            [F超过7天标识] NVARCHAR(200) NULL,
            [F是否积压剔除标识] NVARCHAR(200) NULL,
            [F是否实时签收] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_积压明细_F批次ID' AND object_id = OBJECT_ID(N'STG申通_积压明细'))
        CREATE INDEX [IX_STG申通_积压明细_F批次ID] ON [STG申通_积压明细]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_积压明细_数据作用域' AND object_id = OBJECT_ID(N'STG申通_积压明细'))
        CREATE INDEX [IX_STG申通_积压明细_数据作用域] ON [STG申通_积压明细]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（运单号 + 组织，仅未撤销 + 运单号非空）
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_积压明细_运单号_未撤销' AND object_id = OBJECT_ID(N'STG申通_积压明细'))
        CREATE UNIQUE INDEX [UX_STG申通_积压明细_运单号_未撤销]
            ON [STG申通_积压明细]([F运单号],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3108（积压明细导入） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3108)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3108, 192, N'excelInput', N'申通积压明细导入规则',
        N'{""targetTable"":""STG申通_积压明细"",""outputMode"":""stg"",""headerRow"":1,""dataStartRow"":2,""columnIdentifier"":""运单号,积压3天标识,问题件一级类型,最后扫描类型"",""fullColumnIdentifier"":""业务日期,运单号,大区名称,省区名称,省份名称,所属网点编码,所属网点名称,应签网点编码,应签网点,四级区域编码,四级区域,三段码,最后扫描组织编码,最后扫描组织名称,最后扫描组织父级编码,最后扫描组织父级名称,最后扫描时间,最后扫描类型,最后扫描类型编码,扫描员,扫描员编码,业务员,业务员编码,问题件一级类型,问题件二级类型,退回件标识,积压1天标识,积压2天标识,积压3天标识,积压4天标识,积压5天标识,积压六6天标识,积压7天标识,积压8-15天标识,积压16-30天标识,积压31-60天标识,超过3天标识,超过5天标识,超过7天标识,是否积压剔除标识,是否实时签收"",""columnMapping"":[{""excelColumn"":""业务日期"",""dbColumn"":""F业务日期""},{""excelColumn"":""运单号"",""dbColumn"":""F运单号""},{""excelColumn"":""大区名称"",""dbColumn"":""F大区名称""},{""excelColumn"":""省区名称"",""dbColumn"":""F省区名称""},{""excelColumn"":""省份名称"",""dbColumn"":""F省份名称""},{""excelColumn"":""所属网点编码"",""dbColumn"":""F所属网点编码""},{""excelColumn"":""所属网点名称"",""dbColumn"":""F所属网点名称""},{""excelColumn"":""应签网点编码"",""dbColumn"":""F应签网点编码""},{""excelColumn"":""应签网点"",""dbColumn"":""F应签网点""},{""excelColumn"":""四级区域编码"",""dbColumn"":""F四级区域编码""},{""excelColumn"":""四级区域"",""dbColumn"":""F四级区域""},{""excelColumn"":""三段码"",""dbColumn"":""F三段码""},{""excelColumn"":""最后扫描组织编码"",""dbColumn"":""F最后扫描组织编码""},{""excelColumn"":""最后扫描组织名称"",""dbColumn"":""F最后扫描组织名称""},{""excelColumn"":""最后扫描组织父级编码"",""dbColumn"":""F最后扫描组织父级编码""},{""excelColumn"":""最后扫描组织父级名称"",""dbColumn"":""F最后扫描组织父级名称""},{""excelColumn"":""最后扫描时间"",""dbColumn"":""F最后扫描时间""},{""excelColumn"":""最后扫描类型"",""dbColumn"":""F最后扫描类型""},{""excelColumn"":""最后扫描类型编码"",""dbColumn"":""F最后扫描类型编码""},{""excelColumn"":""扫描员"",""dbColumn"":""F扫描员""},{""excelColumn"":""扫描员编码"",""dbColumn"":""F扫描员编码""},{""excelColumn"":""业务员"",""dbColumn"":""F业务员""},{""excelColumn"":""业务员编码"",""dbColumn"":""F业务员编码""},{""excelColumn"":""问题件一级类型"",""dbColumn"":""F问题件一级类型""},{""excelColumn"":""问题件二级类型"",""dbColumn"":""F问题件二级类型""},{""excelColumn"":""退回件标识"",""dbColumn"":""F退回件标识""},{""excelColumn"":""积压1天标识"",""dbColumn"":""F积压1天标识""},{""excelColumn"":""积压2天标识"",""dbColumn"":""F积压2天标识""},{""excelColumn"":""积压3天标识"",""dbColumn"":""F积压3天标识""},{""excelColumn"":""积压4天标识"",""dbColumn"":""F积压4天标识""},{""excelColumn"":""积压5天标识"",""dbColumn"":""F积压5天标识""},{""excelColumn"":""积压六6天标识"",""dbColumn"":""F积压六6天标识""},{""excelColumn"":""积压7天标识"",""dbColumn"":""F积压7天标识""},{""excelColumn"":""积压8-15天标识"",""dbColumn"":""F积压815天标识""},{""excelColumn"":""积压16-30天标识"",""dbColumn"":""F积压1630天标识""},{""excelColumn"":""积压31-60天标识"",""dbColumn"":""F积压3160天标识""},{""excelColumn"":""超过3天标识"",""dbColumn"":""F超过3天标识""},{""excelColumn"":""超过5天标识"",""dbColumn"":""F超过5天标识""},{""excelColumn"":""超过7天标识"",""dbColumn"":""F超过7天标识""},{""excelColumn"":""是否积压剔除标识"",""dbColumn"":""F是否积压剔除标识""},{""excelColumn"":""是否实时签收"",""dbColumn"":""F是否实时签收""}],""keyFields"":[""运单号""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F运单号""],""batchSplit"":{""enabled"":false}}',
        1, N'申通末端时效积压明细 Excel导入配置', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2308（QC_ST_BACKLOG） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2308)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2308, NULL, 1, GETDATE(), NULL, N'网点质控：申通末端时效积压明细 导入暂存', GETDATE(), NULL, N'申通积压明细导入', NULL, N'QC_ST_BACKLOG', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""末端时效积压明细*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2308（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2308)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2308, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2308, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5108（ExcelInput 批次级自动节点，插件注册=1，规则=3108） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5108)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5108, 2308, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3108);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }

    private static void MigrateV31(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_疑似遗失明细 暂存表（系统列 + 51 业务列 + 标准字段） ═══
        // 注意：「结算重量(kg)」「找回时长(h)」含非法字符 ()，dbColumn 去掉斜杠为 F结算重量kg / F找回时长h（实体/EF/DDL/映射四处一致）。
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_疑似遗失明细')
        CREATE TABLE [STG申通_疑似遗失明细] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3109 columnMapping，51 列）
            [F运单号] NVARCHAR(200) NULL,
            [F先行理赔状态] NVARCHAR(200) NULL,
            [F是否找回] NVARCHAR(200) NULL,
            [F签收标识] NVARCHAR(200) NULL,
            [F实际金额] NVARCHAR(200) NULL,
            [F是否疫情件] NVARCHAR(200) NULL,
            [F内件品名] NVARCHAR(200) NULL,
            [F结算重量kg] NVARCHAR(200) NULL,
            [F订单来源] NVARCHAR(200) NULL,
            [F3日轨迹中断触发类型] NVARCHAR(200) NULL,
            [F包号] NVARCHAR(200) NULL,
            [F集包站点] NVARCHAR(200) NULL,
            [F扫描站点] NVARCHAR(200) NULL,
            [F扫描站点所属省份] NVARCHAR(200) NULL,
            [F扫描站点所属南北区] NVARCHAR(200) NULL,
            [F最后扫描时间] NVARCHAR(200) NULL,
            [F扫描操作人] NVARCHAR(200) NULL,
            [F业务员] NVARCHAR(200) NULL,
            [F下一节点操作截止时间] NVARCHAR(200) NULL,
            [F3日轨迹中断触发时间] NVARCHAR(200) NULL,
            [F找货责任方1] NVARCHAR(200) NULL,
            [F找件责任1所属网点名称] NVARCHAR(200) NULL,
            [F找货责任方2] NVARCHAR(200) NULL,
            [F找件责任2所属网点名称] NVARCHAR(200) NULL,
            [F运输任务号] NVARCHAR(200) NULL,
            [F承运商] NVARCHAR(200) NULL,
            [F车牌号] NVARCHAR(200) NULL,
            [F揽收省份] NVARCHAR(200) NULL,
            [F揽收网点] NVARCHAR(200) NULL,
            [F问题件类型] NVARCHAR(200) NULL,
            [F退回件标识] NVARCHAR(200) NULL,
            [F拦截件标识] NVARCHAR(200) NULL,
            [F停滞用时] NVARCHAR(200) NULL,
            [F是否理赔] NVARCHAR(200) NULL,
            [F目的地省份] NVARCHAR(200) NULL,
            [F目的地网点] NVARCHAR(200) NULL,
            [F找回时的扫描类型] NVARCHAR(200) NULL,
            [F找回时的扫描站点] NVARCHAR(200) NULL,
            [F找回时间] NVARCHAR(200) NULL,
            [F找回时长h] NVARCHAR(200) NULL,
            [F下一站] NVARCHAR(200) NULL,
            [F下一站省份] NVARCHAR(200) NULL,
            [F下一站所属南北区] NVARCHAR(200) NULL,
            [F责任方1所属省区] NVARCHAR(200) NULL,
            [F责任方2所属省区] NVARCHAR(200) NULL,
            [F订单网点] NVARCHAR(200) NULL,
            [F订单省份] NVARCHAR(200) NULL,
            [F考核剔除项] NVARCHAR(200) NULL,
            [F商家编码] NVARCHAR(200) NULL,
            [F商家名称] NVARCHAR(200) NULL,
            [F任务不发起原因] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_疑似遗失明细_F批次ID' AND object_id = OBJECT_ID(N'STG申通_疑似遗失明细'))
        CREATE INDEX [IX_STG申通_疑似遗失明细_F批次ID] ON [STG申通_疑似遗失明细]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_疑似遗失明细_数据作用域' AND object_id = OBJECT_ID(N'STG申通_疑似遗失明细'))
        CREATE INDEX [IX_STG申通_疑似遗失明细_数据作用域] ON [STG申通_疑似遗失明细]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（运单号 + 组织，仅未撤销 + 运单号非空）
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_疑似遗失明细_运单号_未撤销' AND object_id = OBJECT_ID(N'STG申通_疑似遗失明细'))
        CREATE UNIQUE INDEX [UX_STG申通_疑似遗失明细_运单号_未撤销]
            ON [STG申通_疑似遗失明细]([F运单号],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3109（疑似遗失明细导入） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3109)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3109, 192, N'excelInput', N'申通疑似遗失明细导入规则',
        N'{""targetTable"":""STG申通_疑似遗失明细"",""outputMode"":""stg"",""headerRow"":1,""dataStartRow"":2,""columnIdentifier"":""运单号,3日轨迹中断触发类型,是否找回,找货责任方1"",""fullColumnIdentifier"":""运单号,先行理赔状态,是否找回,签收标识,实际金额,是否疫情件,内件品名,结算重量(kg),订单来源,3日轨迹中断触发类型,包号,集包站点,扫描站点,扫描站点所属省份,扫描站点所属南北区,最后扫描时间,扫描操作人,业务员,下一节点操作截止时间,3日轨迹中断触发时间,找货责任方1,找件责任1所属网点名称,找货责任方2,找件责任2所属网点名称,运输任务号,承运商,车牌号,揽收省份,揽收网点,问题件类型,退回件标识,拦截件标识,停滞用时,是否理赔,目的地省份,目的地网点,找回时的扫描类型,找回时的扫描站点,找回时间,找回时长(h),下一站,下一站省份,下一站所属南北区,责任方1所属省区,责任方2所属省区,订单网点,订单省份,考核剔除项,商家编码,商家名称,任务不发起原因"",""columnMapping"":[{""excelColumn"":""运单号"",""dbColumn"":""F运单号""},{""excelColumn"":""先行理赔状态"",""dbColumn"":""F先行理赔状态""},{""excelColumn"":""是否找回"",""dbColumn"":""F是否找回""},{""excelColumn"":""签收标识"",""dbColumn"":""F签收标识""},{""excelColumn"":""实际金额"",""dbColumn"":""F实际金额""},{""excelColumn"":""是否疫情件"",""dbColumn"":""F是否疫情件""},{""excelColumn"":""内件品名"",""dbColumn"":""F内件品名""},{""excelColumn"":""结算重量(kg)"",""dbColumn"":""F结算重量kg""},{""excelColumn"":""订单来源"",""dbColumn"":""F订单来源""},{""excelColumn"":""3日轨迹中断触发类型"",""dbColumn"":""F3日轨迹中断触发类型""},{""excelColumn"":""包号"",""dbColumn"":""F包号""},{""excelColumn"":""集包站点"",""dbColumn"":""F集包站点""},{""excelColumn"":""扫描站点"",""dbColumn"":""F扫描站点""},{""excelColumn"":""扫描站点所属省份"",""dbColumn"":""F扫描站点所属省份""},{""excelColumn"":""扫描站点所属南北区"",""dbColumn"":""F扫描站点所属南北区""},{""excelColumn"":""最后扫描时间"",""dbColumn"":""F最后扫描时间""},{""excelColumn"":""扫描操作人"",""dbColumn"":""F扫描操作人""},{""excelColumn"":""业务员"",""dbColumn"":""F业务员""},{""excelColumn"":""下一节点操作截止时间"",""dbColumn"":""F下一节点操作截止时间""},{""excelColumn"":""3日轨迹中断触发时间"",""dbColumn"":""F3日轨迹中断触发时间""},{""excelColumn"":""找货责任方1"",""dbColumn"":""F找货责任方1""},{""excelColumn"":""找件责任1所属网点名称"",""dbColumn"":""F找件责任1所属网点名称""},{""excelColumn"":""找货责任方2"",""dbColumn"":""F找货责任方2""},{""excelColumn"":""找件责任2所属网点名称"",""dbColumn"":""F找件责任2所属网点名称""},{""excelColumn"":""运输任务号"",""dbColumn"":""F运输任务号""},{""excelColumn"":""承运商"",""dbColumn"":""F承运商""},{""excelColumn"":""车牌号"",""dbColumn"":""F车牌号""},{""excelColumn"":""揽收省份"",""dbColumn"":""F揽收省份""},{""excelColumn"":""揽收网点"",""dbColumn"":""F揽收网点""},{""excelColumn"":""问题件类型"",""dbColumn"":""F问题件类型""},{""excelColumn"":""退回件标识"",""dbColumn"":""F退回件标识""},{""excelColumn"":""拦截件标识"",""dbColumn"":""F拦截件标识""},{""excelColumn"":""停滞用时"",""dbColumn"":""F停滞用时""},{""excelColumn"":""是否理赔"",""dbColumn"":""F是否理赔""},{""excelColumn"":""目的地省份"",""dbColumn"":""F目的地省份""},{""excelColumn"":""目的地网点"",""dbColumn"":""F目的地网点""},{""excelColumn"":""找回时的扫描类型"",""dbColumn"":""F找回时的扫描类型""},{""excelColumn"":""找回时的扫描站点"",""dbColumn"":""F找回时的扫描站点""},{""excelColumn"":""找回时间"",""dbColumn"":""F找回时间""},{""excelColumn"":""找回时长(h)"",""dbColumn"":""F找回时长h""},{""excelColumn"":""下一站"",""dbColumn"":""F下一站""},{""excelColumn"":""下一站省份"",""dbColumn"":""F下一站省份""},{""excelColumn"":""下一站所属南北区"",""dbColumn"":""F下一站所属南北区""},{""excelColumn"":""责任方1所属省区"",""dbColumn"":""F责任方1所属省区""},{""excelColumn"":""责任方2所属省区"",""dbColumn"":""F责任方2所属省区""},{""excelColumn"":""订单网点"",""dbColumn"":""F订单网点""},{""excelColumn"":""订单省份"",""dbColumn"":""F订单省份""},{""excelColumn"":""考核剔除项"",""dbColumn"":""F考核剔除项""},{""excelColumn"":""商家编码"",""dbColumn"":""F商家编码""},{""excelColumn"":""商家名称"",""dbColumn"":""F商家名称""},{""excelColumn"":""任务不发起原因"",""dbColumn"":""F任务不发起原因""}],""keyFields"":[""运单号""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F运单号""],""batchSplit"":{""enabled"":false}}',
        1, N'申通网点疑似遗失明细导出v5 Excel导入配置', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2309（QC_ST_SUSPECTED_LOSS） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2309)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2309, NULL, 1, GETDATE(), NULL, N'网点质控：申通网点疑似遗失明细导出v5 导入暂存', GETDATE(), NULL, N'申通疑似遗失明细导入', NULL, N'QC_ST_SUSPECTED_LOSS', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""*疑似遗失明细*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2309（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2309)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2309, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2309, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5109（ExcelInput 批次级自动节点，插件注册=1，规则=3109） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5109)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5109, 2309, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3109);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }
    private static void MigrateV32(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_进港投诉明细 暂存表（系统列 + 29 业务列 + 标准字段） ═══
        // 注意：「进港/出港」含非法字符 /，dbColumn 去掉斜杠为 F进港出港（实体/EF/DDL/映射四处一致；columnMapping 的 excelColumn 保留原文 进港/出港）。
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_进港投诉明细')
        CREATE TABLE [STG申通_进港投诉明细] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3110 columnMapping，29 列）
            [F统计日期] NVARCHAR(200) NULL,
            [F运单号] NVARCHAR(200) NULL,
            [F投诉类型] NVARCHAR(200) NULL,
            [F工单内容] NVARCHAR(200) NULL,
            [F大区名称] NVARCHAR(200) NULL,
            [F省区名称] NVARCHAR(200) NULL,
            [F行政省名称] NVARCHAR(200) NULL,
            [F片区名称] NVARCHAR(200) NULL,
            [F所属网点编码] NVARCHAR(200) NULL,
            [F所属网点名称] NVARCHAR(200) NULL,
            [F承包区编码] NVARCHAR(200) NULL,
            [F承包区名称] NVARCHAR(200) NULL,
            [F小件员编码] NVARCHAR(200) NULL,
            [F小件员名称] NVARCHAR(200) NULL,
            [F工单类型编码] NVARCHAR(200) NULL,
            [F工单类型名称] NVARCHAR(200) NULL,
            [F工单源编码] NVARCHAR(200) NULL,
            [F工单源名称] NVARCHAR(200) NULL,
            [F工单创建时间] NVARCHAR(200) NULL,
            [F最后到件扫描时间] NVARCHAR(200) NULL,
            [F到件扫描组织编码] NVARCHAR(200) NULL,
            [F到件扫描组织名称] NVARCHAR(200) NULL,
            [F签收时间] NVARCHAR(200) NULL,
            [F签收类型] NVARCHAR(200) NULL,
            [F代收点名称] NVARCHAR(200) NULL,
            [F末端滞留天数] NVARCHAR(200) NULL,
            [F是否按需派送标] NVARCHAR(200) NULL,
            [F进港出港] NVARCHAR(200) NULL,
            [F差行为原因] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_进港投诉明细_F批次ID' AND object_id = OBJECT_ID(N'STG申通_进港投诉明细'))
        CREATE INDEX [IX_STG申通_进港投诉明细_F批次ID] ON [STG申通_进港投诉明细]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_进港投诉明细_数据作用域' AND object_id = OBJECT_ID(N'STG申通_进港投诉明细'))
        CREATE INDEX [IX_STG申通_进港投诉明细_数据作用域] ON [STG申通_进港投诉明细]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（运单号 + 工单创建时间 + 组织，仅未撤销 + 运单号非空）
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_进港投诉明细_运单工单时间_未撤销' AND object_id = OBJECT_ID(N'STG申通_进港投诉明细'))
        CREATE UNIQUE INDEX [UX_STG申通_进港投诉明细_运单工单时间_未撤销]
            ON [STG申通_进港投诉明细]([F运单号],[F工单创建时间],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3110（进港投诉明细导入） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3110)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3110, 192, N'excelInput', N'申通进港投诉明细导入规则',
        N'{""targetTable"":""STG申通_进港投诉明细"",""outputMode"":""stg"",""headerRow"":1,""dataStartRow"":2,""columnIdentifier"":""运单号,投诉类型,工单类型名称,进港/出港"",""fullColumnIdentifier"":""统计日期,运单号,投诉类型,工单内容,大区名称,省区名称,行政省名称,片区名称,所属网点编码,所属网点名称,承包区编码,承包区名称,小件员编码,小件员名称,工单类型编码,工单类型名称,工单源编码,工单源名称,工单创建时间,最后到件扫描时间,到件扫描组织编码,到件扫描组织名称,签收时间,签收类型,代收点名称,末端滞留天数,是否按需派送标,进港/出港,差行为原因"",""columnMapping"":[{""excelColumn"":""统计日期"",""dbColumn"":""F统计日期""},{""excelColumn"":""运单号"",""dbColumn"":""F运单号""},{""excelColumn"":""投诉类型"",""dbColumn"":""F投诉类型""},{""excelColumn"":""工单内容"",""dbColumn"":""F工单内容""},{""excelColumn"":""大区名称"",""dbColumn"":""F大区名称""},{""excelColumn"":""省区名称"",""dbColumn"":""F省区名称""},{""excelColumn"":""行政省名称"",""dbColumn"":""F行政省名称""},{""excelColumn"":""片区名称"",""dbColumn"":""F片区名称""},{""excelColumn"":""所属网点编码"",""dbColumn"":""F所属网点编码""},{""excelColumn"":""所属网点名称"",""dbColumn"":""F所属网点名称""},{""excelColumn"":""承包区编码"",""dbColumn"":""F承包区编码""},{""excelColumn"":""承包区名称"",""dbColumn"":""F承包区名称""},{""excelColumn"":""小件员编码"",""dbColumn"":""F小件员编码""},{""excelColumn"":""小件员名称"",""dbColumn"":""F小件员名称""},{""excelColumn"":""工单类型编码"",""dbColumn"":""F工单类型编码""},{""excelColumn"":""工单类型名称"",""dbColumn"":""F工单类型名称""},{""excelColumn"":""工单源编码"",""dbColumn"":""F工单源编码""},{""excelColumn"":""工单源名称"",""dbColumn"":""F工单源名称""},{""excelColumn"":""工单创建时间"",""dbColumn"":""F工单创建时间""},{""excelColumn"":""最后到件扫描时间"",""dbColumn"":""F最后到件扫描时间""},{""excelColumn"":""到件扫描组织编码"",""dbColumn"":""F到件扫描组织编码""},{""excelColumn"":""到件扫描组织名称"",""dbColumn"":""F到件扫描组织名称""},{""excelColumn"":""签收时间"",""dbColumn"":""F签收时间""},{""excelColumn"":""签收类型"",""dbColumn"":""F签收类型""},{""excelColumn"":""代收点名称"",""dbColumn"":""F代收点名称""},{""excelColumn"":""末端滞留天数"",""dbColumn"":""F末端滞留天数""},{""excelColumn"":""是否按需派送标"",""dbColumn"":""F是否按需派送标""},{""excelColumn"":""进港/出港"",""dbColumn"":""F进港出港""},{""excelColumn"":""差行为原因"",""dbColumn"":""F差行为原因""}],""keyFields"":[""运单号"",""工单创建时间""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F运单号"",""F工单创建时间""],""batchSplit"":{""enabled"":false}}',
        1, N'申通进港投诉明细 Excel导入配置', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2310（QC_ST_INBOUND_COMPLAINT） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2310)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2310, NULL, 1, GETDATE(), NULL, N'网点质控：申通进港投诉明细 导入暂存', GETDATE(), NULL, N'申通进港投诉明细导入', NULL, N'QC_ST_INBOUND_COMPLAINT', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""进港投诉明细*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2310（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2310)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2310, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2310, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5110（ExcelInput 批次级自动节点，插件注册=1，规则=3110） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5110)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5110, 2310, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3110);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }
    private static void MigrateV33(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_投诉账单明细 暂存表（系统列 + 23 业务列 + 标准字段） ═══
        // 双行表头「坑」源：第 1 行是分类合并表头（费用明细数据/仲裁详情/申诉详情/申诉处理结果/申诉合计结果），
        // 第 2 行才是真字段名 → 规则用 headerRow=2, dataStartRow=3。
        // 全 137 列中字段名跨段重复，插件按列名映射会被同名列覆盖；故只映射 row2 中「名称全局唯一」的 23 列，
        // 其余列由插件自动归集进 F其他列数据。
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_投诉账单明细')
        CREATE TABLE [STG申通_投诉账单明细] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3111 columnMapping，23 列，均为 row2 全局唯一列名）
            [F运单号] NVARCHAR(200) NULL,
            [F账单一级类型] NVARCHAR(200) NULL,
            [F账单二级类型] NVARCHAR(200) NULL,
            [F金额] NVARCHAR(200) NULL,
            [F理赔来源] NVARCHAR(200) NULL,
            [F账单生成时间] NVARCHAR(200) NULL,
            [F申诉完结时间] NVARCHAR(200) NULL,
            [F理赔类型] NVARCHAR(200) NULL,
            [F处理结果] NVARCHAR(200) NULL,
            [F投诉网点] NVARCHAR(200) NULL,
            [F被投诉方1] NVARCHAR(200) NULL,
            [F完结方式] NVARCHAR(200) NULL,
            [F投诉时间] NVARCHAR(200) NULL,
            [F补录时间] NVARCHAR(200) NULL,
            [F内件品名] NVARCHAR(200) NULL,
            [F内件实际价值] NVARCHAR(200) NULL,
            [F调查经过] NVARCHAR(200) NULL,
            [F处理人] NVARCHAR(200) NULL,
            [F总部主管审核人姓名] NVARCHAR(200) NULL,
            [F受款方网点编号] NVARCHAR(200) NULL,
            [F受款方网点名称] NVARCHAR(200) NULL,
            [F受款方应受款金额] NVARCHAR(200) NULL,
            [F受款方协商受款金额] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_投诉账单明细_F批次ID' AND object_id = OBJECT_ID(N'STG申通_投诉账单明细'))
        CREATE INDEX [IX_STG申通_投诉账单明细_F批次ID] ON [STG申通_投诉账单明细]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_投诉账单明细_数据作用域' AND object_id = OBJECT_ID(N'STG申通_投诉账单明细'))
        CREATE INDEX [IX_STG申通_投诉账单明细_数据作用域] ON [STG申通_投诉账单明细]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（运单号 + 账单生成时间 + 组织，仅未撤销 + 运单号非空）
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_投诉账单明细_运单账单时间_未撤销' AND object_id = OBJECT_ID(N'STG申通_投诉账单明细'))
        CREATE UNIQUE INDEX [UX_STG申通_投诉账单明细_运单账单时间_未撤销]
            ON [STG申通_投诉账单明细]([F运单号],[F账单生成时间],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3111（投诉账单明细导入，双行表头 headerRow=2/dataStartRow=3） ═══
        // 路由说明：upload-auto 读第 1 行做内容路由，本文件第 1 行是分类名（非字段名），columnIdentifier 内容匹配无效，
        // 故必须靠第三轮 fileNamePattern *投诉账单* 路由；columnIdentifier 仍按映射列写（无害）。
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3111)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3111, 192, N'excelInput', N'申通投诉账单明细导入规则',
        N'{""targetTable"":""STG申通_投诉账单明细"",""outputMode"":""stg"",""headerRow"":2,""dataStartRow"":3,""columnIdentifier"":""运单号,账单一级类型,金额,投诉网点"",""fullColumnIdentifier"":""运单号,账单一级类型,账单二级类型,金额,理赔来源,账单生成时间,申诉完结时间,理赔类型,处理结果,投诉网点,被投诉方1,完结方式,投诉时间,补录时间,内件品名,内件实际价值,调查经过,处理人,总部主管审核人姓名,受款方网点编号,受款方网点名称,受款方应受款金额,受款方协商受款金额"",""columnMapping"":[{""excelColumn"":""运单号"",""dbColumn"":""F运单号""},{""excelColumn"":""账单一级类型"",""dbColumn"":""F账单一级类型""},{""excelColumn"":""账单二级类型"",""dbColumn"":""F账单二级类型""},{""excelColumn"":""金额"",""dbColumn"":""F金额""},{""excelColumn"":""理赔来源"",""dbColumn"":""F理赔来源""},{""excelColumn"":""账单生成时间"",""dbColumn"":""F账单生成时间""},{""excelColumn"":""申诉完结时间"",""dbColumn"":""F申诉完结时间""},{""excelColumn"":""理赔类型"",""dbColumn"":""F理赔类型""},{""excelColumn"":""处理结果"",""dbColumn"":""F处理结果""},{""excelColumn"":""投诉网点"",""dbColumn"":""F投诉网点""},{""excelColumn"":""被投诉方1"",""dbColumn"":""F被投诉方1""},{""excelColumn"":""完结方式"",""dbColumn"":""F完结方式""},{""excelColumn"":""投诉时间"",""dbColumn"":""F投诉时间""},{""excelColumn"":""补录时间"",""dbColumn"":""F补录时间""},{""excelColumn"":""内件品名"",""dbColumn"":""F内件品名""},{""excelColumn"":""内件实际价值"",""dbColumn"":""F内件实际价值""},{""excelColumn"":""调查经过"",""dbColumn"":""F调查经过""},{""excelColumn"":""处理人"",""dbColumn"":""F处理人""},{""excelColumn"":""总部主管审核人姓名"",""dbColumn"":""F总部主管审核人姓名""},{""excelColumn"":""受款方网点编号"",""dbColumn"":""F受款方网点编号""},{""excelColumn"":""受款方网点名称"",""dbColumn"":""F受款方网点名称""},{""excelColumn"":""受款方应受款金额"",""dbColumn"":""F受款方应受款金额""},{""excelColumn"":""受款方协商受款金额"",""dbColumn"":""F受款方协商受款金额""}],""keyFields"":[""运单号"",""账单生成时间""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F运单号"",""F账单生成时间""],""batchSplit"":{""enabled"":false}}',
        1, N'申通收到的投诉账单_账单明细 Excel导入配置（双行表头，仅映射 row2 全局唯一列）', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2311（QC_ST_COMPLAINT_BILL；靠 fileNamePattern 路由） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2311)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2311, NULL, 1, GETDATE(), NULL, N'网点质控：申通收到的投诉账单_账单明细 导入暂存（双行表头，靠 fileNamePattern 路由）', GETDATE(), NULL, N'申通投诉账单明细导入', NULL, N'QC_ST_COMPLAINT_BILL', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""*投诉账单*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2311（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2311)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2311, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2311, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5111（ExcelInput 批次级自动节点，插件注册=1，规则=3111） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5111)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5111, 2311, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3111);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }
    private static void MigrateV34(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_虚签投诉明细 暂存表（系统列 + 36 业务列 + 标准字段） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_虚签投诉明细')
        CREATE TABLE [STG申通_虚签投诉明细] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3112 columnMapping，36 列）
            [F投诉日期] NVARCHAR(200) NULL,
            [F运单号] NVARCHAR(200) NULL,
            [F投诉类型] NVARCHAR(200) NULL,
            [F投诉理由] NVARCHAR(200) NULL,
            [F工单号] NVARCHAR(200) NULL,
            [F投诉时间] NVARCHAR(200) NULL,
            [F省区编号] NVARCHAR(200) NULL,
            [F省区名称] NVARCHAR(200) NULL,
            [F省份编码] NVARCHAR(200) NULL,
            [F省份名称] NVARCHAR(200) NULL,
            [F所属网点编号] NVARCHAR(200) NULL,
            [F所属网点名称] NVARCHAR(200) NULL,
            [F被投诉网点编号] NVARCHAR(200) NULL,
            [F被投诉网点名称] NVARCHAR(200) NULL,
            [F派件业务员id] NVARCHAR(200) NULL,
            [F派件业务员名称] NVARCHAR(200) NULL,
            [F投诉来源] NVARCHAR(200) NULL,
            [F标签类型] NVARCHAR(200) NULL,
            [F电联履约状态] NVARCHAR(200) NULL,
            [F短信履约状态] NVARCHAR(200) NULL,
            [F客户声音履约状态] NVARCHAR(200) NULL,
            [F是否夜间签收] NVARCHAR(200) NULL,
            [F预估考核金额] NVARCHAR(200) NULL,
            [F签收类型] NVARCHAR(200) NULL,
            [F签收人] NVARCHAR(200) NULL,
            [F代收点类型] NVARCHAR(200) NULL,
            [F代收点名称] NVARCHAR(200) NULL,
            [F签收时间] NVARCHAR(200) NULL,
            [F是否下发小件员任务] NVARCHAR(200) NULL,
            [F小件员完结状态] NVARCHAR(200) NULL,
            [F是否二次进线] NVARCHAR(200) NULL,
            [F是否时效件] NVARCHAR(200) NULL,
            [F复核状态] NVARCHAR(200) NULL,
            [F复核状态说明] NVARCHAR(200) NULL,
            [F收件地址] NVARCHAR(200) NULL,
            [F差行为原因] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_虚签投诉明细_F批次ID' AND object_id = OBJECT_ID(N'STG申通_虚签投诉明细'))
        CREATE INDEX [IX_STG申通_虚签投诉明细_F批次ID] ON [STG申通_虚签投诉明细]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_虚签投诉明细_数据作用域' AND object_id = OBJECT_ID(N'STG申通_虚签投诉明细'))
        CREATE INDEX [IX_STG申通_虚签投诉明细_数据作用域] ON [STG申通_虚签投诉明细]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（工单号 + 组织，仅未撤销 + 工单号非空）
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_虚签投诉明细_工单号_未撤销' AND object_id = OBJECT_ID(N'STG申通_虚签投诉明细'))
        CREATE UNIQUE INDEX [UX_STG申通_虚签投诉明细_工单号_未撤销]
            ON [STG申通_虚签投诉明细]([F工单号],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F工单号] IS NOT NULL AND [F工单号] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3112（虚签投诉明细导入） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3112)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3112, 192, N'excelInput', N'申通虚签投诉明细导入规则',
        N'{""targetTable"":""STG申通_虚签投诉明细"",""outputMode"":""stg"",""headerRow"":1,""dataStartRow"":2,""columnIdentifier"":""运单号,投诉类型,投诉理由,工单号,预估考核金额"",""fullColumnIdentifier"":""投诉日期,运单号,投诉类型,投诉理由,工单号,投诉时间,省区编号,省区名称,省份编码,省份名称,所属网点编号,所属网点名称,被投诉网点编号,被投诉网点名称,派件业务员id,派件业务员名称,投诉来源,标签类型,电联履约状态,短信履约状态,客户声音履约状态,是否夜间签收,预估考核金额,签收类型,签收人,代收点类型,代收点名称,签收时间,是否下发小件员任务,小件员完结状态,是否二次进线,是否时效件,复核状态,复核状态说明,收件地址,差行为原因"",""columnMapping"":[{""excelColumn"":""投诉日期"",""dbColumn"":""F投诉日期""},{""excelColumn"":""运单号"",""dbColumn"":""F运单号""},{""excelColumn"":""投诉类型"",""dbColumn"":""F投诉类型""},{""excelColumn"":""投诉理由"",""dbColumn"":""F投诉理由""},{""excelColumn"":""工单号"",""dbColumn"":""F工单号""},{""excelColumn"":""投诉时间"",""dbColumn"":""F投诉时间""},{""excelColumn"":""省区编号"",""dbColumn"":""F省区编号""},{""excelColumn"":""省区名称"",""dbColumn"":""F省区名称""},{""excelColumn"":""省份编码"",""dbColumn"":""F省份编码""},{""excelColumn"":""省份名称"",""dbColumn"":""F省份名称""},{""excelColumn"":""所属网点编号"",""dbColumn"":""F所属网点编号""},{""excelColumn"":""所属网点名称"",""dbColumn"":""F所属网点名称""},{""excelColumn"":""被投诉网点编号"",""dbColumn"":""F被投诉网点编号""},{""excelColumn"":""被投诉网点名称"",""dbColumn"":""F被投诉网点名称""},{""excelColumn"":""派件业务员id"",""dbColumn"":""F派件业务员id""},{""excelColumn"":""派件业务员名称"",""dbColumn"":""F派件业务员名称""},{""excelColumn"":""投诉来源"",""dbColumn"":""F投诉来源""},{""excelColumn"":""标签类型"",""dbColumn"":""F标签类型""},{""excelColumn"":""电联履约状态"",""dbColumn"":""F电联履约状态""},{""excelColumn"":""短信履约状态"",""dbColumn"":""F短信履约状态""},{""excelColumn"":""客户声音履约状态"",""dbColumn"":""F客户声音履约状态""},{""excelColumn"":""是否夜间签收"",""dbColumn"":""F是否夜间签收""},{""excelColumn"":""预估考核金额"",""dbColumn"":""F预估考核金额""},{""excelColumn"":""签收类型"",""dbColumn"":""F签收类型""},{""excelColumn"":""签收人"",""dbColumn"":""F签收人""},{""excelColumn"":""代收点类型"",""dbColumn"":""F代收点类型""},{""excelColumn"":""代收点名称"",""dbColumn"":""F代收点名称""},{""excelColumn"":""签收时间"",""dbColumn"":""F签收时间""},{""excelColumn"":""是否下发小件员任务"",""dbColumn"":""F是否下发小件员任务""},{""excelColumn"":""小件员完结状态"",""dbColumn"":""F小件员完结状态""},{""excelColumn"":""是否二次进线"",""dbColumn"":""F是否二次进线""},{""excelColumn"":""是否时效件"",""dbColumn"":""F是否时效件""},{""excelColumn"":""复核状态"",""dbColumn"":""F复核状态""},{""excelColumn"":""复核状态说明"",""dbColumn"":""F复核状态说明""},{""excelColumn"":""收件地址"",""dbColumn"":""F收件地址""},{""excelColumn"":""差行为原因"",""dbColumn"":""F差行为原因""}],""keyFields"":[""工单号""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F工单号""],""batchSplit"":{""enabled"":false}}',
        1, N'申通虚签投诉明细 Excel导入配置', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2312（QC_ST_FAKE_SIGN_COMPLAINT） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2312)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2312, NULL, 1, GETDATE(), NULL, N'网点质控：申通虚签投诉明细 导入暂存', GETDATE(), NULL, N'申通虚签投诉明细导入', NULL, N'QC_ST_FAKE_SIGN_COMPLAINT', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""虚签投诉明细*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2312（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2312)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2312, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2312, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5112（ExcelInput 批次级自动节点，插件注册=1，规则=3112） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5112)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5112, 2312, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3112);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }
    private static void MigrateV35(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_虚假签收明细 暂存表（系统列 + 34 业务列 + 标准字段） ═══
        // 源文件后缀 .xls 但实为 xlsx（A1 魔数 PK），ExcelInputPlugin 已支持。sheet「0」，单行表头，34 列。
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_虚假签收明细')
        CREATE TABLE [STG申通_虚假签收明细] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3113 columnMapping，34 列）
            [F投诉日期] NVARCHAR(200) NULL,
            [F运单号] NVARCHAR(200) NULL,
            [F工单号] NVARCHAR(200) NULL,
            [F投诉时间] NVARCHAR(200) NULL,
            [F省区编号] NVARCHAR(200) NULL,
            [F省区名称] NVARCHAR(200) NULL,
            [F省份编号] NVARCHAR(200) NULL,
            [F省份名称] NVARCHAR(200) NULL,
            [F所属网点编号] NVARCHAR(200) NULL,
            [F所属网点名称] NVARCHAR(200) NULL,
            [F被投诉网点编号] NVARCHAR(200) NULL,
            [F被投诉网点名称] NVARCHAR(200) NULL,
            [F派件业务员id] NVARCHAR(200) NULL,
            [F派件业务员名称] NVARCHAR(200) NULL,
            [F投诉来源] NVARCHAR(200) NULL,
            [F标签类型] NVARCHAR(200) NULL,
            [F电联履约状态] NVARCHAR(200) NULL,
            [F短信履约状态] NVARCHAR(200) NULL,
            [F客户声音履约状态] NVARCHAR(200) NULL,
            [F是否夜间签收] NVARCHAR(200) NULL,
            [F签收类型] NVARCHAR(200) NULL,
            [F签收人] NVARCHAR(200) NULL,
            [F代收点类型] NVARCHAR(200) NULL,
            [F代收站点名称] NVARCHAR(200) NULL,
            [F签收时间] NVARCHAR(200) NULL,
            [F是否下发小件员任务] NVARCHAR(200) NULL,
            [F小件员完结状态] NVARCHAR(200) NULL,
            [F是否二次进线] NVARCHAR(200) NULL,
            [F是否时效件] NVARCHAR(200) NULL,
            [F复核状态] NVARCHAR(200) NULL,
            [F复核状态说明] NVARCHAR(200) NULL,
            [F投诉类型] NVARCHAR(200) NULL,
            [F投诉理由] NVARCHAR(200) NULL,
            [F收件地址] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_虚假签收明细_F批次ID' AND object_id = OBJECT_ID(N'STG申通_虚假签收明细'))
        CREATE INDEX [IX_STG申通_虚假签收明细_F批次ID] ON [STG申通_虚假签收明细]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_虚假签收明细_数据作用域' AND object_id = OBJECT_ID(N'STG申通_虚假签收明细'))
        CREATE INDEX [IX_STG申通_虚假签收明细_数据作用域] ON [STG申通_虚假签收明细]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（工单号 + 组织，仅未撤销 + 工单号非空）
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_虚假签收明细_工单号_未撤销' AND object_id = OBJECT_ID(N'STG申通_虚假签收明细'))
        CREATE UNIQUE INDEX [UX_STG申通_虚假签收明细_工单号_未撤销]
            ON [STG申通_虚假签收明细]([F工单号],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F工单号] IS NOT NULL AND [F工单号] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3113（虚假签收明细导入） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3113)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3113, 192, N'excelInput', N'申通虚假签收明细导入规则',
        N'{""targetTable"":""STG申通_虚假签收明细"",""outputMode"":""stg"",""headerRow"":1,""dataStartRow"":2,""columnIdentifier"":""运单号,工单号,被投诉网点编号,派件业务员id,复核状态"",""fullColumnIdentifier"":""投诉日期,运单号,工单号,投诉时间,省区编号,省区名称,省份编号,省份名称,所属网点编号,所属网点名称,被投诉网点编号,被投诉网点名称,派件业务员id,派件业务员名称,投诉来源,标签类型,电联履约状态,短信履约状态,客户声音履约状态,是否夜间签收,签收类型,签收人,代收点类型,代收站点名称,签收时间,是否下发小件员任务,小件员完结状态,是否二次进线,是否时效件,复核状态,复核状态说明,投诉类型,投诉理由,收件地址"",""columnMapping"":[{""excelColumn"":""投诉日期"",""dbColumn"":""F投诉日期""},{""excelColumn"":""运单号"",""dbColumn"":""F运单号""},{""excelColumn"":""工单号"",""dbColumn"":""F工单号""},{""excelColumn"":""投诉时间"",""dbColumn"":""F投诉时间""},{""excelColumn"":""省区编号"",""dbColumn"":""F省区编号""},{""excelColumn"":""省区名称"",""dbColumn"":""F省区名称""},{""excelColumn"":""省份编号"",""dbColumn"":""F省份编号""},{""excelColumn"":""省份名称"",""dbColumn"":""F省份名称""},{""excelColumn"":""所属网点编号"",""dbColumn"":""F所属网点编号""},{""excelColumn"":""所属网点名称"",""dbColumn"":""F所属网点名称""},{""excelColumn"":""被投诉网点编号"",""dbColumn"":""F被投诉网点编号""},{""excelColumn"":""被投诉网点名称"",""dbColumn"":""F被投诉网点名称""},{""excelColumn"":""派件业务员id"",""dbColumn"":""F派件业务员id""},{""excelColumn"":""派件业务员名称"",""dbColumn"":""F派件业务员名称""},{""excelColumn"":""投诉来源"",""dbColumn"":""F投诉来源""},{""excelColumn"":""标签类型"",""dbColumn"":""F标签类型""},{""excelColumn"":""电联履约状态"",""dbColumn"":""F电联履约状态""},{""excelColumn"":""短信履约状态"",""dbColumn"":""F短信履约状态""},{""excelColumn"":""客户声音履约状态"",""dbColumn"":""F客户声音履约状态""},{""excelColumn"":""是否夜间签收"",""dbColumn"":""F是否夜间签收""},{""excelColumn"":""签收类型"",""dbColumn"":""F签收类型""},{""excelColumn"":""签收人"",""dbColumn"":""F签收人""},{""excelColumn"":""代收点类型"",""dbColumn"":""F代收点类型""},{""excelColumn"":""代收站点名称"",""dbColumn"":""F代收站点名称""},{""excelColumn"":""签收时间"",""dbColumn"":""F签收时间""},{""excelColumn"":""是否下发小件员任务"",""dbColumn"":""F是否下发小件员任务""},{""excelColumn"":""小件员完结状态"",""dbColumn"":""F小件员完结状态""},{""excelColumn"":""是否二次进线"",""dbColumn"":""F是否二次进线""},{""excelColumn"":""是否时效件"",""dbColumn"":""F是否时效件""},{""excelColumn"":""复核状态"",""dbColumn"":""F复核状态""},{""excelColumn"":""复核状态说明"",""dbColumn"":""F复核状态说明""},{""excelColumn"":""投诉类型"",""dbColumn"":""F投诉类型""},{""excelColumn"":""投诉理由"",""dbColumn"":""F投诉理由""},{""excelColumn"":""收件地址"",""dbColumn"":""F收件地址""}],""keyFields"":[""工单号""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F工单号""],""batchSplit"":{""enabled"":false}}',
        1, N'申通虚假签收明细导出 Excel导入配置（.xls 实为 xlsx）', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2313（QC_ST_FAKE_SIGN） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2313)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2313, NULL, 1, GETDATE(), NULL, N'网点质控：申通虚假签收明细导出 导入暂存', GETDATE(), NULL, N'申通虚假签收明细导入', NULL, N'QC_ST_FAKE_SIGN', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""虚假签收明细*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2313（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2313)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2313, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2313, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5113（ExcelInput 批次级自动节点，插件注册=1，规则=3113） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5113)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5113, 2313, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3113);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }
    private static void MigrateV36(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_照片质检明细 暂存表（系统列 + 25 业务列 + 标准字段） ═══
        // 源文件后缀 .xls 但实为 xlsx（A1 魔数 PK），ExcelInputPlugin 已支持。sheet「0」，单行表头，25 列。
        // 主键列为「单号」（不是「运单号」）。
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_照片质检明细')
        CREATE TABLE [STG申通_照片质检明细] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3114 columnMapping，25 列）
            [F单号] NVARCHAR(200) NULL,
            [F业务类型] NVARCHAR(200) NULL,
            [F是否履约] NVARCHAR(200) NULL,
            [F签收人] NVARCHAR(200) NULL,
            [F是否质检合格] NVARCHAR(200) NULL,
            [F不合格类型] NVARCHAR(200) NULL,
            [F是否上门] NVARCHAR(200) NULL,
            [F小件员名称] NVARCHAR(200) NULL,
            [F小件员编码] NVARCHAR(200) NULL,
            [F网点编码] NVARCHAR(200) NULL,
            [F网点名称] NVARCHAR(200) NULL,
            [F大区编码] NVARCHAR(200) NULL,
            [F大区名称] NVARCHAR(200) NULL,
            [F省区编码] NVARCHAR(200) NULL,
            [F省区名称] NVARCHAR(200) NULL,
            [F片区编码] NVARCHAR(200) NULL,
            [F片区名称] NVARCHAR(200) NULL,
            [F收件地址] NVARCHAR(200) NULL,
            [F收件手机号] NVARCHAR(200) NULL,
            [F投诉类型] NVARCHAR(200) NULL,
            [F投诉内容] NVARCHAR(200) NULL,
            [F投诉时间] NVARCHAR(200) NULL,
            [F投诉来源] NVARCHAR(200) NULL,
            [F是否拍照] NVARCHAR(200) NULL,
            [F分区] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_照片质检明细_F批次ID' AND object_id = OBJECT_ID(N'STG申通_照片质检明细'))
        CREATE INDEX [IX_STG申通_照片质检明细_F批次ID] ON [STG申通_照片质检明细]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_照片质检明细_数据作用域' AND object_id = OBJECT_ID(N'STG申通_照片质检明细'))
        CREATE INDEX [IX_STG申通_照片质检明细_数据作用域] ON [STG申通_照片质检明细]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（单号 + 组织，仅未撤销 + 单号非空）
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_照片质检明细_单号_未撤销' AND object_id = OBJECT_ID(N'STG申通_照片质检明细'))
        CREATE UNIQUE INDEX [UX_STG申通_照片质检明细_单号_未撤销]
            ON [STG申通_照片质检明细]([F单号],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F单号] IS NOT NULL AND [F单号] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3114（照片质检明细导入） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3114)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3114, 192, N'excelInput', N'申通照片质检明细导入规则',
        N'{""targetTable"":""STG申通_照片质检明细"",""outputMode"":""stg"",""headerRow"":1,""dataStartRow"":2,""columnIdentifier"":""单号,是否质检合格,不合格类型,小件员编码"",""fullColumnIdentifier"":""单号,业务类型,是否履约,签收人,是否质检合格,不合格类型,是否上门,小件员名称,小件员编码,网点编码,网点名称,大区编码,大区名称,省区编码,省区名称,片区编码,片区名称,收件地址,收件手机号,投诉类型,投诉内容,投诉时间,投诉来源,是否拍照,分区"",""columnMapping"":[{""excelColumn"":""单号"",""dbColumn"":""F单号""},{""excelColumn"":""业务类型"",""dbColumn"":""F业务类型""},{""excelColumn"":""是否履约"",""dbColumn"":""F是否履约""},{""excelColumn"":""签收人"",""dbColumn"":""F签收人""},{""excelColumn"":""是否质检合格"",""dbColumn"":""F是否质检合格""},{""excelColumn"":""不合格类型"",""dbColumn"":""F不合格类型""},{""excelColumn"":""是否上门"",""dbColumn"":""F是否上门""},{""excelColumn"":""小件员名称"",""dbColumn"":""F小件员名称""},{""excelColumn"":""小件员编码"",""dbColumn"":""F小件员编码""},{""excelColumn"":""网点编码"",""dbColumn"":""F网点编码""},{""excelColumn"":""网点名称"",""dbColumn"":""F网点名称""},{""excelColumn"":""大区编码"",""dbColumn"":""F大区编码""},{""excelColumn"":""大区名称"",""dbColumn"":""F大区名称""},{""excelColumn"":""省区编码"",""dbColumn"":""F省区编码""},{""excelColumn"":""省区名称"",""dbColumn"":""F省区名称""},{""excelColumn"":""片区编码"",""dbColumn"":""F片区编码""},{""excelColumn"":""片区名称"",""dbColumn"":""F片区名称""},{""excelColumn"":""收件地址"",""dbColumn"":""F收件地址""},{""excelColumn"":""收件手机号"",""dbColumn"":""F收件手机号""},{""excelColumn"":""投诉类型"",""dbColumn"":""F投诉类型""},{""excelColumn"":""投诉内容"",""dbColumn"":""F投诉内容""},{""excelColumn"":""投诉时间"",""dbColumn"":""F投诉时间""},{""excelColumn"":""投诉来源"",""dbColumn"":""F投诉来源""},{""excelColumn"":""是否拍照"",""dbColumn"":""F是否拍照""},{""excelColumn"":""分区"",""dbColumn"":""F分区""}],""keyFields"":[""单号""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F单号""],""batchSplit"":{""enabled"":false}}',
        1, N'申通抖音照片质检明细 Excel导入配置（.xls 实为 xlsx，主键列为单号）', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2314（QC_ST_PHOTO_QC） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2314)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2314, NULL, 1, GETDATE(), NULL, N'网点质控：申通抖音照片质检明细 导入暂存', GETDATE(), NULL, N'申通照片质检明细导入', NULL, N'QC_ST_PHOTO_QC', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""*照片质检*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2314（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2314)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2314, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2314, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5114（ExcelInput 批次级自动节点，插件注册=1，规则=3114） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5114)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5114, 2314, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3114);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }
    private static void MigrateV37(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_履约率明细 暂存表（系统列 + 13 业务列 + 标准字段） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_履约率明细')
        CREATE TABLE [STG申通_履约率明细] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3115 columnMapping，13 列）
            [F日期] NVARCHAR(200) NULL,
            [F运单号] NVARCHAR(200) NULL,
            [F收件人] NVARCHAR(200) NULL,
            [F收件地址] NVARCHAR(200) NULL,
            [F履约要求] NVARCHAR(200) NULL,
            [F履约状态] NVARCHAR(200) NULL,
            [F是否虚假上门] NVARCHAR(200) NULL,
            [F小件员名称] NVARCHAR(200) NULL,
            [F小件员工号] NVARCHAR(200) NULL,
            [F签收时间] NVARCHAR(200) NULL,
            [F首次签收类型] NVARCHAR(200) NULL,
            [F签收人] NVARCHAR(200) NULL,
            [F服务要求] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_履约率明细_F批次ID' AND object_id = OBJECT_ID(N'STG申通_履约率明细'))
        CREATE INDEX [IX_STG申通_履约率明细_F批次ID] ON [STG申通_履约率明细]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_履约率明细_数据作用域' AND object_id = OBJECT_ID(N'STG申通_履约率明细'))
        CREATE INDEX [IX_STG申通_履约率明细_数据作用域] ON [STG申通_履约率明细]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（运单号 + 组织，仅未撤销 + 运单号非空）
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_履约率明细_运单号_未撤销' AND object_id = OBJECT_ID(N'STG申通_履约率明细'))
        CREATE UNIQUE INDEX [UX_STG申通_履约率明细_运单号_未撤销]
            ON [STG申通_履约率明细]([F运单号],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3115（履约率明细导入） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3115)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3115, 192, N'excelInput', N'申通履约率明细导入规则',
        N'{""targetTable"":""STG申通_履约率明细"",""outputMode"":""stg"",""headerRow"":1,""dataStartRow"":2,""columnIdentifier"":""运单号,履约要求,履约状态,小件员工号"",""fullColumnIdentifier"":""日期,运单号,收件人,收件地址,履约要求,履约状态,是否虚假上门,小件员名称,小件员工号,签收时间,首次签收类型,签收人,服务要求"",""columnMapping"":[{""excelColumn"":""日期"",""dbColumn"":""F日期""},{""excelColumn"":""运单号"",""dbColumn"":""F运单号""},{""excelColumn"":""收件人"",""dbColumn"":""F收件人""},{""excelColumn"":""收件地址"",""dbColumn"":""F收件地址""},{""excelColumn"":""履约要求"",""dbColumn"":""F履约要求""},{""excelColumn"":""履约状态"",""dbColumn"":""F履约状态""},{""excelColumn"":""是否虚假上门"",""dbColumn"":""F是否虚假上门""},{""excelColumn"":""小件员名称"",""dbColumn"":""F小件员名称""},{""excelColumn"":""小件员工号"",""dbColumn"":""F小件员工号""},{""excelColumn"":""签收时间"",""dbColumn"":""F签收时间""},{""excelColumn"":""首次签收类型"",""dbColumn"":""F首次签收类型""},{""excelColumn"":""签收人"",""dbColumn"":""F签收人""},{""excelColumn"":""服务要求"",""dbColumn"":""F服务要求""}],""keyFields"":[""运单号""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F运单号""],""batchSplit"":{""enabled"":false}}',
        1, N'申通客户声音履约率明细 Excel导入配置', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2315（QC_ST_FULFILL_RATE） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2315)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2315, NULL, 1, GETDATE(), NULL, N'网点质控：申通客户声音履约率明细 导入暂存', GETDATE(), NULL, N'申通履约率明细导入', NULL, N'QC_ST_FULFILL_RATE', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""客户声音履约率*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2315（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2315)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2315, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2315, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5115（ExcelInput 批次级自动节点，插件注册=1，规则=3115） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5115)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5115, 2315, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3115);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }
    private static void MigrateV38(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_送货上门明细 暂存表（系统列 + 17 业务列 + 标准字段） ═══
        // 注意：「违规行为-二级内容」含非法字符 -，dbColumn 去掉为 F违规行为二级内容（实体/EF/DDL/映射四处一致，excelColumn 保留原文）。
        // 「电联录音」为 URL（可能逗号分隔多条），用 NVARCHAR(MAX)。
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_送货上门明细')
        CREATE TABLE [STG申通_送货上门明细] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3116 columnMapping，17 列）
            [F订单来源] NVARCHAR(200) NULL,
            [F统计日期] NVARCHAR(200) NULL,
            [F运单号] NVARCHAR(200) NULL,
            [F运单状态] NVARCHAR(200) NULL,
            [F承包区编号] NVARCHAR(200) NULL,
            [F承包区名称] NVARCHAR(200) NULL,
            [F业务员工号] NVARCHAR(200) NULL,
            [F派送小件员名称] NVARCHAR(200) NULL,
            [F回执情况] NVARCHAR(200) NULL,
            [F签收人信息] NVARCHAR(200) NULL,
            [F履约情况] NVARCHAR(200) NULL,
            [F签收日期] NVARCHAR(200) NULL,
            [F违规行为二级内容] NVARCHAR(200) NULL,
            [F工单判罚类型] NVARCHAR(200) NULL,
            [F是否电联] NVARCHAR(200) NULL,
            [F是否接通] NVARCHAR(200) NULL,
            [F电联录音] NVARCHAR(MAX) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_送货上门明细_F批次ID' AND object_id = OBJECT_ID(N'STG申通_送货上门明细'))
        CREATE INDEX [IX_STG申通_送货上门明细_F批次ID] ON [STG申通_送货上门明细]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_送货上门明细_数据作用域' AND object_id = OBJECT_ID(N'STG申通_送货上门明细'))
        CREATE INDEX [IX_STG申通_送货上门明细_数据作用域] ON [STG申通_送货上门明细]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（运单号 + 统计日期 + 组织，仅未撤销 + 运单号非空）
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_送货上门明细_运单统计日期_未撤销' AND object_id = OBJECT_ID(N'STG申通_送货上门明细'))
        CREATE UNIQUE INDEX [UX_STG申通_送货上门明细_运单统计日期_未撤销]
            ON [STG申通_送货上门明细]([F运单号],[F统计日期],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3116（送货上门明细导入） ═══
        // excelColumn「违规行为-二级内容」保留原文，dbColumn「F违规行为二级内容」去掉非法字符 -。
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3116)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3116, 192, N'excelInput', N'申通送货上门明细导入规则',
        N'{""targetTable"":""STG申通_送货上门明细"",""outputMode"":""stg"",""headerRow"":1,""dataStartRow"":2,""columnIdentifier"":""运单号,履约情况,工单判罚类型,派送小件员名称"",""fullColumnIdentifier"":""订单来源,统计日期,运单号,运单状态,承包区编号,承包区名称,业务员工号,派送小件员名称,回执情况,签收人信息,履约情况,签收日期,违规行为-二级内容,工单判罚类型,是否电联,是否接通,电联录音"",""columnMapping"":[{""excelColumn"":""订单来源"",""dbColumn"":""F订单来源""},{""excelColumn"":""统计日期"",""dbColumn"":""F统计日期""},{""excelColumn"":""运单号"",""dbColumn"":""F运单号""},{""excelColumn"":""运单状态"",""dbColumn"":""F运单状态""},{""excelColumn"":""承包区编号"",""dbColumn"":""F承包区编号""},{""excelColumn"":""承包区名称"",""dbColumn"":""F承包区名称""},{""excelColumn"":""业务员工号"",""dbColumn"":""F业务员工号""},{""excelColumn"":""派送小件员名称"",""dbColumn"":""F派送小件员名称""},{""excelColumn"":""回执情况"",""dbColumn"":""F回执情况""},{""excelColumn"":""签收人信息"",""dbColumn"":""F签收人信息""},{""excelColumn"":""履约情况"",""dbColumn"":""F履约情况""},{""excelColumn"":""签收日期"",""dbColumn"":""F签收日期""},{""excelColumn"":""违规行为-二级内容"",""dbColumn"":""F违规行为二级内容""},{""excelColumn"":""工单判罚类型"",""dbColumn"":""F工单判罚类型""},{""excelColumn"":""是否电联"",""dbColumn"":""F是否电联""},{""excelColumn"":""是否接通"",""dbColumn"":""F是否接通""},{""excelColumn"":""电联录音"",""dbColumn"":""F电联录音""}],""keyFields"":[""运单号"",""统计日期""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F运单号"",""F统计日期""],""batchSplit"":{""enabled"":false}}',
        1, N'申通送货上门达成分析明细 Excel导入配置（违规行为-二级内容含非法字符 -，电联录音为 URL）', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2316（QC_ST_HOME_DELIVERY） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2316)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2316, NULL, 1, GETDATE(), NULL, N'网点质控：申通送货上门达成分析明细 导入暂存', GETDATE(), NULL, N'申通送货上门明细导入', NULL, N'QC_ST_HOME_DELIVERY', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""送货上门达成分析*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2316（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2316)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2316, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2316, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5116（ExcelInput 批次级自动节点，插件注册=1，规则=3116） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5116)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5116, 2316, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3116);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }
    private static void MigrateV39(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_应拦截明细 暂存表（系统列 + 26 业务列 + 标准字段） ═══
        // 源文件后缀 .xls 但实为 xlsx（A1 魔数 PK），ExcelInputPlugin 已支持。sheet「0」，单行表头，26 列。
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_应拦截明细')
        CREATE TABLE [STG申通_应拦截明细] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3117 columnMapping，26 列）
            [F统计日期] NVARCHAR(200) NULL,
            [F运单号] NVARCHAR(200) NULL,
            [F拦截来源] NVARCHAR(200) NULL,
            [F应拦截网点] NVARCHAR(200) NULL,
            [F所属网点] NVARCHAR(200) NULL,
            [F拦截类型] NVARCHAR(200) NULL,
            [F派件小件员] NVARCHAR(200) NULL,
            [F到件时间] NVARCHAR(200) NULL,
            [F最新OP时间] NVARCHAR(200) NULL,
            [F最新OP节点] NVARCHAR(200) NULL,
            [F驿站名称] NVARCHAR(200) NULL,
            [F退件打印时间] NVARCHAR(200) NULL,
            [F退件操作人] NVARCHAR(200) NULL,
            [F最迟转出时间] NVARCHAR(200) NULL,
            [F逆向转出时间] NVARCHAR(200) NULL,
            [F逆向交货组织] NVARCHAR(200) NULL,
            [F逆向转出时长] NVARCHAR(200) NULL,
            [F逆向转出时效] NVARCHAR(200) NULL,
            [F预计考核金额] NVARCHAR(200) NULL,
            [F拦截录入网点] NVARCHAR(200) NULL,
            [F拦截录入时间] NVARCHAR(200) NULL,
            [F拦截发起节点] NVARCHAR(200) NULL,
            [F是否拦截成功] NVARCHAR(200) NULL,
            [F是否转出] NVARCHAR(200) NULL,
            [F是否及时转出] NVARCHAR(200) NULL,
            [F正向签收] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_应拦截明细_F批次ID' AND object_id = OBJECT_ID(N'STG申通_应拦截明细'))
        CREATE INDEX [IX_STG申通_应拦截明细_F批次ID] ON [STG申通_应拦截明细]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_应拦截明细_数据作用域' AND object_id = OBJECT_ID(N'STG申通_应拦截明细'))
        CREATE INDEX [IX_STG申通_应拦截明细_数据作用域] ON [STG申通_应拦截明细]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（运单号 + 统计日期 + 组织，仅未撤销 + 运单号非空）
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_应拦截明细_运单统计日期_未撤销' AND object_id = OBJECT_ID(N'STG申通_应拦截明细'))
        CREATE UNIQUE INDEX [UX_STG申通_应拦截明细_运单统计日期_未撤销]
            ON [STG申通_应拦截明细]([F运单号],[F统计日期],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F运单号] IS NOT NULL AND [F运单号] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3117（应拦截明细导入） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3117)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3117, 192, N'excelInput', N'申通应拦截明细导入规则',
        N'{""targetTable"":""STG申通_应拦截明细"",""outputMode"":""stg"",""headerRow"":1,""dataStartRow"":2,""columnIdentifier"":""运单号,拦截类型,是否拦截成功,是否及时转出"",""fullColumnIdentifier"":""统计日期,运单号,拦截来源,应拦截网点,所属网点,拦截类型,派件小件员,到件时间,最新OP时间,最新OP节点,驿站名称,退件打印时间,退件操作人,最迟转出时间,逆向转出时间,逆向交货组织,逆向转出时长,逆向转出时效,预计考核金额,拦截录入网点,拦截录入时间,拦截发起节点,是否拦截成功,是否转出,是否及时转出,正向签收"",""columnMapping"":[{""excelColumn"":""统计日期"",""dbColumn"":""F统计日期""},{""excelColumn"":""运单号"",""dbColumn"":""F运单号""},{""excelColumn"":""拦截来源"",""dbColumn"":""F拦截来源""},{""excelColumn"":""应拦截网点"",""dbColumn"":""F应拦截网点""},{""excelColumn"":""所属网点"",""dbColumn"":""F所属网点""},{""excelColumn"":""拦截类型"",""dbColumn"":""F拦截类型""},{""excelColumn"":""派件小件员"",""dbColumn"":""F派件小件员""},{""excelColumn"":""到件时间"",""dbColumn"":""F到件时间""},{""excelColumn"":""最新OP时间"",""dbColumn"":""F最新OP时间""},{""excelColumn"":""最新OP节点"",""dbColumn"":""F最新OP节点""},{""excelColumn"":""驿站名称"",""dbColumn"":""F驿站名称""},{""excelColumn"":""退件打印时间"",""dbColumn"":""F退件打印时间""},{""excelColumn"":""退件操作人"",""dbColumn"":""F退件操作人""},{""excelColumn"":""最迟转出时间"",""dbColumn"":""F最迟转出时间""},{""excelColumn"":""逆向转出时间"",""dbColumn"":""F逆向转出时间""},{""excelColumn"":""逆向交货组织"",""dbColumn"":""F逆向交货组织""},{""excelColumn"":""逆向转出时长"",""dbColumn"":""F逆向转出时长""},{""excelColumn"":""逆向转出时效"",""dbColumn"":""F逆向转出时效""},{""excelColumn"":""预计考核金额"",""dbColumn"":""F预计考核金额""},{""excelColumn"":""拦截录入网点"",""dbColumn"":""F拦截录入网点""},{""excelColumn"":""拦截录入时间"",""dbColumn"":""F拦截录入时间""},{""excelColumn"":""拦截发起节点"",""dbColumn"":""F拦截发起节点""},{""excelColumn"":""是否拦截成功"",""dbColumn"":""F是否拦截成功""},{""excelColumn"":""是否转出"",""dbColumn"":""F是否转出""},{""excelColumn"":""是否及时转出"",""dbColumn"":""F是否及时转出""},{""excelColumn"":""正向签收"",""dbColumn"":""F正向签收""}],""keyFields"":[""运单号"",""统计日期""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F运单号"",""F统计日期""],""batchSplit"":{""enabled"":false}}',
        1, N'申通应拦截量数据报表明细 Excel导入配置（.xls 实为 xlsx）', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2317（QC_ST_INTERCEPT） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2317)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2317, NULL, 1, GETDATE(), NULL, N'网点质控：申通应拦截量数据报表明细 导入暂存', GETDATE(), NULL, N'申通应拦截明细导入', NULL, N'QC_ST_INTERCEPT', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""应拦截量数据报表*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2317（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2317)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2317, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2317, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5117（ExcelInput 批次级自动节点，插件注册=1，规则=3117） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5117)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5117, 2317, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3117);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }
    private static void MigrateV40(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_渗透建站考核 暂存表（系统列 + 24 业务列 + 标准字段） ═══
        // sheet「渗透率建站考核」，单行表头，24 列；1 行/网点/周期的汇总表。
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_渗透建站考核')
        CREATE TABLE [STG申通_渗透建站考核] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3118 columnMapping，24 列）
            [F统计周期] NVARCHAR(200) NULL,
            [F网点名称] NVARCHAR(200) NULL,
            [F网点编号] NVARCHAR(200) NULL,
            [F自建渗透率当月目标] NVARCHAR(200) NULL,
            [F已认证自建渗透率] NVARCHAR(200) NULL,
            [F已认证自建渗透率差值] NVARCHAR(200) NULL,
            [F已认证自建渗透率环比] NVARCHAR(200) NULL,
            [F总入库量] NVARCHAR(200) NULL,
            [F已认证自建入库量] NVARCHAR(200) NULL,
            [F建站当季目标] NVARCHAR(200) NULL,
            [F建站当月目标] NVARCHAR(200) NULL,
            [F菜鸟活跃] NVARCHAR(200) NULL,
            [F喵站活跃] NVARCHAR(200) NULL,
            [F多多活跃] NVARCHAR(200) NULL,
            [F喵柜抵扣建站数] NVARCHAR(200) NULL,
            [F建站待完成] NVARCHAR(200) NULL,
            [F菜鸟当月新增] NVARCHAR(200) NULL,
            [F建柜目标] NVARCHAR(200) NULL,
            [F喵柜激活格口数] NVARCHAR(200) NULL,
            [F喵柜激活格口数环比] NVARCHAR(200) NULL,
            [F喵柜待完成格口数] NVARCHAR(200) NULL,
            [F全cp日均入库量] NVARCHAR(200) NULL,
            [F申通日均入库量] NVARCHAR(200) NULL,
            [F喵柜组数] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_渗透建站考核_F批次ID' AND object_id = OBJECT_ID(N'STG申通_渗透建站考核'))
        CREATE INDEX [IX_STG申通_渗透建站考核_F批次ID] ON [STG申通_渗透建站考核]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_渗透建站考核_数据作用域' AND object_id = OBJECT_ID(N'STG申通_渗透建站考核'))
        CREATE INDEX [IX_STG申通_渗透建站考核_数据作用域] ON [STG申通_渗透建站考核]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（网点编号 + 统计周期 + 组织，仅未撤销 + 网点编号非空）
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_渗透建站考核_网点周期_未撤销' AND object_id = OBJECT_ID(N'STG申通_渗透建站考核'))
        CREATE UNIQUE INDEX [UX_STG申通_渗透建站考核_网点周期_未撤销]
            ON [STG申通_渗透建站考核]([F网点编号],[F统计周期],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F网点编号] IS NOT NULL AND [F网点编号] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3118（渗透建站考核导入） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3118)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3118, 192, N'excelInput', N'申通渗透建站考核导入规则',
        N'{""targetTable"":""STG申通_渗透建站考核"",""outputMode"":""stg"",""headerRow"":1,""dataStartRow"":2,""columnIdentifier"":""统计周期,网点编号,已认证自建渗透率,建站当月目标"",""fullColumnIdentifier"":""统计周期,网点名称,网点编号,自建渗透率当月目标,已认证自建渗透率,已认证自建渗透率差值,已认证自建渗透率环比,总入库量,已认证自建入库量,建站当季目标,建站当月目标,菜鸟活跃,喵站活跃,多多活跃,喵柜抵扣建站数,建站待完成,菜鸟当月新增,建柜目标,喵柜激活格口数,喵柜激活格口数环比,喵柜待完成格口数,全cp日均入库量,申通日均入库量,喵柜组数"",""columnMapping"":[{""excelColumn"":""统计周期"",""dbColumn"":""F统计周期""},{""excelColumn"":""网点名称"",""dbColumn"":""F网点名称""},{""excelColumn"":""网点编号"",""dbColumn"":""F网点编号""},{""excelColumn"":""自建渗透率当月目标"",""dbColumn"":""F自建渗透率当月目标""},{""excelColumn"":""已认证自建渗透率"",""dbColumn"":""F已认证自建渗透率""},{""excelColumn"":""已认证自建渗透率差值"",""dbColumn"":""F已认证自建渗透率差值""},{""excelColumn"":""已认证自建渗透率环比"",""dbColumn"":""F已认证自建渗透率环比""},{""excelColumn"":""总入库量"",""dbColumn"":""F总入库量""},{""excelColumn"":""已认证自建入库量"",""dbColumn"":""F已认证自建入库量""},{""excelColumn"":""建站当季目标"",""dbColumn"":""F建站当季目标""},{""excelColumn"":""建站当月目标"",""dbColumn"":""F建站当月目标""},{""excelColumn"":""菜鸟活跃"",""dbColumn"":""F菜鸟活跃""},{""excelColumn"":""喵站活跃"",""dbColumn"":""F喵站活跃""},{""excelColumn"":""多多活跃"",""dbColumn"":""F多多活跃""},{""excelColumn"":""喵柜抵扣建站数"",""dbColumn"":""F喵柜抵扣建站数""},{""excelColumn"":""建站待完成"",""dbColumn"":""F建站待完成""},{""excelColumn"":""菜鸟当月新增"",""dbColumn"":""F菜鸟当月新增""},{""excelColumn"":""建柜目标"",""dbColumn"":""F建柜目标""},{""excelColumn"":""喵柜激活格口数"",""dbColumn"":""F喵柜激活格口数""},{""excelColumn"":""喵柜激活格口数环比"",""dbColumn"":""F喵柜激活格口数环比""},{""excelColumn"":""喵柜待完成格口数"",""dbColumn"":""F喵柜待完成格口数""},{""excelColumn"":""全cp日均入库量"",""dbColumn"":""F全cp日均入库量""},{""excelColumn"":""申通日均入库量"",""dbColumn"":""F申通日均入库量""},{""excelColumn"":""喵柜组数"",""dbColumn"":""F喵柜组数""}],""keyFields"":[""网点编号"",""统计周期""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F网点编号"",""F统计周期""],""batchSplit"":{""enabled"":false}}',
        1, N'申通渗透率建站考核 Excel导入配置（1 行/网点/周期汇总）', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2318（QC_ST_PENETRATION） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2318)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2318, NULL, 1, GETDATE(), NULL, N'网点质控：申通渗透率建站考核 导入暂存', GETDATE(), NULL, N'申通渗透建站考核导入', NULL, N'QC_ST_PENETRATION', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""渗透率建站考核*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2318（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2318)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2318, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2318, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5118（ExcelInput 批次级自动节点，插件注册=1，规则=3118） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5118)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5118, 2318, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3118);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }

    private static void MigrateV41(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══════════════════════════════════════════════════════════════════
        // 多 sheet「坑」源：物流信息指数（excel（物流信息指数）.xls，真 OLE2，含 3 个 sheet）。
        // 一个文件 → 3 张表/3 规则/3 流程。upload-auto 只读 sheet0、无法把单文件 fan-out 到 3 流程，
        // 故这 3 个流程的 F匹配规则=NULL（不参与文件名匹配，避免歧义），靠显式触发导入（指定 PluginRuleId）。
        // 每个规则 JSON 设 sheetName 指向对应 sheet（ExcelInputPlugin 已支持 sheetName）。
        // 本文件无网点编号，仅有网点名称 → 去重按「网点名称 + 统计日期」。
        // ═══════════════════════════════════════════════════════════════════

        // ═══ 表 1/3：及时性汇总（按天）→ STG申通_物流信息及时汇总（15 业务列） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_物流信息及时汇总')
        CREATE TABLE [STG申通_物流信息及时汇总] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3119 columnMapping，15 列）
            [F统计日期] NVARCHAR(200) NULL,
            [F网点名称] NVARCHAR(200) NULL,
            [F所属网点名称] NVARCHAR(200) NULL,
            [F揽收总量] NVARCHAR(200) NULL,
            [F揽收上传不及时量] NVARCHAR(200) NULL,
            [F揽收上传不及时率] NVARCHAR(200) NULL,
            [F派件总量] NVARCHAR(200) NULL,
            [F派件上传不及时量] NVARCHAR(200) NULL,
            [F派件上传不及时率] NVARCHAR(200) NULL,
            [F签收总量] NVARCHAR(200) NULL,
            [F签收上传不及时量] NVARCHAR(200) NULL,
            [F签收上传不及时率] NVARCHAR(200) NULL,
            [F扫描总量] NVARCHAR(200) NULL,
            [F上传不及时总量] NVARCHAR(200) NULL,
            [F上传不及时总率] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_物流信息及时汇总_F批次ID' AND object_id = OBJECT_ID(N'STG申通_物流信息及时汇总'))
        CREATE INDEX [IX_STG申通_物流信息及时汇总_F批次ID] ON [STG申通_物流信息及时汇总]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_物流信息及时汇总_数据作用域' AND object_id = OBJECT_ID(N'STG申通_物流信息及时汇总'))
        CREATE INDEX [IX_STG申通_物流信息及时汇总_数据作用域] ON [STG申通_物流信息及时汇总]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（网点名称 + 统计日期 + 组织，仅未撤销 + 网点名称非空）
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_物流信息及时汇总_网点日期_未撤销' AND object_id = OBJECT_ID(N'STG申通_物流信息及时汇总'))
        CREATE UNIQUE INDEX [UX_STG申通_物流信息及时汇总_网点日期_未撤销]
            ON [STG申通_物流信息及时汇总]([F网点名称],[F统计日期],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F网点名称] IS NOT NULL AND [F网点名称] != '';
        ");

        // ═══ 表 2/3：完整性汇总（按天）→ STG申通_物流信息完整汇总（11 业务列） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_物流信息完整汇总')
        CREATE TABLE [STG申通_物流信息完整汇总] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3120 columnMapping，11 列）
            [F统计日期] NVARCHAR(200) NULL,
            [F网点名称] NVARCHAR(200) NULL,
            [F所属网点名称] NVARCHAR(200) NULL,
            [F订单总量] NVARCHAR(200) NULL,
            [F揽收缺失量] NVARCHAR(200) NULL,
            [F揽收缺失率] NVARCHAR(200) NULL,
            [F签收总量] NVARCHAR(200) NULL,
            [F派件缺失量] NVARCHAR(200) NULL,
            [F派件缺失率] NVARCHAR(200) NULL,
            [F到件缺失量] NVARCHAR(200) NULL,
            [F到件缺失率] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_物流信息完整汇总_F批次ID' AND object_id = OBJECT_ID(N'STG申通_物流信息完整汇总'))
        CREATE INDEX [IX_STG申通_物流信息完整汇总_F批次ID] ON [STG申通_物流信息完整汇总]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_物流信息完整汇总_数据作用域' AND object_id = OBJECT_ID(N'STG申通_物流信息完整汇总'))
        CREATE INDEX [IX_STG申通_物流信息完整汇总_数据作用域] ON [STG申通_物流信息完整汇总]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_物流信息完整汇总_网点日期_未撤销' AND object_id = OBJECT_ID(N'STG申通_物流信息完整汇总'))
        CREATE UNIQUE INDEX [UX_STG申通_物流信息完整汇总_网点日期_未撤销]
            ON [STG申通_物流信息完整汇总]([F网点名称],[F统计日期],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F网点名称] IS NOT NULL AND [F网点名称] != '';
        ");

        // ═══ 表 3/3：准确性汇总（按天）→ STG申通_物流信息准确汇总（14 业务列） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_物流信息准确汇总')
        CREATE TABLE [STG申通_物流信息准确汇总] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3121 columnMapping，14 列）
            [F统计日期] NVARCHAR(200) NULL,
            [F网点名称] NVARCHAR(200) NULL,
            [F所属网点名称] NVARCHAR(200) NULL,
            [F揽收总量] NVARCHAR(200) NULL,
            [F派件总量] NVARCHAR(200) NULL,
            [F签收总量] NVARCHAR(200) NULL,
            [F扫描单量] NVARCHAR(200) NULL,
            [F问题单量] NVARCHAR(200) NULL,
            [F揽收晚于派件量] NVARCHAR(200) NULL,
            [F揽收晚于签收量] NVARCHAR(200) NULL,
            [F派件晚于签收量] NVARCHAR(200) NULL,
            [F不准确率] NVARCHAR(200) NULL,
            [F到件晚于签收量] NVARCHAR(200) NULL,
            [F到件不准确率] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_物流信息准确汇总_F批次ID' AND object_id = OBJECT_ID(N'STG申通_物流信息准确汇总'))
        CREATE INDEX [IX_STG申通_物流信息准确汇总_F批次ID] ON [STG申通_物流信息准确汇总]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_物流信息准确汇总_数据作用域' AND object_id = OBJECT_ID(N'STG申通_物流信息准确汇总'))
        CREATE INDEX [IX_STG申通_物流信息准确汇总_数据作用域] ON [STG申通_物流信息准确汇总]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_物流信息准确汇总_网点日期_未撤销' AND object_id = OBJECT_ID(N'STG申通_物流信息准确汇总'))
        CREATE UNIQUE INDEX [UX_STG申通_物流信息准确汇总_网点日期_未撤销]
            ON [STG申通_物流信息准确汇总]([F网点名称],[F统计日期],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F网点名称] IS NOT NULL AND [F网点名称] != '';
        ");

        // ═══ 规则 3119：及时性汇总（sheetName=及时性汇总（按天），15 列） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3119)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3119, 192, N'excelInput', N'申通物流信息及时汇总导入规则',
        N'{""targetTable"":""STG申通_物流信息及时汇总"",""outputMode"":""stg"",""sheetName"":""及时性汇总（按天）"",""headerRow"":1,""dataStartRow"":2,""columnIdentifier"":""统计日期,网点名称,揽收总量,上传不及时总率"",""fullColumnIdentifier"":""统计日期,网点名称,所属网点名称,揽收总量,揽收上传不及时量,揽收上传不及时率,派件总量,派件上传不及时量,派件上传不及时率,签收总量,签收上传不及时量,签收上传不及时率,扫描总量,上传不及时总量,上传不及时总率"",""columnMapping"":[{""excelColumn"":""统计日期"",""dbColumn"":""F统计日期""},{""excelColumn"":""网点名称"",""dbColumn"":""F网点名称""},{""excelColumn"":""所属网点名称"",""dbColumn"":""F所属网点名称""},{""excelColumn"":""揽收总量"",""dbColumn"":""F揽收总量""},{""excelColumn"":""揽收上传不及时量"",""dbColumn"":""F揽收上传不及时量""},{""excelColumn"":""揽收上传不及时率"",""dbColumn"":""F揽收上传不及时率""},{""excelColumn"":""派件总量"",""dbColumn"":""F派件总量""},{""excelColumn"":""派件上传不及时量"",""dbColumn"":""F派件上传不及时量""},{""excelColumn"":""派件上传不及时率"",""dbColumn"":""F派件上传不及时率""},{""excelColumn"":""签收总量"",""dbColumn"":""F签收总量""},{""excelColumn"":""签收上传不及时量"",""dbColumn"":""F签收上传不及时量""},{""excelColumn"":""签收上传不及时率"",""dbColumn"":""F签收上传不及时率""},{""excelColumn"":""扫描总量"",""dbColumn"":""F扫描总量""},{""excelColumn"":""上传不及时总量"",""dbColumn"":""F上传不及时总量""},{""excelColumn"":""上传不及时总率"",""dbColumn"":""F上传不及时总率""}],""keyFields"":[""网点名称"",""统计日期""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F网点名称"",""F统计日期""],""batchSplit"":{""enabled"":false}}',
        1, N'申通物流信息指数_及时性汇总（按天）Excel导入配置（多sheet，sheetName=及时性汇总（按天））', REPLACE(NEWID(),'-',''), GETDATE());

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3120)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3120, 192, N'excelInput', N'申通物流信息完整汇总导入规则',
        N'{""targetTable"":""STG申通_物流信息完整汇总"",""outputMode"":""stg"",""sheetName"":""完整性汇总（按天）"",""headerRow"":1,""dataStartRow"":2,""columnIdentifier"":""统计日期,网点名称,订单总量,揽收缺失量"",""fullColumnIdentifier"":""统计日期,网点名称,所属网点名称,订单总量,揽收缺失量,揽收缺失率,签收总量,派件缺失量,派件缺失率,到件缺失量,到件缺失率"",""columnMapping"":[{""excelColumn"":""统计日期"",""dbColumn"":""F统计日期""},{""excelColumn"":""网点名称"",""dbColumn"":""F网点名称""},{""excelColumn"":""所属网点名称"",""dbColumn"":""F所属网点名称""},{""excelColumn"":""订单总量"",""dbColumn"":""F订单总量""},{""excelColumn"":""揽收缺失量"",""dbColumn"":""F揽收缺失量""},{""excelColumn"":""揽收缺失率"",""dbColumn"":""F揽收缺失率""},{""excelColumn"":""签收总量"",""dbColumn"":""F签收总量""},{""excelColumn"":""派件缺失量"",""dbColumn"":""F派件缺失量""},{""excelColumn"":""派件缺失率"",""dbColumn"":""F派件缺失率""},{""excelColumn"":""到件缺失量"",""dbColumn"":""F到件缺失量""},{""excelColumn"":""到件缺失率"",""dbColumn"":""F到件缺失率""}],""keyFields"":[""网点名称"",""统计日期""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F网点名称"",""F统计日期""],""batchSplit"":{""enabled"":false}}',
        1, N'申通物流信息指数_完整性汇总（按天）Excel导入配置（多sheet，sheetName=完整性汇总（按天））', REPLACE(NEWID(),'-',''), GETDATE());

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3121)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3121, 192, N'excelInput', N'申通物流信息准确汇总导入规则',
        N'{""targetTable"":""STG申通_物流信息准确汇总"",""outputMode"":""stg"",""sheetName"":""准确性汇总（按天）"",""headerRow"":1,""dataStartRow"":2,""columnIdentifier"":""统计日期,网点名称,问题单量,到件不准确率"",""fullColumnIdentifier"":""统计日期,网点名称,所属网点名称,揽收总量,派件总量,签收总量,扫描单量,问题单量,揽收晚于派件量,揽收晚于签收量,派件晚于签收量,不准确率,到件晚于签收量,到件不准确率"",""columnMapping"":[{""excelColumn"":""统计日期"",""dbColumn"":""F统计日期""},{""excelColumn"":""网点名称"",""dbColumn"":""F网点名称""},{""excelColumn"":""所属网点名称"",""dbColumn"":""F所属网点名称""},{""excelColumn"":""揽收总量"",""dbColumn"":""F揽收总量""},{""excelColumn"":""派件总量"",""dbColumn"":""F派件总量""},{""excelColumn"":""签收总量"",""dbColumn"":""F签收总量""},{""excelColumn"":""扫描单量"",""dbColumn"":""F扫描单量""},{""excelColumn"":""问题单量"",""dbColumn"":""F问题单量""},{""excelColumn"":""揽收晚于派件量"",""dbColumn"":""F揽收晚于派件量""},{""excelColumn"":""揽收晚于签收量"",""dbColumn"":""F揽收晚于签收量""},{""excelColumn"":""派件晚于签收量"",""dbColumn"":""F派件晚于签收量""},{""excelColumn"":""不准确率"",""dbColumn"":""F不准确率""},{""excelColumn"":""到件晚于签收量"",""dbColumn"":""F到件晚于签收量""},{""excelColumn"":""到件不准确率"",""dbColumn"":""F到件不准确率""}],""keyFields"":[""网点名称"",""统计日期""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F网点名称"",""F统计日期""],""batchSplit"":{""enabled"":false}}',
        1, N'申通物流信息指数_准确性汇总（按天）Excel导入配置（多sheet，sheetName=准确性汇总（按天））', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 流程 2319/2320/2321（多 sheet：F匹配规则=NULL，不参与文件名匹配，靠显式触发） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2319)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2319, NULL, 1, GETDATE(), NULL, N'网点质控：申通物流信息指数_及时性汇总（多sheet，F匹配规则=NULL，靠显式触发）', GETDATE(), NULL, N'申通物流信息及时汇总导入', NULL, N'QC_ST_INFOIDX_TIMELY', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, NULL);
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END

        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2320)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2320, NULL, 1, GETDATE(), NULL, N'网点质控：申通物流信息指数_完整性汇总（多sheet，F匹配规则=NULL，靠显式触发）', GETDATE(), NULL, N'申通物流信息完整汇总导入', NULL, N'QC_ST_INFOIDX_COMPLETE', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, NULL);
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END

        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2321)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2321, NULL, 1, GETDATE(), NULL, N'网点质控：申通物流信息指数_准确性汇总（多sheet，F匹配规则=NULL，靠显式触发）', GETDATE(), NULL, N'申通物流信息准确汇总导入', NULL, N'QC_ST_INFOIDX_ACCURATE', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, NULL);
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 版本 2319/2320/2321（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2319)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2319, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2319, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END

        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2320)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2320, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2320, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END

        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2321)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2321, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2321, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 首节点 5119/5120/5121（ExcelInput 批次级自动节点，插件注册=1，规则各对应 3119/3120/3121） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5119)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5119, 2319, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3119);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END

        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5120)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5120, 2320, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3120);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END

        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5121)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5121, 2321, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3121);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }

    private static void MigrateV42(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══════════════════════════════════════════════════════════════════
        // 退化混合表头「坑」源：签收率未达标考核
        // （ossdfa26db73e7f4f548c21e65749ea6d65（签收率未达标考核）.xlsx，sheet Sheet1）。
        // 第 1 行前 4 列是真名（日期/网点编号/网点名称/所属省区），第 5 列起是分段表头
        // （应签量、48小时签收考核…168小时签收考核、总金额），分段下子字段在第 2 行且跨段大量重复；第 3 行起才是数据。
        // 一期只抽第 1 行「全局唯一」的 6 列：日期/网点编号/网点名称/所属省区/应签量/总金额
        // （应签量取当日应签量、总金额取总考核金额）。规则用 headerRow=1, dataStartRow=3。
        // 分时段(48h/72h/96h/120h/144h/168h)明细因表头子字段重复无法按名映射，一期不逐列建模，
        // 余列由插件自动归集进 F其他列数据。
        // 路由：退化表头内容路由不可靠 → 靠 fileNamePattern *签收率未达标考核* 路由。
        // ═══════════════════════════════════════════════════════════════════

        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_签收率考核汇总')
        CREATE TABLE [STG申通_签收率考核汇总] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3122 columnMapping，仅 6 列；分时段明细未逐列建模，归入 F其他列数据）
            [F日期] NVARCHAR(200) NULL,
            [F网点编号] NVARCHAR(200) NULL,
            [F网点名称] NVARCHAR(200) NULL,
            [F所属省区] NVARCHAR(200) NULL,
            [F应签量] NVARCHAR(200) NULL,
            [F总金额] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_签收率考核汇总_F批次ID' AND object_id = OBJECT_ID(N'STG申通_签收率考核汇总'))
        CREATE INDEX [IX_STG申通_签收率考核汇总_F批次ID] ON [STG申通_签收率考核汇总]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_签收率考核汇总_数据作用域' AND object_id = OBJECT_ID(N'STG申通_签收率考核汇总'))
        CREATE INDEX [IX_STG申通_签收率考核汇总_数据作用域] ON [STG申通_签收率考核汇总]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（网点编号 + 日期 + 组织，仅未撤销 + 网点编号非空）
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_签收率考核汇总_网点日期_未撤销' AND object_id = OBJECT_ID(N'STG申通_签收率考核汇总'))
        CREATE UNIQUE INDEX [UX_STG申通_签收率考核汇总_网点日期_未撤销]
            ON [STG申通_签收率考核汇总]([F网点编号],[F日期],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F网点编号] IS NOT NULL AND [F网点编号] != '';
        ");

        // ═══ 规则 3122（退化表头：headerRow=1/dataStartRow=3，仅映射 6 列全局唯一名） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3122)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3122, 192, N'excelInput', N'申通签收率考核汇总导入规则',
        N'{""targetTable"":""STG申通_签收率考核汇总"",""outputMode"":""stg"",""headerRow"":1,""dataStartRow"":3,""columnIdentifier"":""日期,网点编号,网点名称,所属省区,应签量"",""fullColumnIdentifier"":""日期,网点编号,网点名称,所属省区,应签量,总金额"",""columnMapping"":[{""excelColumn"":""日期"",""dbColumn"":""F日期""},{""excelColumn"":""网点编号"",""dbColumn"":""F网点编号""},{""excelColumn"":""网点名称"",""dbColumn"":""F网点名称""},{""excelColumn"":""所属省区"",""dbColumn"":""F所属省区""},{""excelColumn"":""应签量"",""dbColumn"":""F应签量""},{""excelColumn"":""总金额"",""dbColumn"":""F总金额""}],""keyFields"":[""网点编号"",""日期""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F网点编号"",""F日期""],""batchSplit"":{""enabled"":false}}',
        1, N'申通签收率未达标考核 Excel导入配置（退化表头，仅抽网点/日期/应签量/总考核金额6列，分时段明细未逐列建模）', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 流程 2322（QC_ST_SIGNRATE_ASSESS；退化表头靠 fileNamePattern 路由） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2322)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2322, NULL, 1, GETDATE(), NULL, N'网点质控：申通签收率未达标考核 导入暂存（退化表头，靠 fileNamePattern 路由）', GETDATE(), NULL, N'申通签收率考核汇总导入', NULL, N'QC_ST_SIGNRATE_ASSESS', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""*签收率未达标考核*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 版本 2322（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2322)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2322, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2322, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 首节点 5122（ExcelInput 批次级自动节点，插件注册=1，规则=3122） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5122)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5122, 2322, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3122);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }

    private static void MigrateV43(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_出仓考核汇总 暂存表（系统列 + 31 业务列 + 标准字段） ═══
        // 单行表头，31 列，网点×日期粒度（1 行/网点/日期）。
        // 含大量全角括号列（一频次（考核）应出仓量、一频次（考核）预估考核金额（元）等），
        // dbColumn 去掉全角括号 → F一频次考核应出仓量 / F一频次考核预估考核金额元；excelColumn 保留原文。
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_出仓考核汇总')
        CREATE TABLE [STG申通_出仓考核汇总] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3123 columnMapping，31 列；全角括号列已去括号）
            [F所属网点编码] NVARCHAR(200) NULL,
            [F统计日期] NVARCHAR(200) NULL,
            [F省公司] NVARCHAR(200) NULL,
            [F片区] NVARCHAR(200) NULL,
            [F所属网点] NVARCHAR(200) NULL,
            [F转运中心] NVARCHAR(200) NULL,
            [F派次类型] NVARCHAR(200) NULL,
            [F一频次考核应出仓日期] NVARCHAR(200) NULL,
            [F一频次考核应出仓时间] NVARCHAR(200) NULL,
            [F一频次考核应出仓量] NVARCHAR(200) NULL,
            [F一频次考核出仓及时量] NVARCHAR(200) NULL,
            [F一频次考核出仓及时率] NVARCHAR(200) NULL,
            [F一频次考核未及时出仓量] NVARCHAR(200) NULL,
            [F一频次考核考核目标] NVARCHAR(200) NULL,
            [F一频次考核预估考核金额元] NVARCHAR(200) NULL,
            [F二频次监控应出仓日期] NVARCHAR(200) NULL,
            [F二频次监控应出仓时间] NVARCHAR(200) NULL,
            [F二频次监控应出仓量] NVARCHAR(200) NULL,
            [F二频次监控出仓及时量] NVARCHAR(200) NULL,
            [F二频次监控出仓及时率] NVARCHAR(200) NULL,
            [F二频次监控未及时出仓量] NVARCHAR(200) NULL,
            [F二频次监控考核目标] NVARCHAR(200) NULL,
            [F二频次监控预估考核金额元] NVARCHAR(200) NULL,
            [F三频次监控应出仓日期] NVARCHAR(200) NULL,
            [F三频次监控应出仓时间] NVARCHAR(200) NULL,
            [F三频次监控应出仓量] NVARCHAR(200) NULL,
            [F三频次监控出仓及时量] NVARCHAR(200) NULL,
            [F三频次监控出仓及时率] NVARCHAR(200) NULL,
            [F三频次监控未及时出仓量] NVARCHAR(200) NULL,
            [F三频次监控考核目标] NVARCHAR(200) NULL,
            [F三频次监控预估考核金额元] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_出仓考核汇总_F批次ID' AND object_id = OBJECT_ID(N'STG申通_出仓考核汇总'))
        CREATE INDEX [IX_STG申通_出仓考核汇总_F批次ID] ON [STG申通_出仓考核汇总]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_出仓考核汇总_数据作用域' AND object_id = OBJECT_ID(N'STG申通_出仓考核汇总'))
        CREATE INDEX [IX_STG申通_出仓考核汇总_数据作用域] ON [STG申通_出仓考核汇总]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（所属网点编码 + 统计日期 + 组织，仅未撤销 + 网点编码非空）
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_出仓考核汇总_网点日期_未撤销' AND object_id = OBJECT_ID(N'STG申通_出仓考核汇总'))
        CREATE UNIQUE INDEX [UX_STG申通_出仓考核汇总_网点日期_未撤销]
            ON [STG申通_出仓考核汇总]([F所属网点编码],[F统计日期],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F所属网点编码] IS NOT NULL AND [F所属网点编码] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3123（出仓考核汇总导入，全列映射） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3123)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3123, 192, N'excelInput', N'申通出仓考核汇总导入规则',
        N'{""targetTable"":""STG申通_出仓考核汇总"",""outputMode"":""stg"",""headerRow"":1,""columnIdentifier"":""所属网点编码,转运中心,一频次（考核）应出仓量,一频次（考核）预估考核金额（元）"",""fullColumnIdentifier"":""所属网点编码,统计日期,省公司,片区,所属网点,转运中心,派次类型,一频次（考核）应出仓日期,一频次（考核）应出仓时间,一频次（考核）应出仓量,一频次（考核）出仓及时量,一频次（考核）出仓及时率,一频次（考核）未及时出仓量,一频次（考核）考核目标,一频次（考核）预估考核金额（元）,二频次（监控）应出仓日期,二频次（监控）应出仓时间,二频次（监控）应出仓量,二频次（监控）出仓及时量,二频次（监控）出仓及时率,二频次（监控）未及时出仓量,二频次（监控）考核目标,二频次（监控）预估考核金额（元）,三频次（监控）应出仓日期,三频次（监控）应出仓时间,三频次（监控）应出仓量,三频次（监控）出仓及时量,三频次（监控）出仓及时率,三频次（监控）未及时出仓量,三频次（监控）考核目标,三频次（监控）预估考核金额（元）"",""columnMapping"":[{""excelColumn"":""所属网点编码"",""dbColumn"":""F所属网点编码""},{""excelColumn"":""统计日期"",""dbColumn"":""F统计日期""},{""excelColumn"":""省公司"",""dbColumn"":""F省公司""},{""excelColumn"":""片区"",""dbColumn"":""F片区""},{""excelColumn"":""所属网点"",""dbColumn"":""F所属网点""},{""excelColumn"":""转运中心"",""dbColumn"":""F转运中心""},{""excelColumn"":""派次类型"",""dbColumn"":""F派次类型""},{""excelColumn"":""一频次（考核）应出仓日期"",""dbColumn"":""F一频次考核应出仓日期""},{""excelColumn"":""一频次（考核）应出仓时间"",""dbColumn"":""F一频次考核应出仓时间""},{""excelColumn"":""一频次（考核）应出仓量"",""dbColumn"":""F一频次考核应出仓量""},{""excelColumn"":""一频次（考核）出仓及时量"",""dbColumn"":""F一频次考核出仓及时量""},{""excelColumn"":""一频次（考核）出仓及时率"",""dbColumn"":""F一频次考核出仓及时率""},{""excelColumn"":""一频次（考核）未及时出仓量"",""dbColumn"":""F一频次考核未及时出仓量""},{""excelColumn"":""一频次（考核）考核目标"",""dbColumn"":""F一频次考核考核目标""},{""excelColumn"":""一频次（考核）预估考核金额（元）"",""dbColumn"":""F一频次考核预估考核金额元""},{""excelColumn"":""二频次（监控）应出仓日期"",""dbColumn"":""F二频次监控应出仓日期""},{""excelColumn"":""二频次（监控）应出仓时间"",""dbColumn"":""F二频次监控应出仓时间""},{""excelColumn"":""二频次（监控）应出仓量"",""dbColumn"":""F二频次监控应出仓量""},{""excelColumn"":""二频次（监控）出仓及时量"",""dbColumn"":""F二频次监控出仓及时量""},{""excelColumn"":""二频次（监控）出仓及时率"",""dbColumn"":""F二频次监控出仓及时率""},{""excelColumn"":""二频次（监控）未及时出仓量"",""dbColumn"":""F二频次监控未及时出仓量""},{""excelColumn"":""二频次（监控）考核目标"",""dbColumn"":""F二频次监控考核目标""},{""excelColumn"":""二频次（监控）预估考核金额（元）"",""dbColumn"":""F二频次监控预估考核金额元""},{""excelColumn"":""三频次（监控）应出仓日期"",""dbColumn"":""F三频次监控应出仓日期""},{""excelColumn"":""三频次（监控）应出仓时间"",""dbColumn"":""F三频次监控应出仓时间""},{""excelColumn"":""三频次（监控）应出仓量"",""dbColumn"":""F三频次监控应出仓量""},{""excelColumn"":""三频次（监控）出仓及时量"",""dbColumn"":""F三频次监控出仓及时量""},{""excelColumn"":""三频次（监控）出仓及时率"",""dbColumn"":""F三频次监控出仓及时率""},{""excelColumn"":""三频次（监控）未及时出仓量"",""dbColumn"":""F三频次监控未及时出仓量""},{""excelColumn"":""三频次（监控）考核目标"",""dbColumn"":""F三频次监控考核目标""},{""excelColumn"":""三频次（监控）预估考核金额（元）"",""dbColumn"":""F三频次监控预估考核金额元""}],""keyFields"":[""所属网点编码"",""统计日期""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F所属网点编码"",""F统计日期""],""batchSplit"":{""enabled"":false}}',
        1, N'申通出仓考核汇总 Excel导入配置（单行表头31列，全角括号列 dbColumn 去括号）', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2323（QC_ST_OUTBOUND_ASSESS） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2323)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2323, NULL, 1, GETDATE(), NULL, N'网点质控：申通出仓考核汇总 导入暂存', GETDATE(), NULL, N'申通出仓考核汇总导入', NULL, N'QC_ST_OUTBOUND_ASSESS', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""出仓考核汇总*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2323（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2323)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2323, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2323, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5123（ExcelInput 批次级自动节点，插件注册=1，规则=3123） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5123)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5123, 2323, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3123);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }

    private static void MigrateV44(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_小件员履约指标 暂存表（系统列 + 19 业务列 + 标准字段） ═══
        // 双行表头「坑」源：第 1 行是分组名（当日派签情况/按需上门情况/工单情况/违规行为/真实情况），
        // 第 2 行才是真字段名 → 规则用 headerRow=2, dataStartRow=3。
        // 所属网点/所属小件员 虽为纵向合并单元格（A1:A2/B1:B2），但底层 XML 在 row2 同样写入真值，
        // headerRow=2 时可正常按名映射；19 列名 row2 全唯一，全部映射。
        // 员工级粒度（1 行/网点/小件员），此文件无日期列，按「所属网点 + 所属小件员」去重。
        // 路由：upload-auto 读第 1 行=分组名，内容匹配无效 → 靠 fileNamePattern 小件员履约指标* 路由。
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_小件员履约指标')
        CREATE TABLE [STG申通_小件员履约指标] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3124 columnMapping，19 列，均为 row2 全局唯一列名）
            [F所属网点] NVARCHAR(200) NULL,
            [F所属小件员] NVARCHAR(200) NULL,
            [F当日派签量] NVARCHAR(200) NULL,
            [F未当日派签量] NVARCHAR(200) NULL,
            [F当日派签率] NVARCHAR(200) NULL,
            [F应上门量] NVARCHAR(200) NULL,
            [F未上门量] NVARCHAR(200) NULL,
            [F按需上门率] NVARCHAR(200) NULL,
            [F客诉发起量] NVARCHAR(200) NULL,
            [F工单定责量] NVARCHAR(200) NULL,
            [F客诉发起率] NVARCHAR(200) NULL,
            [F虚假电联] NVARCHAR(200) NULL,
            [F无效电联] NVARCHAR(200) NULL,
            [F双签] NVARCHAR(200) NULL,
            [F照片定位虚假] NVARCHAR(200) NULL,
            [F签收文本不规范] NVARCHAR(200) NULL,
            [F引导代收] NVARCHAR(200) NULL,
            [F回访虚假量] NVARCHAR(200) NULL,
            [F回访真实率] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_小件员履约指标_F批次ID' AND object_id = OBJECT_ID(N'STG申通_小件员履约指标'))
        CREATE INDEX [IX_STG申通_小件员履约指标_F批次ID] ON [STG申通_小件员履约指标]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_小件员履约指标_数据作用域' AND object_id = OBJECT_ID(N'STG申通_小件员履约指标'))
        CREATE INDEX [IX_STG申通_小件员履约指标_数据作用域] ON [STG申通_小件员履约指标]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（所属网点 + 所属小件员 + 组织，仅未撤销 + 所属网点非空）
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_小件员履约指标_网点小件员_未撤销' AND object_id = OBJECT_ID(N'STG申通_小件员履约指标'))
        CREATE UNIQUE INDEX [UX_STG申通_小件员履约指标_网点小件员_未撤销]
            ON [STG申通_小件员履约指标]([F所属网点],[F所属小件员],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F所属网点] IS NOT NULL AND [F所属网点] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3124（小件员履约指标导入，双行表头 headerRow=2/dataStartRow=3，全列映射） ═══
        // 路由说明：upload-auto 读第 1 行做内容路由，本文件第 1 行是分组名（非字段名），columnIdentifier 内容匹配无效，
        // 故必须靠 fileNamePattern 小件员履约指标* 路由；columnIdentifier 仍按 row2 字段写（无害）。
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3124)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3124, 192, N'excelInput', N'申通小件员履约指标导入规则',
        N'{""targetTable"":""STG申通_小件员履约指标"",""outputMode"":""stg"",""headerRow"":2,""dataStartRow"":3,""columnIdentifier"":""所属网点,所属小件员,当日派签量,按需上门率,回访真实率"",""fullColumnIdentifier"":""所属网点,所属小件员,当日派签量,未当日派签量,当日派签率,应上门量,未上门量,按需上门率,客诉发起量,工单定责量,客诉发起率,虚假电联,无效电联,双签,照片定位虚假,签收文本不规范,引导代收,回访虚假量,回访真实率"",""columnMapping"":[{""excelColumn"":""所属网点"",""dbColumn"":""F所属网点""},{""excelColumn"":""所属小件员"",""dbColumn"":""F所属小件员""},{""excelColumn"":""当日派签量"",""dbColumn"":""F当日派签量""},{""excelColumn"":""未当日派签量"",""dbColumn"":""F未当日派签量""},{""excelColumn"":""当日派签率"",""dbColumn"":""F当日派签率""},{""excelColumn"":""应上门量"",""dbColumn"":""F应上门量""},{""excelColumn"":""未上门量"",""dbColumn"":""F未上门量""},{""excelColumn"":""按需上门率"",""dbColumn"":""F按需上门率""},{""excelColumn"":""客诉发起量"",""dbColumn"":""F客诉发起量""},{""excelColumn"":""工单定责量"",""dbColumn"":""F工单定责量""},{""excelColumn"":""客诉发起率"",""dbColumn"":""F客诉发起率""},{""excelColumn"":""虚假电联"",""dbColumn"":""F虚假电联""},{""excelColumn"":""无效电联"",""dbColumn"":""F无效电联""},{""excelColumn"":""双签"",""dbColumn"":""F双签""},{""excelColumn"":""照片定位虚假"",""dbColumn"":""F照片定位虚假""},{""excelColumn"":""签收文本不规范"",""dbColumn"":""F签收文本不规范""},{""excelColumn"":""引导代收"",""dbColumn"":""F引导代收""},{""excelColumn"":""回访虚假量"",""dbColumn"":""F回访虚假量""},{""excelColumn"":""回访真实率"",""dbColumn"":""F回访真实率""}],""keyFields"":[""所属网点"",""所属小件员""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F所属网点"",""F所属小件员""],""batchSplit"":{""enabled"":false}}',
        1, N'申通小件员履约指标历史数据 Excel导入配置（双行表头，headerRow=2 映射 19 列，员工级，靠 fileNamePattern 路由）', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2324（QC_ST_COURIER_FULFILL；靠 fileNamePattern 路由） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2324)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2324, NULL, 1, GETDATE(), NULL, N'网点质控：申通小件员履约指标 导入暂存（双行表头/员工级，靠 fileNamePattern 路由）', GETDATE(), NULL, N'申通小件员履约指标导入', NULL, N'QC_ST_COURIER_FULFILL', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""小件员履约指标*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2324（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2324)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2324, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2324, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5124（ExcelInput 批次级自动节点，插件注册=1，规则=3124） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5124)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5124, 2324, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3124);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }

    private static void MigrateV45(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_末端派送网点汇总 暂存表（系统列 + 55 业务列 + 标准字段） ═══
        // 单行表头，55 列，应签网点×日期粒度（1 行/网点/日期），sheet sheet1。
        // 列名均合法（无括号/斜杠等非法字符），dbColumn 直接加 F 前缀（含 FT0/FT1/FT2/FT3 延迟列）。
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_末端派送网点汇总')
        CREATE TABLE [STG申通_末端派送网点汇总] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3125 columnMapping，55 列）
            [F统计日期] NVARCHAR(200) NULL,
            [F中转站] NVARCHAR(200) NULL,
            [F应签所属网点] NVARCHAR(200) NULL,
            [F应签网点] NVARCHAR(200) NULL,
            [F派件员] NVARCHAR(200) NULL,
            [F四级区域] NVARCHAR(200) NULL,
            [F五级区域] NVARCHAR(200) NULL,
            [F频次类型] NVARCHAR(200) NULL,
            [F派次类型] NVARCHAR(200) NULL,
            [F预计考核金额] NVARCHAR(200) NULL,
            [F有偿派费金额] NVARCHAR(200) NULL,
            [F预计返款金额] NVARCHAR(200) NULL,
            [F一阶段考核数量] NVARCHAR(200) NULL,
            [F一阶段及时签收数量] NVARCHAR(200) NULL,
            [F一阶段及时签收率] NVARCHAR(200) NULL,
            [F一阶段目标值] NVARCHAR(200) NULL,
            [F一阶段预计考核金额] NVARCHAR(200) NULL,
            [F一阶段未及时签收数量] NVARCHAR(200) NULL,
            [F二阶段考核数量] NVARCHAR(200) NULL,
            [F二阶段及时签收数量] NVARCHAR(200) NULL,
            [F二阶段及时签收率] NVARCHAR(200) NULL,
            [F二阶段目标值] NVARCHAR(200) NULL,
            [F二阶段预计考核金额] NVARCHAR(200) NULL,
            [F二阶段未及时签收数量] NVARCHAR(200) NULL,
            [FT0延迟签收数量] NVARCHAR(200) NULL,
            [FT1延迟签收数量] NVARCHAR(200) NULL,
            [FT2延迟签收数量] NVARCHAR(200) NULL,
            [FT3延迟签收数量] NVARCHAR(200) NULL,
            [F当天考核数量] NVARCHAR(200) NULL,
            [F当天预估考核金额] NVARCHAR(200) NULL,
            [F当天签收及时量] NVARCHAR(200) NULL,
            [F当天及时签收率] NVARCHAR(200) NULL,
            [F当天目标值] NVARCHAR(200) NULL,
            [F当天签收延迟24h内数量] NVARCHAR(200) NULL,
            [F当天签收延迟24至48h数量] NVARCHAR(200) NULL,
            [F当天签收延迟超48h数量] NVARCHAR(200) NULL,
            [F当天签收延迟24h内率] NVARCHAR(200) NULL,
            [F当天签收延迟24至48h率] NVARCHAR(200) NULL,
            [F当天签收延迟超48h率] NVARCHAR(200) NULL,
            [F14点签收数量] NVARCHAR(200) NULL,
            [F20点签收数量] NVARCHAR(200) NULL,
            [F14点及时签收率] NVARCHAR(200) NULL,
            [F20点及时签收率] NVARCHAR(200) NULL,
            [F签收时长] NVARCHAR(200) NULL,
            [F网点时效用时] NVARCHAR(200) NULL,
            [F先签后派数量] NVARCHAR(200) NULL,
            [F先第三方后派数量] NVARCHAR(200) NULL,
            [F应签收数量] NVARCHAR(200) NULL,
            [F已签收数量] NVARCHAR(200) NULL,
            [F签收进度] NVARCHAR(200) NULL,
            [F未签收数量] NVARCHAR(200) NULL,
            [F未签收有问题件数量] NVARCHAR(200) NULL,
            [F已派件数量] NVARCHAR(200) NULL,
            [F未派件数量] NVARCHAR(200) NULL,
            [F已派未签数量] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_末端派送网点汇总_F批次ID' AND object_id = OBJECT_ID(N'STG申通_末端派送网点汇总'))
        CREATE INDEX [IX_STG申通_末端派送网点汇总_F批次ID] ON [STG申通_末端派送网点汇总]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_末端派送网点汇总_数据作用域' AND object_id = OBJECT_ID(N'STG申通_末端派送网点汇总'))
        CREATE INDEX [IX_STG申通_末端派送网点汇总_数据作用域] ON [STG申通_末端派送网点汇总]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（应签所属网点 + 统计日期 + 组织，仅未撤销 + 应签所属网点非空）
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_末端派送网点汇总_网点日期_未撤销' AND object_id = OBJECT_ID(N'STG申通_末端派送网点汇总'))
        CREATE UNIQUE INDEX [UX_STG申通_末端派送网点汇总_网点日期_未撤销]
            ON [STG申通_末端派送网点汇总]([F应签所属网点],[F统计日期],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F应签所属网点] IS NOT NULL AND [F应签所属网点] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3125（末端派送网点汇总导入，全列映射） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3125)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3125, 192, N'excelInput', N'申通末端派送网点汇总导入规则',
        N'{""targetTable"":""STG申通_末端派送网点汇总"",""outputMode"":""stg"",""headerRow"":1,""columnIdentifier"":""统计日期,应签所属网点,预计考核金额,有偿派费金额"",""fullColumnIdentifier"":""统计日期,中转站,应签所属网点,应签网点,派件员,四级区域,五级区域,频次类型,派次类型,预计考核金额,有偿派费金额,预计返款金额,一阶段考核数量,一阶段及时签收数量,一阶段及时签收率,一阶段目标值,一阶段预计考核金额,一阶段未及时签收数量,二阶段考核数量,二阶段及时签收数量,二阶段及时签收率,二阶段目标值,二阶段预计考核金额,二阶段未及时签收数量,T0延迟签收数量,T1延迟签收数量,T2延迟签收数量,T3延迟签收数量,当天考核数量,当天预估考核金额,当天签收及时量,当天及时签收率,当天目标值,当天签收延迟24h内数量,当天签收延迟24至48h数量,当天签收延迟超48h数量,当天签收延迟24h内率,当天签收延迟24至48h率,当天签收延迟超48h率,14点签收数量,20点签收数量,14点及时签收率,20点及时签收率,签收时长,网点时效用时,先签后派数量,先第三方后派数量,应签收数量,已签收数量,签收进度,未签收数量,未签收有问题件数量,已派件数量,未派件数量,已派未签数量"",""columnMapping"":[{""excelColumn"":""统计日期"",""dbColumn"":""F统计日期""},{""excelColumn"":""中转站"",""dbColumn"":""F中转站""},{""excelColumn"":""应签所属网点"",""dbColumn"":""F应签所属网点""},{""excelColumn"":""应签网点"",""dbColumn"":""F应签网点""},{""excelColumn"":""派件员"",""dbColumn"":""F派件员""},{""excelColumn"":""四级区域"",""dbColumn"":""F四级区域""},{""excelColumn"":""五级区域"",""dbColumn"":""F五级区域""},{""excelColumn"":""频次类型"",""dbColumn"":""F频次类型""},{""excelColumn"":""派次类型"",""dbColumn"":""F派次类型""},{""excelColumn"":""预计考核金额"",""dbColumn"":""F预计考核金额""},{""excelColumn"":""有偿派费金额"",""dbColumn"":""F有偿派费金额""},{""excelColumn"":""预计返款金额"",""dbColumn"":""F预计返款金额""},{""excelColumn"":""一阶段考核数量"",""dbColumn"":""F一阶段考核数量""},{""excelColumn"":""一阶段及时签收数量"",""dbColumn"":""F一阶段及时签收数量""},{""excelColumn"":""一阶段及时签收率"",""dbColumn"":""F一阶段及时签收率""},{""excelColumn"":""一阶段目标值"",""dbColumn"":""F一阶段目标值""},{""excelColumn"":""一阶段预计考核金额"",""dbColumn"":""F一阶段预计考核金额""},{""excelColumn"":""一阶段未及时签收数量"",""dbColumn"":""F一阶段未及时签收数量""},{""excelColumn"":""二阶段考核数量"",""dbColumn"":""F二阶段考核数量""},{""excelColumn"":""二阶段及时签收数量"",""dbColumn"":""F二阶段及时签收数量""},{""excelColumn"":""二阶段及时签收率"",""dbColumn"":""F二阶段及时签收率""},{""excelColumn"":""二阶段目标值"",""dbColumn"":""F二阶段目标值""},{""excelColumn"":""二阶段预计考核金额"",""dbColumn"":""F二阶段预计考核金额""},{""excelColumn"":""二阶段未及时签收数量"",""dbColumn"":""F二阶段未及时签收数量""},{""excelColumn"":""T0延迟签收数量"",""dbColumn"":""FT0延迟签收数量""},{""excelColumn"":""T1延迟签收数量"",""dbColumn"":""FT1延迟签收数量""},{""excelColumn"":""T2延迟签收数量"",""dbColumn"":""FT2延迟签收数量""},{""excelColumn"":""T3延迟签收数量"",""dbColumn"":""FT3延迟签收数量""},{""excelColumn"":""当天考核数量"",""dbColumn"":""F当天考核数量""},{""excelColumn"":""当天预估考核金额"",""dbColumn"":""F当天预估考核金额""},{""excelColumn"":""当天签收及时量"",""dbColumn"":""F当天签收及时量""},{""excelColumn"":""当天及时签收率"",""dbColumn"":""F当天及时签收率""},{""excelColumn"":""当天目标值"",""dbColumn"":""F当天目标值""},{""excelColumn"":""当天签收延迟24h内数量"",""dbColumn"":""F当天签收延迟24h内数量""},{""excelColumn"":""当天签收延迟24至48h数量"",""dbColumn"":""F当天签收延迟24至48h数量""},{""excelColumn"":""当天签收延迟超48h数量"",""dbColumn"":""F当天签收延迟超48h数量""},{""excelColumn"":""当天签收延迟24h内率"",""dbColumn"":""F当天签收延迟24h内率""},{""excelColumn"":""当天签收延迟24至48h率"",""dbColumn"":""F当天签收延迟24至48h率""},{""excelColumn"":""当天签收延迟超48h率"",""dbColumn"":""F当天签收延迟超48h率""},{""excelColumn"":""14点签收数量"",""dbColumn"":""F14点签收数量""},{""excelColumn"":""20点签收数量"",""dbColumn"":""F20点签收数量""},{""excelColumn"":""14点及时签收率"",""dbColumn"":""F14点及时签收率""},{""excelColumn"":""20点及时签收率"",""dbColumn"":""F20点及时签收率""},{""excelColumn"":""签收时长"",""dbColumn"":""F签收时长""},{""excelColumn"":""网点时效用时"",""dbColumn"":""F网点时效用时""},{""excelColumn"":""先签后派数量"",""dbColumn"":""F先签后派数量""},{""excelColumn"":""先第三方后派数量"",""dbColumn"":""F先第三方后派数量""},{""excelColumn"":""应签收数量"",""dbColumn"":""F应签收数量""},{""excelColumn"":""已签收数量"",""dbColumn"":""F已签收数量""},{""excelColumn"":""签收进度"",""dbColumn"":""F签收进度""},{""excelColumn"":""未签收数量"",""dbColumn"":""F未签收数量""},{""excelColumn"":""未签收有问题件数量"",""dbColumn"":""F未签收有问题件数量""},{""excelColumn"":""已派件数量"",""dbColumn"":""F已派件数量""},{""excelColumn"":""未派件数量"",""dbColumn"":""F未派件数量""},{""excelColumn"":""已派未签数量"",""dbColumn"":""F已派未签数量""}],""keyFields"":[""应签所属网点"",""统计日期""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F应签所属网点"",""F统计日期""],""batchSplit"":{""enabled"":false}}',
        1, N'申通末端派送考核(新)网点汇总 Excel导入配置（单行表头55列，全列映射，应签网点×日期粒度）', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2325（QC_ST_DELIVERY_NET_SUMMARY） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2325)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2325, NULL, 1, GETDATE(), NULL, N'网点质控：申通末端派送考核(新)网点汇总 导入暂存', GETDATE(), NULL, N'申通末端派送网点汇总导入', NULL, N'QC_ST_DELIVERY_NET_SUMMARY', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""末端派送考核(新)网点汇总*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2325（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2325)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2325, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2325, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5125（ExcelInput 批次级自动节点，插件注册=1，规则=3125） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5125)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5125, 2325, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3125);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }

    private static void MigrateV46(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_积压监控汇总 暂存表（系统列 + 64 业务列 + 标准字段） ═══
        // 单行表头，64 列，网点×日期粒度（1 行/网点/日期），sheet 积压异常监控。
        // 含括号列（遗失率（ppm)、超3天积压量（疑似遗失）、虚签投诉率(上一周) 等），dbColumn 去括号 → F遗失率ppm 等；
        // 另含连字符列（积压8-15天量、t-1虚签投诉量 等），dbColumn 去 '-' → F积压815天量 / Ft1虚签投诉量。excelColumn 保留原文。
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_积压监控汇总')
        CREATE TABLE [STG申通_积压监控汇总] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3126 columnMapping，64 列；括号/连字符列已去除非法字符）
            [F统计日期] NVARCHAR(200) NULL,
            [F南北中部] NVARCHAR(200) NULL,
            [F大区] NVARCHAR(200) NULL,
            [F省区] NVARCHAR(200) NULL,
            [F省份] NVARCHAR(200) NULL,
            [F片区名称] NVARCHAR(200) NULL,
            [F片区管家] NVARCHAR(200) NULL,
            [F网点编码] NVARCHAR(200) NULL,
            [F网点名称] NVARCHAR(200) NULL,
            [F网点星级] NVARCHAR(200) NULL,
            [F代派网点编码] NVARCHAR(200) NULL,
            [F代派网点名称] NVARCHAR(200) NULL,
            [F异常状态] NVARCHAR(200) NULL,
            [F网点状态] NVARCHAR(200) NULL,
            [F拦截状态] NVARCHAR(200) NULL,
            [F日均出港量] NVARCHAR(200) NULL,
            [F日均进港量] NVARCHAR(200) NULL,
            [F积压倍数] NVARCHAR(200) NULL,
            [F15日累计积压量] NVARCHAR(200) NULL,
            [F14日累计积压量] NVARCHAR(200) NULL,
            [F积压实时数据] NVARCHAR(200) NULL,
            [F常态签收率] NVARCHAR(200) NULL,
            [F近7天签收率] NVARCHAR(200) NULL,
            [F当天签收率] NVARCHAR(200) NULL,
            [F进港量] NVARCHAR(200) NULL,
            [F当天签收量] NVARCHAR(200) NULL,
            [F清件进度] NVARCHAR(200) NULL,
            [F清件能力] NVARCHAR(200) NULL,
            [F近3天日均签收量] NVARCHAR(200) NULL,
            [F超3天积压量疑似遗失] NVARCHAR(200) NULL,
            [F超3天积压实时数量] NVARCHAR(200) NULL,
            [F超3天积压占比] NVARCHAR(200) NULL,
            [F超5天积压量智能遗失] NVARCHAR(200) NULL,
            [F超5天积压实时数量] NVARCHAR(200) NULL,
            [F超5天积压占比] NVARCHAR(200) NULL,
            [F超7天积压量超长单] NVARCHAR(200) NULL,
            [F超7天积压实时数量] NVARCHAR(200) NULL,
            [F超7天积压占比] NVARCHAR(200) NULL,
            [F积压1天量] NVARCHAR(200) NULL,
            [F积压2天量] NVARCHAR(200) NULL,
            [F积压3天量] NVARCHAR(200) NULL,
            [F积压4天量] NVARCHAR(200) NULL,
            [F积压5天量] NVARCHAR(200) NULL,
            [F积压6天量] NVARCHAR(200) NULL,
            [F积压6天实时数据] NVARCHAR(200) NULL,
            [F积压7天量] NVARCHAR(200) NULL,
            [F积压815天量] NVARCHAR(200) NULL,
            [F积压1630天量] NVARCHAR(200) NULL,
            [F积压3160天量] NVARCHAR(200) NULL,
            [F遗失率ppm] NVARCHAR(200) NULL,
            [F遗失量] NVARCHAR(200) NULL,
            [F进港投诉量] NVARCHAR(200) NULL,
            [F进港投诉率] NVARCHAR(200) NULL,
            [F虚签投诉率上一周] NVARCHAR(200) NULL,
            [F7日虚签投诉量] NVARCHAR(200) NULL,
            [Ft1虚签投诉量] NVARCHAR(200) NULL,
            [Ft2虚签投诉量] NVARCHAR(200) NULL,
            [Ft3虚签投诉量] NVARCHAR(200) NULL,
            [Ft4虚签投诉量] NVARCHAR(200) NULL,
            [Ft5虚签投诉量] NVARCHAR(200) NULL,
            [Ft6虚签投诉量] NVARCHAR(200) NULL,
            [Ft7虚签投诉量] NVARCHAR(200) NULL,
            [F剔除前累计积压量] NVARCHAR(200) NULL,
            [F人工剔除量] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_积压监控汇总_F批次ID' AND object_id = OBJECT_ID(N'STG申通_积压监控汇总'))
        CREATE INDEX [IX_STG申通_积压监控汇总_F批次ID] ON [STG申通_积压监控汇总]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_积压监控汇总_数据作用域' AND object_id = OBJECT_ID(N'STG申通_积压监控汇总'))
        CREATE INDEX [IX_STG申通_积压监控汇总_数据作用域] ON [STG申通_积压监控汇总]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（网点编码 + 统计日期 + 组织，仅未撤销 + 网点编码非空）
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_积压监控汇总_网点日期_未撤销' AND object_id = OBJECT_ID(N'STG申通_积压监控汇总'))
        CREATE UNIQUE INDEX [UX_STG申通_积压监控汇总_网点日期_未撤销]
            ON [STG申通_积压监控汇总]([F网点编码],[F统计日期],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F网点编码] IS NOT NULL AND [F网点编码] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3126（积压监控汇总导入，全列映射） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3126)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3126, 192, N'excelInput', N'申通积压监控汇总导入规则',
        N'{""targetTable"":""STG申通_积压监控汇总"",""outputMode"":""stg"",""headerRow"":1,""sheetName"":""积压异常监控"",""columnIdentifier"":""网点编码,积压倍数,遗失率（ppm),进港投诉率"",""fullColumnIdentifier"":""统计日期,南北中部,大区,省区,省份,片区名称,片区管家,网点编码,网点名称,网点星级,代派网点编码,代派网点名称,异常状态,网点状态,拦截状态,日均出港量,日均进港量,积压倍数,15日累计积压量,14日累计积压量,积压实时数据,常态签收率,近7天签收率,当天签收率,进港量,当天签收量,清件进度,清件能力,近3天日均签收量,超3天积压量（疑似遗失）,超3天积压实时数量,超3天积压占比,超5天积压量（智能遗失）,超5天积压实时数量,超5天积压占比,超7天积压量（超长单）,超7天积压实时数量,超7天积压占比,积压1天量,积压2天量,积压3天量,积压4天量,积压5天量,积压6天量,积压6天实时数据,积压7天量,积压8-15天量,积压16-30天量,积压31-60天量,遗失率（ppm),遗失量,进港投诉量,进港投诉率,虚签投诉率(上一周),7日虚签投诉量,t-1虚签投诉量,t-2虚签投诉量,t-3虚签投诉量,t-4虚签投诉量,t-5虚签投诉量,t-6虚签投诉量,t-7虚签投诉量,剔除前累计积压量,人工剔除量"",""columnMapping"":[{""excelColumn"":""统计日期"",""dbColumn"":""F统计日期""},{""excelColumn"":""南北中部"",""dbColumn"":""F南北中部""},{""excelColumn"":""大区"",""dbColumn"":""F大区""},{""excelColumn"":""省区"",""dbColumn"":""F省区""},{""excelColumn"":""省份"",""dbColumn"":""F省份""},{""excelColumn"":""片区名称"",""dbColumn"":""F片区名称""},{""excelColumn"":""片区管家"",""dbColumn"":""F片区管家""},{""excelColumn"":""网点编码"",""dbColumn"":""F网点编码""},{""excelColumn"":""网点名称"",""dbColumn"":""F网点名称""},{""excelColumn"":""网点星级"",""dbColumn"":""F网点星级""},{""excelColumn"":""代派网点编码"",""dbColumn"":""F代派网点编码""},{""excelColumn"":""代派网点名称"",""dbColumn"":""F代派网点名称""},{""excelColumn"":""异常状态"",""dbColumn"":""F异常状态""},{""excelColumn"":""网点状态"",""dbColumn"":""F网点状态""},{""excelColumn"":""拦截状态"",""dbColumn"":""F拦截状态""},{""excelColumn"":""日均出港量"",""dbColumn"":""F日均出港量""},{""excelColumn"":""日均进港量"",""dbColumn"":""F日均进港量""},{""excelColumn"":""积压倍数"",""dbColumn"":""F积压倍数""},{""excelColumn"":""15日累计积压量"",""dbColumn"":""F15日累计积压量""},{""excelColumn"":""14日累计积压量"",""dbColumn"":""F14日累计积压量""},{""excelColumn"":""积压实时数据"",""dbColumn"":""F积压实时数据""},{""excelColumn"":""常态签收率"",""dbColumn"":""F常态签收率""},{""excelColumn"":""近7天签收率"",""dbColumn"":""F近7天签收率""},{""excelColumn"":""当天签收率"",""dbColumn"":""F当天签收率""},{""excelColumn"":""进港量"",""dbColumn"":""F进港量""},{""excelColumn"":""当天签收量"",""dbColumn"":""F当天签收量""},{""excelColumn"":""清件进度"",""dbColumn"":""F清件进度""},{""excelColumn"":""清件能力"",""dbColumn"":""F清件能力""},{""excelColumn"":""近3天日均签收量"",""dbColumn"":""F近3天日均签收量""},{""excelColumn"":""超3天积压量（疑似遗失）"",""dbColumn"":""F超3天积压量疑似遗失""},{""excelColumn"":""超3天积压实时数量"",""dbColumn"":""F超3天积压实时数量""},{""excelColumn"":""超3天积压占比"",""dbColumn"":""F超3天积压占比""},{""excelColumn"":""超5天积压量（智能遗失）"",""dbColumn"":""F超5天积压量智能遗失""},{""excelColumn"":""超5天积压实时数量"",""dbColumn"":""F超5天积压实时数量""},{""excelColumn"":""超5天积压占比"",""dbColumn"":""F超5天积压占比""},{""excelColumn"":""超7天积压量（超长单）"",""dbColumn"":""F超7天积压量超长单""},{""excelColumn"":""超7天积压实时数量"",""dbColumn"":""F超7天积压实时数量""},{""excelColumn"":""超7天积压占比"",""dbColumn"":""F超7天积压占比""},{""excelColumn"":""积压1天量"",""dbColumn"":""F积压1天量""},{""excelColumn"":""积压2天量"",""dbColumn"":""F积压2天量""},{""excelColumn"":""积压3天量"",""dbColumn"":""F积压3天量""},{""excelColumn"":""积压4天量"",""dbColumn"":""F积压4天量""},{""excelColumn"":""积压5天量"",""dbColumn"":""F积压5天量""},{""excelColumn"":""积压6天量"",""dbColumn"":""F积压6天量""},{""excelColumn"":""积压6天实时数据"",""dbColumn"":""F积压6天实时数据""},{""excelColumn"":""积压7天量"",""dbColumn"":""F积压7天量""},{""excelColumn"":""积压8-15天量"",""dbColumn"":""F积压815天量""},{""excelColumn"":""积压16-30天量"",""dbColumn"":""F积压1630天量""},{""excelColumn"":""积压31-60天量"",""dbColumn"":""F积压3160天量""},{""excelColumn"":""遗失率（ppm)"",""dbColumn"":""F遗失率ppm""},{""excelColumn"":""遗失量"",""dbColumn"":""F遗失量""},{""excelColumn"":""进港投诉量"",""dbColumn"":""F进港投诉量""},{""excelColumn"":""进港投诉率"",""dbColumn"":""F进港投诉率""},{""excelColumn"":""虚签投诉率(上一周)"",""dbColumn"":""F虚签投诉率上一周""},{""excelColumn"":""7日虚签投诉量"",""dbColumn"":""F7日虚签投诉量""},{""excelColumn"":""t-1虚签投诉量"",""dbColumn"":""Ft1虚签投诉量""},{""excelColumn"":""t-2虚签投诉量"",""dbColumn"":""Ft2虚签投诉量""},{""excelColumn"":""t-3虚签投诉量"",""dbColumn"":""Ft3虚签投诉量""},{""excelColumn"":""t-4虚签投诉量"",""dbColumn"":""Ft4虚签投诉量""},{""excelColumn"":""t-5虚签投诉量"",""dbColumn"":""Ft5虚签投诉量""},{""excelColumn"":""t-6虚签投诉量"",""dbColumn"":""Ft6虚签投诉量""},{""excelColumn"":""t-7虚签投诉量"",""dbColumn"":""Ft7虚签投诉量""},{""excelColumn"":""剔除前累计积压量"",""dbColumn"":""F剔除前累计积压量""},{""excelColumn"":""人工剔除量"",""dbColumn"":""F人工剔除量""}],""keyFields"":[""网点编码"",""统计日期""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F网点编码"",""F统计日期""],""batchSplit"":{""enabled"":false}}',
        1, N'申通积压异常监控汇总 Excel导入配置（单行表头64列，含括号/连字符列 dbColumn 去除非法字符，sheet 积压异常监控）', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2326（QC_ST_BACKLOG_MONITOR） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2326)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2326, NULL, 1, GETDATE(), NULL, N'网点质控：申通积压异常监控汇总 导入暂存', GETDATE(), NULL, N'申通积压监控汇总导入', NULL, N'QC_ST_BACKLOG_MONITOR', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""积压异常监控*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2326（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2326)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2326, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2326, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5126（ExcelInput 批次级自动节点，插件注册=1，规则=3126） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5126)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5126, 2326, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3126);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }

    private static void MigrateV47(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_交货滞留汇总 暂存表（系统列 + 30 业务列 + 标准字段） ═══
        // 单行表头，30 列，揽收所属网点×日期粒度（1 行/网点/日期），sheet sheet1。
        // 含括号列（交货平均用时(h) → F交货平均用时h）、'&' 列（考核滞留&揽收超48h量 → F考核滞留揽收超48h量）、
        // 连字符列（揽收超48小时预估考核-日 → F揽收超48小时预估考核日 / 滞留预估考核-日 → F滞留预估考核日）。excelColumn 保留原文。
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_交货滞留汇总')
        CREATE TABLE [STG申通_交货滞留汇总] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3127 columnMapping，30 列；括号/连字符/'&' 列已去除非法字符）
            [F统计日期] NVARCHAR(200) NULL,
            [F揽收网点] NVARCHAR(200) NULL,
            [F揽收网点编码] NVARCHAR(200) NULL,
            [F揽收网点所属网点] NVARCHAR(200) NULL,
            [F揽收所属网点编码] NVARCHAR(200) NULL,
            [F揽收网点省区] NVARCHAR(200) NULL,
            [F揽收网点大区] NVARCHAR(200) NULL,
            [F揽收网点省份] NVARCHAR(200) NULL,
            [F中心名称] NVARCHAR(200) NULL,
            [F客户编码] NVARCHAR(200) NULL,
            [F客户名称] NVARCHAR(200) NULL,
            [F线路类型] NVARCHAR(200) NULL,
            [F原始揽收量] NVARCHAR(200) NULL,
            [F总揽收量] NVARCHAR(200) NULL,
            [F交货平均用时h] NVARCHAR(200) NULL,
            [F交货及时量] NVARCHAR(200) NULL,
            [F交货延误量] NVARCHAR(200) NULL,
            [F总滞留量] NVARCHAR(200) NULL,
            [F未交货量] NVARCHAR(200) NULL,
            [F滞留率] NVARCHAR(200) NULL,
            [F目标值] NVARCHAR(200) NULL,
            [F揽收超48h总量] NVARCHAR(200) NULL,
            [F揽收超48h已交货量] NVARCHAR(200) NULL,
            [F揽收超48h未交货量] NVARCHAR(200) NULL,
            [F考核滞留揽收超48h量] NVARCHAR(200) NULL,
            [F揽收超48小时预估考核日] NVARCHAR(200) NULL,
            [F滞留预估考核日] NVARCHAR(200) NULL,
            [F考核滞留量] NVARCHAR(200) NULL,
            [F白名单标识] NVARCHAR(200) NULL,
            [F分频次标识] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_交货滞留汇总_F批次ID' AND object_id = OBJECT_ID(N'STG申通_交货滞留汇总'))
        CREATE INDEX [IX_STG申通_交货滞留汇总_F批次ID] ON [STG申通_交货滞留汇总]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_交货滞留汇总_数据作用域' AND object_id = OBJECT_ID(N'STG申通_交货滞留汇总'))
        CREATE INDEX [IX_STG申通_交货滞留汇总_数据作用域] ON [STG申通_交货滞留汇总]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（揽收所属网点编码 + 统计日期 + 组织，仅未撤销 + 揽收所属网点编码非空）
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_交货滞留汇总_网点日期_未撤销' AND object_id = OBJECT_ID(N'STG申通_交货滞留汇总'))
        CREATE UNIQUE INDEX [UX_STG申通_交货滞留汇总_网点日期_未撤销]
            ON [STG申通_交货滞留汇总]([F揽收所属网点编码],[F统计日期],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F揽收所属网点编码] IS NOT NULL AND [F揽收所属网点编码] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3127（交货滞留汇总导入，全列映射） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3127)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3127, 192, N'excelInput', N'申通交货滞留汇总导入规则',
        N'{""targetTable"":""STG申通_交货滞留汇总"",""outputMode"":""stg"",""headerRow"":1,""sheetName"":""sheet1"",""columnIdentifier"":""揽收所属网点编码,滞留率,考核滞留量,目标值"",""fullColumnIdentifier"":""统计日期,揽收网点,揽收网点编码,揽收网点所属网点,揽收所属网点编码,揽收网点省区,揽收网点大区,揽收网点省份,中心名称,客户编码,客户名称,线路类型,原始揽收量,总揽收量,交货平均用时(h),交货及时量,交货延误量,总滞留量,未交货量,滞留率,目标值,揽收超48h总量,揽收超48h已交货量,揽收超48h未交货量,考核滞留&揽收超48h量,揽收超48小时预估考核-日,滞留预估考核-日,考核滞留量,白名单标识,分频次标识"",""columnMapping"":[{""excelColumn"":""统计日期"",""dbColumn"":""F统计日期""},{""excelColumn"":""揽收网点"",""dbColumn"":""F揽收网点""},{""excelColumn"":""揽收网点编码"",""dbColumn"":""F揽收网点编码""},{""excelColumn"":""揽收网点所属网点"",""dbColumn"":""F揽收网点所属网点""},{""excelColumn"":""揽收所属网点编码"",""dbColumn"":""F揽收所属网点编码""},{""excelColumn"":""揽收网点省区"",""dbColumn"":""F揽收网点省区""},{""excelColumn"":""揽收网点大区"",""dbColumn"":""F揽收网点大区""},{""excelColumn"":""揽收网点省份"",""dbColumn"":""F揽收网点省份""},{""excelColumn"":""中心名称"",""dbColumn"":""F中心名称""},{""excelColumn"":""客户编码"",""dbColumn"":""F客户编码""},{""excelColumn"":""客户名称"",""dbColumn"":""F客户名称""},{""excelColumn"":""线路类型"",""dbColumn"":""F线路类型""},{""excelColumn"":""原始揽收量"",""dbColumn"":""F原始揽收量""},{""excelColumn"":""总揽收量"",""dbColumn"":""F总揽收量""},{""excelColumn"":""交货平均用时(h)"",""dbColumn"":""F交货平均用时h""},{""excelColumn"":""交货及时量"",""dbColumn"":""F交货及时量""},{""excelColumn"":""交货延误量"",""dbColumn"":""F交货延误量""},{""excelColumn"":""总滞留量"",""dbColumn"":""F总滞留量""},{""excelColumn"":""未交货量"",""dbColumn"":""F未交货量""},{""excelColumn"":""滞留率"",""dbColumn"":""F滞留率""},{""excelColumn"":""目标值"",""dbColumn"":""F目标值""},{""excelColumn"":""揽收超48h总量"",""dbColumn"":""F揽收超48h总量""},{""excelColumn"":""揽收超48h已交货量"",""dbColumn"":""F揽收超48h已交货量""},{""excelColumn"":""揽收超48h未交货量"",""dbColumn"":""F揽收超48h未交货量""},{""excelColumn"":""考核滞留&揽收超48h量"",""dbColumn"":""F考核滞留揽收超48h量""},{""excelColumn"":""揽收超48小时预估考核-日"",""dbColumn"":""F揽收超48小时预估考核日""},{""excelColumn"":""滞留预估考核-日"",""dbColumn"":""F滞留预估考核日""},{""excelColumn"":""考核滞留量"",""dbColumn"":""F考核滞留量""},{""excelColumn"":""白名单标识"",""dbColumn"":""F白名单标识""},{""excelColumn"":""分频次标识"",""dbColumn"":""F分频次标识""}],""keyFields"":[""揽收所属网点编码"",""统计日期""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F揽收所属网点编码"",""F统计日期""],""batchSplit"":{""enabled"":false}}',
        1, N'申通网点交货滞留v3汇总 Excel导入配置（单行表头30列，含括号/连字符/''&'' 列 dbColumn 去除非法字符，sheet sheet1）', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2327（QC_ST_HANDOVER_SUMMARY） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2327)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2327, NULL, 1, GETDATE(), NULL, N'网点质控：申通网点交货滞留v3汇总 导入暂存', GETDATE(), NULL, N'申通交货滞留汇总导入', NULL, N'QC_ST_HANDOVER_SUMMARY', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""网点交货滞留v3汇总*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2327（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2327)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2327, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2327, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5127（ExcelInput 批次级自动节点，插件注册=1，规则=3127） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5127)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5127, 2327, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3127);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }

    private static void MigrateV48(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_拦截汇总 暂存表（系统列 + 10 业务列 + 标准字段） ═══
        // 单行表头，10 列，所属网点×日期粒度（1 行/网点/日期）。文件扩展名为 .xls 实为 xlsx，sheet 名为 ""0""。
        // 列名均合法（无非法字符），dbColumn 直接加 F 前缀。
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_拦截汇总')
        CREATE TABLE [STG申通_拦截汇总] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3128 columnMapping，10 列）
            [F统计日期] NVARCHAR(200) NULL,
            [F所属网点] NVARCHAR(200) NULL,
            [F应拦截网点] NVARCHAR(200) NULL,
            [F应拦截量] NVARCHAR(200) NULL,
            [F拦截成功量] NVARCHAR(200) NULL,
            [F拦截成功率] NVARCHAR(200) NULL,
            [F未拦截成功量] NVARCHAR(200) NULL,
            [F及时转出量] NVARCHAR(200) NULL,
            [F及时转出率] NVARCHAR(200) NULL,
            [F未及时转出量] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_拦截汇总_F批次ID' AND object_id = OBJECT_ID(N'STG申通_拦截汇总'))
        CREATE INDEX [IX_STG申通_拦截汇总_F批次ID] ON [STG申通_拦截汇总]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_拦截汇总_数据作用域' AND object_id = OBJECT_ID(N'STG申通_拦截汇总'))
        CREATE INDEX [IX_STG申通_拦截汇总_数据作用域] ON [STG申通_拦截汇总]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（所属网点 + 统计日期 + 组织，仅未撤销 + 所属网点非空）
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_拦截汇总_网点日期_未撤销' AND object_id = OBJECT_ID(N'STG申通_拦截汇总'))
        CREATE UNIQUE INDEX [UX_STG申通_拦截汇总_网点日期_未撤销]
            ON [STG申通_拦截汇总]([F所属网点],[F统计日期],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F所属网点] IS NOT NULL AND [F所属网点] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3128（拦截汇总导入，全列映射） ═══
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3128)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3128, 192, N'excelInput', N'申通拦截汇总导入规则',
        N'{""targetTable"":""STG申通_拦截汇总"",""outputMode"":""stg"",""headerRow"":1,""sheetName"":""0"",""columnIdentifier"":""所属网点,应拦截网点,应拦截量,拦截成功率,及时转出率"",""fullColumnIdentifier"":""统计日期,所属网点,应拦截网点,应拦截量,拦截成功量,拦截成功率,未拦截成功量,及时转出量,及时转出率,未及时转出量"",""columnMapping"":[{""excelColumn"":""统计日期"",""dbColumn"":""F统计日期""},{""excelColumn"":""所属网点"",""dbColumn"":""F所属网点""},{""excelColumn"":""应拦截网点"",""dbColumn"":""F应拦截网点""},{""excelColumn"":""应拦截量"",""dbColumn"":""F应拦截量""},{""excelColumn"":""拦截成功量"",""dbColumn"":""F拦截成功量""},{""excelColumn"":""拦截成功率"",""dbColumn"":""F拦截成功率""},{""excelColumn"":""未拦截成功量"",""dbColumn"":""F未拦截成功量""},{""excelColumn"":""及时转出量"",""dbColumn"":""F及时转出量""},{""excelColumn"":""及时转出率"",""dbColumn"":""F及时转出率""},{""excelColumn"":""未及时转出量"",""dbColumn"":""F未及时转出量""}],""keyFields"":[""所属网点"",""统计日期""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F所属网点"",""F统计日期""],""batchSplit"":{""enabled"":false}}',
        1, N'申通网点数据报表（拦截）汇总 Excel导入配置（单行表头10列，文件扩展名 .xls 实为 xlsx，sheet 名 0）', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2328（QC_ST_INTERCEPT_SUMMARY） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2328)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2328, NULL, 1, GETDATE(), NULL, N'网点质控：申通网点数据报表（拦截）汇总 导入暂存', GETDATE(), NULL, N'申通拦截汇总导入', NULL, N'QC_ST_INTERCEPT_SUMMARY', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""网点数据报表导出*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2328（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2328)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2328, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2328, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5128（ExcelInput 批次级自动节点，插件注册=1，规则=3128） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5128)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5128, 2328, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3128);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }

    private static void MigrateV49(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ═══ 1. 创建 STG申通_揽收考核汇总 暂存表（系统列 + 19 业务列 + 标准字段） ═══
        // 单行表头，19 列，揽收所属网点×日期粒度（1 行/网点/日期），sheet sheet1。
        // 含末尾空格列：揽收承包区编码（原文末尾带空格）→ dbColumn 去尾空格 F揽收承包区编码；excelColumn 保留原始含空格文本。
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'STG申通_揽收考核汇总')
        CREATE TABLE [STG申通_揽收考核汇总] (
            [FID] BIGINT IDENTITY(1,1) PRIMARY KEY,
            [F批次ID] BIGINT NOT NULL,
            [F原始行号] INT NULL,
            [FOrgId] BIGINT NULL,
            [F账套ID] BIGINT NULL,
            [FDataScopeId] NVARCHAR(64) NULL,
            [FSourceWorkItemId] BIGINT NULL,
            [FIsRevoked] BIT NOT NULL DEFAULT 0,
            [F处理状态] INT NOT NULL DEFAULT 0,
            [F错误信息] NVARCHAR(MAX) NULL,
            [F关联凭证ID] BIGINT NULL,
            [F创建时间] DATETIME NOT NULL DEFAULT GETDATE(),
            -- 业务字段（来自 rule 3129 columnMapping，19 列；揽收承包区编码 列 dbColumn 已 trim 尾空格）
            [F统计日期] NVARCHAR(200) NULL,
            [F电商平台] NVARCHAR(200) NULL,
            [F频次] NVARCHAR(200) NULL,
            [F时效类型] NVARCHAR(200) NULL,
            [F揽收大区] NVARCHAR(200) NULL,
            [F揽收省区] NVARCHAR(200) NULL,
            [F揽收省份] NVARCHAR(200) NULL,
            [F揽收所属网点] NVARCHAR(200) NULL,
            [F揽收所属网点编码] NVARCHAR(200) NULL,
            [F揽收承包区] NVARCHAR(200) NULL,
            [F揽收承包区编码] NVARCHAR(200) NULL,
            [F订单总量] NVARCHAR(200) NULL,
            [F及时揽收量] NVARCHAR(200) NULL,
            [F及时揽收率] NVARCHAR(200) NULL,
            [F未及时揽收量] NVARCHAR(200) NULL,
            [F未及时揽收率] NVARCHAR(200) NULL,
            [F揽收平均用时] NVARCHAR(200) NULL,
            [F先揽后下单量] NVARCHAR(200) NULL,
            [F超15天未揽收量] NVARCHAR(200) NULL,
            -- 标准字段
            [F其他列数据] NVARCHAR(MAX) NULL,
            [F业务主键] NVARCHAR(500) NULL,
            [F流水号] NVARCHAR(200) NULL,
            [F归属网点编号] NVARCHAR(50) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_揽收考核汇总_F批次ID' AND object_id = OBJECT_ID(N'STG申通_揽收考核汇总'))
        CREATE INDEX [IX_STG申通_揽收考核汇总_F批次ID] ON [STG申通_揽收考核汇总]([F批次ID]);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_STG申通_揽收考核汇总_数据作用域' AND object_id = OBJECT_ID(N'STG申通_揽收考核汇总'))
        CREATE INDEX [IX_STG申通_揽收考核汇总_数据作用域] ON [STG申通_揽收考核汇总]([FDataScopeId]) WHERE [FDataScopeId] IS NOT NULL;

        -- 跨批次去重唯一索引（揽收所属网点编码 + 统计日期 + 组织，仅未撤销 + 揽收所属网点编码非空）
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_STG申通_揽收考核汇总_网点日期_未撤销' AND object_id = OBJECT_ID(N'STG申通_揽收考核汇总'))
        CREATE UNIQUE INDEX [UX_STG申通_揽收考核汇总_网点日期_未撤销]
            ON [STG申通_揽收考核汇总]([F揽收所属网点编码],[F统计日期],[FOrgId])
            WHERE [FIsRevoked] = 0 AND [F揽收所属网点编码] IS NOT NULL AND [F揽收所属网点编码] != '';
        ");

        // ═══ 2. CfPluginRule: ExcelInput 规则 3129（揽收考核汇总导入，全列映射） ═══
        // excelColumn ""揽收承包区编码 "" 末尾保留原始空格（与文件首行逐字一致，否则匹配失败）；dbColumn 去尾空格 F揽收承包区编码。
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [CF自动插件_规则] ON;

        IF NOT EXISTS (SELECT 1 FROM [CF自动插件_规则] WHERE [FID] = 3129)
        INSERT INTO [CF自动插件_规则] ([FID], [F组织ID], [F类型编码], [F规则名称], [F规则配置JSON], [F状态], [F说明], [F并发戳], [F创建时间])
        VALUES (3129, 192, N'excelInput', N'申通揽收考核汇总导入规则',
        N'{""targetTable"":""STG申通_揽收考核汇总"",""outputMode"":""stg"",""headerRow"":1,""sheetName"":""sheet1"",""columnIdentifier"":""揽收所属网点编码,订单总量,及时揽收率,未及时揽收量"",""fullColumnIdentifier"":""统计日期,电商平台,频次,时效类型,揽收大区,揽收省区,揽收省份,揽收所属网点,揽收所属网点编码,揽收承包区,揽收承包区编码 ,订单总量,及时揽收量,及时揽收率,未及时揽收量,未及时揽收率,揽收平均用时,先揽后下单量,超15天未揽收量"",""columnMapping"":[{""excelColumn"":""统计日期"",""dbColumn"":""F统计日期""},{""excelColumn"":""电商平台"",""dbColumn"":""F电商平台""},{""excelColumn"":""频次"",""dbColumn"":""F频次""},{""excelColumn"":""时效类型"",""dbColumn"":""F时效类型""},{""excelColumn"":""揽收大区"",""dbColumn"":""F揽收大区""},{""excelColumn"":""揽收省区"",""dbColumn"":""F揽收省区""},{""excelColumn"":""揽收省份"",""dbColumn"":""F揽收省份""},{""excelColumn"":""揽收所属网点"",""dbColumn"":""F揽收所属网点""},{""excelColumn"":""揽收所属网点编码"",""dbColumn"":""F揽收所属网点编码""},{""excelColumn"":""揽收承包区"",""dbColumn"":""F揽收承包区""},{""excelColumn"":""揽收承包区编码 "",""dbColumn"":""F揽收承包区编码""},{""excelColumn"":""订单总量"",""dbColumn"":""F订单总量""},{""excelColumn"":""及时揽收量"",""dbColumn"":""F及时揽收量""},{""excelColumn"":""及时揽收率"",""dbColumn"":""F及时揽收率""},{""excelColumn"":""未及时揽收量"",""dbColumn"":""F未及时揽收量""},{""excelColumn"":""未及时揽收率"",""dbColumn"":""F未及时揽收率""},{""excelColumn"":""揽收平均用时"",""dbColumn"":""F揽收平均用时""},{""excelColumn"":""先揽后下单量"",""dbColumn"":""F先揽后下单量""},{""excelColumn"":""超15天未揽收量"",""dbColumn"":""F超15天未揽收量""}],""keyFields"":[""揽收所属网点编码"",""统计日期""],""totalRowDetection"":{""enabled"":true,""containsKeywords"":[""合计"",""总计""],""emptyFields"":[]},""crossBatchDedupEnabled"":true,""crossBatchDedupFields"":[""F揽收所属网点编码"",""F统计日期""],""batchSplit"":{""enabled"":false}}',
        1, N'申通订单揽收考核汇总分析 Excel导入配置（单行表头19列，含末尾空格列 揽收承包区编码 ，excelColumn 保留原始空格 dbColumn trim，sheet sheet1）', REPLACE(NEWID(),'-',''), GETDATE());

        SET IDENTITY_INSERT [CF自动插件_规则] OFF;
        ");

        // ═══ 3. CfFlowDefinition: 流程 2329（QC_ST_PICKUP_ASSESS） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF卡片流程] WHERE [FID] = 2329)
        BEGIN
            SET IDENTITY_INSERT [CF卡片流程] ON;
            INSERT INTO [CF卡片流程] ([FID], [F乐观锁], [F创建人ID], [F创建时间], [F可发起角色JSON], [F描述], [F更新时间], [F标题模板], [F流程名称], [F流程组ID], [F流程编码], [F状态], [F组织ID], [F编号模板], [F触发配置JSON], [F账套ID], [F匹配规则])
            VALUES (2329, NULL, 1, GETDATE(), NULL, N'网点质控：申通订单揽收考核汇总分析 导入暂存', GETDATE(), NULL, N'申通揽收考核汇总导入', NULL, N'QC_ST_PICKUP_ASSESS', N'published', 192, NULL, N'{""type"":""fileUpload""}', NULL, N'{""fileNamePattern"":""订单揽收考核汇总*""}');
            SET IDENTITY_INSERT [CF卡片流程] OFF;
        END
        ");

        // ═══ 4. CfFlowVersion: 版本 2329（当前版本，published） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [FID] = 2329)
        BEGIN
            SET IDENTITY_INSERT [CF流程版本] ON;
            INSERT INTO [CF流程版本] ([FID], [F创建人ID], [F创建时间], [F卡片SchemaJSON], [F发布时间], [F明细SchemaJSON], [F是否当前版本], [F流程定义ID], [F流程设置JSON], [F版本号], [F状态])
            VALUES (2329, 1, GETDATE(), NULL, GETDATE(), NULL, 1, 2329, NULL, 1, N'published');
            SET IDENTITY_INSERT [CF流程版本] OFF;
        END
        ");

        // ═══ 5. CfStageDefinition: 首节点 5129（ExcelInput 批次级自动节点，插件注册=1，规则=3129） ═══
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [FID] = 5129)
        BEGIN
            SET IDENTITY_INSERT [CF流程节点] ON;
            INSERT INTO [CF流程节点] ([FID], [F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式], [F插件注册ID], [F插件规则ID])
            VALUES (5129, 2329, 1, N'Excel导入解析', N'auto', N'batch', N'single', 1, 3129);
            SET IDENTITY_INSERT [CF流程节点] OFF;
        END
        ");
    }

}
