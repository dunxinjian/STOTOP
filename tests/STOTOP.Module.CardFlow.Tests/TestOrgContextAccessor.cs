using STOTOP.Core.Services;

namespace STOTOP.Module.CardFlow.Tests;

/// <summary>测试用组织上下文：可设当前组织以驱动全局过滤器与 FillOrgId。</summary>
public sealed class TestOrgContextAccessor : IOrgContextAccessor
{
    public TestOrgContextAccessor(long? currentOrgId = null) => CurrentOrgId = currentOrgId;
    public long? CurrentOrgId { get; set; }
}
