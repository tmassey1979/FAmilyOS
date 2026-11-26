using FamilyOS.Domain.Meals;

namespace FamilyOS.Application.Interfaces;

/// <summary>Provider-neutral search contract. Live provider credentials stay server-side.</summary>
public interface IGroceryStoreProvider
{
    string ProviderKey { get; }
    Task<IReadOnlyList<GroceryStoreSelection>> SearchStoresAsync(string query, CancellationToken cancellationToken);
}

public sealed record GroceryStoreSearchResult(
    string ProviderKey, string LocationId, string Name, string Address,
    string City, string Region, string PostalCode, double? Latitude, double? Longitude,
    bool IsDemo, string SourceLabel);
