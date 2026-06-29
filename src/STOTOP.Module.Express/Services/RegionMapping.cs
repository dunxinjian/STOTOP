namespace STOTOP.Module.Express.Services;

/// <summary>
/// 申通出港大区映射（流量流向分析用）
/// 与申通总部计费区域一致；省份ID 与 EXP省份 表 FID 对应（详见 ExpressSeeder）。
/// </summary>
public static class RegionMapping
{
    // 大区定义常量
    public const string RegionLocal = "省内";
    public const string Region1 = "一区";
    public const string Region2 = "二区";
    public const string Region3 = "三区";
    public const string Region4 = "四区";
    public const string Region5 = "五区";
    public const string RegionUnknown = "未知";

    // 省份ID到大区的映射字典
    // 注：FID 由 EXP省份 表按种子插入顺序自增分配
    private static readonly Dictionary<int, string> _regionMap = new()
    {
        // 一区：江苏、浙江、上海
        { 9,  Region1 }, // 上海
        { 10, Region1 }, // 江苏
        { 11, Region1 }, // 浙江

        // 二区：安徽、山东、广东、北京、天津、河北、河南、湖北、湖南、江西、福建
        { 1,  Region2 }, // 北京
        { 2,  Region2 }, // 天津
        { 3,  Region2 }, // 河北
        { 12, Region2 }, // 安徽
        { 13, Region2 }, // 福建
        { 14, Region2 }, // 江西
        { 15, Region2 }, // 山东
        { 16, Region2 }, // 河南
        { 17, Region2 }, // 湖北
        { 18, Region2 }, // 湖南
        { 19, Region2 }, // 广东

        // 三区：辽宁、吉林、黑龙江、山西、陕西、甘肃、宁夏、内蒙古、青海
        { 4,  Region3 }, // 山西
        { 5,  Region3 }, // 内蒙古
        { 6,  Region3 }, // 辽宁
        { 7,  Region3 }, // 吉林
        { 8,  Region3 }, // 黑龙江
        { 27, Region3 }, // 陕西
        { 28, Region3 }, // 甘肃
        { 29, Region3 }, // 青海
        { 30, Region3 }, // 宁夏

        // 四区：四川、重庆、云南、贵州、广西、海南
        { 20, Region4 }, // 广西
        { 21, Region4 }, // 海南
        { 22, Region4 }, // 重庆
        { 23, Region4 }, // 四川
        { 24, Region4 }, // 贵州
        { 25, Region4 }, // 云南

        // 五区：新疆、西藏
        { 26, Region5 }, // 西藏
        { 31, Region5 }, // 新疆
    };

    /// <summary>
    /// 获取大区名称
    /// </summary>
    /// <param name="receiverProvinceId">目的省份ID</param>
    /// <param name="senderProvinceId">寄件省份ID（太仓为江苏的ID=10）</param>
    /// <returns>大区名称：省内 / 一区 / 二区 / 三区 / 四区 / 五区 / 未知</returns>
    public static string GetRegion(int receiverProvinceId, int senderProvinceId)
    {
        if (receiverProvinceId <= 0)
            return RegionUnknown;

        if (receiverProvinceId == senderProvinceId)
            return RegionLocal;

        return _regionMap.TryGetValue(receiverProvinceId, out var region) ? region : RegionUnknown;
    }

    /// <summary>
    /// 获取大区下的所有省份ID列表
    /// </summary>
    /// <param name="region">大区名称（一区/二区/三区/四区/五区）</param>
    /// <returns>该大区包含的省份ID集合；省内/未知/不存在的大区均返回空列表</returns>
    public static List<int> GetProvinceIdsByRegion(string region)
    {
        if (string.IsNullOrWhiteSpace(region))
            return new List<int>();

        return _regionMap
            .Where(kv => kv.Value == region)
            .Select(kv => kv.Key)
            .OrderBy(id => id)
            .ToList();
    }

    /// <summary>
    /// 已映射到大区的全部省份ID（不含"省内"判定，省内由寄件省动态决定）
    /// </summary>
    public static IReadOnlyCollection<int> MappedProvinceIds => _regionMap.Keys;

    /// <summary>
    /// 获取所有大区名称列表（按顺序：省内→一→二→三→四→五）
    /// </summary>
    public static readonly string[] AllRegions =
    {
        RegionLocal, Region1, Region2, Region3, Region4, Region5
    };
}
