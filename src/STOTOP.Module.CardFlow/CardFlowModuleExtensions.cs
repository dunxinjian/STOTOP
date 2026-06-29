using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using STOTOP.Core.Interfaces;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.AutoPlugin.Implementations;
using STOTOP.Module.CardFlow.Jobs;
using STOTOP.Module.CardFlow.Models;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.CardFlow.Services.Classification;
using STOTOP.Module.CardFlow.Services.Dispatch;
using STOTOP.Module.CardFlow.Services.DispatchRule;
using STOTOP.Module.CardFlow.Services.Download;
using STOTOP.Module.CardFlow.Services.FileManager;
using STOTOP.Module.CardFlow.Services.Handlers;
using STOTOP.Module.CardFlow.Services.Import;
using STOTOP.Module.CardFlow.Services.Import.TransformEngine;
using STOTOP.Module.CardFlow.Services.Interfaces;
using STOTOP.Module.CardFlow.Services.Quality;
using STOTOP.Module.CardFlow.Services.Staging;
using STOTOP.Module.CardFlow.Services.Validation;
using STOTOP.Module.Finance.Services.Interfaces;
using STOTOP.Module.System.Services;
using STOTOP.Infrastructure.Services;

namespace STOTOP.Module.CardFlow;

public static class CardFlowModuleExtensions
{
    public static IServiceCollection AddCardFlowModule(this IServiceCollection services, IConfiguration? configuration = null)
    {
        // IBudgetOccupationService is registered by the Finance module and consumed by FlowEngineService.
        // 核心引擎
        services.AddScoped<IFlowEngineService, FlowEngineService>();
        services.AddScoped<IConditionRuleEvaluator, ConditionRuleEvaluator>();
        services.AddScoped<IConditionEvaluationContextBuilder, ConditionEvaluationContextBuilder>();
        services.AddScoped<IStageRouteResolver, StageRouteResolver>();
        services.AddScoped<IDynamicStagePolicyResolver, DynamicStagePolicyResolver>();
        services.AddScoped<IAuditSnapshotPolicyService, AuditSnapshotPolicyService>();
        services.AddScoped<ICardFlowPathPreviewService, CardFlowPathPreviewService>();
        services.AddScoped<ICardFlowSourceContextVerifier, CardFlowSourceContextVerifier>();
        services.AddScoped<IConditionEvaluator, ConditionEvaluator>();
        services.AddScoped<IApprovalModeHandler, ApprovalModeHandler>();
        services.AddScoped<IStageConfigParser, StageConfigParser>();
        services.AddScoped<ICardPresentationResolver, CardPresentationResolver>();
        services.AddScoped<IStageViewProfileResolver, StageViewProfileResolver>();
        services.AddScoped<IApproverResolver, ApproverResolver>();
        services.AddScoped<IStageFieldAccessService, StageFieldAccessService>();
        services.AddScoped<IStageActionPolicyService, StageActionPolicyService>();
        services.AddScoped<SequentialApprovalRuntime>();
        services.AddScoped<ReturnToStageRuntime>();
        services.AddScoped<INumberSequenceService, NumberSequenceService>();
        services.AddScoped<ICardSchemaService, CardSchemaService>();
        services.AddScoped<Services.Redaction.ICardRedactionService, Services.Redaction.CardRedactionService>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

        // 通知渠道
        services.AddScoped<INotificationChannel, DingTalkChannel>();
        services.AddHttpClient();

        // 定时任务
        services.AddScoped<CardFlowTimeoutJob>();
        services.AddScoped<PushRetryJob>();

        // CRUD 服务
        services.AddScoped<IFlowDefinitionService, FlowDefinitionService>();
        services.AddScoped<ICardService, CardService>();
        services.AddScoped<ITodoService, TodoService>();
        services.AddScoped<IDelegationService, DelegationService>();
        services.AddScoped<IFlowGroupService, FlowGroupService>();
        services.AddScoped<ICardRelationService, CardRelationService>();
        services.AddScoped<INotificationSettingsService, NotificationSettingsService>();

        // 卡片流程编排中心（Task #2 核心引擎）
        services.AddScoped<OrchestrationEngineService>();
        services.AddScoped<AdHocDispatchService>();

        // 批次触发与后台处理（无界 Channel + Hosted 服务）
        services.AddSingleton<IExcelParserService, STOTOP.Infrastructure.Services.ExcelParserService>();
        services.AddSingleton(_ => Channel.CreateUnbounded<BatchJob>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
            }));
        services.AddScoped<IBatchTriggerService, BatchTriggerService>();
        services.AddScoped<IBatchLifecycleService, BatchLifecycleService>();
        services.AddHostedService<BatchJobProcessorService>();

        // 推送基础设施（Task #10）
        services.AddScoped<IBatchNotifier, BatchNotifier>();

        // 质量问题派发服务（Express PricingPlugin 依赖）
        services.AddScoped<IQualityDispatchService, Services.QualityDispatchService>();
        services.AddScoped<IProcessingIssueService, ProcessingIssueService>();

        // ===== 从 DataCenter 迁移的服务注册 =====
        if (configuration != null)
        {
            services.Configure<SystemServiceAccountOptions>(
                configuration.GetSection(SystemServiceAccountOptions.SectionName));
        }

        // 批量写入服务
        var provider = DbConnectionsHelper.GetProvider();
        if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IBulkInsertService, SqlBulkInsertService>();
        else
            services.AddScoped<IBulkInsertService, EfCoreBulkInsertService>();

        // 内存缓存（SecureFileUploadValidator 依赖）
        services.AddMemoryCache();
        services.AddScoped<SecureFileUploadValidator>();

        // 导入服务（仅保留 RetryBatchAsync）
        services.AddScoped<IImportService, ImportService>();


        // 文件管理服务
        services.AddScoped<FileManagerService>();

        // 暂存表数据管理
        services.AddScoped<StagingService>();

        // 审计追溯
        services.AddScoped<AuditTrailService>();

        // 批次撤销处理器
        services.AddScoped<BatchRevokeHandler>();

        // 孤立数据定期监控 Job
        services.AddScoped<Jobs.OrphanedDataMonitorJob>();

        // 暂存表撤销处理器
        services.AddScoped<StagingRevokeHandler>();
        services.AddScoped<IDataScopeRevokeHandler>(sp => sp.GetRequiredService<StagingRevokeHandler>());

        // 转换引擎
        services.AddScoped<ITransformEngine, JintTransformEngine>();

        // 自动下载
        services.AddScoped<DownloadEngineService>();
        services.AddScoped<DownloadJobService>();
        services.AddScoped<IDownloadTaskService, DownloadTaskService>();

        // 质量规则引擎
        services.AddScoped<IQualityRuleEngine, QualityRuleEngine>();

        // 分类分析引擎
        services.AddScoped<ClassificationEngine>();

        // 派发规则服务
        services.AddScoped<DispatchRuleService>();

        // 分类处理器 Handler
        services.AddTransient<AlertNotifyHandler>();
        services.AddTransient<InfoRecordHandler>();
        services.AddTransient<WorkTaskHandler>();
        services.AddTransient<AutoVoucherHandler>();

        // Handler 工厂
        services.AddSingleton<ClassificationHandlerFactory>(_ =>
        {
            var factory = new ClassificationHandlerFactory();
            factory.Register<AlertNotifyHandler>("AlertNotify");
            factory.Register<InfoRecordHandler>("InfoRecord");
            factory.Register<WorkTaskHandler>("WorkTask");
            factory.Register<AutoVoucherHandler>("AutoVoucher");
            return factory;
        });

        // 凭证生成记录服务
        services.AddScoped<VoucherGenerationService>();

        // 导入计算验证工作台
        services.AddScoped<IImportCalculationValidationService, ImportCalculationValidationService>();
        services.AddScoped<VoucherValidationAnalyzer>();
        services.AddScoped<PricingValidationAnalyzer>();
        services.AddScoped<CostValidationAnalyzer>();
        services.AddScoped<ValidationAttributionClassifier>();
        services.AddScoped<VoucherExplainService>();

        // 异常派发服务
        services.AddScoped<IDispatchService, DispatchService>();


        // 质量问题类型注册器
        services.AddHostedService<QualityIssueTypeRegistrar>();

        // ===== AutoPlugin 前置依赖注册 =====
        services.AddScoped<Services.Import.ExcelParserService>();
        services.AddScoped<IPluginProgressReporter, PluginProgressReporter>();

        // ===== AutoPlugin 插件架构注册 =====

        // AutoPluginFactory（新插件架构）
        services.AddSingleton<AutoPluginFactory>(sp =>
        {
            var factory = new AutoPluginFactory(sp);
            factory.Register<ExcelInputPlugin>("ExcelInput");
            factory.Register<SecurityCheckPlugin>("SecurityCheck");
            factory.Register<QualityAnalysisPlugin>("QualityAnalysis");
            factory.Register<ClassificationPlugin>("Classification");
            factory.Register<AutoVoucherPlugin>("AutoVoucher");
            factory.Register<WorkTaskPlugin>("WorkTask");
            factory.Register<AlertNotifyPlugin>("AlertNotify");
            factory.Register<InfoRecordPlugin>("InfoRecord");
            factory.Register<VoucherMigrationPlugin>("VoucherMigration");
            factory.Register<FanOutPlugin>("FanOut");
            factory.Register<BatchSummaryPlugin>("BatchSummary");
            return factory;
        });

        // AutoPlugin 插件实现类 DI 注册
        services.AddScoped<ExcelInputPlugin>();
        services.AddScoped<SecurityCheckPlugin>();
        services.AddScoped<QualityAnalysisPlugin>();
        services.AddScoped<ClassificationPlugin>();
        services.AddScoped<AutoVoucherPlugin>();
        services.AddScoped<WorkTaskPlugin>();
        services.AddScoped<AlertNotifyPlugin>();
        services.AddScoped<InfoRecordPlugin>();
        services.AddScoped<VoucherMigrationPlugin>();
        services.AddScoped<FanOutPlugin>();
        services.AddScoped<BatchSummaryPlugin>();

        return services;
    }

}
