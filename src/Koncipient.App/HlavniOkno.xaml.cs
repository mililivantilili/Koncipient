using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Koncipient.Jadro;
using Koncipient.Ocr;
using Microsoft.Win32;

namespace Koncipient.App;

/// <summary>
/// Prohlížeč spisu: vlevo seznam spisů a stran, uprostřed přepsaný text,
/// vpravo fotka. Kliknutí na slovo ukáže jeho místo na fotce a naopak.
/// </summary>
public partial class HlavniOkno : Window
{
    private static readonly Brush BarvaSporne = new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0xB2));
    private static readonly Brush BarvaOpraveno = new SolidColorBrush(Color.FromRgb(0xC8, 0xE6, 0xC9));
    private static readonly Brush BarvaVybrane = new SolidColorBrush(Color.FromRgb(0xFF, 0xF1, 0x76));
    private static readonly Brush BarvaRamecku = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xD6, 0x00));

    private readonly Uloziste _uloziste = new();
    private Spis? _spis;
    private Strana? _strana;
    private VysledekOcr? _vysledek;
    private readonly Dictionary<SlovoOcr, Run> _behy = new();
    private SlovoOcr? _vybrane;
    private CancellationTokenSource? _zruseni;

    public HlavniOkno()
    {
        InitializeComponent();
        NactiSpisy();
        Stav.Text = $"Spisy jsou ve složce {_uloziste.Slozka}";
    }

    // ---------- Spisy a strany ----------

    private sealed record PolozkaSpisu(Spis Spis)
    {
        public override string ToString() =>
            string.IsNullOrWhiteSpace(Spis.Nazev) ? Spis.Id : $"{Spis.Id}  {Spis.Nazev}";
    }

    private sealed record PolozkaStrany(Spis Spis, Strana Strana)
    {
        public override string ToString()
        {
            var znacka = Spis.MaOcr(Strana, Spis.Opraveno) ? "✎"
                : Spis.MaOcr(Strana, Motory.Paddle) || Spis.MaOcr(Strana, Motory.Tesseract) ? "✓" : "…";
            return $"{znacka}  Strana {Strana.Cislo}   {Strana.PuvodniNazev}";
        }
    }

    private void NactiSpisy(string? vybratId = null)
    {
        SeznamSpisu.ItemsSource = _uloziste.Spisy().Select(s => new PolozkaSpisu(s)).ToList();
        var id = vybratId ?? _spis?.Id;
        SeznamSpisu.SelectedItem = SeznamSpisu.Items.Cast<PolozkaSpisu>().FirstOrDefault(p => p.Spis.Id == id);
    }

    private void NactiStrany()
    {
        var cislo = _strana?.Cislo;
        SeznamStran.ItemsSource = _spis?.Strany.Select(s => new PolozkaStrany(_spis, s)).ToList();
        SeznamStran.SelectedItem = SeznamStran.Items.Cast<PolozkaStrany>().FirstOrDefault(p => p.Strana.Cislo == cislo)
            ?? SeznamStran.Items.Cast<PolozkaStrany>().FirstOrDefault();
    }

    private void SeznamSpisu_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _spis = (SeznamSpisu.SelectedItem as PolozkaSpisu)?.Spis;
        _strana = null;
        PridatFotky.IsEnabled = _spis is not null;
        NactiStrany();
    }

    private void SeznamStran_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var polozka = SeznamStran.SelectedItem as PolozkaStrany;
        _strana = polozka?.Strana;
        ZobrazStranu();
    }

    private void NovySpis_Click(object sender, RoutedEventArgs e)
    {
        var nazev = VstupDialog.Zeptej(this, "Nový spis",
            "Pojmenování spisu (zůstane jen v tomto počítači, do exportu nejde):", "");
        if (nazev is null) return;
        var spis = _uloziste.Zaloz(nazev.Trim());
        NactiSpisy(spis.Id);
    }

    private async void PridatFotky_Click(object sender, RoutedEventArgs e)
    {
        if (_spis is null) return;
        var dialog = new OpenFileDialog
        {
            Title = "Vyber fotky stran",
            Multiselect = true,
            Filter = "Fotky|*.jpg;*.jpeg;*.png;*.tif;*.tiff|Všechny soubory|*.*",
        };
        if (dialog.ShowDialog(this) != true) return;

        var spis = _spis;
        Stav.Text = $"Přidávám {dialog.FileNames.Length} fotek…";
        IsEnabled = false;
        try
        {
            await Task.Run(() => spis.PridejFotky(dialog.FileNames));
            Stav.Text = $"Přidáno {dialog.FileNames.Length} fotek. Přepis spustíš tlačítkem Spustit přepis.";
        }
        finally
        {
            IsEnabled = true;
        }
        NactiStrany();
    }

    // ---------- Zobrazení strany ----------

    private void ZobrazStranu()
    {
        _behy.Clear();
        _vybrane = null;
        Vrstva.Children.Clear();
        var dokument = TextStrany.Document;
        dokument.Blocks.Clear();

        if (_spis is null || _strana is null)
        {
            Fotka.Source = null;
            return;
        }

        var bitmapa = new BitmapImage();
        bitmapa.BeginInit();
        bitmapa.CacheOption = BitmapCacheOption.OnLoad; // nezamkne soubor
        bitmapa.UriSource = new Uri(_spis.CestaFotky(_strana));
        bitmapa.EndInit();
        Fotka.Source = bitmapa;
        // Rozměry v pixelech, aby souřadnice slov z OCR seděly bez přepočtu DPI
        Fotka.Width = Vrstva.Width = bitmapa.PixelWidth;
        Fotka.Height = Vrstva.Height = bitmapa.PixelHeight;
        Dispatcher.BeginInvoke(new Action(NaSirku), System.Windows.Threading.DispatcherPriority.Loaded);

        _vysledek = Motory.ProZobrazeni(_spis, _strana);
        if (_vysledek is null)
        {
            dokument.Blocks.Add(new Paragraph(new Run("Strana zatím nemá přepis. Spusť přepis tlačítkem nahoře."))
            {
                Foreground = Brushes.Gray,
            });
            return;
        }

        foreach (var radek in _vysledek.Radky)
        {
            var odstavec = new Paragraph { Margin = new Thickness(0, 0, 0, 4) };
            foreach (var slovo in radek.Slova)
            {
                if (odstavec.Inlines.Count > 0) odstavec.Inlines.Add(new Run(" "));
                var beh = new Run(slovo.Platny) { Tag = slovo, Cursor = Cursors.Hand };
                NastavVzhled(beh, slovo);
                beh.MouseLeftButtonDown += Slovo_MouseLeftButtonDown;
                odstavec.Inlines.Add(beh);
                _behy[slovo] = beh;
            }
            dokument.Blocks.Add(odstavec);
        }
        var sporna = _vysledek.Slova.Count(s => s.Sporne);
        Stav.Text = $"{_spis.Id}, strana {_strana.Cislo}: {_vysledek.Slova.Count()} slov, sporných {sporna}, motor {_vysledek.Motor}";
    }

    private void NastavVzhled(Run beh, SlovoOcr slovo)
    {
        beh.Text = slovo.Platny;
        beh.Background = slovo == _vybrane ? BarvaVybrane
            : slovo.Oprava is not null ? BarvaOpraveno
            : slovo.Sporne ? BarvaSporne
            : null;
        beh.ToolTip = slovo.Oprava is not null ? $"Opraveno, OCR četlo: {slovo.Text}"
            : slovo.Jistota is float j ? $"Jistota OCR {j:P0}" : null;
    }

    private void Vyber(SlovoOcr? slovo, bool posunoutText)
    {
        var predchozi = _vybrane;
        _vybrane = slovo;
        if (predchozi is not null && _behy.TryGetValue(predchozi, out var stary)) NastavVzhled(stary, predchozi);
        Vrstva.Children.Clear();
        if (slovo is null) return;

        if (_behy.TryGetValue(slovo, out var beh))
        {
            NastavVzhled(beh, slovo);
            if (posunoutText) beh.BringIntoView();
        }

        var r = slovo.Ramecek;
        var obdelnik = new Rectangle
        {
            Width = r.Sirka + 8, Height = r.Vyska + 8,
            Fill = BarvaRamecku, Stroke = Brushes.OrangeRed, StrokeThickness = 3,
        };
        Canvas.SetLeft(obdelnik, r.X - 4);
        Canvas.SetTop(obdelnik, r.Y - 4);
        Vrstva.Children.Add(obdelnik);

        // posun fotky tak, aby bylo slovo uprostřed
        double m = Meritko.ScaleX;
        FotkaScroll.ScrollToHorizontalOffset((r.X + r.Sirka / 2.0) * m - FotkaScroll.ViewportWidth / 2);
        FotkaScroll.ScrollToVerticalOffset((r.Y + r.Vyska / 2.0) * m - FotkaScroll.ViewportHeight / 2);
    }

    private void Slovo_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Run { Tag: SlovoOcr slovo } beh) return;
        if (e.ClickCount == 2) Oprav(slovo, beh);
        else Vyber(slovo, posunoutText: false);
        e.Handled = true;
    }

    private void Fotka_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_vysledek is null) return;
        var bod = e.GetPosition(Fotka); // v pixelech fotky, protože Fotka má rozměry v pixelech
        var slovo = _vysledek.SlovoNa(bod.X, bod.Y);
        if (slovo is not null) Vyber(slovo, posunoutText: true);
    }

    private void Oprav(SlovoOcr slovo, Run beh)
    {
        if (_spis is null || _strana is null || _vysledek is null) return;
        Vyber(slovo, posunoutText: false);
        var novy = VstupDialog.Zeptej(this, "Oprava slova", $"OCR přečetlo: {slovo.Text}", slovo.Platny);
        if (novy is null) return;
        novy = novy.Trim();
        slovo.Oprava = novy == slovo.Text ? null : novy;
        _spis.UlozOcr(_strana, _vysledek, Spis.Opraveno);
        NastavVzhled(beh, slovo);
    }

    // ---------- Zvětšení ----------

    private void Zvetseni_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Meritko is null) return;
        Meritko.ScaleX = Meritko.ScaleY = e.NewValue;
    }

    private void NaSirku_Click(object sender, RoutedEventArgs e) => NaSirku();

    private void NaSirku()
    {
        if (Fotka.Source is null || Fotka.Width <= 0 || FotkaScroll.ViewportWidth <= 0) return;
        Zvetseni.Value = Math.Clamp((FotkaScroll.ViewportWidth - 4) / Fotka.Width, Zvetseni.Minimum, Zvetseni.Maximum);
    }

    private void FotkaScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        Zvetseni.Value = Math.Clamp(Zvetseni.Value * (e.Delta > 0 ? 1.15 : 1 / 1.15), Zvetseni.Minimum, Zvetseni.Maximum);
        e.Handled = true;
    }

    // ---------- Dávkový přepis ----------

    private async void SpustitPrepis_Click(object sender, RoutedEventArgs e)
    {
        if (!Motory.ModelyJsou)
        {
            var odpoved = MessageBox.Show(this,
                "Pro přepis je potřeba jednou stáhnout modely OCR (asi 140 MB). Stahují se jen modely, " +
                "žádné fotky ani text se nikam neposílají.\n\nStáhnout teď?",
                "Koncipient", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (odpoved != MessageBoxResult.Yes) return;
        }

        _zruseni = new CancellationTokenSource();
        SpustitPrepis.IsEnabled = false;
        Zastavit.IsEnabled = true;
        Prubeh.Visibility = Visibility.Visible;
        BezSpanku(true);
        try
        {
            await Motory.StahniAsync(new Progress<string>(t => Stav.Text = t), _zruseni.Token);
            var prubeh = new Progress<StavDavky>(s =>
            {
                Prubeh.Maximum = Math.Max(s.Celkem, 1);
                Prubeh.Value = s.Hotovo;
                Stav.Text = $"{s.Popis}  ({s.Hotovo}/{s.Celkem})";
            });
            await Davka.ZpracujAsync(_uloziste.Spisy(), Motory.ProDavku(), prubeh, _zruseni.Token);
            Stav.Text = "Přepis dokončen.";
        }
        catch (OperationCanceledException)
        {
            Stav.Text = "Přepis zastaven. Při dalším spuštění se pokračuje tam, kde skončil.";
        }
        finally
        {
            BezSpanku(false);
            Prubeh.Visibility = Visibility.Collapsed;
            SpustitPrepis.IsEnabled = true;
            Zastavit.IsEnabled = false;
            _zruseni.Dispose();
            _zruseni = null;
        }
        NactiSpisy();
    }

    private void Zastavit_Click(object sender, RoutedEventArgs e)
    {
        _zruseni?.Cancel();
        Stav.Text = "Zastavuji po dokončení rozpracované strany…";
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_zruseni is not null &&
            MessageBox.Show(this, "Přepis ještě běží. Opravdu zavřít?", "Koncipient",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }
        _zruseni?.Cancel();
        base.OnClosing(e);
    }

    // Během přepisu přes noc nesmí počítač usnout.
    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);

    private static void BezSpanku(bool zapnout)
    {
        const uint EsContinuous = 0x80000000, EsSystemRequired = 0x00000001;
        SetThreadExecutionState(zapnout ? EsContinuous | EsSystemRequired : EsContinuous);
    }
}
