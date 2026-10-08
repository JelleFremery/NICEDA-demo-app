using System.Collections.Frozen;

namespace NICEDA.ApiService.Security;

public sealed record DemoUser(
    string UserId,
    string DisplayName,
    bool IsAdministrator,
    IReadOnlySet<string> AllowedCustomerIds,
    IReadOnlySet<string> AllowedRegions)
{
    public bool CanAccess(string customerId, string? region)
    {
        if (IsAdministrator)
        {
            return true;
        }

        return AllowedCustomerIds.Contains(customerId)
            && (string.IsNullOrWhiteSpace(region)
                || AllowedRegions.Contains(region));
    }
}

public static class DemoIdentity
{
    private static readonly IReadOnlySet<string> JaneCustomerIds =
        new[] { "betabit", "fenetre", "techorama" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlySet<string> EuRegions =
        new[] { "EU" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlySet<string> NoCustomers =
        Array.Empty<string>().ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlySet<string> NoRegions =
        Array.Empty<string>().ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static DemoUser Resolve(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException("A demo user identity is required.");
        }

        if (string.Equals(userId, "admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(userId, "administrator", StringComparison.OrdinalIgnoreCase))
        {
            return new DemoUser("admin", "Admin", true, NoCustomers, NoRegions);
        }

        if (string.Equals(userId, "jane", StringComparison.OrdinalIgnoreCase)
            || string.Equals(userId, "jane.okafor@niceda.example", StringComparison.OrdinalIgnoreCase))
        {
            return new DemoUser("jane", "Jane", false, JaneCustomerIds, EuRegions);
        }

        throw new UnauthorizedAccessException($"Unknown demo user '{userId}'.");
    }
}
