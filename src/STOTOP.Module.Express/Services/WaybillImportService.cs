using Microsoft.EntityFrameworkCore;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.HSSF.UserModel;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services;

public class WaybillImportService : IWaybillImportService
{
    private readonly IRepository<ExpWaybill> _waybillRepository;
    private readonly IRepository<ExpProvince> _provinceRepository;
    private readonly IRepository<ExpShop> _shopRepository;

    public WaybillImportService(
        IRepository<ExpWaybill> waybillRepository,
        IRepository<ExpProvince> provinceRepository,
        IRepository<ExpShop> shopRepository)
    {
        _waybillRepository = waybillRepository;
        _provinceRepository = provinceRepository;
        _shopRepository = shopRepository;
    }

    public async Task<WaybillImportResult> ImportAsync(Stream excelStream, string brandCode)
    {
        var result = new WaybillImportResult();

        // 构建省份三级匹配字典
        var provinces = await _provinceRepository.Query().ToListAsync();
        var nameDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var shortNameDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var strippedDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in provinces)
        {
            nameDict.TryAdd(p.FName, p.FID);
            if (!string.IsNullOrWhiteSpace(p.FShortName))
                shortNameDict.TryAdd(p.FShortName, p.FID);
            var stripped = StripProvinceSuffix(p.FName);
            if (!string.IsNullOrWhiteSpace(stripped))
                strippedDict.TryAdd(stripped, p.FID);
        }

        // 读取 Excel
        IWorkbook workbook;
        try
        {
            workbook = WorkbookFactory.Create(excelStream);
        }
        catch
        {
            result.Errors.Add(new WaybillImportError { RowNumber = 0, ErrorMessage = "无法读取Excel文件" });
            result.FailCount = 1;
            return result;
        }

        var sheet = workbook.GetSheetAt(0);
        if (sheet == null || sheet.LastRowNum < 1)
        {
            result.Errors.Add(new WaybillImportError { RowNumber = 0, ErrorMessage = "工作表为空" });
            return result;
        }

