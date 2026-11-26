using SkiaSharp;

namespace FamilyOS.Maui.Pages;

/// <summary>Interactive circular profile-photo cropper. The saved image is a square JPEG.</summary>
public sealed class AvatarCropPage : ContentPage
{
    private const double PreviewSize = 300;
    private readonly Func<Stream, string, Task> _save;
    private readonly byte[] _sourceBytes;
    private readonly SKBitmap _bitmap;
    private readonly Image _preview;
    private readonly Label _status;
    private double _scale = 1;
    private double _panX;
    private double _panY;
    private double _panStartX;
    private double _panStartY;
    private double _pinchStartScale = 1;
    private bool _saving;

    public AvatarCropPage(byte[] sourceBytes, Func<Stream, string, Task> save)
    {
        _save = save;
        Title = "Edit profile photo";
        _sourceBytes = sourceBytes;
        using var decode = new MemoryStream(_sourceBytes);
        _bitmap = SKBitmap.Decode(decode) ?? throw new InvalidOperationException("This image format could not be opened.");
        if (_bitmap.Width < 2 || _bitmap.Height < 2)
            throw new InvalidOperationException("The selected image is too small.");

        _preview = new Image
        {
            Source = ImageSource.FromStream(() => new MemoryStream(_sourceBytes)),
            Aspect = Aspect.AspectFill,
            WidthRequest = PreviewSize,
            HeightRequest = PreviewSize,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        _status = new Label { Text = "Pinch to zoom. Drag to position your photo.", HorizontalTextAlignment = TextAlignment.Center, FontSize = 13 };
        var viewport = new Grid
        {
            WidthRequest = PreviewSize,
            HeightRequest = PreviewSize,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Clip = new Microsoft.Maui.Controls.Shapes.EllipseGeometry
            {
                Center = new Point(PreviewSize / 2, PreviewSize / 2),
                RadiusX = PreviewSize / 2,
                RadiusY = PreviewSize / 2
            },
            BackgroundColor = Colors.Black,
            Children = { _preview }
        };

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnPanUpdated;
        _preview.GestureRecognizers.Add(pan);
        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinchUpdated;
        _preview.GestureRecognizers.Add(pinch);

        var reset = new Button { Text = "Reset crop" };
        reset.Clicked += (_, _) => ResetCrop();
        var cancel = new Button { Text = "Cancel" };
        cancel.Clicked += async (_, _) => await Navigation.PopModalAsync();
        var saveButton = new Button { Text = "Use photo" };
        saveButton.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        saveButton.Clicked += async (_, _) => await SaveAsync();

        var content = new VerticalStackLayout
        {
            Padding = new Thickness(20, 24),
            Spacing = 18,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = "Position your photo", FontSize = 23, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center },
                viewport,
                _status,
                new Label { Text = "The circular area is what your family will see.", FontSize = 12, HorizontalTextAlignment = TextAlignment.Center },
                new HorizontalStackLayout { Spacing = 12, HorizontalOptions = LayoutOptions.Center, Children = { reset, cancel, saveButton } }
            }
        };
        Content = new ScrollView { Content = content };
        BackgroundColor = Colors.Black;
    }

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (_saving) return;
        if (e.StatusType == GestureStatus.Started)
        {
            _panStartX = _panX;
            _panStartY = _panY;
        }
        else if (e.StatusType == GestureStatus.Running)
        {
            _panX = _panStartX + e.TotalX;
            _panY = _panStartY + e.TotalY;
            ClampPan();
            ApplyTransform();
        }
    }

    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (_saving) return;
        if (e.Status == GestureStatus.Started) _pinchStartScale = _scale;
        else if (e.Status == GestureStatus.Running)
        {
            _scale = Math.Clamp(_pinchStartScale * e.Scale, 1, 6);
            ClampPan();
            ApplyTransform();
        }
    }

    private void ClampPan()
    {
        var baseScale = PreviewSize / Math.Min(_bitmap.Width, _bitmap.Height);
        var renderedScale = baseScale * _scale;
        _panX = Math.Clamp(_panX, -(Math.Max(0, _bitmap.Width * renderedScale - PreviewSize) / 2), Math.Max(0, _bitmap.Width * renderedScale - PreviewSize) / 2);
        _panY = Math.Clamp(_panY, -(Math.Max(0, _bitmap.Height * renderedScale - PreviewSize) / 2), Math.Max(0, _bitmap.Height * renderedScale - PreviewSize) / 2);
    }

    private void ApplyTransform()
    {
        _preview.Scale = _scale;
        _preview.TranslationX = _panX;
        _preview.TranslationY = _panY;
    }

    private void ResetCrop()
    {
        _scale = 1;
        _panX = 0;
        _panY = 0;
        ApplyTransform();
    }

    private async Task SaveAsync()
    {
        if (_saving) return;
        _saving = true;
        _status.Text = "Preparing photo…";
        try
        {
            var baseScale = PreviewSize / Math.Min(_bitmap.Width, _bitmap.Height);
            var sourcePerScreen = 1d / (baseScale * _scale);
            var cropSize = PreviewSize * sourcePerScreen;
            var centerX = _bitmap.Width / 2d - _panX * sourcePerScreen;
            var centerY = _bitmap.Height / 2d - _panY * sourcePerScreen;
            var left = Math.Clamp(centerX - cropSize / 2d, 0, _bitmap.Width - cropSize);
            var top = Math.Clamp(centerY - cropSize / 2d, 0, _bitmap.Height - cropSize);
            var sourceRect = new SKRect((float)left, (float)top, (float)(left + cropSize), (float)(top + cropSize));

            using var output = new SKBitmap(640, 640, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(output))
            {
                canvas.Clear(SKColors.White);
                using var sourceImage = SKImage.FromBitmap(_bitmap);
                canvas.DrawImage(sourceImage, sourceRect, new SKRect(0, 0, 640, 640), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
                canvas.Flush();
            }

            using var encoded = SKImage.FromBitmap(output).Encode(SKEncodedImageFormat.Jpeg, 90);
            await using var upload = new MemoryStream(encoded.ToArray());
            await _save(upload, "profile-cropped.jpg");
            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _saving = false;
        }
    }
}
