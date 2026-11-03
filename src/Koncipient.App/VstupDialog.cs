using System.Windows;
using System.Windows.Controls;

namespace Koncipient.App;

/// <summary>Jednoduché okno s jedním textovým polem.</summary>
public static class VstupDialog
{
    /// <summary>Vrátí zadaný text, nebo null, když uživatel zrušil.</summary>
    public static string? Zeptej(Window vlastnik, string titulek, string otazka, string vychozi)
    {
        var pole = new TextBox { Text = vychozi, Margin = new Thickness(0, 6, 0, 10), FontSize = 15 };
        var ok = new Button { Content = "OK", Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        var zrusit = new Button { Content = "Zrušit", Width = 80, IsCancel = true };
        var tlacitka = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        tlacitka.Children.Add(ok);
        tlacitka.Children.Add(zrusit);
        var obsah = new StackPanel { Margin = new Thickness(14) };
        obsah.Children.Add(new TextBlock { Text = otazka, TextWrapping = TextWrapping.Wrap });
        obsah.Children.Add(pole);
        obsah.Children.Add(tlacitka);

        var okno = new Window
        {
            Title = titulek,
            Content = obsah,
            Owner = vlastnik,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
        };
        ok.Click += (_, _) => okno.DialogResult = true;
        okno.Loaded += (_, _) => { pole.Focus(); pole.SelectAll(); };
        return okno.ShowDialog() == true ? pole.Text : null;
    }
}

/// <summary>Okno s výběrem jedné položky ze seznamu.</summary>
public static class VyberDialog
{
    public static T? Vyber<T>(Window vlastnik, string titulek, string otazka, IReadOnlyList<T> polozky, Func<T, string> popis)
        where T : class
    {
        var seznam = new ListBox { Height = 220, Margin = new Thickness(0, 6, 0, 10) };
        foreach (var p in polozky) seznam.Items.Add(new ListBoxItem { Content = popis(p), Tag = p });
        var ok = new Button { Content = "OK", Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        var zrusit = new Button { Content = "Zrušit", Width = 80, IsCancel = true };
        var tlacitka = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        tlacitka.Children.Add(ok);
        tlacitka.Children.Add(zrusit);
        var obsah = new StackPanel { Margin = new Thickness(14) };
        obsah.Children.Add(new TextBlock { Text = otazka, TextWrapping = TextWrapping.Wrap });
        obsah.Children.Add(seznam);
        obsah.Children.Add(tlacitka);
        var okno = new Window
        {
            Title = titulek, Content = obsah, Owner = vlastnik, Width = 420,
            SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
        };
        ok.Click += (_, _) => { if (seznam.SelectedItem is not null) okno.DialogResult = true; };
        seznam.MouseDoubleClick += (_, _) => { if (seznam.SelectedItem is not null) okno.DialogResult = true; };
        return okno.ShowDialog() == true ? (seznam.SelectedItem as ListBoxItem)?.Tag as T : null;
    }
}
