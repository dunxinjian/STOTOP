using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Services;

/// <summary>
/// 企业信息服务实现
/// 使用 SYS系统参数 表存储企业信息配置
/// </summary>
public class EnterpriseInfoService : IEnterpriseInfoService
{
    private readonly STOTOPDbContext _context;

    // 配置键名
    private const string KeyName = "enterprise.name";
    private const string KeyShortName = "enterprise.shortName";
    private const string KeyLogo = "enterprise.logo";

    // 默认值
    private const string DefaultName = "MDSTO";
    private const string DefaultShortName = "MDSTO";

    public EnterpriseInfoService(STOTOPDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResult<EnterpriseInfoDto>> GetEnterpriseInfoAsync()
    {
        var configs = await _context.Set<SysConfig>()
            .Where(c => c.FKey == KeyName || c.FKey == KeyShortName || c.FKey == KeyLogo)
            .ToListAsync();

        var dto = new EnterpriseInfoDto
        {
            Name = GetValue(configs, KeyName, DefaultName) ?? DefaultName,
            ShortName = GetValue(configs, KeyShortName, DefaultShortName) ?? DefaultShortName,
            LogoUrl = GetValue(configs, KeyLogo, null)
        };

        return ApiResult<EnterpriseInfoDto>.Success(dto);
    }

    public async Task<ApiResult<EnterpriseInfoDto>> UpdateEnterpriseInfoAsync(EnterpriseInfoUpdateDto dto)
    {
        await SetConfigValueAsync(KeyName, dto.Name, "string", "企业全称");
        await SetConfigValueAsync(KeyShortName, dto.ShortName, "string", "企业简称");
        await SetConfigValueAsync(KeyLogo, dto.LogoUrl ?? "", "string", "Logo图片URL");

        await _context.SaveChangesAsync();

        return await GetEnterpriseInfoAsync();
    }

    private static string? GetValue(List<SysConfig> configs, string key, string? defaultValue)
    {
        var config = configs.FirstOrDefault(c => c.FKey == key);
        return config?.FValue ?? defaultValue;
    }

    private async Task SetConfigValueAsync(string key, string value, string dataType, string description)
    {
        var config = await _context.Set<SysConfig>()
            .FirstOrDefaultAsync(c => c.FKey == key);

        if (config == null)
        {
            config = new SysConfig
            {
                FKey = key,
                FValue = value,
                FDataType = dataType,
                FDescription = description,
                FIsBuiltIn = true,
                FCreateTime = DateTime.Now
            };
            _context.Set<SysConfig>().Add(config);
        }
        else
        {
            config.FValue = value;
            config.FUpdateTime = DateTime.Now;
        }
    }
}
