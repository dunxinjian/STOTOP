using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using STOTOP.Module.CardFlow.Services.Import;

namespace STOTOP.Module.CardFlow.AutoPlugin.Implementations;

/// <summary>安全检查插件 - 验证文件安全性（可选，可在管道中灵活配置）</summary>
public class SecurityCheckPlugin : BatchPluginBase
{
    private readonly SecureFileUploadValidator _validator;
    private readonly IPluginProgressReporter _progressReporter;
    private readonly ILogger<SecurityCheckPlugin> _logger;

    public SecurityCheckPlugin(
        SecureFileUploadValidator validator,
        IPluginProgressReporter progressReporter,
        ILogger<SecurityCheckPlugin> logger)
    {
        _validator = validator;
        _progressReporter = progressReporter;
        _logger = logger;
    }

    public override string PluginName => "SecurityCheck";
    public override string DisplayName => "安全检查";

    public override async Task<PluginResult> ExecuteAsync(PluginContext context)
    {
        const int totalSteps = 2;
        var db = context.Services.GetRequiredService<STOTOP.Infrastructure.Data.STOTOPDbContext>();

        // Step 1: 文件格式检查
        await _progressReporter.ReportProgressAsync(context.BatchId, 0, totalSteps, "文件格式检查");

        // 从 CfPluginRule 加载配置，获取文件信息
        var (fileName, fileSize, fileStream) = await ResolveFileInfoAsync(context, db);

        if (fileStream == null)
        {
            return PluginResult.Fail("文件流为空，无法进行安全验证");
        }

        await _progressReporter.ReportProgressAsync(context.BatchId, 1, totalSteps, "文件格式检查");

        // Step 2: 安全扫描
        await _progressReporter.ReportProgressAsync(context.BatchId, 1, totalSteps, "安全扫描");

        var validationResult = await _validator.ValidateAsync(fileName, fileSize, fileStream);

        if (!validationResult.IsValid)
        {
            return PluginResult.Fail(string.Join("; ", validationResult.Errors));
        }

        await _progressReporter.ReportProgressAsync(context.BatchId, 2, totalSteps, "安全扫描");

        // 将警告信息记录到日志（原 SharedData["SecurityWarnings"] 已废弃，改用日志）
        if (validationResult.Warnings.Count > 0)
        {
            _logger.LogWarning("SecurityCheckPlugin: 安全扫描发现 {Count} 条警告：{Warnings}",
                validationResult.Warnings.Count, string.Join("; ", validationResult.Warnings));
        }

        return PluginResult.Ok("安全校验通过");
    }

    public override Task RollbackAsync(PluginContext context)
    {
        // 安全检查无副作用，空实现
        return Task.CompletedTask;
    }

    public override PluginMetadata GetMetadata()
    {
        var metadata = base.GetMetadata();
        metadata.Description = "对上传文件进行安全性校验（文件格式、大小、内容安全）";
        return metadata;
    }

    /// <summary>从批次记录和文件管理器解析文件信息</summary>
    private async Task<(string fileName, long fileSize, Stream? fileStream)> ResolveFileInfoAsync(
        PluginContext context, STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        var batch = await db.Set<Entities.CfBatch>()
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.FID == context.BatchId);

        var fileName = batch?.FFileName ?? string.Empty;
        var filePath = batch?.FFilePath;
        long fileSize = batch?.FFileSize ?? 0;

        Stream? fileStream = null;
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (fileSize == 0 && fileStream.CanSeek)
                fileSize = fileStream.Length;
        }

        return (fileName, fileSize, fileStream);
    }
}
