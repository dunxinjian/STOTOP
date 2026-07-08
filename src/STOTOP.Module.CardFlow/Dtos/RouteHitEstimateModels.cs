namespace STOTOP.Module.CardFlow.Dtos;

/// <summary>路由条件命中率试算请求（设计 B4/E6）</summary>
public class RouteHitEstimateRequest
{
    /// <summary>条件 JSON（ConditionRuleEvaluator 口径的组/项结构）</summary>
    public string? ConditionJson { get; set; }
}

/// <summary>命中率试算结果：近 30 天采样卡片（上限 500 张）干跑条件</summary>
public class RouteHitEstimateDto
{
    /// <summary>采样卡片总数（0 = 无历史，前端显示冷启动灰态）</summary>
    public int Total { get; set; }

    /// <summary>条件引用字段全部有值的卡片数（&lt; Total 时前端降级为"历史覆盖不全"黄态）</summary>
    public int WithValue { get; set; }

    /// <summary>命中条件的卡片数</summary>
    public int Hit { get; set; }
}
