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
        // pravé tlačítko v seznamu stran nejdřív vybere stranu, na kterou se kliklo
        SeznamStran.PreviewMouseRightButtonDown += (_, e) =>
        {
            if (ItemsControl.ContainerFromElement(SeznamStran, (DependencyObject)e.OriginalSource) is ListBoxItem polozka)
                polozka.IsSelected = true;
        };
        NactiSpisy();
        Stav.Text = $"Spisy jsou ve složce {_uloziste.Slozka}";
        Loaded += (_, _) => Dispatcher.BeginInvoke(new Action(PripomenZalohu), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    // ---------- Klávesové zkratky (i pro agenty, kteří okno ovládají) ----------

    private void Okno_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox) return; // psaní v polích nechat být
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        void Klik(Button b) { if (b.IsEnabled) b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); }
        switch (e.Key)
        {
            case Key.N when ctrl: NovySpis_Click(this, e); break;
            case Key.O when ctrl && shift: Klik(PridatSlozku); break;
            case Key.O when ctrl: Klik(PridatFotky); break;
            case Key.F5: Klik(SpustitPrepis); break;
            case Key.E when ctrl: Klik(Exportovat); break;
            case Key.F when ctrl: if (_spis is not null) HledatOtevrit(); break;
            case Key.K when ctrl:
                if (Zkontrolovano.IsEnabled) { Zkontrolovano.IsChecked = Zkontrolovano.IsChecked != true; Zkontrolovano_Click(this, e); }
                break;
            case Key.PageDown when ctrl: SeznamStran.SelectedIndex = Math.Min(SeznamStran.SelectedIndex + 1, SeznamStran.Items.Count - 1); break;
            case Key.PageUp when ctrl: SeznamStran.SelectedIndex = Math.Max(SeznamStran.SelectedIndex - 1, 0); break;
            default: return;
        }
        e.Handled = true;
    }

    // ---------- Spisy a strany ----------

    private sealed record PolozkaSpisu(Spis Spis)
    {
        public override string ToString() =>
            string.IsNullOrWhiteSpace(Spis.Nazev) ? Spis.Id : $"{Spis.Id}  {Spis.Nazev}";
    }

    private sealed record PolozkaStrany(Spis Spis, Strana Strana, string Skupina)
    {
        public override string ToString()
        {
            var znacka = Spis.MaOcr(Strana, Spis.Opraveno) ? "✎"
                : Motory.PoradiZobrazeni.Any(m => Spis.MaOcr(Strana, m)) ? "✓" : "…";
            var kontrola = Strana.Zkontrolovano ? " ☑" : "";
            return $"{znacka}{kontrola}  Strana {Spis.Poradi(Strana)}   {Strana.PuvodniNazev}";
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
        if (_spis is null) { SeznamStran.ItemsSource = null; return; }
        // strany seskupené pod nadpisy dokumentů
        var polozky = _spis.StranyPoDokumentech()
            .SelectMany((x, k) => x.Strany.Select(s => new PolozkaStrany(_spis, s, $"{k + 1}. {x.Dokument.Nazev}  ({x.Strany.Count} str.)")))
            .ToList();
        var pohled = new System.Windows.Data.ListCollectionView(polozky);
        pohled.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(nameof(PolozkaStrany.Skupina)));
        SeznamStran.ItemsSource = pohled;
        SeznamStran.SelectedItem = SeznamStran.Items.Cast<PolozkaStrany>().FirstOrDefault(p => p.Strana.Cislo == cislo)
            ?? SeznamStran.Items.Cast<PolozkaStrany>().FirstOrDefault();
    }

    /// <summary>Řádek tabulky Osoby a údaje. Úpravy se rovnou propisují do klíče.</summary>
    private sealed class RadekTabulky(Entita e, bool odsazeny, Action<RadekTabulky> zmenaInicial)
    {
        public Entita Entita { get; } = e;

        public string Udaj => (odsazeny ? "      └ " : "") + Nazvy.Typ(Entita.Typ)
            + (Entita.Typ == TypUdaje.Osoba && Entita.Rod is { } r ? (r == Rod.Zena ? " (žena)" : " (muž)") : "");

        public string Skutecnost
        {
            get => Entita.Popis;
            set
            {
                // u osoby jde opravit jméno v 1. pádě, které se vrací do dokumentů
                if (Entita.Typ != TypUdaje.Osoba) return;
                var slova = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (slova.Length == 0) return;
                Entita.Prijmeni = slova[^1];
                Entita.Jmeno = slova.Length > 1 ? string.Join(" ", slova[..^1]) : null;
                Entita.Rod = Sklonovani.UrciRod(Entita.Jmeno, Entita.Prijmeni);
                Entita.JmenoRucne = true; // program už jméno z textu znovu neodvozuje
            }
        }

        public string Nahrada
        {
            get => Entita.TextNahrady;
            set
            {
                var v = value.Trim();
                if (v == Entita.TextNahrady) return;
                Entita.Akce = Akce.Pseudonym;
                var slova = v.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                // jméno a příjmení se dál skloňují, cokoli jiného je pevná náhrada
                if (Entita.Typ == TypUdaje.Osoba && slova.Length is 1 or 2 && slova.All(s => char.IsUpper(s[0]) && s.All(char.IsLetter)))
                {
                    Entita.PseudoPrijmeni = slova[^1];
                    Entita.PseudoJmeno = slova.Length == 2 ? slova[0] : null;
                    Entita.VlastniNahrada = null;
                }
                else Entita.VlastniNahrada = v.Length == 0 ? null : v;
            }
        }

        public string? Role
        {
            get => Entita.Role;
            set => Entita.Role = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        public string AkceText => Entita.Akce switch
        {
            Akce.Odstranit => "odstranit",
            Akce.Ponechat => "neanonymizovat",
            _ => "nahradit",
        };

        public bool Inicialy
        {
            get => Entita.Inicialy;
            set
            {
                if (Entita.Typ != TypUdaje.Osoba || value == Entita.Inicialy) return;
                Entita.Inicialy = value;
                zmenaInicial(this);
            }
        }

        public string Tvary => string.Join(" · ", Entita.Tvary.Select(t => string.Join(" ", t)).Distinct());
    }

    private void NactiUdaje()
    {
        // osoby a firmy, pod nimi jejich údaje, nakonec ostatní
        var radky = new List<RadekTabulky>();
        void Pridej(Entita e, bool odsazeny) => radky.Add(new RadekTabulky(e, odsazeny, ZmenaInicial));
        var hlavni = _klic.Entity.Where(e => e.Typ is TypUdaje.Osoba or TypUdaje.Firma)
            .OrderBy(e => e.Typ).ThenBy(e => e.Id, StringComparer.Ordinal).ToList();
        foreach (var h in hlavni)
        {
            Pridej(h, false);
            foreach (var u in _klic.Entity.Where(u => u.PatriK == h.Id)) Pridej(u, true);
        }
        foreach (var u in _klic.Entity.Where(u => !hlavni.Contains(u) && _klic.Podle(u.PatriK) is null)
                     .OrderBy(e => e.Typ).ThenBy(e => e.Id, StringComparer.Ordinal))
            Pridej(u, false);
        TabulkaUdaju.ItemsSource = radky;

        _nacitamStyl = true;
        StylOsob.SelectedItem = StylOsob.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == _klic.Styl.ToString());
        _nacitamStyl = false;
    }

    private bool _nacitamStyl;

    /// <summary>Všechna slova spisu, aby se vymyšlené jméno nepodobalo ničemu ve spisu.</summary>
    private List<string> SlovaSpisu() => _spis is null ? new()
        : _spis.Strany.Select(s => Motory.ProZobrazeni(_spis, s)).OfType<VysledekOcr>()
            .SelectMany(Anonymizator.Slova).ToList();

    private void ZmenaInicial(RadekTabulky radek)
    {
        Anonymizator.NovePseudonym(_klic, radek.Entita, SlovaSpisu());
        Dispatcher.BeginInvoke(new Action(UlozKlicAObnov));
    }

    private void SeznamSpisu_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _spis = (SeznamSpisu.SelectedItem as PolozkaSpisu)?.Spis;
        _klic = _spis is null ? new Klic() : Anonymizator.NactiKlic(_spis);
        _strana = null;
        PridatFotky.IsEnabled = PridatSlozku.IsEnabled = Oddelovace.IsEnabled = PdfArchiv.IsEnabled = ZaznamExportu.IsEnabled = NajitUdaje.IsEnabled = Exportovat.IsEnabled = VratitJmena.IsEnabled = _spis is not null;
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
            Title = "Vyber fotky stran nebo PDF",
            Multiselect = true,
            Filter = "Fotky a PDF|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.pdf|Všechny soubory|*.*",
        };
        if (dialog.ShowDialog(this) != true) return;

        var spis = _spis;
        Stav.Text = $"Přidávám {dialog.FileNames.Length} souborů…";
        IsEnabled = false;
        try
        {
            int dokumentuPred = spis.Dokumenty.Count;
            var oddelovacePred = spis.Oddelovace.ToList();
            var nove = await Task.Run(() => spis.PridejSoubory(dialog.FileNames));
            IsEnabled = true;
            int vraceno = ZkontrolujOddelovace(spis, oddelovacePred);
            int cernych = spis.Oddelovace.Count(o => !oddelovacePred.Contains(o));
            Stav.Text = $"Přidáno {nove.Count + vraceno} stran v {spis.Dokumenty.Count - dokumentuPred} dokumentech"
                + (cernych > 0 ? $" ({cernych} černé fotky oddělily dokumenty)" : "") + ". Přepis spustíš tlačítkem Spustit přepis.";
        }
        catch (Exception ex) when (ex is System.IO.InvalidDataException or System.IO.IOException or UnauthorizedAccessException)
        {
            // poškozené nebo zamčené PDF či fotka: strany přidané před chybou zůstávají
            Stav.Text = "Přidávání se zastavilo: " + ex.Message;
            MessageBox.Show(this, ex.Message + "\n\nStrany přidané před tímto souborem ve spisu zůstaly.",
                "Koncipient: soubor nejde přidat", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsEnabled = true;
        }
        NactiStrany();
    }

    private async void PridatSlozku_Click(object sender, RoutedEventArgs e)
    {
        if (_spis is null) return;
        var dialog = new OpenFolderDialog { Title = "Vyber složku s fotkami (podsložky = dokumenty)" };
        if (dialog.ShowDialog(this) != true) return;
        var spis = _spis;
        int pred = spis.Dokumenty.Count;
        Stav.Text = "Přidávám složku…";
        IsEnabled = false;
        try
        {
            var oddelovacePred = spis.Oddelovace.ToList();
            var nove = await Task.Run(() => spis.PridejSlozku(dialog.FolderName));
            IsEnabled = true;
            int vraceno = ZkontrolujOddelovace(spis, oddelovacePred);
            Stav.Text = $"Přidáno {nove.Count + vraceno} stran v {spis.Dokumenty.Count - pred} dokumentech. Přepis spustíš tlačítkem Spustit přepis.";
        }
        catch (Exception ex) when (ex is System.IO.InvalidDataException or System.IO.IOException or UnauthorizedAccessException)
        {
            // poškozené nebo zamčené PDF či fotka: strany přidané před chybou zůstávají
            Stav.Text = "Přidávání se zastavilo: " + ex.Message;
            MessageBox.Show(this, ex.Message + "\n\nStrany přidané před tímto souborem ve spisu zůstaly.",
                "Koncipient: soubor nejde přidat", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsEnabled = true;
        }
        NactiStrany();
    }

    /// <summary>
    /// Ukáže oddělovače z právě proběhlého importu a zaškrtnuté vrátí mezi strany.
    /// Vrátí počet vrácených.
    /// </summary>
    private int ZkontrolujOddelovace(Spis spis, IReadOnlyCollection<Oddelovac> pred)
    {
        var nove = spis.Oddelovace.Where(o => !pred.Contains(o)).ToList();
        if (nove.Count == 0) return 0;
        var vratit = OddelovaceDialog.Zkontroluj(this, spis, nove);
        foreach (var o in vratit) spis.VratOddelovac(o);
        return vratit.Count;
    }

    private void Oddelovace_Click(object sender, RoutedEventArgs e)
    {
        if (_spis is null) return;
        if (_spis.Oddelovace.Count == 0)
        {
            MessageBox.Show(this, "Spis nemá žádné fotky vzaté jako oddělovače dokumentů.", "Oddělovače dokumentů");
            return;
        }
        int vraceno = ZkontrolujOddelovace(_spis, Array.Empty<Oddelovac>());
        if (vraceno > 0) { Stav.Text = $"Vráceno {vraceno} fotek mezi strany."; NactiStrany(); }
    }

    // ---------- Dokumenty a pořadí stran ----------

    private void PoUpraveDokumentu()
    {
        NactiStrany();
        if (_strana is not null)
            SeznamStran.SelectedItem = SeznamStran.Items.Cast<PolozkaStrany>().FirstOrDefault(p => p.Strana == _strana);
    }

    private void PosunoutStranu_Click(object sender, RoutedEventArgs e)
    {
        if (_spis is null || _strana is null || sender is not MenuItem { Tag: string tag }) return;
        _spis.PosunStranu(_strana, int.Parse(tag));
        PoUpraveDokumentu();
    }

    private void PresunoutDoDokumentu_Click(object sender, RoutedEventArgs e)
    {
        if (_spis is null || _strana is null) return;
        var moznosti = _spis.StranyPoDokumentech().Select(x => x.Dokument)
            .Where(d => d.Id != _strana.Dokument).Append(new Dokument { Id = "", Nazev = "(nový dokument)" }).ToList();
        var cil = VyberDialog.Vyber(this, "Přesunout stranu", "Do kterého dokumentu stranu přesunout (zařadí se na jeho konec):",
            moznosti, d => d.Nazev);
        if (cil is null) return;
        string? nazev = null;
        if (cil.Id == "")
        {
            nazev = VstupDialog.Zeptej(this, "Nový dokument", "Název nového dokumentu (prázdný = navrhne se z textu):", "");
            if (nazev is null) return;
        }
        _spis.PresunDoDokumentu(_strana, cil.Id == "" ? null : cil.Id, nazev);
        PoUpraveDokumentu();
    }

    private void ZacitDokument_Click(object sender, RoutedEventArgs e)
    {
        if (_spis is null || _strana is null) return;
        var nazev = VstupDialog.Zeptej(this, "Nový dokument", "Název dokumentu, který začne touto stranou (prázdný = navrhne se z textu):", "");
        if (nazev is null) return;
        _spis.ZacniDokument(_strana, nazev);
        if (string.IsNullOrWhiteSpace(nazev)) _spis.NavrhniNazvy();
        PoUpraveDokumentu();
    }

    private void SpojitDokument_Click(object sender, RoutedEventArgs e)
    {
        if (_spis is null || _strana is null) return;
        _spis.SpojSPredchozim(_strana);
        PoUpraveDokumentu();
    }

    private void PrejmenovatDokument_Click(object sender, RoutedEventArgs e)
    {
        if (_spis?.DokumentStrany(_strana!) is not { } d) return;
        var nazev = VstupDialog.Zeptej(this, "Přejmenovat dokument",
            "Název dokumentu (do exportu pro Clauda jde anonymizovaný):", d.Nazev);
        if (string.IsNullOrWhiteSpace(nazev)) return;
        d.Nazev = nazev.Trim();
        d.NavrhnoutNazev = false;
        _spis.Uloz();
        PoUpraveDokumentu();
    }

    private void NavrhnoutNazev_Click(object sender, RoutedEventArgs e)
    {
        if (_spis?.DokumentStrany(_strana!) is not { } d) return;
        d.NavrhnoutNazev = true;
        if (_spis.NavrhniNazvy() == 0)
        {
            d.NavrhnoutNazev = false;
            Stav.Text = "Název nejde navrhnout, první strana dokumentu zatím nemá přepis.";
        }
        PoUpraveDokumentu();
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
        if (udaj is not null) popis.Add($"{udaj.TextNahrady}  ({Nazvy.Typ(udaj.Typ)}{(udaj.Role is null ? "" : ", " + udaj.Role)})");
        if (slovo.Oprava is not null) popis.Add(slovo.Text.Length == 0 ? "Doplněno ručně" : $"Opraveno, OCR četlo: {slovo.Text}");
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

    // Ctrl + tažení na fotce označí oblast s textem, který OCR vynechalo
    private Point? _tahOd;
    private Rectangle? _tahRamecek;

    private void Fotka_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_vysledek is null && _strana is null) return;
        if (Keyboard.Modifiers == ModifierKeys.Control && _strana is not null)
        {
            _tahOd = e.GetPosition(Fotka);
            _tahRamecek = new Rectangle { Stroke = Brushes.SeaGreen, StrokeThickness = 4, Fill = new SolidColorBrush(Color.FromArgb(0x30, 0x2E, 0x8B, 0x57)) };
            Vrstva.Children.Add(_tahRamecek);
            Fotka.CaptureMouse();
            e.Handled = true;
            return;
        }
        if (_vysledek is null) return;
        var bod = e.GetPosition(Fotka); // v pixelech fotky, protože Fotka má rozměry v pixelech
        var slovo = _vysledek.SlovoNa(bod.X, bod.Y);
        if (slovo is null || !_indexy.TryGetValue(slovo, out var i)) return;
        if (Keyboard.Modifiers == ModifierKeys.Shift && _kotva >= 0) Vyber(_kotva, i, true);
        else { _kotva = i; Vyber(i, i, true); }
    }

    private void Fotka_MouseMove(object sender, MouseEventArgs e)
    {
        if (_tahOd is not Point od || _tahRamecek is null) return;
        var bod = e.GetPosition(Fotka);
        Canvas.SetLeft(_tahRamecek, Math.Min(od.X, bod.X));
        Canvas.SetTop(_tahRamecek, Math.Min(od.Y, bod.Y));
        _tahRamecek.Width = Math.Abs(bod.X - od.X);
        _tahRamecek.Height = Math.Abs(bod.Y - od.Y);
    }

    private void Fotka_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_tahOd is not Point od) return;
        Fotka.ReleaseMouseCapture();
        var bod = e.GetPosition(Fotka);
        _tahOd = null;
        if (_tahRamecek is not null) Vrstva.Children.Remove(_tahRamecek);
        _tahRamecek = null;
        var oblast = new Obdelnik((int)Math.Min(od.X, bod.X), (int)Math.Min(od.Y, bod.Y),
            (int)Math.Abs(bod.X - od.X), (int)Math.Abs(bod.Y - od.Y));
        if (oblast.Sirka < 5 || oblast.Vyska < 5) return;
        var text = VstupDialog.Zeptej(this, "Doplnit vynechaný text", "Text v označené oblasti fotky:", "");
        if (string.IsNullOrWhiteSpace(text) || _spis is null || _strana is null) return;
        _vysledek ??= new VysledekOcr { Motor = Spis.Opraveno };
        int prvni = UpravyStrany.VlozRadek(_vysledek, oblast, text);
        UlozUpravu(prvni);
    }

    // ---------- Úpravy textu ----------

    /// <summary>Uloží upravený přepis jako ruční opravu strany a zobrazí ho znovu.</summary>
    private void UlozUpravu(int vybrat = -1)
    {
        if (_spis is null || _strana is null || _vysledek is null) return;
        _spis.UlozOcr(_strana, _vysledek, Spis.Opraveno);
        ZobrazStranu();
        if (vybrat >= 0 && vybrat < _slova.Count) Vyber(vybrat, vybrat, true);
    }

    private void DoplnitZa_Click(object sender, RoutedEventArgs e)
    {
        if (_vysledek is null || _vyberOd < 0) return;
        var text = VstupDialog.Zeptej(this, "Doplnit vynechaný text", $"Text, který chybí za slovem „{_slova[_vyberDo].Platny}“:", "");
        if (string.IsNullOrWhiteSpace(text)) return;
        UpravyStrany.VlozZa(_vysledek, _vyberDo, text);
        UlozUpravu(_vyberDo + 1);
    }

    private void SmazatSlova_Click(object sender, RoutedEventArgs e)
    {
        if (_vysledek is null || _vyberOd < 0) return;
        UpravyStrany.Smaz(_vysledek, _vyberOd, _vyberDo);
        UlozUpravu();
    }

    private void Zalomit_Click(object sender, RoutedEventArgs e)
    {
        if (_vysledek is null || _vyberOd < 0) return;
        int i = _vyberOd;
        UpravyStrany.Zalom(_vysledek, i);
        UlozUpravu(i);
    }

    private void Spojit_Click(object sender, RoutedEventArgs e)
    {
        if (_vysledek is null || _vyberOd < 0) return;
        int i = _vyberOd;
        UpravyStrany.SpojSDalsim(_vysledek, i);
        UlozUpravu(i);
    }

    private void PosunoutRadek_Click(object sender, RoutedEventArgs e)
    {
        if (_vysledek is null || _vyberOd < 0 || sender is not MenuItem { Tag: string tag }) return;
        var slovo = _slova[_vyberOd];
        UpravyStrany.Posun(_vysledek, _vyberOd, int.Parse(tag));
        UlozUpravu(_vysledek.Slova.ToList().IndexOf(slovo));
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
        if (novy.Contains(' '))
        {
            // víc slov: první nahradí opravované slovo, ostatní se doplní za něj
            var casti = novy.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            slovo.Oprava = casti[0] == slovo.Text ? null : casti[0];
            if (casti.Length > 1) UpravyStrany.VlozZa(_vysledek, i, casti[1]);
        }
        else if (novy.Length == 0) UpravyStrany.Smaz(_vysledek, i, i);
        else slovo.Oprava = novy == slovo.Text ? null : novy;
        UlozUpravu(Math.Min(i, _vysledek.Slova.Count() - 1));
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
        if (typ == TypUdaje.Osoba) Anonymizator.DoplnOsoby(_klic, SlovaSpisu()); // hned vymyšlené jméno, ne [OSOBA_…]
        UlozKlicAObnov();
        Stav.Text = $"Označeno jako {entita.Id}. Stejné jméno se nahradí na všech stranách spisu, i v jiném pádě.";
    }

    private void Necitlive_Click(object sender, RoutedEventArgs e)
    {
        if (_spis is null || _vyberOd < 0) return;
        // údaje, které výběr zasahuje, se nechají neanonymizované (i víceslovné, např. adresa soudu)
        var udaje = Enumerable.Range(_vyberOd, _vyberDo - _vyberOd + 1).Select(i => _udaje[i]).OfType<Entita>().Distinct().ToList();
        foreach (var u in udaje) u.Akce = Akce.Ponechat;
        if (udaje.Count == 0)
            for (int i = _vyberOd; i <= _vyberDo; i++) Anonymizator.OznacNecitlive(_klic, _slova[i].Platny);
        UlozKlicAObnov();
        Stav.Text = udaje.Count > 0
            ? $"Ponecháno bez anonymizace: {string.Join(", ", udaje.Select(u => u.Id))}. Vrátit jde v záložce Osoby a údaje."
            : "Slova označena jako necitlivá.";
    }

    private List<Entita> VybraneUdaje() => TabulkaUdaju.SelectedItems.OfType<RadekTabulky>().Select(r => r.Entita).ToList();

    private void NastavAkci(Akce akce)
    {
        foreach (var e in VybraneUdaje()) e.Akce = akce;
        UlozKlicAObnov();
    }

    private void UdajPseudonym_Click(object sender, RoutedEventArgs e) => NastavAkci(Akce.Pseudonym);
    private void UdajOdstranit_Click(object sender, RoutedEventArgs e) => NastavAkci(Akce.Odstranit);
    private void UdajPonechat_Click(object sender, RoutedEventArgs e) => NastavAkci(Akce.Ponechat);

    private void UdajNoveJmeno_Click(object sender, RoutedEventArgs e)
    {
        var slova = SlovaSpisu();
        foreach (var u in VybraneUdaje().Where(u => u.Typ == TypUdaje.Osoba)) Anonymizator.NovePseudonym(_klic, u, slova);
        UlozKlicAObnov();
    }

    private void UdajSmazat_Click(object sender, RoutedEventArgs e)
    {
        foreach (var u in VybraneUdaje())
        {
            _klic.Entity.Remove(u);
            foreach (var z in _klic.Entity.Where(z => z.PatriK == u.Id)) z.PatriK = null;
        }
        UlozKlicAObnov();
    }

    private void TabulkaUdaju_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) return;
        // hodnota se do řádku zapíše až po události, uložit hned potom
        Dispatcher.BeginInvoke(new Action(UlozKlicAObnov), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void StylOsob_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_nacitamStyl || _spis is null || StylOsob.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
        var styl = Enum.Parse<StylPseudonymu>(tag);
        if (styl == _klic.Styl) return;
        _klic.Styl = styl;
        foreach (var o in _klic.Entity.Where(o => o.Typ == TypUdaje.Osoba && o.VlastniNahrada is null))
            o.PseudoJmeno = o.PseudoPrijmeni = null;
        Anonymizator.DoplnOsoby(_klic, SlovaSpisu());
        UlozKlicAObnov();
    }

    private void Prevzit_Click(object sender, RoutedEventArgs e)
    {
        if (_spis is null) return;
        var jine = _uloziste.Spisy().Where(s => s.Id != _spis.Id).ToList();
        if (jine.Count == 0) { MessageBox.Show(this, "Jiný spis zatím není.", "Koncipient"); return; }
        var zdroj = VyberDialog.Vyber(this, "Převzít z jiného spisu",
            "Stejné osoby a údaje dostanou stejné náhrady jako ve vybraném spisu:", jine,
            s => string.IsNullOrWhiteSpace(s.Nazev) ? s.Id : $"{s.Id}  {s.Nazev}");
        if (zdroj is null) return;
        var (prevzato, pridano) = Anonymizator.Prevezmi(_klic, Anonymizator.NactiKlic(zdroj));
        UlozKlicAObnov();
        Stav.Text = $"Převzato {prevzato} údajů, přidáno {pridano} osob a údajů, které se zatím v tomto spisu nevyskytly.";
    }

    private void Zalozky_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource == Zalozky && Zalozky.SelectedIndex == 1) NactiUdaje();
        if (e.OriginalSource == Zalozky && Zalozky.SelectedIndex == 2) RkNacti();
        if (e.OriginalSource == Zalozky && Zalozky.SelectedIndex == 4) DataNacti();
    }

    private void VratitJmena_Click(object sender, RoutedEventArgs e)
    {
        if (_spis is null) return;
        new ZpetneOkno(_spis, _klic) { Owner = this }.Show();
    }

    private void UlozKlicAObnov()
    {
        if (_spis is null) return;
        TabulkaUdaju.CommitEdit(DataGridEditingUnit.Row, true);
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

    // ---------- Ruční otočení ----------

    private void OtocitVlevo_Click(object sender, RoutedEventArgs e) => OtocStranu(270);
    private void OtocitVpravo_Click(object sender, RoutedEventArgs e) => OtocStranu(90);

    private void OtocStranu(int stupne)
    {
        if (_spis is null || _strana is null || _zruseni is not null) return;
        bool obnoveno = _spis.OtocStranu(_strana, stupne);
        NactiStrany();
        ZobrazStranu(posunoutFotku: true);
        Stav.Text = obnoveno
            ? "Strana otočena, přepis i opravy pro toto natočení se vrátily."
            : "Strana otočena. Přepis tohoto natočení se udělá při příštím spuštění přepisu, dosavadní zůstává odložený.";
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
        e.Handled = true;
        // bod pod myší zůstane po zvětšení na stejném místě
        var naFotce = e.GetPosition(Platno);       // v pixelech fotky (před zvětšením)
        var veVyrezu = e.GetPosition(FotkaScroll); // poloha myši v okně
        Zvetseni.Value = Math.Clamp(Zvetseni.Value * (e.Delta > 0 ? 1.15 : 1 / 1.15), Zvetseni.Minimum, Zvetseni.Maximum);
        FotkaScroll.UpdateLayout();
        FotkaScroll.ScrollToHorizontalOffset(naFotce.X * Meritko.ScaleX - veVyrezu.X);
        FotkaScroll.ScrollToVerticalOffset(naFotce.Y * Meritko.ScaleY - veVyrezu.Y);
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
        bool prepisDobehl = false;
        Title = "Koncipient – přepis běží…";
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
            var chyby = (await Davka.ZpracujAsync(spisy, Motory.ProDavku(), prubeh, _zruseni.Token, Motory.Narovnani())).ToList();

            // po přepisu rovnou najít údaje, ať jsou ráno připravené ke kontrole
            prepisDobehl = true;
            Stav.Text = "Hledám údaje…";
            await Task.Run(() =>
            {
                foreach (var spis in spisy)
                {
                    try
                    {
                        var klic = Anonymizator.NactiKlic(spis);
                        Anonymizator.AktualizujKlic(spis, klic);
                        Anonymizator.UlozKlic(spis, klic);
                    }
                    catch (Exception ex) { chyby.Add(new ChybaStrany(spis.Id, 0, "hledání údajů", ex.Message)); }
                }
            });
            // stav strany se po načtení přepíše, konec dávky proto zůstane v titulku okna
            if (chyby.Count == 0)
            {
                Stav.Text = "Přepis dokončen a údaje vyhledány.";
                Title = $"Koncipient – přepis dokončen {DateTime.Now:d. M. H:mm}";
            }
            else
            {
                Stav.Text = $"Přepis dokončen, {chyby.Count} stran se nepodařilo zpracovat.";
                Title = $"Koncipient – přepis dokončen s chybami {DateTime.Now:d. M. H:mm}";
                MessageBox.Show(this,
                    "Přepis doběhl, ale tyto strany se nepodařilo zpracovat. Ostatní strany jsou hotové.\n\n"
                    + string.Join("\n", chyby.Take(15).Select(c => c.Strana > 0 ? $"• Spis {c.Spis}, strana {c.Strana} ({c.Krok})"
                        : c.Spis.Length > 0 ? $"• Spis {c.Spis} ({c.Krok})" : $"• {c.Krok}: {c.Zprava}"))
                    + (chyby.Count > 15 ? $"\n… a dalších {chyby.Count - 15}" : "")
                    + "\n\nPři dalším spuštění přepisu se zkusí znovu. Když chyba zůstane, pošli prosím Matějovi číslo spisu a strany (ne fotku).",
                    "Koncipient: některé strany se nepřepsaly", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (OperationCanceledException)
        {
            Title = "Koncipient";
            Stav.Text = "Přepis zastaven. Při dalším spuštění se pokračuje tam, kde skončil.";
        }
        catch (Exception ex)
        {
            Title = "Koncipient";
            Stav.Text = "Přepis skončil chybou.";
            MessageBox.Show(this, "Přepis se zastavil kvůli chybě. Hotové strany zůstávají uložené a při dalším spuštění se pokračuje.\n\n"
                + "Podrobnosti (anglicky): " + ex.Message, "Koncipient: chyba přepisu", MessageBoxButton.OK, MessageBoxImage.Error);
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
        if (prepisDobehl) NabidniPrefoceni();
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
