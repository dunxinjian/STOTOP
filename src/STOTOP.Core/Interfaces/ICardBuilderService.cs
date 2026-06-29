using STOTOP.Core.Models;

namespace STOTOP.Core.Interfaces;

/// <summary>
/// 卡片构建服务接口 - 将业务数据转换为 CardPayload 结构
/// </summary>
public interface ICardBuilderService
{
    /// <summary>
    /// 构建流程选择器卡片 - 用户选择要发起的流程类型
    /// </summary>
    /// <param name="processConfigs">可用的流程配置列表（动态类型，由实现方解释）</param>
    /// <returns>流程选择器卡片</returns>
    CardPayload BuildProcessSelectorCard(IEnumerable<object> processConfigs);

    /// <summary>
    /// 构建表单输入卡片 - 根据流程类型显示相应的输入表单
    /// </summary>
    /// <param name="processConfig">流程配置（动态类型）</param>
    /// <param name="approvalNodes">审批节点配置列表（动态类型）</param>
    /// <returns>表单输入卡片</returns>
    CardPayload BuildFormCard(object processConfig, IEnumerable<object>? approvalNodes = null);

    /// <summary>
    /// 构建审批待办卡片 - 显示待审批信息供审批人操作
    /// </summary>
    /// <param name="task">审批任务（动态类型）</param>
    /// <param name="businessDocument">业务单据对象（动态类型）</param>
    /// <param name="processInstance">流程实例（动态类型）</param>
    /// <returns>审批待办卡片</returns>
    CardPayload BuildApprovalCard(object task, object businessDocument, object processInstance);

    /// <summary>
    /// 构建流程进度卡片 - 显示流程各节点的审批进度
    /// </summary>
    /// <param name="processInstance">流程实例（动态类型）</param>
    /// <param name="tasks">所有审批任务列表（动态类型）</param>
    /// <returns>进度时间线卡片</returns>
    CardPayload BuildProgressCard(object processInstance, IEnumerable<object> tasks);

    /// <summary>
    /// 构建流程结果卡片 - 显示流程最终结果
    /// </summary>
    /// <param name="processInstance">流程实例（动态类型）</param>
    /// <param name="result">结果描述（通过/驳回等）</param>
    /// <returns>流程结果卡片</returns>
    CardPayload BuildResultCard(object processInstance, string result);

    /// <summary>
    /// 构建信息卡片 - 显示一般信息
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="content">内容</param>
    /// <param name="facts">事实项列表</param>
    /// <returns>信息卡片</returns>
    CardPayload BuildInfoCard(string title, string content, List<FactItem>? facts = null);

    /// <summary>
    /// 构建告警操作卡片 - 用于质量异常、系统告警等需要用户响应的场景
    /// </summary>
    /// <param name="alertType">告警类型</param>
    /// <param name="title">标题</param>
    /// <param name="description">描述</param>
    /// <param name="facts">事实项列表</param>
    /// <param name="actions">可用操作列表</param>
    /// <returns>告警操作卡片</returns>
    CardPayload BuildAlertActionCard(string alertType, string title, string description, List<FactItem>? facts = null, List<CardAction>? actions = null);

    /// <summary>
    /// 构建数据摘要卡片 - 用于数据导入、报表生成等场景的结果展示
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="summaryData">摘要数据</param>
    /// <param name="details">详细信息</param>
    /// <returns>数据摘要卡片</returns>
    CardPayload BuildDataSummaryCard(string title, Dictionary<string, object> summaryData, List<FactItem>? details = null);

    /// <summary>
    /// 构建提醒卡片 - 用于合同到期、任务到期等提醒场景
    /// </summary>
    /// <param name="reminderType">提醒类型</param>
    /// <param name="title">标题</param>
    /// <param name="dueDate">到期时间</param>
    /// <param name="facts">事实项列表</param>
    /// <param name="actions">可用操作列表</param>
    /// <returns>提醒卡片</returns>
    CardPayload BuildReminderCard(string reminderType, string title, DateTime dueDate, List<FactItem>? facts = null, List<CardAction>? actions = null);
}
