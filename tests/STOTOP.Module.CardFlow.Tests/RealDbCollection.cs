using Xunit;

namespace STOTOP.Module.CardFlow.Tests;

/// <summary>
/// 真库集成测试串行化标记。
///
/// 背景（flaky 根因）：多个真库集成测试类都连同一个共享开发库 stotop，且其中
/// LogisticsCompletenessImportIntegrationTests 与 ShentongQualityBatchE2ETests.Part3
/// 会导入同一个文件（excel (未到件).xls）的同 15 行进同一张表 [STG申通_物流完整性明细]，
/// 去重键同为「F运单号+F问题类型」。xUnit 默认跨测试类并行执行，两类一旦并行：
/// 谁先写入这 15 行，另一类的「首次导入」就会全部命中跨批次去重 → 插件返回
/// 「未产生新数据」soft-fail → Assert.True(result.Success) 失败。先到者非确定，
/// 故失败测试在不同 run 间随机切换（典型并行竞态指纹）。各自按 F批次ID 清理也救不了——
/// 冲突发生在两者「同时在途」期间，跨批去重按运单号命中对方在途行。
///
/// 修复：把所有访问共享 stotop 库的真库集成测试类归入同一个 Collection，并
/// DisableParallelization=true，使它们彼此串行。纯 InMemory / 纯函数单测不带此标记，
/// 仍按 xUnit 默认跨类并行，整体测试时长基本不受影响。
///
/// 残留自愈（机制 B）：实现 <see cref="ICollectionFixture{T}"/>&lt;<see cref="ShentongStgResidueResetFixture"/>&gt;，
/// 使 xUnit 在本 collection 任何测试运行前一次性清空 org=192 的 [STG申通_*]/[QL申通_*] 残留——
/// 根治被中断进程留下的孤儿残留命中「跨批次去重」导致首次导入被整批跳过的假阴性。详见 <see cref="ShentongStgResidueReset"/>。
/// </summary>
[CollectionDefinition("StotopRealDb", DisableParallelization = true)]
public sealed class StotopRealDbCollection : ICollectionFixture<ShentongStgResidueResetFixture>
{
    // [CollectionDefinition] 载体 + 机制 B 基线重置 fixture（跨轮自愈）。
}
