using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace GoDental.Wpf.Behaviors;

/// <summary>Datos de presentación heredados del host, sin depender de una ventana concreta.</summary>
public static class WorkspaceViewport
{
    public static readonly DependencyProperty HeightProperty = DependencyProperty.RegisterAttached(
        "Height", typeof(double), typeof(WorkspaceViewport), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.Inherits));
    public static double GetHeight(DependencyObject element) => (double)element.GetValue(HeightProperty);
    public static void SetHeight(DependencyObject element, double value) => element.SetValue(HeightProperty, value);

    public static readonly DependencyProperty HasPinnedLoadingProperty = DependencyProperty.RegisterAttached(
        "HasPinnedLoading", typeof(bool), typeof(WorkspaceViewport), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
    public static bool GetHasPinnedLoading(DependencyObject element) => (bool)element.GetValue(HasPinnedLoadingProperty);
    public static void SetHasPinnedLoading(DependencyObject element, bool value) => element.SetValue(HasPinnedLoadingProperty, value);

    public static readonly DependencyProperty ResetSectionScrollProperty = DependencyProperty.RegisterAttached(
        "ResetSectionScroll", typeof(bool), typeof(WorkspaceViewport), new PropertyMetadata(false, OnResetSectionScrollChanged));
    public static bool GetResetSectionScroll(DependencyObject element) => (bool)element.GetValue(ResetSectionScrollProperty);
    public static void SetResetSectionScroll(DependencyObject element, bool value) => element.SetValue(ResetSectionScrollProperty, value);

    private static void OnResetSectionScrollChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not TabControl tabs) return;
        if ((bool)e.OldValue) tabs.SelectionChanged -= OnSectionChanged;
        if ((bool)e.NewValue) tabs.SelectionChanged += OnSectionChanged;
    }

    private static void OnSectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, sender)) return;
        var tabs = (TabControl)sender;
        _ = tabs.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (tabs.Template.FindName("SectionScroll", tabs) is ScrollViewer scroll) scroll.ScrollToHome();
            if (tabs.ItemContainerGenerator.ContainerFromIndex(tabs.SelectedIndex) is TabItem selected) selected.BringIntoView();
        }));
    }
}
