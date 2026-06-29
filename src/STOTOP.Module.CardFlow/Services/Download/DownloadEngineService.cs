using STOTOP.Module.CardFlow.AutoPlugin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Import;

namespace STOTOP.Module.CardFlow.Services.Download;

using STOTOP.Module.CardFlow.Services;

/// <summary>
/// 自动下载引擎 — Playwright 自动化执行
/// </summary>
public class DownloadEngineService
{
    private readonly STOTOPDbContext _context;
    private readonly IProgressNotifier _progressNotifier;
    private readonly IOrgContextAccessor _orgContextAccessor;
    private readonly ILogger<DownloadEngineService> _logger;

    public DownloadEngineService(
        STOTOPDbContext context,
        IProgressNotifier progressNotifier,
        IOrgContextAccessor orgContextAccessor,
        ILogger<DownloadEngineService> logger)
    {
        _context = context;
        _progressNotifier = progressNotifier;
        _orgContextAccessor = orgContextAccessor;
        _logger = logger;
    }

    /// <summary>
    /// 执行下载任务
    /// </summary>
    public async Task ExecuteDownloadAsync(long taskId, CancellationToken ct = default)
    {
        // 1. 加载任务配置
        var task = await _context.Set<CfDownloadTask>()
            .FirstOrDefaultAsync(t => t.FID == taskId, ct);

        if (task == null)
        {
            _logger.LogWarning("下载任务 {TaskId} 不存在", taskId);
            return;
        }

        // 加载步骤列表
        var steps = await _context.Set<CfDownloadStep>()
            .Where(s => s.FTaskId == taskId)
            .OrderBy(s => s.FSortOrder)
            .ToListAsync(ct);

        // 2. 创建下载日志（状态=执行中）
        var log = new CfDownloadLog
        {
            FTaskId = taskId,
            FStartTime = DateTime.Now,
            FStatus = 1, // 执行中
            FDownloadFileCount = 0
        };
        _context.Set<CfDownloadLog>().Add(log);
        await _context.SaveChangesAsync(ct);

        IPlaywright? playwright = null;
        IBrowser? browser = null;
        IBrowserContext? browserContext = null;
        IPage? page = null;

        try
        {
            _logger.LogInformation("开始执行下载任务 [{TaskName}]，共 {StepCount} 个步骤",
                task.FTaskName, steps.Count);

            // 3. 启动 Playwright 浏览器
            playwright = await Playwright.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[] { "--no-sandbox", "--disable-dev-shm-usage" }
            });
            browserContext = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                AcceptDownloads = true
            });
            page = await browserContext.NewPageAsync();
            page.SetDefaultTimeout(30000);

            var downloadedFiles = new List<string>();

            // 4. 按步骤顺序执行
            foreach (var step in steps)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    await ExecuteStepAsync(page, step, task.FStoragePath, downloadedFiles, ct);
                    _logger.LogInformation("步骤 {Order} [{Type}] 执行完成: {Desc}",
                        step.FSortOrder, step.FActionType, step.FDescription);

                    // 通知下载进度
                    var stepIndex = steps.IndexOf(step);
                    await _progressNotifier.NotifyDownloadProgressAsync(taskId, stepIndex + 1, steps.Count, step.FDescription ?? step.FActionType);
                }
                catch (Exception stepEx)
                {
                    _logger.LogError(stepEx, "步骤 {Order} [{Type}] 执行失败: {Selector}",
                        step.FSortOrder, step.FActionType, step.FSelector);
                    throw new Exception(
                        $"步骤 {step.FSortOrder} ({step.FActionType}) 执行失败: {stepEx.Message}", stepEx);
                }
            }

            // 更新日志中的下载文件数和路径
            log.FDownloadFileCount = downloadedFiles.Count;
            log.FFilePathList = string.Join(";", downloadedFiles);

            // 5. 下载完成后：自动导入已下载的文件
            // TODO: ImportService 已删除，待基于 IBatchTriggerService 重写自动导入逻辑
            if (downloadedFiles.Count > 0)
            {
                var targetOrgId = _orgContextAccessor.CurrentOrgId ?? 0;
                foreach (var filePath in downloadedFiles)
                {
                    try
                    {
                        _logger.LogInformation("自动导入文件（功能待重建）: {FilePath}", filePath);
                        // TODO: 待基于 IBatchTriggerService 重写
                        // await batchTriggerService.TriggerByFileUploadAsync(...)
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "导入文件失败: {FilePath}", filePath);
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(task.FStoragePath) && Directory.Exists(task.FStoragePath))
            {
                // 回退：如果没有通过 download 步骤下载文件，扫描存储目录
                var files = Directory.GetFiles(task.FStoragePath, "*.xls*")
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .Take(10)
                    .ToList();

                var fallbackOrgId = _orgContextAccessor.CurrentOrgId ?? 0;
                foreach (var filePath in files)
                {
                    try
                    {
                        _logger.LogInformation("自动导入文件（功能待重建）: {FilePath}", filePath);
                        // TODO: 待基于 IBatchTriggerService 重写
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "导入文件失败: {FilePath}", filePath);
                    }
                }
            }
            else
            {
                _logger.LogWarning("无下载文件且存储路径未配置或不存在: {Path}，跳过自动导入", task.FStoragePath);
            }

            // 6. 更新日志（状态=成功）
            log.FStatus = 2; // 成功
            log.FEndTime = DateTime.Now;
            _logger.LogInformation("下载任务 [{TaskName}] 执行成功", task.FTaskName);
        }
        catch (OperationCanceledException)
        {
            log.FStatus = 3; // 失败
            log.FEndTime = DateTime.Now;
            log.FErrorMessage = "任务被取消";
            _logger.LogWarning("下载任务 [{TaskName}] 被取消", task.FTaskName);
        }
        catch (Exception ex)
        {
            log.FStatus = 3; // 失败
            log.FEndTime = DateTime.Now;
            log.FErrorMessage = ex.Message;
            _logger.LogError(ex, "下载任务 [{TaskName}] 执行失败", task.FTaskName);
        }
        finally
        {
            // 清理 Playwright 资源
            if (page != null) await page.CloseAsync();
            if (browserContext != null) await browserContext.CloseAsync();
            if (browser != null) await browser.CloseAsync();
            playwright?.Dispose();

            _context.Set<CfDownloadLog>().Update(log);
            await _context.SaveChangesAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// 执行单个步骤 — 真实 Playwright 浏览器操作
    /// </summary>
    private async Task ExecuteStepAsync(IPage page, CfDownloadStep step,
        string? storagePath, List<string> downloadedFiles, CancellationToken ct)
    {
        switch (step.FActionType.ToLower())
        {
            case "navigate":
                await page.GotoAsync(step.FValue!, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = step.FWaitTime > 0 ? step.FWaitTime.Value : 30000
                });
                break;

            case "fill":
                await page.WaitForSelectorAsync(step.FSelector!);
                await page.FillAsync(step.FSelector!, step.FValue ?? "");
                break;

            case "click":
                await page.WaitForSelectorAsync(step.FSelector!);
                await page.ClickAsync(step.FSelector!);
                break;

            case "select":
                await page.WaitForSelectorAsync(step.FSelector!);
                await page.SelectOptionAsync(step.FSelector!, step.FValue ?? "");
                break;

            case "wait":
                var waitMs = step.FWaitTime ?? 1000;
                await Task.Delay(waitMs, ct);
                break;

            case "download":
                // 监听下载事件
                var downloadTask = page.WaitForDownloadAsync();
                if (!string.IsNullOrWhiteSpace(step.FSelector))
                {
                    await page.ClickAsync(step.FSelector);
                }
                var download = await downloadTask;

                // 保存文件到指定路径
                var targetDir = storagePath ?? Path.Combine(Path.GetTempPath(), "stotop-downloads");
                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                var fileName = download.SuggestedFilename;
                var filePath = Path.Combine(targetDir, fileName);
                await download.SaveAsAsync(filePath);
                downloadedFiles.Add(filePath);

                _logger.LogInformation("文件已下载: {FilePath}", filePath);
                break;

            default:
                _logger.LogWarning("未知操作类型: {ActionType}", step.FActionType);
                break;
        }

        // 步骤间延迟（download 和 wait 类型除外，因为它们已有内置等待）
        if (step.FActionType.ToLower() != "wait" && step.FActionType.ToLower() != "download"
            && step.FWaitTime.HasValue && step.FWaitTime > 0)
        {
            await Task.Delay(step.FWaitTime.Value, ct);
        }
    }
}
