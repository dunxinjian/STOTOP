using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services.Billing;

/// <summary>
/// 省份名称→ID 缓存，支持全称、简称、去后缀模糊匹配
/// </summary>
public class ProvinceCache
{
    /// <summary>省份名称 → FID（同时包含全称和简称）</summary>
    private Dictionary<string, int> _nameToId = new();

    public async Task LoadAsync(IRepository<ExpProvince> provinceRepo)
    {
        var provinces = await provinceRepo.Query().ToListAsync();
        foreach (var p in provinces)
        {
            // 全称映射：如 "广东省" → id
            _nameToId[p.FName.Trim()] = p.FID;
            // 简称映射：如 "广东" → id
            if (!string.IsNullOrWhiteSpace(p.FShortName))
                _nameToId[p.FShortName.Trim()] = p.FID;
        }
    }

    /// <summary>
    /// 根据省份名称查找省份ID，支持三级匹配：
    /// 1. 全称/简称精确匹配
    /// 2. 去除"省/市/自治区/特别行政区"等后缀后匹配
    /// </summary>
    public int? FindId(string? provinceName)
    {
        if (string.IsNullOrWhiteSpace(provinceName))
            return null;

        var name = provinceName.Trim();

        // 第一级：精确匹配（全称或简称）
        if (_nameToId.TryGetValue(name, out var id))
            return id;

        // 第二级：去除后缀后匹配
        var trimmed = name
            .Replace("特别行政区", "")
            .Replace("维吾尔自治区", "")
            .Replace("壮族自治区", "")
            .Replace("回族自治区", "")
            .Replace("自治区", "");
        trimmed = trimmed.TrimEnd('省').TrimEnd('市');

        if (trimmed != name && _nameToId.TryGetValue(trimmed, out id))
            return id;

        return null;
    }
}
