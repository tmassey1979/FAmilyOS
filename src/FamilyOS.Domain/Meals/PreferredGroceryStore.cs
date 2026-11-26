using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Meals;

/// <summary>The family's selected grocery branch. Provider identifiers remain opaque to the domain.</summary>
public sealed class PreferredGroceryStore : Entity
{
    public Guid FamilyId { get; private set; }
    public string ProviderKey { get; private set; } = string.Empty;
    public string ProviderLocationId { get; private set; } = string.Empty;
    public string StoreName { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string Region { get; private set; } = string.Empty;
    public string PostalCode { get; private set; } = string.Empty;
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public bool IsDemo { get; private set; }
    public DateTime VerifiedAtUtc { get; private set; }

    private PreferredGroceryStore() { }

    public static PreferredGroceryStore Create(Guid familyId, GroceryStoreSelection selection)
    {
        if (familyId == Guid.Empty) throw new ArgumentException("A family is required.", nameof(familyId));
        var store = new PreferredGroceryStore { FamilyId = familyId };
        store.Update(selection);
        return store;
    }

    public void Update(GroceryStoreSelection selection)
    {
        if (string.IsNullOrWhiteSpace(selection.ProviderKey)
            || string.IsNullOrWhiteSpace(selection.LocationId)
            || string.IsNullOrWhiteSpace(selection.Name)
            || string.IsNullOrWhiteSpace(selection.Address))
            throw new ArgumentException("A provider, location ID, store name, and street address are required.");

        ProviderKey = selection.ProviderKey.Trim().ToLowerInvariant();
        ProviderLocationId = selection.LocationId.Trim();
        StoreName = selection.Name.Trim();
        Address = selection.Address.Trim();
        City = selection.City?.Trim() ?? string.Empty;
        Region = selection.Region?.Trim() ?? string.Empty;
        PostalCode = selection.PostalCode?.Trim() ?? string.Empty;
        Latitude = selection.Latitude;
        Longitude = selection.Longitude;
        IsDemo = selection.IsDemo;
        VerifiedAtUtc = DateTime.UtcNow;
        MarkUpdated();
    }
}

public sealed record GroceryStoreSelection(
    string ProviderKey, string LocationId, string Name, string Address,
    string? City = null, string? Region = null, string? PostalCode = null,
    double? Latitude = null, double? Longitude = null, bool IsDemo = false);
