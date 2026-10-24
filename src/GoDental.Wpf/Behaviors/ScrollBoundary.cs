using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace GoDental.Wpf.Behaviors;

/// <summary>Continúa el desplazamiento de una lista en su contenedor al alcanzar un extremo.</summary>
public static class ScrollBoundary
{
    public static readonly DependencyProperty ContinueAtBoundaryProperty = DependencyProperty.RegisterAttached(
        "ContinueAtBoundary", typeof(bool), typeof(ScrollBoundary), new PropertyMetadata(false, Changed));

    public static bool GetContinueAtBoundary(DependencyObject element) => (bool)element.GetValue(ContinueAtBoundaryProperty);
    public static void SetContinueAtBoundary(DependencyObject element, bool value) => element.SetValue(ContinueAtBoundaryProperty, value);

    private static void Changed(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not ScrollViewer viewer) return;
        if ((bool)args.OldValue) viewer.PreviewMouseWheel -= OnPreviewMouseWheel;
        if ((bool)args.NewValue) viewer.PreviewMouseWheel += OnPreviewMouseWheel;
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || e.Delta == 0 || Keyboard.Modifiers != ModifierKeys.None) return;
        var host = (ScrollViewer)sender;
        var viewers = new List<ScrollViewer>();
        for (var node = e.OriginalSource as DependencyObject; node is not null; node = Parent(node))
        {
            // La rueda de campos y desplegables conserva su semántica de edición.
            if (node is TextBoxBase or PasswordBox or ComboBox) return;
            if (node is ScrollViewer viewer) viewers.Add(viewer);
            if (ReferenceEquals(node, host)) break;
        }
        if (viewers.Count < 2 || !ReferenceEquals(viewers[^1], host) || CanScroll(viewers[0], e.Delta)) return;
        var target = viewers.Skip(1).FirstOrDefault(viewer => CanScroll(viewer, e.Delta));
        if (target is null) return;
        e.Handled = true;
        // Evento de burbuja: WPF aplica la configuración de rueda del sistema y el scroll lógico/físico.
        target.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = Mouse.MouseWheelEvent });
    }

    private static bool CanScroll(ScrollViewer viewer, int delta) =>
        delta > 0 ? viewer.VerticalOffset > 0 : viewer.VerticalOffset < viewer.ScrollableHeight;

    private static DependencyObject? Parent(DependencyObject node) => node is Visual or Visual3D
        ? VisualTreeHelper.GetParent(node) : (node as FrameworkContentElement)?.Parent;
}
