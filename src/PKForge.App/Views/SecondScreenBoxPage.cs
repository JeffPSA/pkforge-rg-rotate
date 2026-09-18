using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;
using PKForge.App.Services;
using PKForge.App.Theme;
using PKForge.App.ViewModels;
using PKForge.Chrome;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace PKForge.App.Views;

/// <summary>
/// Slide-up detail panel for the RG Rotate (single-screen): a Pokémon summary that
/// slides up from the bottom of the box browser grid when a Pokémon is selected.
/// Shows the mon's name, sprite, gender, dex number, level, type badges, legality
/// verdict, and stat radar. Dismisses on tap-outside (scrim) or swipe-down on the
/// handle. Keeps the pixel aesthetic (UiTokens, DsChrome.PixelFont, Kit primitives).
/// </summary>
public sealed class SecondScreenBoxPage : Grid
{
    private static readonly string[] TypeNames =
    [
        "Normal", "Fighting", "Flying", "Poison", "Ground", "Rock", "Bug", "Ghost", "Steel",
        "Fire", "Water", "Grass", "Electric", "Psychic", "Ice", "Dragon", "Dark", "Fairy",
    ];

    private readonly BoxBrowserViewModel _viewModel;
    private readonly ISpriteService _sprites;
    private readonly Grid _host;
    private readonly Action? _onEdit;
    private readonly SKCanvasView _sprite;
    private readonly PropertyChangedEventHandler _viewModelHandler;

    private readonly Label _name = null!;
    private readonly Image _gender = new() { WidthRequest = 22, HeightRequest = 22, VerticalOptions = LayoutOptions.Center, IsVisible = false };
    private readonly Label _facts = new() { TextColor = UiTokens.Ink0, FontSize = 13 };
    private readonly HorizontalStackLayout _typeBadges = new() { Spacing = 6 };
    private readonly Image _shinyMark = new()
    {
        Source = PksmIcons.Source("shiny", PksmIcons.Indigo),
        WidthRequest = 16,
        HeightRequest = 16,
        VerticalOptions = LayoutOptions.Center,
        IsVisible = false,
    };
    private readonly Label _badge = new() { FontFamily = DsChrome.PixelFont, FontSize = 13, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.End, VerticalTextAlignment = TextAlignment.Center };

    private SKCanvasView _statRadar = null!;
    private float _bounce;
    private int _bounceTicks = -1;
    private long _animElapsedMs;
    private IDispatcherTimer? _animTimer;

    private bool _isShowing;
    private bool _userDismissed;
    private bool _contentVisible;
    private double _panelHeight;
    private Border? _panel;
    private BoxView? _scrim;

