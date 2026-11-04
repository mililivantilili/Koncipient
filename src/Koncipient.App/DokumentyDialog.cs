using System.Windows;
using System.Windows.Controls;
using Koncipient.Jadro;

namespace Koncipient.App;

/// <summary>Výběr dokumentů spisu (export pro Clauda, PDF pro archiv).</summary>
public static class DokumentyDialog
{
    /// <summary>
    /// Vrátí Id vybraných dokumentů, null = celý spis (vybráno vše), nebo prázdný seznam při zrušení.
    /// </summary>
    public static IReadOnlyCollection<string>? Vyber(Window vlastnik, Spis spis, string titulek, string popis, string tlacitko, out bool zruseno)
    {
        var volby = new List<(CheckBox Volba, Dokument Dokument)>();
        var seznam = new StackPanel();
        int k = 0;
        foreach (var (d, strany) in spis.StranyPoDokumentech())
        {
            k++;
            var stav = d.Exportovano is { } kdy ? $"exportováno {kdy:d. M. H:mm}" : "nový, ještě neexportovaný";
            var volba = new CheckBox
            {
                Content = $"{k}. {d.Nazev}  ({strany.Count} str., {stav})",
                IsChecked = true, Margin = new Thickness(0, 2, 0, 2),
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(volba, $"Dokument_{k}");
            seznam.Children.Add(volba);
            volby.Add((volba, d));
        }
        var vse = new Button { Content = "Vše", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0) };
        var nove = new Button { Content = "Jen nové", Padding = new Thickness(8, 2, 8, 2), ToolTip = "Dokumenty, které ještě neodešly v žádném exportu (doplnění spisu)." };
        vse.Click += (_, _) => volby.ForEach(v => v.Volba.IsChecked = true);
        nove.Click += (_, _) => volby.ForEach(v => v.Volba.IsChecked = v.Dokument.Exportovano is null);
        var rychle = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        rychle.Children.Add(vse);
        rychle.Children.Add(nove);

        var ok = new Button { Content = tlacitko, IsDefault = true, Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 8, 0) };
        var zrusit = new Button { Content = "Zrušit", IsCancel = true, Padding = new Thickness(10, 3, 10, 3) };
        var dole = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        dole.Children.Add(ok);
        dole.Children.Add(zrusit);

        var obsah = new StackPanel { Margin = new Thickness(14) };
        obsah.Children.Add(new TextBlock { Text = popis, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        obsah.Children.Add(rychle);
        obsah.Children.Add(new ScrollViewer { Content = seznam, MaxHeight = 380, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        obsah.Children.Add(dole);
        var okno = new Window
        {
            Title = titulek, Content = obsah, Owner = vlastnik, Width = 560, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
        };
        ok.Click += (_, _) => okno.DialogResult = true;
        zruseno = okno.ShowDialog() != true;
        if (zruseno) return Array.Empty<string>();
        var vybrane = volby.Where(v => v.Volba.IsChecked == true).Select(v => v.Dokument.Id).ToList();
        if (vybrane.Count == 0) { zruseno = true; return Array.Empty<string>(); }
        return vybrane.Count == volby.Count ? null : vybrane;
    }
}