        // 解析表头
        var headerRow = sheet.GetRow(0);
        var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (headerRow != null)
        {
            for (int i = 0; i <= headerRow.LastCellNum; i++)
            {
                var cell = headerRow.GetCell(i);
                if (cell != null)
                {
                    var name = cell.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(name))
                        columnMap.TryAdd(name, i);
                }
            }
        }

        // 查询已有运单号（当前品牌）
        var existingWaybillNos = await _waybillRepository.Query()
            .Where(w => w.FBrandCode == brandCode)
            .Select(w => w.FWaybillNo)
            .ToListAsync();
        var existingSet = new HashSet<string>(existingWaybillNos, StringComparer.OrdinalIgnoreCase);

        // 查询已有店铺名称
        var existingShopNames = await _shopRepository.Query()
            .Select(s => s.FName)
            .ToListAsync();
        var existingShopSet = new HashSet<string>(existingShopNames, StringComparer.OrdinalIgnoreCase);
        var newShopNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var batch = new List<ExpWaybill>();
        var seenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int rowIndex = 1; rowIndex <= sheet.LastRowNum; rowIndex++)
        {
            var row = sheet.GetRow(rowIndex);
            if (row == null) continue;

            result.TotalRows++;

            try
            {
                var waybillNo = GetCellString(row, columnMap, "运单号");
                if (string.IsNullOrWhiteSpace(waybillNo))
                {
                    result.Errors.Add(new WaybillImportError
                    {
                        RowNumber = rowIndex + 1,
                        ErrorMessage = "运单号为空"
                    });
                    result.FailCount++;
                    continue;
                }

                // 去重：已存在于数据库或本次文件已出现
                if (existingSet.Contains(waybillNo) || seenInFile.Contains(waybillNo))
                {
                    result.SkipCount++;
                    continue;
                }
                seenInFile.Add(waybillNo);

                var shopName = GetCellString(row, columnMap, "店铺名称") ?? string.Empty;
                var senderProvince = GetCellString(row, columnMap, "寄件省");
                var destProvinceStr = GetCellString(row, columnMap, "目的省份");
                var waybillDateStr = GetCellString(row, columnMap, "运单日期");
                var clientAlias = GetCellString(row, columnMap, "客户别名");

                // 省份三级匹配
                int? receiverProvinceId = null;
                if (!string.IsNullOrWhiteSpace(destProvinceStr))
                {
                    receiverProvinceId = MatchProvince(destProvinceStr.Trim(), nameDict, shortNameDict, strippedDict);
                }

                // 解析日期
                DateTime waybillDate;
                if (!string.IsNullOrWhiteSpace(waybillDateStr))
                {
                    if (!DateTime.TryParse(waybillDateStr, out waybillDate))
                        waybillDate = DateTime.Now.Date;
                }
                else
                {
                    // 尝试从单元格获取日期值
                    waybillDate = GetCellDate(row, columnMap, "运单日期") ?? DateTime.Now.Date;
                }

                var entity = new ExpWaybill
                {
                    FWaybillNo = waybillNo,
                    FBrandCode = brandCode,
                    FShopName = shopName,
                    FSenderProvince = senderProvince,
                    FReceiverProvinceId = receiverProvinceId,
                    FPickupWeight = GetCellDecimal(row, columnMap, "揽收重量"),
                    FTransitWeight = GetCellDecimal(row, columnMap, "中转重量"),
                    FDeliveryWeight = GetCellDecimal(row, columnMap, "到件重量"),
                    FBagWeight = GetCellDecimal(row, columnMap, "集包重量"),
                    FBubbleWeight = GetCellDecimal(row, columnMap, "计泡重量"),
                    FHqWeight = GetCellDecimal(row, columnMap, "总部重量"),
                    FLength = GetCellDecimal(row, columnMap, "长"),
                    FWidth = GetCellDecimal(row, columnMap, "宽"),
                    FHeight = GetCellDecimal(row, columnMap, "高"),
                    FDeclaredValue = GetCellDecimal(row, columnMap, "声明价值"),
                    FWaybillDate = waybillDate,
                    FClientAlias = clientAlias,
                    FBillingStatus = 0,
                    FCreatedTime = DateTime.Now
                };

                batch.Add(entity);

                // 记录新店铺
                if (!string.IsNullOrWhiteSpace(shopName) && !existingShopSet.Contains(shopName))
                {
                    newShopNames.Add(shopName);
                }

                // 批量保存（每1000条）
                if (batch.Count >= 1000)
                {
                    await SaveBatchAsync(batch);
                    result.SuccessCount += batch.Count;
                    batch.Clear();
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add(new WaybillImportError
                {
                    RowNumber = rowIndex + 1,
                    ErrorMessage = ex.Message
                });
                result.FailCount++;
            }
        }

        // 保存剩余批次
        if (batch.Count > 0)
        {
            await SaveBatchAsync(batch);
            result.SuccessCount += batch.Count;
            batch.Clear();
        }

        result.NewShopsFound = newShopNames.Count;
        return result;
    }

    private async Task SaveBatchAsync(List<ExpWaybill> batch)
    {
        foreach (var entity in batch)
        {
            await _waybillRepository.AddAsync(entity);
        }
    }

    /// <summary>
    /// 省份三级匹配：精确Name → ShortName → 去后缀
    /// </summary>
    private static int? MatchProvince(
        string input,
        Dictionary<string, int> nameDict,
        Dictionary<string, int> shortNameDict,
        Dictionary<string, int> strippedDict)
    {
        // 1. 精确匹配 Name
        if (nameDict.TryGetValue(input, out var id1))
            return id1;

        // 2. 匹配 ShortName
        if (shortNameDict.TryGetValue(input, out var id2))
            return id2;

        // 3. 去后缀后匹配
        var stripped = StripProvinceSuffix(input);
        if (!string.IsNullOrWhiteSpace(stripped) && strippedDict.TryGetValue(stripped, out var id3))
            return id3;

        return null;
    }

    /// <summary>
    /// 去除省份后缀
    /// </summary>
    private static string StripProvinceSuffix(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;

        // 按长度降序匹配，确保先匹配更长的后缀
        string[] suffixes =
        [
            "维吾尔自治区", "壮族自治区", "回族自治区", "特别行政区", "自治区", "省", "市"
        ];

        foreach (var suffix in suffixes)
        {
            if (name.EndsWith(suffix))
                return name[..^suffix.Length];
        }

        return name;
    }

    private static string? GetCellString(IRow row, Dictionary<string, int> columnMap, string columnName)
    {
        if (!columnMap.TryGetValue(columnName, out var colIndex))
            return null;
        var cell = row.GetCell(colIndex);
        if (cell == null) return null;
        return cell.CellType switch
        {
            CellType.String => cell.StringCellValue?.Trim(),
            CellType.Numeric => cell.NumericCellValue.ToString(),
            CellType.Boolean => cell.BooleanCellValue.ToString(),
            _ => cell.ToString()?.Trim()
        };
    }

    private static decimal? GetCellDecimal(IRow row, Dictionary<string, int> columnMap, string columnName)
    {
        if (!columnMap.TryGetValue(columnName, out var colIndex))
            return null;
        var cell = row.GetCell(colIndex);
        if (cell == null) return null;
        return cell.CellType switch
        {
            CellType.Numeric => (decimal)cell.NumericCellValue,
            CellType.String when decimal.TryParse(cell.StringCellValue, out var v) => v,
            _ => null
        };
    }

    private static DateTime? GetCellDate(IRow row, Dictionary<string, int> columnMap, string columnName)
    {
        if (!columnMap.TryGetValue(columnName, out var colIndex))
            return null;
        var cell = row.GetCell(colIndex);
        if (cell == null) return null;
        try
        {
            if (cell.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
                return cell.DateCellValue;
            if (cell.CellType == CellType.String && DateTime.TryParse(cell.StringCellValue, out var dt))
                return dt;
        }
        catch { }
        return null;
    }
}
