using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Express.Entities;
using STOTOP.Module.Express.Models;

namespace STOTOP.Module.Express.Services.Billing;

/// <summary>
/// 报价缓存
/// 从 ExpQuotation.FMatrixJson 反序列化加载矩阵数据，替代原 ExpPriceWeightSegment + ExpPriceCell 两表查询。
/// 索引策略：城市单元格优先，其次 (QuotationId, SegmentIndex, ProvinceId) → PricingCell，支持省份降级匹配。
/// </summary>
public class PricePlanCache
{
    /// <summary>QuotationId → 重量段列表（按 WeightFrom 排序）</summary>
    private Dictionary<long, List<PricingSegment>> _segmentIndex = new();
    /// <summary>(QuotationId, SegmentIndex, ProvinceId) → PricingCell</summary>
    private Dictionary<(long, int, int), PricingCell> _cellIndex = new();
    /// <summary>(QuotationId, SegmentIndex, ProvinceId, CityName) → PricingCell</summary>
    private Dictionary<(long, int, int, string), PricingCell> _cityCellIndex = new();

    /// <summary>
    /// 从数据库加载所有生效报价的矩阵JSON
    /// </summary>
    public async Task LoadAsync(IRepository<ExpQuotation> quotationRepo, long orgId)
    {
        var quotations = await quotationRepo.Query()
            .Where(q => q.FStatus == 1 && q.FOrgId == orgId)
            .Select(q => new { q.FID, q.FMatrixJson })
            .ToListAsync();

        foreach (var q in quotations)
        {
            var matrix = PricingMatrixSerializer.Deserialize(q.FMatrixJson);

            // Segments 按 WeightFrom 升序，便于按计费重量顺序匹配
            var sortedSegments = matrix.Segments
                .OrderBy(s => s.WeightFrom ?? 0m)
                .ToList();
            _segmentIndex[q.FID] = sortedSegments;

            // 遍历每个 segment 的 cells 填充 _cellIndex
            foreach (var seg in sortedSegments)
            {
                foreach (var cell in seg.Cells)
                {
                    if (!string.IsNullOrWhiteSpace(cell.CityName))
                    {
                        _cityCellIndex[(q.FID, seg.SegmentIndex, cell.ProvinceId, NormalizeCityKeyword(cell.CityName))] = cell;
                        continue;
                    }

                    _cellIndex[(q.FID, seg.SegmentIndex, cell.ProvinceId)] = cell;
                }
            }
        }
    }

    /// <summary>
    /// 按重量范围查找匹配的重量段
    /// </summary>
    public PricingSegment? FindSegment(long quotationId, decimal billableWeight)
    {
        if (!_segmentIndex.TryGetValue(quotationId, out var segments))
            return null;

        foreach (var s in segments)
        {
            var from = s.WeightFrom ?? 0m;
            var to = s.WeightTo ?? decimal.MaxValue;
            if (billableWeight >= from && billableWeight < to)
                return s;
        }
        return null;
    }

    /// <summary>
    /// 查找矩阵单元格，Key 使用 SegmentIndex（而非 SegmentId）。
    /// 支持省份降级：精确匹配 → ProvinceId=0（全国统一价）
    /// </summary>
    public PricingCell? FindCell(long quotationId, int segmentIndex, int provinceId)
        => FindCell(quotationId, segmentIndex, provinceId, null);

    /// <summary>
    /// 查找矩阵单元格，城市单元格优先。
    /// 支持降级：城市精确匹配 → 省份 → ProvinceId=0（全国统一价）
    /// </summary>
    public PricingCell? FindCell(long quotationId, int segmentIndex, int provinceId, string? cityName)
    {
        if (!string.IsNullOrWhiteSpace(cityName))
        {
            var normalizedCity = NormalizeCityKeyword(cityName);
            if (_cityCellIndex.TryGetValue((quotationId, segmentIndex, provinceId, normalizedCity), out var cityCell))
                return cityCell;
        }

        // 1. 精确匹配省份
        if (_cellIndex.TryGetValue((quotationId, segmentIndex, provinceId), out var cell))
            return cell;

        // 2. 降级：尝试查找全国统一价（provinceId=0）
        if (provinceId != 0 && _cellIndex.TryGetValue((quotationId, segmentIndex, 0), out var fallbackCell))
            return fallbackCell;

        return null;
    }

    private static string NormalizeCityKeyword(string cityName)
    {
        var name = new string(cityName.Trim().Where(c => !char.IsWhiteSpace(c)).ToArray());
        return name.EndsWith("市", StringComparison.Ordinal) && name.Length > 1
            ? name[..^1]
            : name;
    }
}
