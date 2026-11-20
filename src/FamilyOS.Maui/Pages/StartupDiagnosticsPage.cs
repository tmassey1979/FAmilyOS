using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

public sealed class StartupDiagnosticsPage : ContentPage
{
    private readonly StartupDiagnosticsService _diagnostics;
    private readonly Editor _report = new()
    {
        IsReadOnly = true,
        AutoSize = EditorAutoSizeOption.Disabled,
        HeightRequest = 420,
        FontFamily = "monospace",
        FontSize = 11
    };
    private readonly Label _documentsStatus = new() { FontSize = 12 };

    public StartupDiagnosticsPage(StartupDiagnosticsService diagnostics)
    {
        _diagnostics = diagnostics;
        Title = "Startup diagnostics";

        var refresh = new Button { Text = "Refresh report" };
        refresh.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        refresh.Clicked += (_, _) => LoadReport();

        var copy = new Button { Text = "Copy report" };
        copy.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        copy.Clicked += async (_, _) =>
        {
            await Clipboard.Default.SetTextAsync(_diagnostics.GetReport());
            await DisplayAlertAsync("Copied", "The diagnostic report is on your clipboard.", "OK");
        };

        var share = new Button { Text = "Share report" };
        share.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        share.Clicked += async (_, _) =>
        {
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = "FamilyOS startup diagnostics",
                Text = _diagnostics.GetReport()
            });
        };

        var clear = new Button { Text = "Clear report", TextColor = Colors.Red };
        clear.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        clear.Clicked += async (_, _) =>
        {
            if (!await DisplayAlertAsync("Clear diagnostics",
                    "Delete the locally stored startup and crash history?", "Clear", "Cancel"))
                return;
            _diagnostics.Clear();
            LoadReport();
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 18,
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Startup diagnostics", FontSize = 24, FontAttributes = FontAttributes.Bold },
                    new Label
                    {
                        Text = "Local launch checkpoints and exception details. If FamilyOS crashes, reopen it and copy or share this report. Review it before sharing because exception text can contain technical or account-related details.",
                        FontSize = 13
                    },
                    _documentsStatus,
                    new HorizontalStackLayout { Spacing = 8, Children = { refresh, copy } },
                    share,
                    _report,
                    clear
                }
            }
        };

        LoadReport();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadReport();
    }

    private void LoadReport()
    {
        _report.Text = _diagnostics.GetReport();
        _documentsStatus.Text = _diagnostics.GetDocumentsStatus();
    }
}
