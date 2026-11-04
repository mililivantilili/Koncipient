using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Koncipient.Jadro;

namespace Koncipient.App;

/// <summary>
/// Po importu ukáže fotky, které program vzal jako oddělovače dokumentů, aby se
/// skutečná stránka (třeba hodně tmavá) potichu neztratila. Zaškrtnuté se vrátí mezi strany.
/// </summary>
public static class OddelovaceDialog
{
    /// <summary>Vrátí oddělovače, které má program vrátit mezi strany (prázdný seznam = nic).</summary>
    public static IReadOnlyList<Oddelovac> Zkontroluj(Window vlastnik, Spis spis, IReadOnlyList<Oddelovac> oddelovace)
    {
        var volby = new List<(CheckBox Volba, Oddelovac Oddelovac)>();
        var nahledy = new WrapPanel();
        foreach (var o in oddelovace)
        {
            var volba = new CheckBox
            {
                Content = "Je to stránka, vrátit",
                Margin = new Thickness(0, 4, 0, 0),
                ToolTip = "Fotka se zařadí na své místo mezi strany a dokumenty, které rozdělila, se zase spojí.",
            };
            AutomationProperties_Nastav(volba, "VratitOddelovac_" + volby.Count);
            var polozka = new StackPanel { Width = 170, Margin = new Thickness(6) };
            polozka.Children.Add(new Border
            {
                BorderBrush = System.Windows.Media.Brushes.Gray, BorderThickness = new Thickness(1), Height = 200,
                Child = new Image { Source = Nahled(Path.Combine(spis.Slozka, o.Fotka)), Stretch = System.Windows.Media.Stretch.Uniform },
            });
            polozka.Children.Add(new TextBlock { Text = o.PuvodniNazev ?? "", TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 4, 0, 0) });
            polozka.Children.Add(volba);
            nahledy.Children.Add(polozka);
            volby.Add((volba, o));
        }

        var hotovo = new Button { Content = "Hotovo", Width = 90, IsDefault = true, IsCancel = true };
        AutomationProperties_Nastav(hotovo, "OddelovaceHotovo");
        var obsah = new DockPanel { Margin = new Thickness(14) };
        var popis = new TextBlock
        {
            Text = $"Tyto fotky ({oddelovace.Count}) jsem vzal jako oddělovače dokumentů (zakrytý objektiv) a neuložil je mezi strany. "
                 + "Když je některá ve skutečnosti stránka spisu, zaškrtni ji a vrátím ji na její místo.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10),
        };
        DockPanel.SetDock(popis, Dock.Top);
        var dole = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        dole.Children.Add(hotovo);
        DockPanel.SetDock(dole, Dock.Bottom);
        obsah.Children.Add(popis);
        obsah.Children.Add(dole);
        obsah.Children.Add(new ScrollViewer { Content = nahledy, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

        var okno = new Window
        {
            Title = "Oddělovače dokumentů",
            Content = obsah,
            Owner = vlastnik,
            Width = 640,
            Height = oddelovace.Count > 3 ? 600 : 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
        };
        hotovo.Click += (_, _) => okno.DialogResult = true;
        okno.ShowDialog();
        return volby.Where(v => v.Volba.IsChecked == true).Select(v => v.Oddelovac).ToList();
    }

    private static void AutomationProperties_Nastav(FrameworkElement prvek, string id) =>
        System.Windows.Automation.AutomationProperties.SetAutomationId(prvek, id);

    private static BitmapImage? Nahled(string cesta)
    {
        if (!File.Exists(cesta)) return null;
        // načíst do paměti, aby soubor nezůstal zamčený (při vrácení se přesouvá)
        var obrazek = new BitmapImage();
        obrazek.BeginInit();
        obrazek.CacheOption = BitmapCacheOption.OnLoad;
        obrazek.DecodePixelWidth = 340;
        obrazek.UriSource = new Uri(cesta);
        obrazek.EndInit();
        obrazek.Freeze();
        return obrazek;
    }
}
