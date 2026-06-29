using System.Text.Json;
using System.Text.Json.Serialization;

namespace STOTOP.Module.Express.Models;

/// <summary>
/// 报价矩阵根对象（一口价成本 / 快递报价 通用）
/// </summary>
public class PricingMatrix
{
    [JsonPropertyName("segments")]
    public List<PricingSegment> Segments { get; set; } = new();
}

/// <summary>
/// 重量段定义
/// </summary>
public class PricingSegment
{
    [JsonPropertyName("calcMethod")]
    public int CalcMethod { get; set; }

    [JsonPropertyName("segmentIndex")]
    public int SegmentIndex { get; set; }

    [JsonPropertyName("weightFrom")]
    public decimal? WeightFrom { get; set; }

    [JsonPropertyName("weightTo")]
    public decimal? WeightTo { get; set; }

    /// <summary>
    /// 舍位方式：1=向上取整 2=向下取整 3=四舍五入 4=截断
    /// </summary>
    [JsonPropertyName("roundingMethod")]
    public int RoundingMethod { get; set; } = 1;

    /// <summary>
    /// 舍位参数（截断精度，如0.1表示精确到0.1kg）
    /// </summary>
    [JsonPropertyName("truncParam")]
    public decimal? TruncParam { get; set; }

    /// <summary>
    /// 进位参数（向上取整精度，如1表示按1kg进位）
    /// </summary>
    [JsonPropertyName("ceilParam")]
    public decimal? CeilParam { get; set; }

    [JsonPropertyName("cells")]
    public List<PricingCell> Cells { get; set; } = new();
}

/// <summary>
/// 价格单元格
/// </summary>
public class PricingCell
{
    [JsonPropertyName("provinceId")]
    public int ProvinceId { get; set; }

    /// <summary>
    /// 城市ID（城市加收模式下使用）
    /// </summary>
    [JsonPropertyName("cityId")]
    public int? CityId { get; set; }

    /// <summary>
    /// 首重价格/基础价格（a），非空，默认0
    /// </summary>
    [JsonPropertyName("basePrice")]
    public decimal BasePrice { get; set; }

    /// <summary>
    /// 续重价格（b），非空，默认0。旧JSON null → 0
    /// </summary>
    [JsonPropertyName("continuePrice")]
    [JsonConverter(typeof(NullToDefaultConverter<decimal>))]
    public decimal ContinuePrice { get; set; }

    /// <summary>
    /// 首重重量（x），非空，默认0。旧JSON null → 0
    /// 统一公式：运费 = a + CEIL(MAX(0, w-x) / s) * b
    /// </summary>
    [JsonPropertyName("firstWeight")]
    [JsonConverter(typeof(NullToDefaultConverter<decimal>))]
    public decimal FirstWeight { get; set; }

    /// <summary>
    /// 续重步进（s），非空，默认1。旧JSON null → 1
    /// 统一公式：运费 = a + CEIL(MAX(0, w-x) / s) * b
    /// </summary>
    [JsonPropertyName("continueStep")]
    [JsonConverter(typeof(NullToOneDecimalConverter))]
    public decimal ContinueStep { get; set; } = 1;

    /// <summary>
    /// 舍位方式覆盖（单元格级）
    /// </summary>
    [JsonPropertyName("roundingMethodOverride")]
    public int? RoundingMethodOverride { get; set; }

    /// <summary>
    /// 截断参数覆盖（单元格级）
    /// </summary>
    [JsonPropertyName("truncParamOverride")]
    public decimal? TruncParamOverride { get; set; }

    /// <summary>
    /// 进位参数覆盖（单元格级）
    /// </summary>
    [JsonPropertyName("ceilParamOverride")]
    public decimal? CeilParamOverride { get; set; }

    /// <summary>
    /// 成本方案城市级定价
    /// </summary>
    [JsonPropertyName("cityName")]
    public string? CityName { get; set; }
}

/// <summary>
/// 成本方案矩阵（包裹多个成本项目的PricingMatrix）
/// </summary>
public class CostPlanMatrix
{
    [JsonPropertyName("costItems")]
    public List<CostItemEntry> CostItems { get; set; } = new();
}

/// <summary>
/// 成本项目条目（成本方案中每个成本项独立拥有一组重量段）
/// </summary>
public class CostItemEntry
{
    [JsonPropertyName("costItemId")]
    public int CostItemId { get; set; }

    /// <summary>
    /// 定价范围：national=全国单价, province=省份矩阵, city=城市加收
    /// </summary>
    [JsonPropertyName("pricingScope")]
    public string PricingScope { get; set; } = "province";

    [JsonPropertyName("segments")]
    public List<PricingSegment> Segments { get; set; } = new();
}

/// <summary>
/// JSON null → default(T) 转换器，处理旧JSON中 nullable 字段反序列化为 non-nullable 类型
/// </summary>
public class NullToDefaultConverter<T> : JsonConverter<T> where T : struct
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return default;
        return JsonSerializer.Deserialize<T>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}

/// <summary>
/// JSON null → 1 转换器，专用于 ContinueStep 字段（步进默认值为1而非0）
/// </summary>
public class NullToOneDecimalConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return 1;
        return reader.GetDecimal();
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

/// <summary>
/// 统一序列化/反序列化工具
/// </summary>
public static class PricingMatrixSerializer
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public static PricingMatrix Deserialize(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? new PricingMatrix()
            : JsonSerializer.Deserialize<PricingMatrix>(json, Options) ?? new();

    public static string Serialize(PricingMatrix matrix)
        => JsonSerializer.Serialize(matrix, Options);

    public static CostPlanMatrix DeserializeCostPlan(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? new CostPlanMatrix()
            : JsonSerializer.Deserialize<CostPlanMatrix>(json, Options) ?? new();

    public static string SerializeCostPlan(CostPlanMatrix matrix)
        => JsonSerializer.Serialize(matrix, Options);
}