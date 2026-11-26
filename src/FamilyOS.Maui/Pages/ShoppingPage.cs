using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>Household shopping queue, active store carts, and purchase history entry point.</summary>
public sealed class ShoppingPage : ContentPage
{
    private readonly ProcurementApiClient _api;
    private readonly VerticalStackLayout _queue = new() { Spacing = 10 };
    private readonly VerticalStackLayout _carts = new() { Spacing = 10 };
    private readonly Label _status = new() { FontSize = 12 };
    private bool _loading;

    public ShoppingPage(ProcurementApiClient api)
    {
        _api = api;
        Title = "Shopping";
        _status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");

        var heading = new Label { Text = "Shopping", FontSize = 28, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");
        var subtitle = new Label { Text = "One shared list for the household. Add what you need, group a trip by store, and check things off as you buy them.", FontSize = 13, LineBreakMode = LineBreakMode.WordWrap };
        subtitle.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");

        var add = new Button { Text = "＋ Add an item" };
        add.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        add.Clicked += async (_, _) => await AddItemAsync();

        var refresh = new Button { Text = "Refresh" };
        refresh.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        refresh.Clicked += async (_, _) => await LoadAsync();

        var queueTitle = SectionTitle("Shopping list");
        var cartTitle = SectionTitle("Store trips");
        var createCart = new Button { Text = "＋ Start a store trip" };
        createCart.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        createCart.Clicked += async (_, _) => await CreateCartAsync();

        Content = new RefreshView
        {
            Command = new Command(async () => await LoadAsync()),
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(18, 14, 18, 28),
                    Spacing = 13,
                    Children = { heading, subtitle, add, refresh, _status, queueTitle, _queue, cartTitle, createCart, _carts }
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_loading) await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        _status.Text = "Loading household shopping…";
        try
        {
            var queueTask = _api.GetQueueAsync();
            var cartsTask = _api.GetCartsAsync();
            await Task.WhenAll(queueTask, cartsTask);
            _queue.Children.Clear();
            _carts.Children.Clear();
            var items = await queueTask;
            var carts = await cartsTask;

            if (items.Count == 0)
                _queue.Children.Add(EmptyCard("Your shopping list is clear", "Add groceries, household supplies, or anything the family needs."));
            else
                foreach (var item in items)
                    _queue.Children.Add(BuildQueueItem(item, carts));

            if (carts.Count == 0)
                _carts.Children.Add(EmptyCard("No store trips yet", "Start a trip when you are ready to shop."));
            else
                foreach (var cart in carts)
                    _carts.Children.Add(BuildCart(cart));

            _status.Text = $"{items.Count} item{(items.Count == 1 ? "" : "s")} waiting · {carts.Sum(c => c.Items.Count)} in active trips";
        }
        catch (Exception ex)
        {
            _status.Text = ex is FamilyApiException ? ex.Message : "Could not load shopping. Check your connection and try again.";
        }
        finally { _loading = false; }
    }

    private View BuildQueueItem(ProcurementItemDto item, IReadOnlyList<CartDto> carts)
    {
        var details = string.Join(" · ", new[] { item.Brand, item.Size, item.Category, item.Quantity is decimal q ? $"Qty {q:0.##}" : null }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var stack = new VerticalStackLayout { Spacing = 5 };
        var name = new Label { Text = item.Name, FontSize = 16, FontAttributes = FontAttributes.Bold };
        name.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
        stack.Children.Add(name);
        if (!string.IsNullOrWhiteSpace(details))
        {
            var meta = new Label { Text = details, FontSize = 11 };
            meta.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
            stack.Children.Add(meta);
        }
        if (item.EstimatedPrice is decimal price)
        {
            var cost = new Label { Text = $"Est. {price:C}", FontSize = 12 };
            cost.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
            stack.Children.Add(cost);
        }
        var actions = new FlexLayout { Wrap = FlexWrap.Wrap, Direction = FlexDirection.Row };
        if (carts.Count > 0)
        {
            var cartPicker = new Picker { Title = "Add to trip", WidthRequest = 170, FontSize = 12 };
            foreach (var cart in carts) cartPicker.Items.Add(cart.StoreName);
            var addToCart = new Button { Text = "Add to trip", FontSize = 11 };
            addToCart.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
            addToCart.Clicked += async (_, _) =>
            {
                if (cartPicker.SelectedIndex < 0) { await DisplayAlertAsync("Choose a store", "Select a store trip first.", "OK"); return; }
                await RunItemActionAsync(() => _api.AddToCartAsync(item.Id, carts[cartPicker.SelectedIndex].Id));
            };
            actions.Children.Add(cartPicker);
            actions.Children.Add(addToCart);
        }
        var defer = new Button { Text = "Later", FontSize = 11 };
        defer.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        defer.Clicked += async (_, _) => await RunItemActionAsync(() => _api.DeferAsync(item.Id));
        var hold = new Button { Text = "Hold", FontSize = 11 };
        hold.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        hold.Clicked += async (_, _) => await RunItemActionAsync(() => _api.HoldAsync(item.Id));
        var done = new Button { Text = "Remove", FontSize = 11 };
        done.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        done.Clicked += async (_, _) => await RunItemActionAsync(() => _api.NoLongerNeededAsync(item.Id));
        actions.Children.Add(defer);
        actions.Children.Add(hold);
        actions.Children.Add(done);
        stack.Children.Add(actions);
        return Card(stack);
    }

    private View BuildCart(CartDto cart)
    {
        var stack = new VerticalStackLayout { Spacing = 8 };
        var title = new Label { Text = cart.StoreName, FontSize = 17, FontAttributes = FontAttributes.Bold };
        title.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
        stack.Children.Add(title);
        if (cart.Items.Count == 0)
            stack.Children.Add(new Label { Text = "No items in this trip yet.", FontSize = 12 });
        foreach (var item in cart.Items)
        {
            var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
            row.Add(new Label { Text = $"{item.Name}{(item.Quantity is decimal q ? $"  × {q:0.##}" : "")}", VerticalTextAlignment = TextAlignment.Center, FontSize = 13 }, 0, 0);
            var purchased = new Button { Text = "Bought", FontSize = 11 };
            purchased.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
            purchased.Clicked += async (_, _) => await MarkPurchasedAsync(item, cart.StoreName);
            row.Add(purchased, 1, 0);
            stack.Children.Add(row);
        }
        if (cart.Items.Count > 0)
        {
            var abandon = new Button { Text = "End this trip", FontSize = 11 };
            abandon.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
            abandon.Clicked += async (_, _) =>
            {
                if (await DisplayAlertAsync("End store trip?", "Items will return to the household shopping list.", "End trip", "Keep trip"))
                    await RunActionAsync(() => _api.AbandonCartAsync(cart.Id));
            };
            stack.Children.Add(abandon);
        }
        return Card(stack);
    }

    private async Task AddItemAsync()
    {
        var name = new Entry { Placeholder = "What do you need?", ReturnType = ReturnType.Next };
        var quantity = new Entry { Placeholder = "1", Keyboard = Keyboard.Numeric, Text = "1", WidthRequest = 90 };
        var brand = new Entry { Placeholder = "Brand (optional)" };
        var size = new Entry { Placeholder = "Size (optional)" };
        var category = new Picker { Title = "Category (optional)" };
        foreach (var value in new[] { "Groceries", "Produce", "Dairy", "Meat", "Frozen", "Pantry", "Household", "Personal care", "Pet", "Other" }) category.Items.Add(value);
        var store = new Entry { Placeholder = "Preferred store (optional)" };
        var price = new Entry { Placeholder = "Estimated price (optional)", Keyboard = Keyboard.Numeric };
        var form = new VerticalStackLayout { Spacing = 10, Children = { name, quantity, brand, size, category, store, price } };
        var scroll = new ScrollView { Content = form, HeightRequest = 380 };
        // Keep item creation together in one compact form.
        var modal = new ContentPage
        {
            Title = "Add shopping item",
            Content = new VerticalStackLayout
            {
                Padding = 18, Spacing = 12,
                Children =
                {
                    new Label { Text = "Add to the family list", FontSize = 22, FontAttributes = FontAttributes.Bold },
                    scroll,
                    new Button { Text = "Save item", Command = new Command(async () =>
                    {
                        if (string.IsNullOrWhiteSpace(name.Text)) { await DisplayAlertAsync("Item name required", "Enter an item to add.", "OK"); return; }
                        decimal? qty = decimal.TryParse(quantity.Text, out var q) ? q : 1;
                        decimal? est = decimal.TryParse(price.Text, out var p) ? p : null;
                        try
                        {
                            await _api.CreateItemAsync(new CreateQueueItemDto(name.Text.Trim(), brand.Text, size.Text,
                                category.SelectedItem?.ToString(), qty, est, store.Text));
                            await Navigation.PopModalAsync();
                            await LoadAsync();
                        }
                        catch (Exception ex) { await DisplayAlertAsync("Item not saved", ex is FamilyApiException ? ex.Message : "Please try again.", "OK"); }
                    }) }
                }
            }
        };
        modal.Content.VerticalOptions = LayoutOptions.Fill;
        await Navigation.PushModalAsync(new NavigationPage(modal));
    }

    private async Task CreateCartAsync()
    {
        var store = await DisplayPromptAsync("Start a store trip", "Where are you shopping?", "Create", "Cancel", "Store name");
        if (string.IsNullOrWhiteSpace(store)) return;
        await RunActionAsync(() => _api.CreateCartAsync(store.Trim()));
    }

    private async Task MarkPurchasedAsync(ProcurementItemDto item, string store)
    {
        var amount = await DisplayPromptAsync("Mark as bought", $"Actual price for {item.Name}?", "Save", "Cancel", keyboard: Keyboard.Numeric);
        if (amount is null) return;
        if (!decimal.TryParse(amount, out var price) || price < 0)
        {
            await DisplayAlertAsync("Check the price", "Enter a valid amount, such as 4.99.", "OK");
            return;
        }
        await RunActionAsync(() => _api.PurchaseAsync(item.Id, price, store));
    }

    private async Task RunItemActionAsync(Func<Task<ProcurementItemDto>> action) => await RunActionAsync(async () => { await action(); });
    private async Task RunActionAsync(Func<Task> action)
    {
        try { await action(); await LoadAsync(); }
        catch (Exception ex) { await DisplayAlertAsync("Shopping not updated", ex is FamilyApiException ? ex.Message : "Please try again.", "OK"); }
    }

    private static Label SectionTitle(string value)
    {
        var label = new Label { Text = value, FontSize = 20, FontAttributes = FontAttributes.Bold };
        label.SetDynamicResource(VisualElement.StyleProperty, "TextTitle");
        return label;
    }

    private static View EmptyCard(string title, string description)
    {
        var stack = new VerticalStackLayout { Spacing = 5, Children = { new Label { Text = title, FontSize = 15, FontAttributes = FontAttributes.Bold }, new Label { Text = description, FontSize = 12 } } };
        return Card(stack);
    }

    private static Border Card(View content)
    {
        var border = new Border { Padding = 14, Content = content };
        border.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        return border;
    }
}
