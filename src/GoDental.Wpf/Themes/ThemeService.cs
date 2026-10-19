using System.Windows;

namespace GoDental.Wpf.Themes;

public interface IThemeService
{
    string CurrentTheme { get; }
    void Apply(string theme);
}

public sealed class ThemeService : IThemeService
{
    public string CurrentTheme { get; private set; } = "Light";
    public void Apply(string theme)
    {
        var selectedTheme = theme == "Dark" ? "Dark" : "Light";
        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(d => d.Source?.OriginalString.EndsWith("Theme.xaml", StringComparison.Ordinal) == true);
        var replacement = new ResourceDictionary { Source = new Uri($"/GoDental;component/Themes/{selectedTheme}Theme.xaml", UriKind.Relative) };
        if (existing is null) dictionaries.Insert(0, replacement);
        else dictionaries[dictionaries.IndexOf(existing)] = replacement;
        CurrentTheme = selectedTheme;
    }
}
