using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Contract.Entities;
using STOTOP.Module.Contract.Events;

namespace STOTOP.Module.Contract.Jobs;

/// <summary>
/// 合同到期自动提醒 Hangfire Job - 每日执行一次
/// 1. 检查即将到期的合同（30天/7天/1天），自动生成到期提醒
/// 2. 将已过期且状态仍为"已生效"的合同自动更新为"已到期"
/// </summary>
[AutomaticRetry(Attempts = 3)]
public class ContractExpiryReminderJob
{
    private readonly IRepository<ConContract> _contractRepo;
    private readonly IRepository<ConContractReminder> _reminderRepo;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly ILogger<ContractExpiryReminderJob> _logger;

    /// <summary>
    /// 提醒间隔天数（可配置）
    /// </summary>
    private static readonly int[] ReminderDays = [30, 7, 1];

    /// <summary>
    /// 合同状态: 已生效=3, 已到期=4
    /// </summary>
    private const int StatusActive = 3;
    private const int StatusExpired = 4;

    /// <summary>
    /// 提醒类型: 1=到期
    /// </summary>
    private const int ReminderTypeExpiry = 1;

    public ContractExpiryReminderJob(
        IRepository<ConContract> contractRepo,
        IRepository<ConContractReminder> reminderRepo,
        IEventDispatcher eventDispatcher,
        ILogger<ContractExpiryReminderJob> logger)
    {
        _contractRepo = contractRepo;
        _reminderRepo = reminderRepo;
        _eventDispatcher = eventDispatcher;
        _logger = logger;
    }

    /// <summary>
    /// Hangfire RecurringJob 入口 - 每日凌晨执行
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("合同到期提醒Job开始执行...");

        try
        {
            var today = DateTime.Today;

            // 1. 为即将到期的合同生成提醒
            await GenerateExpiryRemindersAsync(today);

            // 2. 自动更新已过期合同状态
            await UpdateExpiredContractsAsync(today);

            _logger.LogInformation("合同到期提醒Job执行完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "合同到期提醒Job执行失败");
            throw; // 抛出让Hangfire自动重试
        }
    }

    /// <summary>
    /// 为即将到期的合同生成提醒记录（30天/7天/1天）
    /// </summary>
    private async Task GenerateExpiryRemindersAsync(DateTime today)
    {
        // 查询所有"已生效"且有结束日期的合同
        var activeContracts = await _contractRepo.Query()
            .Include(c => c.Parties)
            .Where(c => c.FStatus == StatusActive && c.FEndDate != null)
            .ToListAsync();

        var createdCount = 0;

        foreach (var contract in activeContracts)
        {
            var endDate = contract.FEndDate!.Value;
            var daysUntilExpiry = (endDate - today).Days;

            foreach (var reminderDay in ReminderDays)
            {
                if (daysUntilExpiry != reminderDay) continue;

                // 检查是否已存在同一合同、同一提醒日期、同一类型的提醒（避免重复）
                var alreadyExists = await _reminderRepo.Query()
                    .AnyAsync(r =>
                        r.FContractId == contract.FID &&
                        r.FReminderType == ReminderTypeExpiry &&
                        r.FReminderDate == today);

                if (alreadyExists)
                {
                    _logger.LogDebug("合同 {ContractNo} 今日到期提醒已存在，跳过", contract.FContractNo);
                    continue;
                }

                // 确定接收人：优先取甲方（FPartyRole=1）关联业务ID，否则用合同创建人占位0
                var recipientId = contract.Parties
                    .Where(p => p.FPartyRole == 1 && p.FRelatedBusinessId.HasValue)
                    .Select(p => p.FRelatedBusinessId!.Value)
                    .FirstOrDefault();

                var reminder = new ConContractReminder
                {
                    FContractId = contract.FID,
                    FReminderType = ReminderTypeExpiry,
                    FReminderDate = today,
                    FRecipientId = recipientId,
                    FIsHandled = false,
                    FRemark = $"合同「{contract.FTitle}」（{contract.FContractNo}）将于{reminderDay}天后到期（{endDate:yyyy-MM-dd}）",
                    FCreatorName = "系统自动",
                    FCreatedTime = DateTime.Now
                };

                await _reminderRepo.AddAsync(reminder);
                createdCount++;

                _logger.LogInformation(
                    "已为合同 {ContractNo} 创建{Days}天到期提醒",
                    contract.FContractNo, reminderDay);

                // 发布合同即将到期事件
                try
                {
                    await _eventDispatcher.PublishAsync(new ContractExpiringEvent
                    {
                        ContractId = contract.FID,
                        ContractNo = contract.FContractNo ?? "",
                        ExpiryDate = endDate,
                        RecipientId = recipientId,
                        DaysRemaining = daysUntilExpiry,
                        TriggeredByUserId = 0, // 系统触发
                        ModuleCode = "contract"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "发布合同到期事件失败，ContractId={ContractId}", contract.FID);
                }
            }
        }

        if (createdCount > 0)
        {
            _logger.LogInformation("本次共创建 {Count} 条到期提醒", createdCount);
        }
    }

    /// <summary>
    /// 将已过期但状态仍为"已生效"的合同自动更新为"已到期"
    /// </summary>
    private async Task UpdateExpiredContractsAsync(DateTime today)
    {
        var expiredContracts = await _contractRepo.Query()
            .Include(c => c.Parties)
            .AsTracking()
            .Where(c => c.FStatus == StatusActive
                        && c.FEndDate != null
                        && c.FEndDate < today)
            .ToListAsync();

        if (expiredContracts.Count == 0) return;

        foreach (var contract in expiredContracts)
        {
            contract.FStatus = StatusExpired;
            contract.FUpdaterName = "系统自动";
            contract.FUpdatedTime = DateTime.Now;
            await _contractRepo.UpdateAsync(contract);
        
            _logger.LogInformation(
                "合同 {ContractNo} 已过期，状态已自动更新为「已到期」",
                contract.FContractNo);
        
            // 发布合同已过期事件
            try
            {
                // 获取负责人ID：优先取甲方关联业务ID
                var responsibleUserId = contract.Parties
                    .Where(p => p.FPartyRole == 1 && p.FRelatedBusinessId.HasValue)
                    .Select(p => p.FRelatedBusinessId!.Value)
                    .FirstOrDefault();
        
                await _eventDispatcher.PublishAsync(new ContractExpiredEvent
                {
                    ContractId = contract.FID,
                    ContractNo = contract.FContractNo ?? "",
                    ExpiryDate = contract.FEndDate!.Value,
                    ResponsibleUserId = responsibleUserId,
                    TriggeredByUserId = 0, // 系统触发
                    ModuleCode = "contract"
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "发布合同过期事件失败，ContractId={ContractId}", contract.FID);
            }
        }

        _logger.LogInformation("本次共更新 {Count} 个过期合同状态", expiredContracts.Count);
    }
}
