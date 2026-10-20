using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace GoDental.Wpf.Dialogs;

public sealed class DialogWindow : Window
{
    private readonly IDialogModel model;
    private bool saved;

    public DialogWindow(IDialogModel model, UIElement body)
    {
        this.model = model;
        Title = model.Title;
        DataContext = model;
        Content = body;
        Style = (Style)FindResource("DialogWindow");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = Math.Min(920, SystemParameters.WorkArea.Width * 0.85);
        Height = Math.Min(690, SystemParameters.WorkArea.Height * 0.86);
        MinWidth = Math.Min(560, Width);
        MinHeight = Math.Min(400, Height);
        model.Saved += OnSaved;
        model.CancelRequested += OnCancelRequested;
        Closing += OnClosing;
        Closed += OnClosed;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnSaved(object? sender, EventArgs e)
    {
        saved = true;
        DialogResult = true;
    }

    private void OnCancelRequested(object? sender, EventArgs e) => Close();

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (saved) return;
        if (model is MvpFormModel { SecretRequired: true, Operation.HasPending: true } authorization)
        {
            authorization.StatusMessage = "Consulta el estado de la autorización o vuelve a ingresar la contraseña para reenviar la misma solicitud antes de cerrar.";
            e.Cancel = true; return;
        }
        if (model.IsBusy) { e.Cancel = true; return; }
        if (!model.HasUnsavedChanges) return;
        var confirmation = new DialogWindow(new DiscardConfirmationModel(), new TextBlock
        {
            Text = "Hay cambios sin guardar. ¿Quieres descartarlos?",
            TextWrapping = TextWrapping.Wrap
        })
        {
            Owner = this,
            Width = Math.Min(480, SystemParameters.WorkArea.Width * 0.7),
            Height = 210,
            MinWidth = 320,
            MinHeight = 180
        };
        if (confirmation.ShowDialog() != true) e.Cancel = true;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        model.Saved -= OnSaved;
        model.CancelRequested -= OnCancelRequested;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close(); e.Handled = true; return; }
        if (e.Key != Key.Enter || !model.ShowPrimaryAction || e.OriginalSource is not DependencyObject source) return;
        if (FindAncestor<TextBox>(source) is { AcceptsReturn: true }) return;
        if (FindAncestor<ComboBox>(source) is { IsDropDownOpen: true }) return;
        if (FindAncestor<ButtonBase>(source) is not null) return;
        if (FindAncestor<GoDental.Wpf.Views.Shared.ProductPickerView>(source) is not null
            || FindAncestor<GoDental.Wpf.Views.Operations.CustomerLookupView>(source) is not null
            || FindAncestor<GoDental.Wpf.Views.Operations.OrderDraftView>(source) is not null
            || FindAncestor<GoDental.Wpf.Views.Operations.CheckoutView>(source) is not null) return;
        if (model.SaveCommand.CanExecute(null)) { model.SaveCommand.Execute(null); e.Handled = true; }
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            current = current is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current);
        }
        return null;
    }
}
