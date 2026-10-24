using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace GoDental.Wpf.Controls;

// El secreto queda únicamente en controles visuales; no es DependencyProperty ni binding al VM.
public partial class PasswordEntry : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(PasswordEntry), new PropertyMetadata("Contraseña", LabelChanged));
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Password => MaskedInput.Password;
    public bool IsRevealed { get; private set; }
    private bool updating;
    private Window? owner;

    public PasswordEntry()
    {
        InitializeComponent();
        Loaded += (_, _) => { AttachOwner(); UpdateButton(); };
        Unloaded += (_, _) => { DetachOwner(); Clear(); };
        DataContextChanged += (_, _) => Clear();
        IsVisibleChanged += (_, _) => { if (!IsVisible) Clear(); };
        IsEnabledChanged += (_, _) => { if (!IsEnabled) Clear(); };
    }

    public string ReadAndClear() { var password = Password; Clear(); return password; }
    public void Clear() { MaskedInput.Clear(); HidePassword(); CapsNotice.Visibility = Visibility.Collapsed; }
    public void HidePassword()
    {
        IsRevealed = false;
        VisibleInput.Clear();
        VisibleInput.Visibility = Visibility.Collapsed;
        MaskedInput.Visibility = Visibility.Visible;
        UpdateButton();
    }
    public void FocusInput() { if (IsRevealed) VisibleInput.Focus(); else MaskedInput.Focus(); }

    private void RevealClicked(object sender, RoutedEventArgs e)
    {
        if (IsRevealed) HidePassword();
        else
        {
            VisibleInput.Text = Password;
            IsRevealed = true;
            MaskedInput.Visibility = Visibility.Collapsed;
            VisibleInput.Visibility = Visibility.Visible;
            VisibleInput.CaretIndex = VisibleInput.Text.Length;
            UpdateButton();
        }
        FocusInput(); UpdateCaps();
    }
    private void MaskedChanged(object sender, RoutedEventArgs e)
    {
        if (updating || !IsRevealed) return;
        updating = true;
        try { VisibleInput.Text = Password; } finally { updating = false; }
    }
    private void VisibleChanged(object sender, TextChangedEventArgs e)
    {
        if (updating || !IsRevealed) return;
        updating = true;
        try { MaskedInput.Password = VisibleInput.Text; } finally { updating = false; }
    }
    private static void LabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    { if (d is PasswordEntry entry && entry.RevealButton is not null) entry.UpdateButton(); }
    private void UpdateButton()
    {
        RevealButton.Content = IsRevealed ? "Ocultar" : "Mostrar";
        AutomationProperties.SetName(RevealButton, (IsRevealed ? "Ocultar " : "Mostrar ") + Label.ToLowerInvariant());
    }
    private void EntryFocused(object sender, KeyboardFocusChangedEventArgs e) { if (ReferenceEquals(e.NewFocus, this)) FocusInput(); UpdateCaps(); }
    private void EntryKeyUp(object sender, KeyEventArgs e) => UpdateCaps();
    private void EntryLostFocus(object sender, KeyboardFocusChangedEventArgs e) => UpdateCaps();
    private void UpdateCaps() => CapsNotice.Visibility = IsKeyboardFocusWithin && Keyboard.IsKeyToggled(Key.CapsLock) ? Visibility.Visible : Visibility.Collapsed;
    private void AttachOwner() { DetachOwner(); owner = Window.GetWindow(this); if (owner is not null) owner.Deactivated += OwnerDeactivated; }
    private void DetachOwner() { if (owner is not null) owner.Deactivated -= OwnerDeactivated; owner = null; }
    private void OwnerDeactivated(object? sender, EventArgs e) => HidePassword();
}
