using STOTOP.Module.CardFlow.Models;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public sealed record UserMemberships(HashSet<long> RoleIds, HashSet<long> OrgIds, HashSet<long> PositionIds);

public interface IInitiatorScopeResolver
{
    Task<UserMemberships> GetUserMembershipsAsync(long userId, CancellationToken ct = default);
    bool IsInScope(UserMemberships memberships, long userId, InitiatorScope? scope);
}
