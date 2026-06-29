using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Finance.Constants;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Services;

public class AssetService : IAssetService
{
    private readonly IRepository<FinAssetCategory> _categoryRepository;
    private readonly IRepository<FinAssetCard> _cardRepository;
    private readonly IRepository<FinAccount> _accountRepository;
    private readonly IRepository<FinVoucher> _voucherRepository;
    private readonly IRepository<FinVoucherEntry> _voucherEntryRepository;
    private readonly IRepository<FinAccountPeriod> _periodRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AssetService(
        IRepository<FinAssetCategory> categoryRepository,
        IRepository<FinAssetCard> cardRepository,
        IRepository<FinAccount> accountRepository,
        IRepository<FinVoucher> voucherRepository,
        IRepository<FinVoucherEntry> voucherEntryRepository,
        IRepository<FinAccountPeriod> periodRepository,
        IHttpContextAccessor httpContextAccessor)
    {
        _categoryRepository = categoryRepository;
        _cardRepository = cardRepository;
        _accountRepository = accountRepository;
        _voucherRepository = voucherRepository;
        _voucherEntryRepository = voucherEntryRepository;
        _periodRepository = periodRepository;
        _httpContextAccessor = httpContextAccessor;
    }

    private long GetCurrentOrgId()
    {
        var orgIdObj = _httpContextAccessor.HttpContext?.Items["CurrentOrgId"];
        if (orgIdObj is long orgId) return orgId;
        return 0;
    }

    #region Asset Category

    public async Task<List<AssetCategoryDto>> GetCategoriesAsync(long accountSetId)
    {
        var categories = await _categoryRepository.Query()
            .Where(c => c.FAccountSetId == accountSetId)
            .ToListAsync();
        var accounts = await _accountRepository.Query().ToListAsync();
        
        return categories.Select(c => MapCategoryToDto(c, accounts)).ToList();
    }

    public async Task<AssetCategoryDto?> GetCategoryByIdAsync(long id)
    {
        var category = await _categoryRepository.GetByIdAsync(id);
        if (category == null) return null;
        
        var accounts = await _accountRepository.Query().ToListAsync();
        return MapCategoryToDto(category, accounts);
    }

