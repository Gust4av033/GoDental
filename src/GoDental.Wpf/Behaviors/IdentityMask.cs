using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GoDental.Domain.Rules;
namespace GoDental.Wpf.Behaviors;

// Se aplica a acciones de usuario, nunca recorta datos históricos al cargarlos.
public static class IdentityMask
{
    public static readonly DependencyProperty DigitsProperty = DependencyProperty.RegisterAttached("Digits", typeof(int), typeof(IdentityMask), new PropertyMetadata(0, Changed));
    public static int GetDigits(DependencyObject o) => (int)o.GetValue(DigitsProperty);
    public static void SetDigits(DependencyObject o, int value) => o.SetValue(DigitsProperty, value);
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox t) return;
        t.PreviewTextInput -= Input; t.PreviewKeyDown -= Key; DataObject.RemovePastingHandler(t, Paste);
        if ((int)e.NewValue is not (9 or 14)) return;
        t.PreviewTextInput += Input; t.PreviewKeyDown += Key; DataObject.AddPastingHandler(t, Paste);
    }
    private static void Input(object sender, TextCompositionEventArgs e) { e.Handled = true; Replace((TextBox)sender, e.Text); }
    private static void Paste(object sender, DataObjectPastingEventArgs e)
    {
        e.CancelCommand(); if (e.DataObject.GetData(DataFormats.UnicodeText) is string text) Replace((TextBox)sender, text);
    }
    private static void Key(object sender, KeyEventArgs e)
    {
        if (e.Key is not (System.Windows.Input.Key.Back or System.Windows.Input.Key.Delete) || Keyboard.Modifiers != ModifierKeys.None) return;
        var t = (TextBox)sender;
        if (t.SelectionLength == 0)
        {
            var pos = t.SelectionStart;
            if (e.Key == System.Windows.Input.Key.Back)
            {
                var start = pos - 1; while (start >= 0 && !char.IsAsciiDigit(t.Text[start])) start--;
                if (start >= 0) t.Select(start, pos - start);
            }
            else
            {
                var end = pos; while (end < t.Text.Length && !char.IsAsciiDigit(t.Text[end])) end++;
                if (end < t.Text.Length) t.Select(pos, end - pos + 1);
            }
        }
        e.Handled = true; Replace(t, "");
    }
    private static void Replace(TextBox t, string input)
    {
        if (input.Any(c => !char.IsAsciiDigit(c) && c is not ('-' or ' '))) return;
        var prefix = t.Text[..t.SelectionStart];
        var candidate = prefix + input + t.Text[(t.SelectionStart + t.SelectionLength)..];
        if (candidate.Any(c => !char.IsAsciiDigit(c) && c is not ('-' or ' '))) return;
        var digits = IdentityFormat.Digits(candidate); if (digits.Length > GetDigits(t)) return;
        var count = IdentityFormat.Digits(prefix + input).Length;
        var formatted = IdentityFormat.Format(digits, GetDigits(t));
        t.SetCurrentValue(TextBox.TextProperty, formatted);
        var caret = 0; while (caret < formatted.Length && count > 0) { if (char.IsAsciiDigit(formatted[caret])) count--; caret++; }
        t.Select(caret, 0); t.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }
}
