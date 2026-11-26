using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Meals;

namespace FamilyOS.Infrastructure.GroceryStores;

/// <summary>
/// Explicitly marked demo data used until a real retailer developer account is configured.
/// This never claims that a branch is real or that its prices are live.
/// </summary>
public sealed class MockGroceryStoreProvider : IGroceryStoreProvider
{
    public string ProviderKey => "kroger";

    private static readonly GroceryStoreSelection[] DemoStores =
    [
        new("kroger", "demo-tremont-001", "Kroger — Tremont demo branch",
            "100 Sample Street", "Tremont", "IL", "61568", 40.5278, -89.4926, true),
        new("kroger", "demo-pekin-001", "Kroger — Pekin demo branch",
            "200 Sample Avenue", "Pekin", "IL", "61554", 40.5675, -89.6407, true),
        new("kroger", "demo-morton-001", "Kroger — Morton demo branch",
            "300 Sample Road", "Morton", "IL", "61550", 40.6120, -89.4590, true)
    ];

    public Task<IReadOnlyList<GroceryStoreSelection>> SearchStoresAsync(
        string query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult<IReadOnlyList<GroceryStoreSelection>>([]);
        var normalized = query.Trim();
        var matches = DemoStores.Where(x =>
            x.City.Contains(normalized, StringComparison.OrdinalIgnoreCase)
            || x.Region.Contains(normalized, StringComparison.OrdinalIgnoreCase)
            || x.PostalCode.Contains(normalized, StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(x.City, StringComparison.OrdinalIgnoreCase)).ToArray();
        return Task.FromResult<IReadOnlyList<GroceryStoreSelection>>(matches);
    }
}
