using Microsoft.Extensions.Caching.Memory;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services.Import;

public class SecureFileUploadValidator
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".xlsx", ".xls", ".csv" };

    private static readonly HashSet<string> WindowsReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private const long DefaultMaxFileSize = 52428800; // 50MB
    private const string MaxFileSizeSettingKey = "DataCenter.MaxFileSize";
    private const string CacheKey = "SecureFileUploadValidator:MaxFileSize";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly ISystemSettingsService _settingsService;
    private readonly IMemoryCache _cache;

    public SecureFileUploadValidator(ISystemSettingsService settingsService, IMemoryCache cache)
    {
        _settingsService = settingsService;
        _cache = cache;
    }

    /// <summary>
    /// 从数据库系统设置中读取最大文件大小（带 5 分钟内存缓存）
    /// </summary>
    private async Task<long> GetMaxFileSizeAsync()
    {
        if (_cache.TryGetValue(CacheKey, out long cachedSize))
            return cachedSize;

        try
        {
            var result = await _settingsService.GetByKeyAsync(MaxFileSizeSettingKey);
            if (result.Data != null && long.TryParse(result.Data.Value, out var size) && size > 0)
            {
                _cache.Set(CacheKey, size, CacheDuration);
                return size;
            }
        }
        catch
        {
            // 读取失败时回退到默认值
        }

        _cache.Set(CacheKey, DefaultMaxFileSize, CacheDuration);
        return DefaultMaxFileSize;
    }

    /// <summary>
    /// 异步验证（推荐使用）
    /// </summary>
    public async Task<FileValidationResult> ValidateAsync(string fileName, long fileSize, Stream fileStream)
    {
        var result = new FileValidationResult();

        // 1. 文件名安全性
        ValidateFileName(fileName, result);

        // 2. 文件大小（异步获取限制值）
        await ValidateFileSizeAsync(fileSize, result);

        if (!result.IsValid) return result;

        // 3. 扩展名白名单
        var extension = Path.GetExtension(fileName);
        ValidateExtension(extension, result);

        // 4. MIME 类型推断（仅警告）
        ValidateMimeType(extension, result);

        if (!result.IsValid) return result;

        // 5. 文件签名（Magic Number）
        ValidateMagicNumber(extension, fileStream, result);

        if (!result.IsValid) return result;

        // 6. 内容结构检查
        ValidateContentStructure(extension, fileStream, result);

        return result;
    }

    /// <summary>
    /// 同步验证（向后兼容）
    /// </summary>
    public FileValidationResult Validate(string fileName, long fileSize, Stream fileStream)
    {
        return ValidateAsync(fileName, fileSize, fileStream).GetAwaiter().GetResult();
    }

    private static void ValidateFileName(string fileName, FileValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            result.IsValid = false;
            result.Errors.Add("文件名不能为空");
            return;
        }

        if (fileName.Length > 255)
        {
            result.IsValid = false;
            result.Errors.Add("文件名长度不能超过255个字符");
            return;
        }

        // 防路径遍历
        if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
        {
            result.IsValid = false;
            result.Errors.Add("文件名包含非法字符（路径遍历攻击检测）");
            return;
        }

        // Windows 保留名
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        if (WindowsReservedNames.Contains(nameWithoutExt))
        {
            result.IsValid = false;
            result.Errors.Add("文件名使用了Windows保留名称");
        }
    }

    private async Task ValidateFileSizeAsync(long fileSize, FileValidationResult result)
    {
        if (fileSize <= 0)
        {
            result.IsValid = false;
            result.Errors.Add("文件不能为空");
            return;
        }

        var maxFileSize = await GetMaxFileSizeAsync();
        if (fileSize > maxFileSize)
        {
            result.IsValid = false;
            result.Errors.Add($"文件大小超过限制（最大{maxFileSize / 1024.0 / 1024.0:F0}MB，当前{fileSize / 1024.0 / 1024.0:F2}MB）");
        }
    }

    private static void ValidateExtension(string extension, FileValidationResult result)
    {
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            result.IsValid = false;
            result.Errors.Add($"不支持的文件扩展名：{extension}，仅支持 .xlsx / .xls / .csv");
        }
    }

    private static void ValidateMimeType(string extension, FileValidationResult result)
    {
        var expectedMime = extension.ToLowerInvariant() switch
        {
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls" => "application/vnd.ms-excel",
            ".csv" => "text/csv",
            _ => null
        };

        if (expectedMime == null)
        {
            result.Warnings.Add("无法推断文件MIME类型");
        }
    }

    private static void ValidateMagicNumber(string extension, Stream fileStream, FileValidationResult result)
    {
        if (!fileStream.CanSeek) return;

        var originalPosition = fileStream.Position;
        try
        {
            fileStream.Position = 0;
            var header = new byte[4];
            var bytesRead = fileStream.Read(header, 0, 4);

            if (bytesRead < 2)
            {
                result.IsValid = false;
                result.Errors.Add("文件内容过短，无法验证文件签名");
                return;
            }

            switch (extension.ToLowerInvariant())
            {
                case ".xls":
                case ".xlsx":
                    // 支持 OLE2 (.xls) 和 ZIP (.xlsx) 两种文件头
                    bool isOLE2 = bytesRead >= 4 && header[0] == 0xD0 && header[1] == 0xCF 
                                  && header[2] == 0x11 && header[3] == 0xE0;
                    bool isZIP = bytesRead >= 2 && header[0] == 0x50 && header[1] == 0x4B;
                    
                    if (!isOLE2 && !isZIP)
                    {
                        result.IsValid = false;
                        result.Errors.Add("文件格式无法识别（期望Excel OLE2或XLSX格式）");
                    }
                    else if ((extension == ".xls" && isZIP) || (extension == ".xlsx" && isOLE2))
                    {
                        // 扩展名与实际格式不匹配，但可以正常处理，仅记录警告
                        result.Warnings.Add($"文件扩展名({extension})与实际格式不匹配（已自动识别）");
                    }
                    break;

                case ".csv":
                    // CSV 是文本文件，检查是否为可打印字符
                    var isText = header.Take(bytesRead).All(b => b == 0x0A || b == 0x0D || b == 0x09 || (b >= 0x20 && b <= 0x7E) || b >= 0x80);
                    if (!isText)
                    {
                        result.Warnings.Add("CSV文件包含非文本字符");
                    }
                    break;
            }
        }
        finally
        {
            fileStream.Position = originalPosition;
        }
    }

    private static void ValidateContentStructure(string extension, Stream fileStream, FileValidationResult result)
    {
        if (!fileStream.CanSeek) return;

        var ext = extension.ToLowerInvariant();
        if (ext == ".csv") return; // CSV 无需 NPOI 验证

        try
        {
            // 将 stream 内容复制到独立的 MemoryStream，避免 NPOI 关闭/破坏调用方的 Stream
            fileStream.Position = 0;
            using var tempMs = new MemoryStream();
            fileStream.CopyTo(tempMs);
            fileStream.Position = 0; // 复位原始 stream 供后续阶段使用
            var buffer = tempMs.ToArray();

            using var ms = new MemoryStream(buffer, writable: false);
            IWorkbook workbook = WorkbookFactory.Create(ms);

            try
            {
                if (workbook.NumberOfSheets == 0)
                {
                    result.Warnings.Add("Excel文件不包含任何工作表");
                }
            }
            finally
            {
                workbook.Close();
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Excel文件内容结构验证失败：{ex.Message}");
        }
    }
}

public class FileValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
