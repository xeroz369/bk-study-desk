using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using AvaloniaMath.Controls;
using XamlMath;

namespace BKStudyDesk.Desktop.Views.Practice;

public sealed class MathView : ContentControl
{
    public static readonly StyledProperty<string> TexProperty = AvaloniaProperty.Register<MathView, string>(nameof(Tex), "");

    public static readonly StyledProperty<bool> DisplayProperty = AvaloniaProperty.Register<MathView, bool>(nameof(Display));

    public static Action<string> Warn { get; set; } = _ => { };

    private static readonly HashSet<string> Logged = [];

    private IDisposable? _scaleBinding;

    internal FormulaBlock Block { get; }

    private readonly TextBlock _fallback = new() { TextWrapping = TextWrapping.Wrap };

    public MathView()
    {
        Block = new FormulaBlock { MathStyle = TexStyle.Text };
        Block.Bind(TemplatedControl.ForegroundProperty, Block.GetResourceObservable("Text"));
        Block.Bind(FormulaBlock.SystemTextFontNameProperty, Block.GetResourceObservable("ContentControlThemeFontFamily", FontName));
        Block.Failed += (_, e) => OnFailed(e);
        BindScale();
        Content = Block;
    }

    public string Tex
    {
        get => GetValue(TexProperty);
        set => SetValue(TexProperty, value);
    }

    public bool Display
    {
        get => GetValue(DisplayProperty);
        set => SetValue(DisplayProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DisplayProperty)
        {
            Block.MathStyle = Display ? TexStyle.Display : TexStyle.Text;
            BindScale();
        }
        if (change.Property == TexProperty || change.Property == DisplayProperty)
        {
            Block.Formula = Tex ?? "";
            Content = Block.HasError ? Fallback() : Block;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        var baseline = Content == Block ? Block.Ascent : 0;
        if (TextBlock.GetBaselineOffset(this) != baseline) TextBlock.SetBaselineOffset(this, baseline);
        return size;
    }

    private void BindScale()
    {
        _scaleBinding?.Dispose();
        _scaleBinding = Block.Bind(FormulaBlock.ScaleProperty, Block.GetResourceObservable(Display ? "MathScaleDisplay" : "MathScale"));
    }

    private static object? FontName(object? resource) =>
        resource is FontFamily family ? family.Name : FontManager.Current.DefaultFontFamily.Name;

    private void OnFailed(Exception e)
    {
        var tex = Tex ?? "";
        bool first;
        lock (Logged) first = Logged.Add(tex);
        if (first) Warn($"Công thức không vẽ được ({e.GetType().Name}: {e.Message.Split('\n')[0]}): {tex}");
        Content = Fallback();
    }

    private TextBlock Fallback()
    {
        var tex = Tex ?? "";
        _fallback.Text = Display ? $@"\[{tex}\]" : $@"\({tex}\)";
        return _fallback;
    }
}
