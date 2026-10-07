using System;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Media;
using Avalonia.Threading;
using XamlMath;
using AvaloniaMath.Parsers;
using AvaloniaMath.Rendering;
using XamlMath.Boxes;
using XamlMath.Exceptions;
using XamlMath.Rendering;
using Size = Avalonia.Size;

namespace AvaloniaMath.Controls;

public class FormulaBlock : TemplatedControl
{
    private Box? _box;

    public static readonly StyledProperty<string> FormulaProperty =
        AvaloniaProperty.Register<FormulaBlock, string>(
            nameof(Formula), string.Empty, false, BindingMode.TwoWay);

    public static readonly StyledProperty<double> ScaleProperty =
        AvaloniaProperty.Register<FormulaBlock, double>(
            nameof(Scale), 20d, false, BindingMode.Default);

    public static readonly StyledProperty<string> SystemTextFontNameProperty =
        AvaloniaProperty.Register<FormulaBlock, string>(
            nameof(SystemTextFontName), "Arial", false, BindingMode.Default);

    public static readonly StyledProperty<TexStyle> MathStyleProperty =
        AvaloniaProperty.Register<FormulaBlock, TexStyle>(nameof(MathStyle), TexStyle.Display);

    public static readonly StyledProperty<bool> HasErrorProperty =
        AvaloniaProperty.Register<FormulaBlock, bool>(nameof(HasError));

    public static readonly StyledProperty<ObservableCollection<Exception>> ErrorsProperty =
        AvaloniaProperty.Register<FormulaBlock, ObservableCollection<Exception>>(nameof(Errors));

    public static readonly StyledProperty<ControlTemplate> ErrorTemplateProperty =
        AvaloniaProperty.Register<FormulaBlock, ControlTemplate>(nameof(ErrorTemplate));

    /// <summary>
    /// Initializes static members of the <see cref="FormulaBlock"/> class.
    /// </summary>
    static FormulaBlock()
    {
        ClipToBoundsProperty.OverrideDefaultValue<FormulaBlock>(true);

        AffectsRender<FormulaBlock>(
            FormulaProperty,
            ScaleProperty);
    }
    /// <summary>
    /// Initializes a new instance of the <see cref="FormulaBlock"/> class.
    /// </summary>
    public FormulaBlock()
    {
        Errors = new ObservableCollection<Exception>();
        // Bản vendored: khử răng cưa thường (grayscale). Subpixel làm viền glyph có màu khác màu chữ,
        // thấy rõ ở nét dọc mảnh như +, ngoặc vuông.
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Antialias);
    }

    // Bản vendored: thay System.Reactive bằng OnPropertyChanged, bớt một phụ thuộc.
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FormulaProperty || change.Property == ScaleProperty || change.Property == SystemTextFontNameProperty
            || change.Property == MathStyleProperty)
        {
            InvalidateFormattedText();
            InvalidateMeasure();
        }
    }

    public string Formula
    {
        get => GetValue(FormulaProperty);
        set => SetValue(FormulaProperty, value);
    }

    public double Scale
    {
        get => GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    public string SystemTextFontName
    {
        get => GetValue(SystemTextFontNameProperty);
        set => SetValue(SystemTextFontNameProperty, value);
    }

    public bool HasError
    {
        get => GetValue(HasErrorProperty);
        private set => SetValue(HasErrorProperty, value);
    }

    public ObservableCollection<Exception> Errors
    {
        get => GetValue(ErrorsProperty);
        private set => SetValue(ErrorsProperty, value);
    }

    // TODO[#353]: Make it used
    public ControlTemplate ErrorTemplate
    {
        get => GetValue(ErrorTemplateProperty);
        set => SetValue(ErrorTemplateProperty, value);
    }

    /// <summary>Style TeX ban đầu: Text cho công thức trong dòng, Display cho công thức đứng riêng (bản vendored).</summary>
    public TexStyle MathStyle
    {
        get => GetValue(MathStyleProperty);
        set => SetValue(MathStyleProperty, value);
    }

    /// <summary>Lỗi gần nhất (parse, dựng box hay vẽ), null nếu công thức vẽ được.</summary>
    public Exception? LastError { get; private set; }

    /// <summary>Công thức không vẽ được. Control không ném lỗi ra ngoài, chỉ báo qua sự kiện này và HasError.</summary>
    public event EventHandler<Exception>? Failed;

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_box == null) return;

        // Bản vendored: lỗi lúc vẽ không được lan ra làm sập app (upstream để lọt lỗi thiếu glyph ở đây).
        try
        {
            var renderer = new AvaloniaElementRenderer(context, Scale, Background, Foreground);
            TeXFormulaExtensions.Render(_box, renderer, 0.0, 0.0);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Dispatcher.UIThread.Post(() => { SetError(e); InvalidateMeasure(); });
        }
    }

    /// <summary>
    /// Invalidates <see cref="FormattedText"/>.
    /// </summary>
    protected void InvalidateFormattedText()
    {
        var tex = Formula?.Trim() ?? "";
        if (tex.Length == 0)
        {
            _box = null;
            ClearError();
            return;
        }
        try
        {
            var formula = AvaloniaTeXFormulaParser.Instance.Parse(tex);
            var texEnvironment = AvaloniaTeXEnvironment.Create(
                style: MathStyle,
                scale: Scale,
                systemTextFontName: SystemTextFontName);
            var box = formula.CreateBox(texEnvironment);
            // Thử lấy glyph của mọi ký tự ngay lúc dựng: thiếu glyph thì báo lỗi ở đây, không đợi tới lúc Render.
            TeXFormulaExtensions.Render(box, new GlyphProbeRenderer(Scale), 0.0, 0.0);
            _box = box;
            ClearError();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            SetError(e);
        }
    }

    private void ClearError()
    {
        LastError = null;
        HasError = false;
        Errors.Clear();
    }

    /// <summary>
    /// Measures the control.
    /// </summary>
    /// <param name="availableSize">The available size for the control.</param>
    /// <returns>The desired size.</returns>
    /// <summary>Bản vendored: khoảng từ đỉnh công thức tới đường chân (pixel), để đặt công thức thẳng hàng trong dòng chữ.</summary>
    public double Ascent => _box == null ? 0 : _box.Height * Scale;

    protected override Size MeasureOverride(Size availableSize)
    {
        return _box == null
            ? new Size()
            : new Size(_box.TotalWidth * Scale, _box.TotalHeight * Scale);
    }

    private void SetError(Exception exception)
    {
        Errors.Add(exception);
        LastError = exception;
        _box = null;
        HasError = true;
        Failed?.Invoke(this, exception);
    }
}

