using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Koncipient.Jadro;
using Koncipient.Jadro.Anonymizace;
using Koncipient.Ocr;
using Microsoft.Win32;

namespace Koncipient.App;

/// <summary>
/// Prohlížeč spisu: vlevo spisy, strany a údaje, uprostřed přepsaný text,
/// vpravo fotka. Kliknutí na slovo ukáže jeho místo na fotce a naopak.
/// V textu se označují údaje k anonymizaci a odtud se dělá export pro Clauda.
/// </summary>
public partial class HlavniOkno : Window
{
    private static readonly Brush BarvaUdaj = Zmrazit(new SolidColorBrush(Color.FromRgb(0xE1, 0xBE, 0xE7)));
    private static readonly Brush BarvaSporne = Zmrazit(new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0xB2)));
    private static readonly Brush BarvaOpraveno = Zmrazit(new SolidColorBrush(Color.FromRgb(0xC8, 0xE6, 0xC9)));
    private static readonly Brush BarvaVybrane = Zmrazit(new SolidColorBrush(Color.FromRgb(0xFF, 0xF1, 0x76)));
    private static readonly Brush BarvaRamecku = Zmrazit(new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xD6, 0x00)));

    private static Brush Zmrazit(Brush b) { b.Freeze(); return b; }

    private readonly Uloziste _uloziste = new();
    private Spis? _spis;
    private Klic _klic = new();
    private Strana? _strana;
    private VysledekOcr? _vysledek;

    // slova strany v pořadí čtení, jejich běhy v textu a údaje, kterým patří
    private List<SlovoOcr> _slova = new();
    private readonly Dictionary<SlovoOcr, int> _indexy = new();
    private readonly Dictionary<int, Run> _behy = new();
    private Entita?[] _udaje = Array.Empty<Entita?>();

    // vybraný úsek slov (Shift+klik rozšiřuje)
    private int _kotva = -1, _vyberOd = -1, _vyberDo = -1;
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
            var kontrola = Strana.Zkontrolovano ? " ☑" : "";
            return $"{znacka}{kontrola}  Strana {Strana.Cislo}   {Strana.PuvodniNazev}";
        }
    }

    private sealed record PolozkaUdaje(Entita Entita)
    {
        public override string ToString()
        {
            var akce = Entita.Akce switch
            {
                Akce.Odstranit => "  [odstranit]",
                Akce.Ponechat => "  [ponechat]",
                _ => "",
            };
            var role = string.IsNullOrWhiteSpace(Entita.Role) ? "" : $"  ({Entita.Role})";
            return $"{Entita.Id}  {Entita.Popis}{role}{akce}";
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

    private void NactiUdaje()
    {
        SeznamUdaju.ItemsSource = _klic.Entity
            .OrderBy(e => e.Typ).ThenBy(e => e.Id, StringComparer.Ordinal)
            .Select(e => new PolozkaUdaje(e)).ToList();
    }

    private void SeznamSpisu_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _spis = (SeznamSpisu.SelectedItem as PolozkaSpisu)?.Spis;
        _klic = _spis is null ? new Klic() : Anonymizator.NactiKlic(_spis);
        _strana = null;
        PridatFotky.IsEnabled = NajitUdaje.IsEnabled = Exportovat.IsEnabled = _spis is not null;
        NactiUdaje();
        NactiStrany();
    }

    private void SeznamStran_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _strana = (SeznamStran.SelectedItem as PolozkaStrany)?.Strana;
        Zkontrolovano.IsEnabled = _strana is not null;
        Zkontrolovano.IsChecked = _strana?.Zkontrolovano ?? false;
        ZobrazStranu(posunoutFotku: true);
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

    private void ZobrazStranu(bool posunoutFotku = false)
    {
        _behy.Clear();
        _indexy.Clear();
        _slova = new List<SlovoOcr>();
        _kotva = _vyberOd = _vyberDo = -1;
        Vrstva.Children.Clear();
        var dokument = TextStrany.Document;
        dokument.Blocks.Clear();

        if (_spis is null || _strana is null)
        {
            Fotka.Source = null;
            return;
        }

        if (posunoutFotku || Fotka.Source is null)
        {
            var bitmapa = new BitmapImage();
            bitmapa.BeginInit();
            bitmapa.CacheOption = BitmapCacheOption.OnLoad; // nezamkne soubor
            bitmapa.CreateOptions = BitmapCreateOptions.IgnoreImageCache; // po otočení načte novou fotku
            bitmapa.UriSource = new Uri(_spis.CestaFotky(_strana));
            bitmapa.EndInit();
            Fotka.Source = bitmapa;
            // Rozměry v pixelech, aby souřadnice slov z OCR seděly bez přepočtu DPI
            Fotka.Width = Vrstva.Width = bitmapa.PixelWidth;
            Fotka.Height = Vrstva.Height = bitmapa.PixelHeight;
            Dispatcher.BeginInvoke(new Action(NaSirku), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        _vysledek = Motory.ProZobrazeni(_spis, _strana);
        if (_vysledek is null)
        {
            dokument.Blocks.Add(new Paragraph(new Run("Strana zatím nemá přepis. Spusť přepis tlačítkem nahoře."))
            {
                Foreground = Brushes.Gray,
            });
            return;
        }

        _slova = _vysledek.Slova.ToList();
        for (int i = 0; i < _slova.Count; i++) _indexy[_slova[i]] = i;
        _udaje = new Entita?[_slova.Count];
        foreach (var v in Anonymizator.Vyskyty(Anonymizator.Slova(_vysledek), _klic))
            for (int i = v.Od; i <= v.Do; i++) _udaje[i] = v.Entita;

        if (Nahled.IsChecked == true)
        {
            // text tak, jak ho dostane Claude
            foreach (var radek in Anonymizator.AnonymizovanyText(_vysledek, _klic).Split('\n'))
                dokument.Blocks.Add(new Paragraph(new Run(radek)) { Margin = new Thickness(0, 0, 0, 4) });
        }
        else
        {
            int index = 0;
            foreach (var radek in _vysledek.Radky)
            {
                var odstavec = new Paragraph { Margin = new Thickness(0, 0, 0, 4) };
                foreach (var _ in radek.Slova)
                {
                    if (odstavec.Inlines.Count > 0) odstavec.Inlines.Add(new Run(" "));
                    var beh = new Run { Tag = index, Cursor = Cursors.Hand };
                    beh.MouseLeftButtonDown += Slovo_MouseLeftButtonDown;
                    beh.MouseRightButtonDown += Slovo_MouseRightButtonDown;
                    _behy[index] = beh;
                    NastavVzhled(index);
                    odstavec.Inlines.Add(beh);
                    index++;
                }
                dokument.Blocks.Add(odstavec);
            }
        }

        var sporna = _slova.Count(s => s.Sporne);
        var udaje = _udaje.Count(u => u is not null);
        Stav.Text = $"{_spis.Id}, strana {_strana.Cislo}: {_slova.Count} slov, sporných {sporna}, slov v údajích {udaje}";
    }

    private bool JeVybrane(int i) => i >= _vyberOd && i <= _vyberDo && _vyberOd >= 0;

    private void NastavVzhled(int i)
    {
        if (!_behy.TryGetValue(i, out var beh)) return;
        var slovo = _slova[i];
        var udaj = _udaje[i];
        beh.Text = slovo.Platny;
        beh.Background = JeVybrane(i) ? BarvaVybrane
            : udaj is not null ? BarvaUdaj
            : slovo.Oprava is not null ? BarvaOpraveno
            : slovo.Sporne ? BarvaSporne
            : null;
        var popis = new List<string>();
        if (udaj is not null) popis.Add($"{udaj.Nahrada}  ({Nazvy.Typ(udaj.Typ)})");
        if (slovo.Oprava is not null) popis.Add($"Opraveno, OCR četlo: {slovo.Text}");
        if (slovo.Sporne) popis.Add("Druhý motor zde četl něco jiného.");
        if (slovo.Jistota is float j) popis.Add($"Jistota OCR {j:P0}");
        beh.ToolTip = popis.Count > 0 ? string.Join("\n", popis) : null;
    }

    private void Vyber(int od, int @do, bool posunoutText)
    {
        int stareOd = _vyberOd, stareDo = _vyberDo;
        (_vyberOd, _vyberDo) = (Math.Min(od, @do), Math.Max(od, @do));
        if (stareOd >= 0) for (int i = stareOd; i <= stareDo; i++) NastavVzhled(i);
        for (int i = _vyberOd; i <= _vyberDo; i++) NastavVzhled(i);
        Vrstva.Children.Clear();
        if (_vyberOd < 0) return;

        if (posunoutText && _behy.TryGetValue(_vyberOd, out var beh)) beh.BringIntoView();

        foreach (var slovo in _slova.Skip(_vyberOd).Take(_vyberDo - _vyberOd + 1))
        {
            var r = slovo.Ramecek;
            var obdelnik = new Rectangle
            {
                Width = r.Sirka + 8, Height = r.Vyska + 8,
                Fill = BarvaRamecku, Stroke = Brushes.OrangeRed, StrokeThickness = 3,
            };
            Canvas.SetLeft(obdelnik, r.X - 4);
            Canvas.SetTop(obdelnik, r.Y - 4);
            Vrstva.Children.Add(obdelnik);
        }

        // posun fotky tak, aby bylo vybrané slovo uprostřed
        var prvni = _slova[_vyberOd].Ramecek;
        double m = Meritko.ScaleX;
        FotkaScroll.ScrollToHorizontalOffset((prvni.X + prvni.Sirka / 2.0) * m - FotkaScroll.ViewportWidth / 2);
        FotkaScroll.ScrollToVerticalOffset((prvni.Y + prvni.Vyska / 2.0) * m - FotkaScroll.ViewportHeight / 2);
    }

    private void Slovo_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Run { Tag: int i }) return;
        if (e.ClickCount == 2) { Vyber(i, i, false); Oprav(i); }
        else if (Keyboard.Modifiers == ModifierKeys.Shift && _kotva >= 0) Vyber(_kotva, i, false);
        else { _kotva = i; Vyber(i, i, false); }
        e.Handled = true;
    }

    private void Slovo_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // pravé tlačítko mimo výběr vybere kliknuté slovo, uvnitř výběru ho nechá
        if (sender is Run { Tag: int i } && !JeVybrane(i)) { _kotva = i; Vyber(i, i, false); }
    }

    private void Fotka_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_vysledek is null) return;
        var bod = e.GetPosition(Fotka); // v pixelech fotky, protože Fotka má rozměry v pixelech
        var slovo = _vysledek.SlovoNa(bod.X, bod.Y);
        if (slovo is null || !_indexy.TryGetValue(slovo, out var i)) return;
        if (Keyboard.Modifiers == ModifierKeys.Shift && _kotva >= 0) Vyber(_kotva, i, true);
        else { _kotva = i; Vyber(i, i, true); }
    }

    // ---------- Oprava slova ----------

    private void OpravitSlovo_Click(object sender, RoutedEventArgs e)
    {
        if (_vyberOd >= 0) Oprav(_vyberOd);
    }

    private void Oprav(int i)
    {
        if (_spis is null || _strana is null || _vysledek is null) return;
        var slovo = _slova[i];
        var novy = VstupDialog.Zeptej(this, "Oprava slova", $"OCR přečetlo: {slovo.Text}", slovo.Platny);
        if (novy is null) return;
        novy = novy.Trim();
        slovo.Oprava = novy == slovo.Text ? null : novy;
        _spis.UlozOcr(_strana, _vysledek, Spis.Opraveno);
        ZobrazStranu();
        Vyber(i, i, false);
    }

    // ---------- Anonymizace ----------

    private async void NajitUdaje_Click(object sender, RoutedEventArgs e)
    {
        if (_spis is null) return;
        var spis = _spis;
        var klic = _klic;
        Stav.Text = "Hledám údaje ve spisu…";
        var nove = await Task.Run(() => Anonymizator.AktualizujKlic(spis, klic));
        Anonymizator.UlozKlic(spis, klic);
        NactiUdaje();
        ZobrazStranu();
        Stav.Text = $"Nalezeno {nove} nových údajů. Zkontroluj je v textu (fialově) a v seznamu vlevo dole.";
    }

    private void OznacitJako_Click(object sender, RoutedEventArgs e)
    {
        if (_spis is null || _vysledek is null || _vyberOd < 0) return;
        if (sender is not MenuItem { Tag: string tag } || !Enum.TryParse<TypUdaje>(tag, out var typ)) return;
        var entita = Anonymizator.OznacRucne(_klic, typ, Anonymizator.Slova(_vysledek), _vyberOd, _vyberDo);
        UlozKlicAObnov();
        Stav.Text = $"Označeno jako {entita.Id}. Stejné jméno se nahradí na všech stranách spisu, i v jiném pádě.";
    }

    private void Necitlive_Click(object sender, RoutedEventArgs e)
    {
        if (_spis is null || _vyberOd < 0) return;
        for (int i = _vyberOd; i <= _vyberDo; i++) Anonymizator.OznacNecitlive(_klic, _slova[i].Platny);
        UlozKlicAObnov();
    }

    private Entita? VybranyUdaj => (SeznamUdaju.SelectedItem as PolozkaUdaje)?.Entita;

    private void NastavAkci(Akce akce)
    {
        if (VybranyUdaj is not { } e) return;
        e.Akce = akce;
        UlozKlicAObnov();
    }

    private void UdajPseudonym_Click(object sender, RoutedEventArgs e) => NastavAkci(Akce.Pseudonym);
    private void UdajOdstranit_Click(object sender, RoutedEventArgs e) => NastavAkci(Akce.Odstranit);
    private void UdajPonechat_Click(object sender, RoutedEventArgs e) => NastavAkci(Akce.Ponechat);

    private void UdajRole_Click(object sender, RoutedEventArgs e)
    {
        if (VybranyUdaj is not { } u) return;
        var role = VstupDialog.Zeptej(this, "Role ve věci",
            $"Role pro {u.Id}, např. žalobce, žalovaný, svědek. Claude ji uvidí, skutečné jméno ne:", u.Role ?? "");
        if (role is null) return;
        u.Role = string.IsNullOrWhiteSpace(role) ? null : role.Trim();
        UlozKlicAObnov();
    }

    private void UdajSmazat_Click(object sender, RoutedEventArgs e)
    {
        if (VybranyUdaj is not { } u) return;
        _klic.Entity.Remove(u);
        UlozKlicAObnov();
    }

    private void UlozKlicAObnov()
    {
        if (_spis is null) return;
        Anonymizator.UlozKlic(_spis, _klic);
        int od = _vyberOd, @do = _vyberDo;
        NactiUdaje();
        ZobrazStranu();
        if (od >= 0 && @do < _slova.Count) Vyber(od, @do, false);
    }

    private void Nahled_Click(object sender, RoutedEventArgs e) => ZobrazStranu();

    private void Zkontrolovano_Click(object sender, RoutedEventArgs e)
    {
        if (_spis is null || _strana is null) return;
        _strana.Zkontrolovano = Zkontrolovano.IsChecked == true;
        _spis.Uloz();
        NactiStrany();
    }

    private void Exportovat_Click(object sender, RoutedEventArgs e)
    {
        if (_spis is null) return;
        var vysledek = Export.Proved(_spis, _klic);
        if (!vysledek.Uspech)
        {
            var seznam = string.Join("\n", vysledek.Problemy.Take(15));
            if (vysledek.Problemy.Count > 15) seznam += $"\n… a dalších {vysledek.Problemy.Count - 15}";
            MessageBox.Show(this, "Export se neprovedl, nic nebylo zapsáno:\n\n" + seznam, "Koncipient",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        MessageBox.Show(this,
            $"Anonymizovaný spis je ve složce\n{vysledek.Slozka}\n\n" +
            "Na počítač s Claude Code přenes celou složku ProClaude (flash diskem nebo sdílenou složkou, " +
            "ve které není nic jiného). Fotky ani klíč pseudonymů v ní nejsou.",
            "Koncipient", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ---------- Ruční otočení ----------

    private void OtocitVlevo_Click(object sender, RoutedEventArgs e) => OtocStranu(270);
    private void OtocitVpravo_Click(object sender, RoutedEventArgs e) => OtocStranu(90);

    private void OtocStranu(int stupne)
    {
        if (_spis is null || _strana is null || _zruseni is not null) return;
        if (_spis.MaOcr(_strana, Spis.Opraveno) &&
            MessageBox.Show(this, "Otočením se smažou ruční opravy na této straně. Pokračovat?", "Koncipient",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        _spis.OtocStranu(_strana, stupne);
        NactiStrany();
        ZobrazStranu(posunoutFotku: true);
        Stav.Text = "Strana otočena. Přepis se udělá znovu při příštím spuštění přepisu.";
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
            var spisy = _uloziste.Spisy();
            await Davka.ZpracujAsync(spisy, Motory.ProDavku(), prubeh, _zruseni.Token, Motory.Narovnani());

            // po přepisu rovnou najít údaje, ať jsou ráno připravené ke kontrole
            Stav.Text = "Hledám údaje…";
            await Task.Run(() =>
            {
                foreach (var spis in spisy)
                {
                    var klic = Anonymizator.NactiKlic(spis);
                    Anonymizator.AktualizujKlic(spis, klic);
                    Anonymizator.UlozKlic(spis, klic);
                }
            });
            Stav.Text = "Přepis dokončen a údaje vyhledány.";
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
        _spis = null; // znovu načíst spis a klíč z disku
        NactiSpisy(SeznamSpisu.SelectedItem is PolozkaSpisu p ? p.Spis.Id : null);
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
