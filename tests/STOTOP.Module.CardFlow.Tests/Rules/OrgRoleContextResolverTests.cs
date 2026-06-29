using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class OrgRoleContextResolverTests
{
    [Fact]
    public void BuildOrgChain_LinearHierarchy_WalksUpIncludingSelf()
    {
        var chain = OrgRoleContextResolver.BuildOrgChain(
            new[] { (3L, 2L), (2L, 1L), (1L, 0L) }, startOrgId: 3L);
        Assert.Equal(new[] { "3", "2", "1" }, chain);
    }

    [Fact]
    public void BuildOrgChain_StartNotInOrgs_ReturnsEmpty()
    {
        var chain = OrgRoleContextResolver.BuildOrgChain(
            new[] { (1L, 0L) }, startOrgId: 99L);
        Assert.Empty(chain);
    }

    [Fact]
    public void BuildOrgChain_CycleInParents_DoesNotLoopForever()
    {
        var chain = OrgRoleContextResolver.BuildOrgChain(
            new[] { (1L, 2L), (2L, 1L) }, startOrgId: 1L);
        Assert.Equal(new[] { "1", "2" }, chain);
    }

    [Fact]
    public void BuildOrgChain_SingleOrg_ReturnsSelf()
    {
        var chain = OrgRoleContextResolver.BuildOrgChain(
            new[] { (1L, 0L) }, startOrgId: 1L);
        Assert.Equal(new[] { "1" }, chain);
    }
}