    public SecondScreenBoxPage(BoxBrowserViewModel viewModel, ISpriteService sprites, Grid host, Action? onEdit = null)
    {
        _viewModel = viewModel;
        _sprites = sprites;
        _host = host;
        _onEdit = onEdit;
        BackgroundColor = Colors.Transparent;

        _sprite = new SKCanvasView { EnableTouchEvents = true };
        _sprite.PaintSurface += PaintSprite;
        _sprite.Touch += (_, args) =>
        {
            if (args.ActionType == SKTouchAction.Pressed) { args.Handled = true; return; }
            if (args.ActionType != SKTouchAction.Released) return;
            args.Handled = true;
            StartBounce();
        };

        // The mon name rides the maroon Gen-5 header strip; gender icon and the
        // legality verdict sit beside it - the verdict must never scroll or clip away.
        var nameHeader = (Border)Kit.HeaderBar("Pokémon");
        _name = (Label)nameHeader.Content!;

        var header = new Grid
        {
            ColumnSpacing = 6,
            ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto)],
            Children = { nameHeader, _gender, _badge },
        };
        Grid.SetColumn(_gender, 1);
        Grid.SetColumn(_badge, 2);

        _statRadar = new SKCanvasView { HeightRequest = 110, HorizontalOptions = LayoutOptions.Fill };
        _statRadar.PaintSurface += PaintRadar;

        var radarPanel = new Border
        {
            BackgroundColor = UiTokens.Paper,
            Stroke = UiTokens.ShellEdge,
            StrokeThickness = 2,
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            Padding = new Thickness(6, 6, 6, 2),
            Content = _statRadar,
        };

        var factsRow = new HorizontalStackLayout { Spacing = 6, Children = { _facts, _shinyMark } };

        // Two-column layout: sprite on left (compact), facts/types/radar on right.
        var spriteColumn = Kit.LcdPanel(_sprite, padding: 4);

        var factsColumn = new Grid
        {
            RowSpacing = 4,
            RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star)],
            Children = { header, factsRow, _typeBadges, radarPanel },
        };
        Grid.SetRow(factsRow, 1);
        Grid.SetRow(_typeBadges, 2);
        Grid.SetRow(radarPanel, 3);

        var summaryContent = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions = [new(new GridLength(72)), new(GridLength.Star)],
            Children = { spriteColumn, factsColumn },
        };
        Grid.SetColumn(factsColumn, 1);

        // Action row: EDIT and CLOSE buttons at the bottom.
        var editBtn = new Button
        {
            Text = "EDIT",
            FontFamily = DsChrome.PixelFont,
            FontSize = 12,
            BackgroundColor = UiTokens.MenuBlue,
            TextColor = UiTokens.OnAccent,
            CornerRadius = 6,
            Padding = new Thickness(16, 6),
            HeightRequest = 40,
            MinimumWidthRequest = 80,
        };
        editBtn.Clicked += (_, _) => { Dismiss(); _onEdit?.Invoke(); };

        var closeBtn = new Button
        {
            Text = "CLOSE",
            FontFamily = DsChrome.PixelFont,
            FontSize = 12,
            BackgroundColor = UiTokens.Ink1,
            TextColor = UiTokens.Paper,
            CornerRadius = 6,
            Padding = new Thickness(16, 6),
            HeightRequest = 40,
            MinimumWidthRequest = 80,
        };
        closeBtn.Clicked += (_, _) => Dismiss();

        var actionRow = new Grid
        {
            ColumnSpacing = 8,
            Padding = new Thickness(0, 4, 0, 0),
            ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)],
            Children = { editBtn, closeBtn },
        };
        Grid.SetColumn(closeBtn, 1);

        // Body: summary content + action row.
        var body = new Grid
        {
            RowSpacing = 4,
            Padding = new Thickness(10, 6, 10, 8),
            RowDefinitions = [new(GridLength.Star), new(GridLength.Auto)],
            Children = { summaryContent, actionRow },
        };
        Grid.SetRow(summaryContent, 0);
        Grid.SetRow(actionRow, 1);

        BuildPanel(body);
        VerticalOptions = LayoutOptions.End;

        // Start off-screen.
        TranslationY = 2000;
        IsVisible = false;

        host.SizeChanged += (_, _) => UpdatePanelHeight();
        UpdatePanelHeight();

        // React to ViewModel changes.
        _viewModelHandler = (_, args) =>
        {
            if (args.PropertyName is nameof(BoxBrowserViewModel.Selected) or nameof(BoxBrowserViewModel.LegalityBadge))
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UpdateSummary();
                    _sprite.InvalidateSurface();
                    _statRadar.InvalidateSurface();
                    if (_viewModel.Selected is { IsEmpty: false } && !_userDismissed)
                        Show();
                    else if (_viewModel.Selected is null or { IsEmpty: true })
                    {
                        _userDismissed = false;
                        Dismiss();
                    }
                });
        };
        _viewModel.PropertyChanged += _viewModelHandler;
    }

    private void BuildPanel(View body)
    {
        // Drag handle at the top of the panel: a small pill, 44dp tall for touch.
        var handle = new Border
        {
            HeightRequest = 44,
            BackgroundColor = Colors.Transparent,
            Padding = new Thickness(0, 10),
            Content = new Border
            {
                WidthRequest = 36,
                HeightRequest = 4,
                StrokeShape = new RoundRectangle { CornerRadius = 2 },
                BackgroundColor = UiTokens.Ink1,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
            },
        };

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnHandlePan;
        handle.GestureRecognizers.Add(pan);

        var panelGrid = new Grid
        {
            RowSpacing = 0,
            RowDefinitions = [new(GridLength.Auto), new(GridLength.Star)],
            Children = { handle, body },
        };
        Grid.SetRow(handle, 0);
        Grid.SetRow(body, 1);

        _panel = new Border
        {
            BackgroundColor = UiTokens.Housing,
            Stroke = UiTokens.ShellEdge,
            StrokeThickness = 2,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = 0,
            Content = panelGrid,
        };

        _scrim = new BoxView { Color = Color.FromArgb("#99000000"), IsVisible = false };
        var scrimTap = new TapGestureRecognizer();
        scrimTap.Tapped += (_, _) => Dismiss();
        _scrim.GestureRecognizers.Add(scrimTap);

        RowDefinitions = [new(GridLength.Star), new(GridLength.Auto)];
        Children.Clear();
        Children.Add(_scrim);
        Children.Add(_panel);
        Grid.SetRow(_panel, 1);
    }

    private void UpdatePanelHeight()
    {
        _panelHeight = Math.Max(280, _host.Height * 0.45);
        if (_panel is not null)
            _panel.HeightRequest = _panelHeight;
    }

    private void OnHandlePan(object? sender, PanUpdatedEventArgs e)
    {
        if (_panel is null) return;
        switch (e.StatusType)
        {
            case GestureStatus.Running:
                var dy = Math.Max(0, e.TotalY);
                _panel.TranslationY = dy;
                break;
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (_panel.TranslationY > _panelHeight * 0.3)
                    Dismiss();
                else
                    _ = _panel.TranslateToAsync(0, 0, 150, Easing.CubicOut);
                break;
        }
    }

    /// <summary>Slides the panel up from the bottom.</summary>
    public void Show()
    {
        if (_isShowing) return;
        IsVisible = true;
        _scrim!.IsVisible = true;
        _isShowing = true;
        if (_panel is not null)
            _panel.TranslationY = 0;
        this.TranslateToAsync(0, 0, 250, Easing.CubicOut);
        SetAnimating(true);
        UpdateSummary();
        _sprite.InvalidateSurface();
        _statRadar.InvalidateSurface();
    }

    /// <summary>Slides the panel back down and hides it.</summary>
    public void Dismiss()
    {
        if (!_isShowing) return;
        _isShowing = false;
        _userDismissed = true;
        SetAnimating(false);
        this.TranslateToAsync(0, _panelHeight + 80, 200, Easing.CubicIn)
            .ContinueWith(_ =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    IsVisible = false;
                    _scrim!.IsVisible = false;
                });
            });
    }

    /// <summary>Force-hide without animation (tab switch, box manage mode).</summary>
    public void ForceHide()
    {
        _isShowing = false;
        SetAnimating(false);
        IsVisible = false;
        _scrim!.IsVisible = false;
        if (_panel is not null)
            _panel.TranslationY = 0;
        TranslationY = _panelHeight + 80;
    }

    /// <summary>Clear the user-dismissed flag so the next selection shows the panel.</summary>
    public void ResetDismiss() => _userDismissed = false;

    /// <summary>Detach from the shared view model before the host is discarded.</summary>
    public void Cleanup()
    {
        _viewModel.PropertyChanged -= _viewModelHandler;
        SetAnimating(false);
    }

    private bool _spriteAnimated;

    private void SetAnimating(bool on)
    {
        if (!on)
        {
            _animTimer?.Stop();
            _animTimer = null;
            return;
        }
        if (_animTimer is not null) return;
        _animTimer = Dispatcher.CreateTimer();
        _animTimer.Interval = TimeSpan.FromMilliseconds(40);
        _animTimer.Tick += (_, _) =>
        {
            _animElapsedMs += 40;
            if (_contentVisible && _spriteAnimated) _sprite.InvalidateSurface();
        };
        _animTimer.Start();
    }

    private void UpdateSummary()
    {
        var detail = _viewModel.Selected;
        if (detail is null || detail.IsEmpty)
        {
            _contentVisible = false;
            return;
        }

        _contentVisible = true;

        _name.Text = detail.Nickname is { Length: > 0 } nick ? nick : $"#{detail.Species}";
        _gender.Source = detail.Gender switch
        {
            0 => PksmIcons.Source("male", PksmIcons.Indigo),
            1 => PksmIcons.Source("female", PksmIcons.Indigo),
            _ => null,
        };
        _gender.IsVisible = detail.Gender is 0 or 1;
        _facts.Text = $"No. {detail.Species:000}  Lv. {detail.Level}";
        _shinyMark.IsVisible = detail.IsShiny;

        _typeBadges.Children.Clear();
        foreach (var type in detail.Types ?? [])
        {
            var typeName = (uint)type < (uint)TypeNames.Length ? TypeNames[type] : $"?{type}";
            _typeBadges.Children.Add(new Border
            {
                BackgroundColor = TypePalette.ForType(type),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 5 },
                Padding = new Thickness(10, 2),
                Content = new Label
                {
                    Text = typeName.ToUpperInvariant(),
                    TextColor = Colors.White,
                    FontSize = 11,
                    FontAttributes = FontAttributes.Bold,
                    CharacterSpacing = 1,
                },
            });
        }

        _badge.Text = _viewModel.LegalityBadge switch
        {
            "✓" => "✓ LEGAL",
            "✗" => "✗ NOT LEGAL",
            _ => "",
        };
        _badge.TextColor = _viewModel.LegalityBadge == "✓" ? UiTokens.Ok : UiTokens.Bad;
    }

    private static readonly string[] StatAxes = ["HP", "ATK", "DEF", "SPA", "SPD", "SPE"];

    private void PaintRadar(object? sender, SKPaintSurfaceEventArgs args)
    {
        var canvas = args.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        if (!_contentVisible) return;
        var detail = _viewModel.Selected;
        if (detail?.Stats is not { Count: 6 } stats) return;

        var info = args.Info;
        var cx = info.Width / 2f;
        var cy = info.Height / 2f;
        var radius = Math.Min(info.Width, info.Height) * 0.28f;
        var max = Math.Max(1, stats.Max());

        SKPoint Vertex(int i, float r)
        {
            var angle = (float)(-Math.PI / 2 + i * Math.PI / 3);
            return new SKPoint(cx + r * (float)Math.Cos(angle), cy + r * (float)Math.Sin(angle));
        }

        using var gridPaint = new SKPaint { Color = UiTokens.SkLcdTileEdge.WithAlpha(0x66), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };
        for (var ring = 1; ring <= 4; ring++)
        {
            using var ringPath = new SKPath();
            for (var i = 0; i < 6; i++)
            {
                var p = Vertex(i, radius * ring / 4f);
                if (i == 0) ringPath.MoveTo(p); else ringPath.LineTo(p);
            }
            ringPath.Close();
            canvas.DrawPath(ringPath, gridPaint);
        }
        for (var i = 0; i < 6; i++)
            canvas.DrawLine(cx, cy, Vertex(i, radius).X, Vertex(i, radius).Y, gridPaint);

        using var fill = new SKPaint { Color = Pksm.IndigoLight.WithAlpha(0x66), Style = SKPaintStyle.Fill, IsAntialias = true };
        using var edge = new SKPaint { Color = Pksm.Indigo, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true };
        using var dot = new SKPaint { Color = Pksm.IndigoDeep, Style = SKPaintStyle.Fill, IsAntialias = true };
        using var shape = new SKPath();
        for (var i = 0; i < 6; i++)
        {
            var p = Vertex(i, radius * stats[i] / max);
            if (i == 0) shape.MoveTo(p); else shape.LineTo(p);
        }
        shape.Close();
        canvas.DrawPath(shape, fill);
        canvas.DrawPath(shape, edge);
        for (var i = 0; i < 6; i++)
        {
            var p = Vertex(i, radius * stats[i] / max);
            canvas.DrawCircle(p.X, p.Y, 2.5f, dot);
        }

        using var capFont = new SKFont { Size = 10f, Edging = SKFontEdging.Antialias, Embolden = true };
        using var valFont = new SKFont { Size = 12f, Edging = SKFontEdging.Antialias, Embolden = true };
        using var capPaint = new SKPaint { Color = UiTokens.SkLcdText.WithAlpha(0xB0), IsAntialias = true };
        using var valPaint = new SKPaint { Color = UiTokens.SkLcdText, IsAntialias = true };
        for (var i = 0; i < 6; i++)
        {
            var label = Vertex(i, radius + 10f);
            var align = Math.Abs(label.X - cx) < 4 ? SKTextAlign.Center : label.X < cx ? SKTextAlign.Right : SKTextAlign.Left;
            canvas.DrawText(StatAxes[i], label.X, label.Y - 1, align, capFont, capPaint);
            canvas.DrawText(stats[i].ToString(), label.X, label.Y + 10, align, valFont, valPaint);
        }
    }

    private void StartBounce()
    {
        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); }
        catch { }
        if (_bounceTicks >= 0) return;
        _bounceTicks = 0;
        var timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(30);
        timer.Tick += (_, _) =>
        {
            _bounceTicks++;
            var progress = _bounceTicks / 20f;
            _bounce = (float)(Math.Abs(Math.Sin(progress * Math.PI * 2)) * (1 - progress) * 0.12);
            _sprite.InvalidateSurface();
            if (_bounceTicks >= 20)
            {
                _bounce = 0;
                _bounceTicks = -1;
                timer.Stop();
                _sprite.InvalidateSurface();
            }
        };
        timer.Start();
    }

    private void PaintSprite(object? sender, SKPaintSurfaceEventArgs args)
    {
        var canvas = args.Surface.Canvas;
        canvas.Clear(Pksm.SummaryStripe);
        _spriteAnimated = false;
        if (!_contentVisible) return;
        var detail = _viewModel.Selected;
        if (detail is null || detail.IsEmpty) return;

        if (!_sprites.TryGetShowdown(detail.Species, detail.IsShiny, out var animated))
        {
            _sprites.WarmShowdown(detail.Species, detail.IsShiny, () => MainThread.BeginInvokeOnMainThread(_sprite.InvalidateSurface));
            return;
        }
        _spriteAnimated = animated is not null;
        var home = animated is null ? _sprites.GetHome(detail.Species, detail.IsShiny) : null;
        if (animated is null && home is null)
            _sprites.WarmHome(detail.Species, detail.IsShiny, () => MainThread.BeginInvokeOnMainThread(_sprite.InvalidateSurface));

        var bitmap = animated?.FrameAt(_animElapsedMs) ?? home ?? _sprites.GetSprite(detail.Species, detail.Form, detail.IsShiny);
        if (bitmap is null)
        {
            _sprites.Warm(detail.Species, detail.Form, detail.IsShiny, () => MainThread.BeginInvokeOnMainThread(_sprite.InvalidateSurface));
            return;
        }

        var info = args.Info;
        var box = Math.Min(info.Width, info.Height) * (animated is not null ? 0.72f : 0.86f);
        var scale = Math.Min(box / bitmap.Width, box / bitmap.Height);
        var w = bitmap.Width * scale;
        var h = bitmap.Height * scale;
        var hop = _bounce * h;
        var dest = new SKRect(info.Width / 2f - w / 2, info.Height / 2f - h / 2 - hop, info.Width / 2f + w / 2, info.Height / 2f + h / 2 - hop);
        using var image = SKImage.FromBitmap(bitmap);
        var sampling = home is not null
            ? new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
            : new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
        canvas.DrawImage(image, dest, sampling);

        var ballBitmap = _sprites.GetBall(detail.Ball);
        if (ballBitmap is null)
        {
            _sprites.WarmBall(detail.Ball, () => MainThread.BeginInvokeOnMainThread(_sprite.InvalidateSurface));
        }
        else
        {
            var ballSize = Math.Min(info.Width, info.Height) * 0.2f;
            using var ballImage = SKImage.FromBitmap(ballBitmap);
            canvas.DrawImage(ballImage, new SKRect(6, 6, 6 + ballSize, 6 + ballSize),
                new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
        }
    }
}