    public async Task<AssetCategoryDto> CreateCategoryAsync(CreateAssetCategoryRequest request, long accountSetId)
    {
        var existing = await _categoryRepository.Query()
            .FirstOrDefaultAsync(c => c.FCode == request.Code && c.FAccountSetId == accountSetId);
        
        if (existing != null)
        {
            throw new InvalidOperationException($"资产类别编码 {request.Code} 已存在");
        }

        var category = new FinAssetCategory
        {
            FCode = request.Code,
            FName = request.Name,
            FDepreciationMethod = request.DepreciationMethod,
            FUsefulLife = request.UsefulLife,
            FResidualRate = request.ResidualRate,
            FDepreciationAccountId = request.DepreciationAccountId,
            FAccountSetId = accountSetId,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _categoryRepository.AddAsync(category);
        
        var accounts = await _accountRepository.Query().ToListAsync();
        return MapCategoryToDto(category, accounts);
    }

    public async Task<AssetCategoryDto?> UpdateCategoryAsync(long id, CreateAssetCategoryRequest request)
    {
        var category = await _categoryRepository.GetByIdAsync(id);
        if (category == null) return null;

        category.FName = request.Name;
        category.FDepreciationMethod = request.DepreciationMethod;
        category.FUsefulLife = request.UsefulLife;
        category.FResidualRate = request.ResidualRate;
        category.FDepreciationAccountId = request.DepreciationAccountId;
        category.FUpdatedTime = DateTime.Now;
        
        await _categoryRepository.UpdateAsync(category);
        
        var accounts = await _accountRepository.Query().ToListAsync();
        return MapCategoryToDto(category, accounts);
    }

    public async Task<bool> DeleteCategoryAsync(long id)
    {
        var category = await _categoryRepository.GetByIdAsync(id);
        if (category == null) return false;

        var hasCards = await _cardRepository.Query()
            .AnyAsync(c => c.FCategoryId == id);
        
        if (hasCards)
        {
            throw new InvalidOperationException("该类别下有资产卡片，无法删除");
        }

        await _categoryRepository.DeleteAsync(id);
        return true;
    }

    #endregion

    #region Asset Card

    public async Task<List<AssetCardDto>> GetCardsAsync(long accountSetId, long? categoryId = null)
    {
        var currentOrgId = GetCurrentOrgId();
        var query = _cardRepository.Query()
            .Where(c => c.FOrgId == currentOrgId)
            .Where(c => c.FAccountSetId == accountSetId);
        
        if (categoryId.HasValue)
        {
            query = query.Where(c => c.FCategoryId == categoryId.Value);
        }
        
        var cards = await query.OrderBy(c => c.FCode).ToListAsync();
        var categories = await _categoryRepository.Query().ToListAsync();
        
        return cards.Select(c => MapCardToDto(c, categories)).ToList();
    }

    public async Task<AssetCardDto?> GetCardByIdAsync(long id)
    {
        var card = await _cardRepository.GetByIdAsync(id);
        if (card == null) return null;
        
        var categories = await _categoryRepository.Query().ToListAsync();
        return MapCardToDto(card, categories);
    }

    public async Task<AssetCardDto> CreateCardAsync(CreateAssetCardRequest request, long accountSetId)
    {
        var existing = await _cardRepository.Query()
            .FirstOrDefaultAsync(c => c.FCode == request.Code && c.FAccountSetId == accountSetId);
        
        if (existing != null)
        {
            throw new InvalidOperationException($"资产卡片编码 {request.Code} 已存在");
        }

        var category = await _categoryRepository.GetByIdAsync(request.CategoryId);
        
        var card = new FinAssetCard
        {
            FCode = request.Code,
            FName = request.Name,
            FCategoryId = request.CategoryId,
            FDepartmentId = request.DepartmentId,
            FOrgId = GetCurrentOrgId(),
            FAccountSetId = accountSetId,
            FOriginalValue = request.OriginalValue,
            FAccumulatedDepreciation = 0,
            FNetValue = request.OriginalValue,
            FEntryDate = request.EntryDate,
            FStartDepreciationDate = request.StartDepreciationDate,
            FUsefulLife = request.UsefulLife ?? category?.FUsefulLife,
            FResidualRate = request.ResidualRate ?? category?.FResidualRate,
            FStatus = 1,
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _cardRepository.AddAsync(card);
        
        var categories = await _categoryRepository.Query().ToListAsync();
        return MapCardToDto(card, categories);
    }

    public async Task<AssetCardDto?> UpdateCardAsync(long id, UpdateAssetCardRequest request)
    {
        var card = await _cardRepository.GetByIdAsync(id);
        if (card == null) return null;

        card.FName = request.Name;
        card.FDepartmentId = request.DepartmentId;
        card.FRemark = request.Remark;
        card.FUpdatedTime = DateTime.Now;
        
        await _cardRepository.UpdateAsync(card);
        
        var categories = await _categoryRepository.Query().ToListAsync();
        return MapCardToDto(card, categories);
    }

    public async Task<bool> DeleteCardAsync(long id)
    {
        var card = await _cardRepository.GetByIdAsync(id);
        if (card == null) return false;

        await _cardRepository.DeleteAsync(id);
        return true;
    }

    #region 小番财务资产卡片导入

    // 小番财务导出的表头（前 16 列）
    private static readonly string[] XiaofanHeaders = new[]
    {
        "卡片编码", "卡片名称", "资产类别编码", "开始使用日期", "标签",
        "折旧/摊销方法", "默认使用总期限（月）", "账务处理借方科目代码",
        "借方辅助核算类别", "借方辅助核算编码", "账务处理贷方科目代码",
        "贷方辅助核算类别", "贷方辅助核算编码", "原值", "残值率(%)", "备注"
    };

    public async Task<AssetImportResult> ImportFromXiaofanAsync(Stream stream, string fileName, long accountSetId)
    {
        var result = new AssetImportResult();

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        ms.Position = 0;

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        IWorkbook workbook = ext == ".xls" ? new HSSFWorkbook(ms) : new XSSFWorkbook(ms);

        try
        {
            var sheet = workbook.GetSheetAt(0);
            if (sheet == null)
            {
                result.Errors.Add(new AssetImportError { RowNumber = 0, Message = "Excel 文件中没有工作表" });
                return result;
            }

            var headerRow = sheet.GetRow(0);
            if (headerRow == null)
            {
                result.Errors.Add(new AssetImportError { RowNumber = 1, Message = "缺少表头行" });
                return result;
            }

            // 校验表头
            for (int i = 0; i < XiaofanHeaders.Length; i++)
            {
                var cellValue = GetCellString(headerRow.GetCell(i)).Trim();
                if (cellValue != XiaofanHeaders[i])
                {
                    result.Errors.Add(new AssetImportError
                    {
                        RowNumber = 1,
                        Message = $"第{i + 1}列表头应为\"{XiaofanHeaders[i]}\"，实际为\"{cellValue}\""
                    });
                }
            }
            if (result.Errors.Count > 0) return result;

            // 预加载引用数据
            var currentOrgId = GetCurrentOrgId();
            var categories = await _categoryRepository.Query().ToListAsync();
            var categoryByCode = categories
                .Where(c => !string.IsNullOrWhiteSpace(c.FCode))
                .GroupBy(c => c.FCode)
                .ToDictionary(g => g.Key, g => g.First());

            var existingCodes = (await _cardRepository.Query()
                    .Where(c => c.FOrgId == currentOrgId)
                    .Select(c => c.FCode).ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var cardsToAdd = new List<FinAssetCard>();
            var pendingCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int rowIdx = 1; rowIdx <= sheet.LastRowNum; rowIdx++)
            {
                var row = sheet.GetRow(rowIdx);
                if (row == null) continue;

                // 跳过空行
                bool hasData = false;
                for (int c = 0; c < XiaofanHeaders.Length; c++)
                {
                    if (!string.IsNullOrWhiteSpace(GetCellString(row.GetCell(c)))) { hasData = true; break; }
                }
                if (!hasData) continue;

                var excelRow = rowIdx + 1;
                result.TotalRows++;

                var code = GetCellString(row.GetCell(0)).Trim();
                var name = GetCellString(row.GetCell(1)).Trim();
                var categoryCode = GetCellString(row.GetCell(2)).Trim();
                var startDate = GetCellDate(row.GetCell(3));
                // 标签列忽略（4）
                var methodStr = GetCellString(row.GetCell(5)).Trim();
                var totalMonthsStr = GetCellString(row.GetCell(6)).Trim();
                var debitAcctCode = GetCellString(row.GetCell(7)).Trim();
                var debitAuxType = GetCellString(row.GetCell(8)).Trim();
                var debitAuxCode = GetCellString(row.GetCell(9)).Trim();
                var creditAcctCode = GetCellString(row.GetCell(10)).Trim();
                var creditAuxType = GetCellString(row.GetCell(11)).Trim();
                var creditAuxCode = GetCellString(row.GetCell(12)).Trim();
                var originalValueStr = GetCellString(row.GetCell(13)).Trim();
                var residualRateStr = GetCellString(row.GetCell(14)).Trim();
                var remark = GetCellString(row.GetCell(15)).Trim();

                // 必填校验
                if (string.IsNullOrEmpty(code))
                { result.Errors.Add(new AssetImportError { RowNumber = excelRow, Message = "卡片编码不能为空" }); result.SkippedCount++; continue; }
                if (string.IsNullOrEmpty(name))
                { result.Errors.Add(new AssetImportError { RowNumber = excelRow, Message = "卡片名称不能为空" }); result.SkippedCount++; continue; }
                if (string.IsNullOrEmpty(categoryCode))
                { result.Errors.Add(new AssetImportError { RowNumber = excelRow, Message = "资产类别编码不能为空" }); result.SkippedCount++; continue; }

                if (existingCodes.Contains(code) || !pendingCodes.Add(code))
                { result.Errors.Add(new AssetImportError { RowNumber = excelRow, Message = $"卡片编码 {code} 已存在或重复" }); result.SkippedCount++; continue; }

                if (!categoryByCode.TryGetValue(categoryCode, out var category))
                { result.Errors.Add(new AssetImportError { RowNumber = excelRow, Message = $"资产类别编码 {categoryCode} 不存在，请先在《资产类别》中维护" }); result.SkippedCount++; continue; }

                if (!startDate.HasValue)
                { result.Errors.Add(new AssetImportError { RowNumber = excelRow, Message = $"开始使用日期格式错误或为空" }); result.SkippedCount++; continue; }

                if (!decimal.TryParse(originalValueStr, out var originalValue) || originalValue < 0)
                { result.Errors.Add(new AssetImportError { RowNumber = excelRow, Message = $"原值格式错误: \"{originalValueStr}\"" }); result.SkippedCount++; continue; }

                decimal residualRate = 0;
                if (!string.IsNullOrEmpty(residualRateStr) && !decimal.TryParse(residualRateStr, out residualRate))
                { result.Errors.Add(new AssetImportError { RowNumber = excelRow, Message = $"残值率格式错误: \"{residualRateStr}\"" }); result.SkippedCount++; continue; }

                // 使用总期限（月）→ F使用年限（年）
                int? usefulLifeYears = null;
                int totalMonths = 0;
                if (!string.IsNullOrEmpty(totalMonthsStr))
                {
                    if (!int.TryParse(totalMonthsStr.Replace(".0", ""), out totalMonths) || totalMonths <= 0)
                    { result.Errors.Add(new AssetImportError { RowNumber = excelRow, Message = $"使用总期限格式错误: \"{totalMonthsStr}\"" }); result.SkippedCount++; continue; }
                    usefulLifeYears = Math.Max(1, (int)Math.Round(totalMonths / 12d));
                }

                // 折旧方法映射 (仅记录与类别不一致时放入备注)
                var mappedMethod = MapDepreciationMethod(methodStr);

                // 拼装备注：保留借贷科目/辅助核算信息 + 原备注
                var remarkParts = new List<string>();
                if (!string.IsNullOrEmpty(debitAcctCode))
                    remarkParts.Add($"借:{debitAcctCode}{(string.IsNullOrEmpty(debitAuxCode) ? "" : $"/{debitAuxType}{debitAuxCode}")}");
                if (!string.IsNullOrEmpty(creditAcctCode))
                    remarkParts.Add($"贷:{creditAcctCode}{(string.IsNullOrEmpty(creditAuxCode) ? "" : $"/{creditAuxType}{creditAuxCode}")}");
                if (totalMonths > 0 && totalMonths % 12 != 0)
                    remarkParts.Add($"使用期限{totalMonths}月");
                if (!string.IsNullOrEmpty(mappedMethod) && mappedMethod != category.FDepreciationMethod)
                    remarkParts.Add($"折旧方法:{mappedMethod}");
                if (!string.IsNullOrEmpty(remark))
                    remarkParts.Add(remark);
                var finalRemark = string.Join(" | ", remarkParts);
                if (finalRemark.Length > 500) finalRemark = finalRemark.Substring(0, 500);

                var card = new FinAssetCard
                {
                    FCode = code,
                    FName = name,
                    FCategoryId = category.FID,
                    FDepartmentId = null,
                    FOrgId = currentOrgId,
                    FAccountSetId = accountSetId,
                    FOriginalValue = originalValue,
                    FAccumulatedDepreciation = 0,
                    FNetValue = originalValue,
                    FEntryDate = startDate.Value,
                    FStartDepreciationDate = startDate.Value,
                    FUsefulLife = usefulLifeYears ?? category.FUsefulLife,
                    FResidualRate = residualRate,
                    FStatus = 1,
                    FRemark = string.IsNullOrEmpty(finalRemark) ? null : finalRemark,
                    FCreatedTime = DateTime.Now,
                    FUpdatedTime = DateTime.Now
                };
                cardsToAdd.Add(card);
            }

            foreach (var card in cardsToAdd)
            {
                await _cardRepository.AddAsync(card);
            }
            result.ImportedCount = cardsToAdd.Count;
        }
        finally
        {
            workbook.Close();
        }

        return result;
    }

    // 小番财务资产类别导出的表头（前 15 列）
    private static readonly string[] XiaofanCategoryHeaders = new[]
    {
        "类别编码", "类别名称", "折旧/摊销方法", "默认使用总期限（月）", "默认残值率（%）",
        "默认资产科目编码", "资产科目辅助核算类别", "资产科目辅助核算编码",
        "财务处理默认借方科目编码", "借方辅助核算类别", "借方辅助核算编码",
        "财务处理默认贷方科目编码", "贷方辅助核算类别", "贷方辅助核算编码", "备注"
    };

    public async Task<AssetImportResult> ImportCategoriesFromXiaofanAsync(Stream stream, string fileName, long accountSetId)
    {
        var result = new AssetImportResult();

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        ms.Position = 0;

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        IWorkbook workbook = ext == ".xls" ? new HSSFWorkbook(ms) : new XSSFWorkbook(ms);

        try
        {
            var sheet = workbook.GetSheetAt(0);
            if (sheet == null)
            {
                result.Errors.Add(new AssetImportError { RowNumber = 0, Message = "Excel 文件中没有工作表" });
                return result;
            }

            var headerRow = sheet.GetRow(0);
            if (headerRow == null)
            {
                result.Errors.Add(new AssetImportError { RowNumber = 1, Message = "缺少表头行" });
                return result;
            }

            for (int i = 0; i < XiaofanCategoryHeaders.Length; i++)
            {
                var cellValue = GetCellString(headerRow.GetCell(i)).Trim();
                if (cellValue != XiaofanCategoryHeaders[i])
                {
                    result.Errors.Add(new AssetImportError
                    {
                        RowNumber = 1,
                        Message = $"第{i + 1}列表头应为\"{XiaofanCategoryHeaders[i]}\"，实际为\"{cellValue}\""
                    });
                }
            }
            if (result.Errors.Count > 0) return result;

            var existing = await _categoryRepository.Query().ToListAsync();
            var existingByCode = existing
                .Where(c => !string.IsNullOrWhiteSpace(c.FCode))
                .GroupBy(c => c.FCode)
                .ToDictionary(g => g.Key, g => g.First());

            // 账套范围内按科目编码查 FID，用于 F对应折旧科目ID
            // 资产类别是全局表（无账套ID），此处默认按当前账套匹配；
            // 无法确定当前账套时取编码最小的账套
            var allAccounts = await _accountRepository.Query()
                .Select(a => new { a.FID, a.FCode, a.FAccountSetId })
                .ToListAsync();

            var addList = new List<FinAssetCategory>();
            var updateList = new List<FinAssetCategory>();
            var pendingCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int rowIdx = 1; rowIdx <= sheet.LastRowNum; rowIdx++)
            {
                var row = sheet.GetRow(rowIdx);
                if (row == null) continue;

                bool hasData = false;
                for (int c = 0; c < XiaofanCategoryHeaders.Length; c++)
                {
                    if (!string.IsNullOrWhiteSpace(GetCellString(row.GetCell(c)))) { hasData = true; break; }
                }
                if (!hasData) continue;

                var excelRow = rowIdx + 1;
                result.TotalRows++;

                var code = GetCellString(row.GetCell(0)).Trim();
                var name = GetCellString(row.GetCell(1)).Trim();
                var methodStr = GetCellString(row.GetCell(2)).Trim();
                var totalMonthsStr = GetCellString(row.GetCell(3)).Trim();
                var residualRateStr = GetCellString(row.GetCell(4)).Trim();
                // 列 5、6、7 资产科目信息暂不使用
                // 列 8 借方科目编码（费用类）
                // 列 11 贷方科目编码（累计折旧，作为 F对应折旧科目ID）
                var creditAcctCode = GetCellString(row.GetCell(11)).Trim();

                if (string.IsNullOrEmpty(code))
                { result.Errors.Add(new AssetImportError { RowNumber = excelRow, Message = "类别编码不能为空" }); result.SkippedCount++; continue; }
                if (string.IsNullOrEmpty(name))
                { result.Errors.Add(new AssetImportError { RowNumber = excelRow, Message = "类别名称不能为空" }); result.SkippedCount++; continue; }
                if (!pendingCodes.Add(code))
                { result.Errors.Add(new AssetImportError { RowNumber = excelRow, Message = $"类别编码 {code} 在当前文件中重复" }); result.SkippedCount++; continue; }

                int usefulLifeYears = 0;
                if (!string.IsNullOrEmpty(totalMonthsStr))
                {
                    if (!int.TryParse(totalMonthsStr.Replace(".0", ""), out var months) || months <= 0)
                    { result.Errors.Add(new AssetImportError { RowNumber = excelRow, Message = $"使用总期限格式错误: \"{totalMonthsStr}\"" }); result.SkippedCount++; continue; }
                    usefulLifeYears = Math.Max(1, (int)Math.Round(months / 12d));
                }

                decimal residualRate = 0;
                if (!string.IsNullOrEmpty(residualRateStr) && !decimal.TryParse(residualRateStr, out residualRate))
                { result.Errors.Add(new AssetImportError { RowNumber = excelRow, Message = $"残值率格式错误: \"{residualRateStr}\"" }); result.SkippedCount++; continue; }

                var mappedMethod = MapDepreciationMethod(methodStr);
                if (string.IsNullOrEmpty(mappedMethod)) mappedMethod = "直线法";

                // 按贷方累计折旧科目编码找 FID（任意账套下的第一条，用户后续可在前端调整）
                long? depAccountId = null;
                if (!string.IsNullOrEmpty(creditAcctCode))
                {
                    var acct = allAccounts.FirstOrDefault(a => a.FCode == creditAcctCode);
                    if (acct != null) depAccountId = acct.FID;
                }

                if (existingByCode.TryGetValue(code, out var existingCategory))
                {
                    existingCategory.FName = name;
                    existingCategory.FDepreciationMethod = mappedMethod;
                    existingCategory.FUsefulLife = usefulLifeYears > 0 ? usefulLifeYears : existingCategory.FUsefulLife;
                    existingCategory.FResidualRate = residualRate;
                    if (depAccountId.HasValue) existingCategory.FDepreciationAccountId = depAccountId;
                    existingCategory.FUpdatedTime = DateTime.Now;
                    updateList.Add(existingCategory);
                }
                else
                {
                    addList.Add(new FinAssetCategory
                    {
                        FCode = code,
                        FName = name,
                        FDepreciationMethod = mappedMethod,
                        FUsefulLife = usefulLifeYears > 0 ? usefulLifeYears : 5,
                        FResidualRate = residualRate,
                        FDepreciationAccountId = depAccountId,
                        FAccountSetId = accountSetId,
                        FCreatedTime = DateTime.Now,
                        FUpdatedTime = DateTime.Now
                    });
                }
            }

            foreach (var cat in addList)
                await _categoryRepository.AddAsync(cat);
            foreach (var cat in updateList)
                await _categoryRepository.UpdateAsync(cat);

            result.ImportedCount = addList.Count + updateList.Count;
        }
        finally
        {
            workbook.Close();
        }

        return result;
    }

    private static string MapDepreciationMethod(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return string.Empty;
        return source.Trim() switch
        {
            "平均年限法" => "直线法",
            "直线法" => "直线法",
            "双倍余额递减法" => "双倍余额递减法",
            "年数总和法" => "年数总和法",
            _ => source.Trim()
        };
    }

    private static string GetCellString(ICell? cell)
    {
        if (cell == null) return string.Empty;
        switch (cell.CellType)
        {
            case CellType.String: return cell.StringCellValue ?? string.Empty;
            case CellType.Numeric:
                if (DateUtil.IsCellDateFormatted(cell))
                    return cell.DateCellValue?.ToString("yyyy-MM-dd") ?? string.Empty;
                return cell.NumericCellValue.ToString("G");
            case CellType.Boolean: return cell.BooleanCellValue.ToString();
            case CellType.Formula:
                try
                {
                    return cell.CachedFormulaResultType switch
                    {
                        CellType.String => cell.StringCellValue ?? string.Empty,
                        CellType.Numeric => DateUtil.IsCellDateFormatted(cell)
                            ? cell.DateCellValue?.ToString("yyyy-MM-dd") ?? string.Empty
                            : cell.NumericCellValue.ToString("G"),
                        _ => cell.ToString() ?? string.Empty
                    };
                }
                catch { return string.Empty; }
            case CellType.Blank:
            default: return string.Empty;
        }
    }

    private static DateTime? GetCellDate(ICell? cell)
    {
        if (cell == null) return null;
        if (cell.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
            return cell.DateCellValue;
        var s = GetCellString(cell).Trim();
        if (string.IsNullOrEmpty(s)) return null;
        if (DateTime.TryParse(s, out var dt)) return dt;
        return null;
    }

    #endregion

    #endregion

    #region Depreciation

    public async Task<DepreciationResultDto> CalculateDepreciationAsync(long periodId, string creator)
    {
        var cards = await _cardRepository.Query()
            .Where(c => c.FStatus == 1 && c.FStartDepreciationDate != null)
            .ToListAsync();

        var categories = await _categoryRepository.Query().ToListAsync();
        
        decimal totalDepreciation = 0;
        int depreciatedCount = 0;

        foreach (var card in cards)
        {
            var category = categories.FirstOrDefault(c => c.FID == card.FCategoryId);
            if (category == null) continue;

            decimal monthlyDepreciation = CalculateMonthlyDepreciation(card, category);

            if (monthlyDepreciation > 0)
            {
                card.FAccumulatedDepreciation += monthlyDepreciation;
                card.FNetValue = card.FOriginalValue - card.FAccumulatedDepreciation;
                card.FUpdatedTime = DateTime.Now;
                await _cardRepository.UpdateAsync(card);

                totalDepreciation += monthlyDepreciation;
                depreciatedCount++;
            }
        }

        // 生成折旧凭证
        long? voucherId = null;
        if (totalDepreciation > 0)
        {
            var voucher = new FinVoucher
            {
                FVoucherWord = VoucherWord.Ji,
                FVoucherNo = await GetNextVoucherNoAsync(periodId),
                FDate = DateTime.Now,
                FPeriodId = periodId,
                FAttachmentCount = 0,
                FCreator = creator,
                FStatus = 1,
                FSource = "折旧计提",
                FRemark = $"计提折旧，共{depreciatedCount}项资产",
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };
            await _voucherRepository.AddAsync(voucher);
            voucherId = voucher.FID;

            // 借：管理费用-折旧费
            var expenseEntry = new FinVoucherEntry
            {
                FVoucherId = voucher.FID,
                FLineNo = 1,
                FSummary = "计提折旧",
                FAccountId = 0,
                FAccountCode = "560205",
                FAccountName = "管理费用-折旧费",
                FDebitAmount = totalDepreciation,
                FCreditAmount = 0,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };
            await _voucherEntryRepository.AddAsync(expenseEntry);

            // 贷：累计折旧
            var depreciationEntry = new FinVoucherEntry
            {
                FVoucherId = voucher.FID,
                FLineNo = 2,
                FSummary = "计提折旧",
                FAccountId = 0,
                FAccountCode = "1602",
                FAccountName = "累计折旧",
                FDebitAmount = 0,
                FCreditAmount = totalDepreciation,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };
            await _voucherEntryRepository.AddAsync(depreciationEntry);
        }

        return new DepreciationResultDto
        {
            DepreciatedCount = depreciatedCount,
            TotalDepreciationAmount = totalDepreciation,
            VoucherId = voucherId
        };
    }

    public async Task<DepreciationPreviewDto> CalculateDepreciationPreviewAsync(long periodId, long accountSetId)
    {
        var currentOrgId = GetCurrentOrgId();
        var cards = await _cardRepository.Query()
            .Where(c => c.FStatus == 1 && c.FStartDepreciationDate != null && c.FOrgId == currentOrgId)
            .ToListAsync();

        var categories = await _categoryRepository.Query().ToListAsync();

        var details = new List<DepreciationDetailDto>();
        decimal totalAmount = 0;

        foreach (var card in cards)
        {
            var category = categories.FirstOrDefault(c => c.FID == card.FCategoryId);
            if (category == null) continue;

            decimal monthlyDepreciation = CalculateMonthlyDepreciation(card, category);
            if (monthlyDepreciation <= 0) continue;

            details.Add(new DepreciationDetailDto
            {
                AssetId = card.FID,
                AssetCode = card.FCode,
                AssetName = card.FName,
                CategoryName = category.FName,
                DepreciationMethod = category.FDepreciationMethod,
                OriginalValue = card.FOriginalValue,
                NetValue = card.FNetValue,
                AccumulatedDepreciation = card.FAccumulatedDepreciation,
                MonthlyDepreciation = monthlyDepreciation
            });

            totalAmount += monthlyDepreciation;
        }

        return new DepreciationPreviewDto
        {
            Details = details,
            TotalAmount = totalAmount,
            AssetCount = details.Count
        };
    }

    public async Task<DepreciationResultDto> GenerateDepreciationVouchersAsync(long periodId, long accountSetId)
    {
        var preview = await CalculateDepreciationPreviewAsync(periodId, accountSetId);
        if (preview.AssetCount == 0)
        {
            return new DepreciationResultDto
            {
                DepreciatedCount = 0,
                TotalDepreciationAmount = 0
            };
        }

        // 获取期间信息
        var period = await _periodRepository.GetByIdAsync(periodId);

        // 更新资产卡片
        foreach (var detail in preview.Details)
        {
            var card = await _cardRepository.GetByIdAsync(detail.AssetId);
            if (card == null) continue;

            card.FAccumulatedDepreciation += detail.MonthlyDepreciation;
            card.FNetValue = card.FOriginalValue - card.FAccumulatedDepreciation;
            card.FUpdatedTime = DateTime.Now;
            await _cardRepository.UpdateAsync(card);
        }

        // 查找科目
        var expenseAccount = await _accountRepository.Query()
            .FirstOrDefaultAsync(a => a.FCode == "560201" && a.FAccountSetId == accountSetId);
        if (expenseAccount == null)
        {
            // 回退到 560205
            expenseAccount = await _accountRepository.Query()
                .FirstOrDefaultAsync(a => a.FCode == "560205" && a.FAccountSetId == accountSetId);
        }

        var accDepAccount = await _accountRepository.Query()
            .FirstOrDefaultAsync(a => a.FCode == "1602" && a.FAccountSetId == accountSetId);

        // 生成折旧凭证
        var maxNo = await _voucherRepository.Query()
            .Where(v => v.FPeriodId == periodId && v.FAccountSetId == accountSetId)
            .MaxAsync(v => (int?)v.FVoucherNo) ?? 0;

        var entries = new List<FinVoucherEntry>
        {
            new FinVoucherEntry
            {
                FLineNo = 1,
                FSummary = "计提折旧",
                FAccountId = expenseAccount?.FID ?? 0,
                FAccountCode = expenseAccount?.FCode ?? "560201",
                FAccountName = expenseAccount?.FName ?? "管理费用-折旧费",
                FDebitAmount = preview.TotalAmount,
                FCreditAmount = 0,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            },
            new FinVoucherEntry
            {
                FLineNo = 2,
                FSummary = "计提折旧",
                FAccountId = accDepAccount?.FID ?? 0,
                FAccountCode = accDepAccount?.FCode ?? "1602",
                FAccountName = accDepAccount?.FName ?? "累计折旧",
                FDebitAmount = 0,
                FCreditAmount = preview.TotalAmount,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            }
        };

        var voucher = new FinVoucher
        {
            FVoucherWord = "记",
            FVoucherNo = maxNo + 1,
            FDate = period?.FEndDate ?? DateTime.Now,
            FPeriodId = periodId,
            FAttachmentCount = 0,
            FCreator = "系统",
            FAuditor = "系统",
            FStatus = 2,
            FSource = "资产折旧",
            FRemark = $"计提折旧，共{preview.AssetCount}项资产，金额 {preview.TotalAmount:N2}",
            FAccountSetId = accountSetId,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now,
            Entries = entries
        };

        await _voucherRepository.AddAsync(voucher);

        return new DepreciationResultDto
        {
            DepreciatedCount = preview.AssetCount,
            TotalDepreciationAmount = preview.TotalAmount,
            VoucherId = voucher.FID,
            VoucherIds = new List<long> { voucher.FID }
        };
    }

    /// <summary>
    /// 根据折旧方法计算月折旧额
    /// </summary>
    private static decimal CalculateMonthlyDepreciation(FinAssetCard card, FinAssetCategory category)
    {
        var residualRate = (card.FResidualRate ?? category.FResidualRate) / 100m;
        var residualValue = card.FOriginalValue * residualRate;
        var usefulLife = card.FUsefulLife ?? category.FUsefulLife;
        if (usefulLife <= 0) return 0;

        // 如果净值已等于残值，不再计提
        if (card.FNetValue <= residualValue) return 0;

        var method = category.FDepreciationMethod;
        decimal monthlyDepreciation = 0;

        switch (method)
        {
            case "直线法":
                // (原值 - 残值) / 使用年限 / 12
                monthlyDepreciation = (card.FOriginalValue - residualValue) / usefulLife / 12m;
                break;

            case "双倍余额递减法":
                // 2 / 使用年限 * 期初净值 / 12
                monthlyDepreciation = 2m / usefulLife * card.FNetValue / 12m;
                break;

            case "年数总和法":
            {
                // 计算已使用年数
                if (card.FStartDepreciationDate == null) return 0;
                var usedMonths = (int)((DateTime.Now - card.FStartDepreciationDate.Value).TotalDays / 30.44);
                var usedYears = usedMonths / 12;
                var remainingYears = usefulLife - usedYears;
                if (remainingYears <= 0) return 0;

                // 年数总和 = n*(n+1)/2
                var yearSum = usefulLife * (usefulLife + 1) / 2;
                // (原值 - 残值) * 剩余使用年限 / 年数总和 / 12
                monthlyDepreciation = (card.FOriginalValue - residualValue) * remainingYears / yearSum / 12m;
                break;
            }

            default:
                // 默认直线法
                monthlyDepreciation = (card.FOriginalValue - residualValue) / usefulLife / 12m;
                break;
        }

        // 确保折旧后净值不低于残值
        var maxDepreciation = card.FNetValue - residualValue;
        if (monthlyDepreciation > maxDepreciation)
            monthlyDepreciation = maxDepreciation;

        return Math.Round(monthlyDepreciation, 2);
    }

    private async Task<int> GetNextVoucherNoAsync(long periodId)
    {
        var maxNo = await _voucherRepository.Query()
            .Where(v => v.FPeriodId == periodId && v.FVoucherWord == VoucherWord.Ji)
            .MaxAsync(v => (int?)v.FVoucherNo) ?? 0;
        return maxNo + 1;
    }

    #endregion

    private static AssetCategoryDto MapCategoryToDto(FinAssetCategory category, List<FinAccount> accounts)
    {
        var account = accounts.FirstOrDefault(a => a.FID == category.FDepreciationAccountId);
        return new AssetCategoryDto
        {
            Id = category.FID,
            Code = category.FCode,
            Name = category.FName,
            DepreciationMethod = category.FDepreciationMethod,
            UsefulLife = category.FUsefulLife,
            ResidualRate = category.FResidualRate,
            DepreciationAccountId = category.FDepreciationAccountId,
            DepreciationAccountName = account?.FName
        };
    }

    private static AssetCardDto MapCardToDto(FinAssetCard card, List<FinAssetCategory> categories)
    {
        var category = categories.FirstOrDefault(c => c.FID == card.FCategoryId);
        return new AssetCardDto
        {
            Id = card.FID,
            Code = card.FCode,
            Name = card.FName,
            CategoryId = card.FCategoryId,
            CategoryName = category?.FName ?? "",
            DepartmentId = card.FDepartmentId,
            OriginalValue = card.FOriginalValue,
            AccumulatedDepreciation = card.FAccumulatedDepreciation,
            NetValue = card.FNetValue,
            EntryDate = card.FEntryDate,
            StartDepreciationDate = card.FStartDepreciationDate,
            UsefulLife = card.FUsefulLife,
            ResidualRate = card.FResidualRate,
            Status = card.FStatus,
            Remark = card.FRemark
        };
    }
}
