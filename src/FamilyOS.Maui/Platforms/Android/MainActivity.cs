using Android.App;
using Android.Content.PM;
using Android.Views;
using Android.Widget;

namespace FamilyOS.Maui;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize
        | ConfigChanges.Orientation
        | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Android.OS.Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Shell does not expose a public tab-bar height property. Size the native
        // Material bottom-navigation view after Shell has attached its content.
        Window?.DecorView?.Post(ApplyBottomNavigationSizing);
    }

    private void ApplyBottomNavigationSizing()
    {
        var root = Window?.DecorView;
        if (root is null) return;

        var bottomNavigation = FindBottomNavigation(root);
        if (bottomNavigation is null) return;

        var height = (int)Android.Util.TypedValue.ApplyDimension(
            Android.Util.ComplexUnitType.Dip, 84, Resources!.DisplayMetrics!);

        bottomNavigation.MinimumHeight = height;
        if (bottomNavigation.LayoutParameters is { } layout)
        {
            layout.Height = height;
            bottomNavigation.LayoutParameters = layout;
        }

        // Give each destination the full touch target and enlarge its icon
        // without changing Shell routing, labels, or selected-state behavior.
        for (var i = 0; i < bottomNavigation.ChildCount; i++)
        {
            if (bottomNavigation.GetChildAt(i) is not ViewGroup item) continue;
            item.MinimumHeight = height;
            SizeNavigationIcons(item);
        }

        bottomNavigation.RequestLayout();
    }

    private static ViewGroup? FindBottomNavigation(View view)
    {
        if (view is ViewGroup group)
        {
            if (view.GetType().FullName?.Contains("BottomNavigationView", StringComparison.Ordinal) == true)
                return group;

            for (var i = 0; i < group.ChildCount; i++)
            {
                var found = FindBottomNavigation(group.GetChildAt(i)!);
                if (found is not null) return found;
            }
        }

        return null;
    }

    private int _navigationIconSize;

    private void SizeNavigationIcons(View view)
    {
        if (view is ImageView image)
        {
            _navigationIconSize = (int)Android.Util.TypedValue.ApplyDimension(
                Android.Util.ComplexUnitType.Dip, 28, Resources!.DisplayMetrics!);
            image.LayoutParameters = new ViewGroup.LayoutParams(_navigationIconSize, _navigationIconSize);
            return;
        }

        if (view is ViewGroup group)
        {
            for (var i = 0; i < group.ChildCount; i++)
                SizeNavigationIcons(group.GetChildAt(i)!);
        }
    }
}
