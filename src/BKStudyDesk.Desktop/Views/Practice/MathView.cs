using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using AvaloniaMath.Controls;
using XamlMath;

namespace BKStudyDesk.Desktop.Views.Practice;

/// <summary>
/// Một công thức TeX của trang Luyện tập (bộ dựng: src/ThirdParty/XamlMath). Màu chữ theo token Text, cỡ theo MathScale
/// (trong dòng) hay MathScaleDisplay (đứng riêng), chữ trong \text theo phông của app. Công thức lỗi thì hiện TeX gốc
/// dạng chữ và ghi log một lần, không bao giờ ném lỗi ra ngoài.
/// </summary>
public sealed class MathView : ContentControl
{
    public static readonly StyledProperty<string> TexProperty = AvaloniaProperty.Register<MathView, string>(nameof(Tex), "");

    public static readonly StyledProperty<bool> DisplayProperty = AvaloniaProperty.Register<MathView, bool>(nameof(Display));

    /// <summary>Ghi cảnh báo (app gán Log.Warn lúc mở). Mặc định bỏ qua, để test và công cụ dev không ghi vào data của người dùng.</summary>
    public static Action<string> Warn { get; set; } = _ => { };

    // Mỗi công thức lỗi chỉ ghi log một lần mỗi phiên: một trang có thể vẽ lại cùng công thức nhiều lần.
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

    /// <summary>Công thức đứng riêng (\[ \]): style Display, cỡ MathScaleDisplay.</summary>
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

    // Nằm trong dòng chữ (InlineUIContainer): báo đường chân cho TextBlock, để dòng thẳng hàng và đủ cao cho phần dưới đường chân.
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

    // Phông của app: ContentControlThemeFontFamily khi người dùng chọn phông, không có thì phông mặc định của hệ điều hành.
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
