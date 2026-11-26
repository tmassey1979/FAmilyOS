using Microsoft.Maui.Storage;

namespace FamilyOS.Maui.Services;

/// <summary>
/// Purchase-provider boundary for onboarding. Replace the demo implementation with
/// Google Play Billing when store products and server verification are configured.
/// </summary>
public interface ISubscriptionPurchaseService
{
    Task<SubscriptionPlan?> GetCurrentPlanAsync(CancellationToken ct = default);
    Task<SubscriptionPlan> ActivateTestPlanAsync(SubscriptionPlan plan, CancellationToken ct = default);
    Task ClearTestPlanAsync(CancellationToken ct = default);
}

public sealed record SubscriptionPlan(bool IsAnnual, bool LocationEnabled, bool FinanceEnabled)
{
    public decimal CorePrice => IsAnnual ? 89m : 9.99m;
    public decimal LocationPrice => !LocationEnabled ? 0m : FinanceEnabled
        ? (IsAnnual ? 79m : 8.99m) - 0m
        : IsAnnual ? 39m : 4.99m;
    public decimal FinancePrice => !FinanceEnabled || LocationEnabled
        ? 0m
        : IsAnnual ? 49m : 5.99m;
    public decimal TotalPrice => CorePrice + LocationPrice + FinancePrice;
    public string BillingLabel => IsAnnual ? "year" : "month";
    public string ModulesLabel => (LocationEnabled, FinanceEnabled) switch
    {
        (true, true) => "Core + Location + Finance bundle",
        (true, false) => "Core + Location",
        (false, true) => "Core + Finance",
        _ => "Core"
    };
}

/// <summary>
/// Local-only simulator. It never contacts a payment provider or grants server entitlements.
/// The explicit test-mode boundary makes it safe to replace with a real Play implementation.
/// </summary>
public sealed class DemoSubscriptionPurchaseService : ISubscriptionPurchaseService
{
    private const string ActiveKey = "familyos.demo_subscription.active";
    private const string AnnualKey = "familyos.demo_subscription.annual";
    private const string LocationKey = "familyos.demo_subscription.location";
    private const string FinanceKey = "familyos.demo_subscription.finance";

    public Task<SubscriptionPlan?> GetCurrentPlanAsync(CancellationToken ct = default)
    {
        if (!Preferences.Get(ActiveKey, false))
            return Task.FromResult<SubscriptionPlan?>(null);

        return Task.FromResult<SubscriptionPlan?>(new SubscriptionPlan(
            Preferences.Get(AnnualKey, false),
            Preferences.Get(LocationKey, false),
            Preferences.Get(FinanceKey, false)));
    }

    public Task<SubscriptionPlan> ActivateTestPlanAsync(SubscriptionPlan plan, CancellationToken ct = default)
    {
        Preferences.Set(AnnualKey, plan.IsAnnual);
        Preferences.Set(LocationKey, plan.LocationEnabled);
        Preferences.Set(FinanceKey, plan.FinanceEnabled);
        Preferences.Set(ActiveKey, true);
        return Task.FromResult(plan);
    }

    public Task ClearTestPlanAsync(CancellationToken ct = default)
    {
        Preferences.Remove(ActiveKey);
        Preferences.Remove(AnnualKey);
        Preferences.Remove(LocationKey);
        Preferences.Remove(FinanceKey);
        return Task.CompletedTask;
    }
}
