namespace STOTOP.Module.CardFlow.Services;

public static class RouteGraphValidator
{
    /// <summary>
    /// nodeKeys：全部 stage key（去空去重）。entryKey：首 sortOrder 节点 key。
    /// edges：active 路由规则 (from, to)。返回错误列表（空 = 通过）。
    /// 先 Kahn 环检测（阻断所有环、短路）；无环（DAG）才从入口 BFS 查可达性。
    /// 所有 key 比较大小写不敏感。
    /// </summary>
    public static IReadOnlyList<string> Validate(
        IReadOnlyCollection<string> nodeKeys,
        string? entryKey,
        IReadOnlyCollection<(string From, string To)> edges)
    {
        var nodes = nodeKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (nodes.Count == 0)
            return Array.Empty<string>();

        // 防御性过滤：只保留两端都在 nodes 里的边（来源/目标存在性由 ValidateRouteRulesAsync 既有校验先保证）
        var validEdges = edges
            .Where(e => !string.IsNullOrWhiteSpace(e.From) && !string.IsNullOrWhiteSpace(e.To)
                && nodes.Contains(e.From) && nodes.Contains(e.To))
            .ToList();

        var inDegree = nodes.ToDictionary(n => n, _ => 0, StringComparer.OrdinalIgnoreCase);
        var adj = nodes.ToDictionary(n => n, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var (from, to) in validEdges)
        {
            inDegree[to] += 1;
            adj[from].Add(to);
        }

        // ① Kahn 环检测（消耗 inDegree；结束后仍 >0 者即环及其下游）
        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var visited = 0;
        while (queue.Count > 0)
        {
            var n = queue.Dequeue();
            visited++;
            foreach (var next in adj[n])
            {
                inDegree[next] -= 1;
                if (inDegree[next] == 0) queue.Enqueue(next);
            }
        }
        if (visited < nodes.Count)
        {
            var cyclic = inDegree.Where(kv => kv.Value > 0).Select(kv => kv.Key)
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
            return new List<string> { $"流程路由存在环，涉及节点：{string.Join("、", cyclic)}" };
        }

        // ② 从入口 BFS 可达性
        var errors = new List<string>();
        if (!string.IsNullOrWhiteSpace(entryKey) && nodes.Contains(entryKey))
        {
            var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { entryKey };
            var bfs = new Queue<string>();
            bfs.Enqueue(entryKey);
            while (bfs.Count > 0)
            {
                foreach (var next in adj[bfs.Dequeue()])
                    if (reachable.Add(next)) bfs.Enqueue(next);
            }
            var unreachable = nodes.Where(n => !reachable.Contains(n))
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
            if (unreachable.Count > 0)
                errors.Add($"流程存在从起点不可达的节点：{string.Join("、", unreachable)}");
        }

        return errors;
    }
}
