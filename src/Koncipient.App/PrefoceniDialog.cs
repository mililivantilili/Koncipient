using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Koncipient.Jadro;
using Microsoft.Win32;

namespace Koncipient.App;

/// <summary>
/// Strany, které vyšly špatně (rozmazané, tmavé, uříznuté, nejistý přepis), s náhledem.
/// Každou jde rovnou nahradit novou fotkou; přepíše se při dalším přepisu.
/// </summary>
public static class PrefoceniDialog
{
    /// <summary>Ukáže strany k přefocení. Vrátí počet nahrazených fotek a stranu, kterou chce advokátka otevřít.</summary>
    public static (int Nahrazeno, Strana? Otevrit) Ukaz(Window vlastnik, Spis spis, IReadOnlyList<StranaKPrefoceni> strany)
    {
        int nahrazeno = 0;
        Strana? otevrit = null;
        Window? okno = null;
        var seznam = new StackPanel();
        foreach (var k in strany)
        {
            var radek = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var nahled = new Image { Width = 120, Height = 160, Stretch = System.Windows.Media.Stretch.Uniform, Source = Nahled(spis.CestaFotky(k.Strana)) };
            DockPanel.SetDock(nahled, Dock.Left);
            radek.Children.Add(nahled);
            var vpravo = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
            vpravo.Children.Add(new TextBlock
            {
                Text = $"Strana {spis.Poradi(k.Strana)}  ({spis.DokumentStrany(k.Strana)?.Nazev}, {k.Strana.PuvodniNazev})",
                FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap,
            });
            foreach (var d in k.Duvody) vpravo.Children.Add(new TextBlock { Text = "• " + d, TextWrapping = TextWrapping.Wrap });
            var stav = new TextBlock { Foreground = System.Windows.Media.Brushes.DarkGreen, Margin = new Thickness(0, 4, 0, 0) };
            var tlacitka = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            var nahradit = new Button { Content = "Nahradit novou fotkou…", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0) };
            var otevritTl = new Button { Content = "Otevřít stranu", Padding = new Thickness(8, 2, 8, 2) };
            var strana = k.Strana;
            nahradit.Click += (_, _) =>
            {
                var dialog = new OpenFileDialog { Title = $"Nová fotka strany {spis.Poradi(strana)}", Filter = "Fotky|*.jpg;*.jpeg;*.png;*.tif;*.tiff" };
                if (dialog.ShowDialog(okno) != true) return;
                try
                {
                    spis.NahradFotku(strana, dialog.FileName);
                    nahled.Source = Nahled(spis.CestaFotky(strana));
                    stav.Text = "Nahrazeno. Přepíše se při dalším spuštění přepisu.";
                    nahradit.IsEnabled = false;
                    nahrazeno++;
                }
                catch (Exception ex) when (ex is InvalidDataException or IOException)
                {
                    MessageBox.Show(okno, ex.Message, "Přefocení", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            otevritTl.Click += (_, _) => { otevrit = strana; okno!.DialogResult = true; };
            tlacitka.Children.Add(nahradit);
            tlacitka.Children.Add(otevritTl);
            vpravo.Children.Add(tlacitka);
            vpravo.Children.Add(stav);
            radek.Children.Add(vpravo);
            seznam.Children.Add(radek);
        }

        var zavrit = new Button { Content = "Zavřít", IsDefault = true, IsCancel = true, Padding = new Thickness(10, 3, 10, 3), HorizontalAlignment = HorizontalAlignment.Right };
        var obsah = new DockPanel { Margin = new Thickness(14) };
        var popis = new TextBlock
        {
            Text = $"Tyto strany ({strany.Count}) vyšly špatně. Přefoť je, dokud máš spis v ruce: lépe osvětlit, celou stránku, ostře. "
                 + "Novou fotku přidej tlačítkem u strany, nahradí původní na stejném místě.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10),
        };
        DockPanel.SetDock(popis, Dock.Top);
        DockPanel.SetDock(zavrit, Dock.Bottom);
        obsah.Children.Add(popis);
        obsah.Children.Add(zavrit);
        obsah.Children.Add(new ScrollViewer { Content = seznam, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 0, 10) });
        okno = new Window
        {
            Title = "Stránky k přefocení", Content = obsah, Owner = vlastnik, Width = 620, Height = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false,
        };
        zavrit.Click += (_, _) => okno.DialogResult = false;
        okno.ShowDialog();
        return (nahrazeno, otevrit);
    }

    private static BitmapImage? Nahled(string cesta)
    {
        if (!File.Exists(cesta)) return null;
        var b = new BitmapImage();
        b.BeginInit();
        b.CacheOption = BitmapCacheOption.OnLoad; // soubor nezamknout, při nahrazení se přepisuje
        b.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        b.DecodePixelWidth = 240;
        b.UriSource = new Uri(cesta);
        b.EndInit();
        b.Freeze();
        return b;
    }
}
