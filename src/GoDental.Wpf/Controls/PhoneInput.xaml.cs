using System.Windows;
using System.Windows.Controls;
using GoDental.Application.Localization;
namespace GoDental.Wpf.Controls;
public partial class PhoneInput : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(PhoneInput), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, Changed));
    public static readonly DependencyProperty RegionProperty = DependencyProperty.Register(nameof(Region), typeof(string), typeof(PhoneInput), new PropertyMetadata("SV", Changed));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(PhoneInput), new PropertyMetadata("Teléfono"));
    public static readonly DependencyProperty MaxLengthProperty = DependencyProperty.Register(nameof(MaxLength), typeof(int), typeof(PhoneInput), new PropertyMetadata(30));
    private static readonly DependencyPropertyKey HintPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Hint), typeof(string), typeof(PhoneInput), new PropertyMetadata(""));
    public static readonly DependencyProperty HintProperty = HintPropertyKey.DependencyProperty;
    public string? Text { get => (string?)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string? Region { get => (string?)GetValue(RegionProperty); set => SetValue(RegionProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public int MaxLength { get => (int)GetValue(MaxLengthProperty); set => SetValue(MaxLengthProperty, value); }
    public string Hint => (string)GetValue(HintProperty);
    public PhoneInput() { InitializeComponent(); UpdateHint(); }
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((PhoneInput)d).UpdateHint();
    private void UpdateHint() { PhoneFormat.TryFormat(Text, Region, out _, out var hint); SetValue(HintPropertyKey, hint); }
}
