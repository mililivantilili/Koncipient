using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Koncipient.Jadro;
using Koncipient.Jadro.Anonymizace;
using Koncipient.Ocr;

namespace Koncipient.App;

/// <summary>
/// Záložka Rychlá kontrola: místo procházení celých stran jen místa, kde program údaj nahradí,
/// a slova, která vypadají jako přehlédnutý údaj. Strana, na které je vše vyřízené,
/// se označí jako zkontrolovaná.
/// </summary>
public partial class HlavniOkno
{
    private List<MistoKontroly> _mista = new();
    private readonly HashSet<string> _vyrizeno = new();
    /// <summary>Strany, na kterých advokátka v rychlé kontrole něco posoudila.</summary>
    private readonly HashSet<int> _posouzeneStrany = new();
    private string? _rkSpis;
    private (int Cislo, BitmapSource Obrazek)? _rkFotka;

    private static string KlicMista(MistoKontroly m) => m.Klic;

    private void RkNacist_Click(object sender, RoutedEventArgs e) => RkNacti();

    private void RkNacti(string? vybrat = null)
    {
        if (_spis is null) return;
        if (_rkSpis != _spis.Id) { _vyrizeno.Clear(); _posouzeneStrany.Clear(); _rkSpis = _spis.Id; _rkFotka = null; }
        _mista = RychlaKontrola.Najdi(_spis, _klic);
        // strana, na které jsou všechna místa vyřízená (nebo zmizela po Není citlivé), je hotová
        OznacHotoveStrany(_spis.Strany.Where(s => _posouzeneStrany.Contains(s.Cislo)));

        RkSeznam.Items.Clear();
        foreach (var m in _mista.Where(m => !_vyrizeno.Contains(KlicMista(m))))
            RkSeznam.Items.Add(new ListBoxItem { Content = PopisMista(m), Tag = m });
        int bezNalezu = RychlaKontrola.StranyBezNalezu(_spis, _mista).Count(s => !s.Zkontrolovano);
        RkSouhrn.Text = RkSeznam.Items.Count == 0
            ? "Všechna místa jsou vyřízená."
            : $"Ke kontrole {RkSeznam.Items.Count} míst na {_mista.Where(m => !_vyrizeno.Contains(KlicMista(m))).SelectMany(m => m.Strany).Distinct().Count()} stranách. Enter = v pořádku, N = není citlivé, O = osoba, U = jiný údaj. "
              + "Kontrola ukazuje jen místa, která program našel: důležité strany projdi i v Náhledu pro Clauda.";
        RkBezNalezu.Content = $"Strany bez nálezů ({bezNalezu}): označit jako zkontrolované";
        RkBezNalezu.IsEnabled = bezNalezu > 0;

        var vyber = RkSeznam.Items.Cast<ListBoxItem>().FirstOrDefault(i => KlicMista((MistoKontroly)i.Tag) == vybrat)
                    ?? RkSeznam.Items.Cast<ListBoxItem>().FirstOrDefault();
        RkSeznam.SelectedItem = vyber;
        if (vyber is null) RkZobraz(null);
        else { RkSeznam.ScrollIntoView(vyber); vyber.Focus(); }
    }

    private string PopisMista(MistoKontroly m)
    {
        var strana = $"s. {_spis!.Poradi(m.Strana)}";
        var vic = m.Strany.Count > 1 ? $"   ({m.Pocet}× na {m.Strany.Count} stranách)" : m.Pocet > 1 ? $"   ({m.Pocet}×)" : "";
        return m.Druh == DruhMista.Nahrazeno
            ? $"{strana}   {m.Udaj}  →  {m.Nahrada}{vic}"
            : $"{strana}   ⚠ {m.Udaj}   zůstane, vypadá jako přehlédnutý údaj{vic}";
    }

    private MistoKontroly? VybraneMisto => (RkSeznam.SelectedItem as ListBoxItem)?.Tag as MistoKontroly;

    private void RkSeznam_SelectionChanged(object sender, SelectionChangedEventArgs e) => RkZobraz(VybraneMisto);

    private void RkZobraz(MistoKontroly? m)
    {
        RkKontext.Inlines.Clear();
        RkVyrez.Source = null;
        RkOk.IsEnabled = RkNecitlive.IsEnabled = m is not null;
        RkOsoba.IsEnabled = RkJiny.IsEnabled = m?.Druh == DruhMista.Podezrele;
        if (m is null || _spis is null) return;

        RkKontext.Inlines.Add(new Run("… " + m.Pred + " ") { Foreground = Brushes.Gray });
        RkKontext.Inlines.Add(new Run(m.Udaj) { FontWeight = FontWeights.Bold, Background = m.Druh == DruhMista.Nahrazeno ? Brushes.LightGreen : Brushes.Khaki });
        if (m.Druh == DruhMista.Nahrazeno) RkKontext.Inlines.Add(new Run("  →  " + m.Nahrada) { FontWeight = FontWeights.Bold, Foreground = Brushes.DarkGreen });
        RkKontext.Inlines.Add(new Run(" " + m.Po + " …") { Foreground = Brushes.Gray });

        try { RkVyrez.Source = Vyrez(m); }
        catch (Exception) { RkVyrez.Source = null; } // výřez je jen pomůcka
    }

    /// <summary>Výřez fotky kolem místa, aby šlo porovnat s přepisem bez přepínání.</summary>
    private BitmapSource? Vyrez(MistoKontroly m)
    {
        var v = Motory.ProZobrazeni(_spis!, m.Strana);
        if (v is null || m.Ramecek.Sirka <= 1) return null;
        if (_rkFotka?.Cislo != m.Strana.Cislo)
        {
            var b = new BitmapImage();
            b.BeginInit();
            b.CacheOption = BitmapCacheOption.OnLoad;
            b.UriSource = new Uri(_spis!.CestaFotky(m.Strana));
            b.EndInit();
            b.Freeze();
            _rkFotka = (m.Strana.Cislo, b);
        }
        var obrazek = _rkFotka.Value.Obrazek;
        double k = v.Sirka > 0 ? obrazek.PixelWidth / (double)v.Sirka : 1;
        var r = m.Ramecek;
        int okraj = (int)(r.Vyska * 2.5);
        int x0 = Math.Max(0, (int)((r.X - 12 * r.Vyska) * k)), y0 = Math.Max(0, (int)((r.Y - okraj) * k));
        int x1 = Math.Min(obrazek.PixelWidth, (int)((r.X + r.Sirka + 12 * r.Vyska) * k));
        int y1 = Math.Min(obrazek.PixelHeight, (int)((r.Y + r.Vyska + okraj) * k));
        if (x1 - x0 < 4 || y1 - y0 < 4) return null;
        return new CroppedBitmap(obrazek, new Int32Rect(x0, y0, x1 - x0, y1 - y0));
    }

    private void RkDalsi(MistoKontroly vyrizene)
    {
        _vyrizeno.Add(KlicMista(vyrizene));
        foreach (var s in vyrizene.Strany) _posouzeneStrany.Add(s.Cislo);
        int i = RkSeznam.SelectedIndex;
        var dalsi = RkSeznam.Items.Cast<ListBoxItem>().Skip(i + 1).Select(x => (MistoKontroly)x.Tag)
            .FirstOrDefault(x => !_vyrizeno.Contains(KlicMista(x)));
        RkNacti(dalsi is null ? null : KlicMista(dalsi));
    }

    private void RkOk_Click(object sender, RoutedEventArgs e)
    {
        if (VybraneMisto is { } m) RkDalsi(m);
    }

    private void RkNecitlive_Click(object sender, RoutedEventArgs e)
    {
        if (_spis is null || VybraneMisto is not { } m) return;
        // osoba nebo číslo by šly do exportu skutečně: jedna klávesa nestačí
        if (m.Entita is { Typ: not (TypUdaje.Adresa or TypUdaje.Firma or TypUdaje.SpisovaZnacka) } citlivy
            && MessageBox.Show(this,
                $"„{m.Udaj}“ ({Nazvy.Typ(citlivy.Typ)}) se pak ve všech stranách pošle Claudovi skutečně, bez náhrady.\n\nOpravdu to není citlivé?",
                "Není citlivé", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
            return;
        if (m.Entita is not null) m.Entita.Akce = Akce.Ponechat;
        else Anonymizator.OznacNecitlive(_klic, m.Udaj);
        Anonymizator.UlozKlic(_spis, _klic);
        Stav.Text = m.Entita is not null
            ? $"{m.Udaj}: ponecháno bez anonymizace ve všech stranách. Vrátit jde v záložce Osoby a údaje."
            : $"{m.Udaj}: označeno jako necitlivé.";
        RkDalsi(m);
    }

    private void RkOznacit_Click(object sender, RoutedEventArgs e)
    {
        if (_spis is null || VybraneMisto is not { Druh: DruhMista.Podezrele } m || sender is not Button { Tag: string tag }) return;
        var typ = Enum.Parse<TypUdaje>(tag);
        var v = Motory.ProZobrazeni(_spis, m.Strana);
        if (v is null) return;
        var entita = Anonymizator.OznacRucne(_klic, typ, Anonymizator.Slova(v), m.Od, m.Do);
        if (typ == TypUdaje.Osoba) Anonymizator.DoplnOsoby(_klic, SlovaSpisu());
        Anonymizator.UlozKlic(_spis, _klic);
        // nově označený údaj advokátka právě posoudila, už ho nehlásit
        _vyrizeno.Add("E:" + entita.Id);
        Stav.Text = $"{m.Udaj}: anonymizováno ve všech stranách spisu.";
        RkDalsi(m);
    }

    private void RkOtevrit_Click(object sender, RoutedEventArgs e)
    {
        if (VybraneMisto is not { } m) return;
        SeznamStran.SelectedItem = SeznamStran.Items.OfType<PolozkaStrany>().FirstOrDefault(p => p.Strana == m.Strana);
        Zalozky.SelectedIndex = 0;
        Dispatcher.BeginInvoke(new Action(() => { if (m.Do < _slova.Count) Vyber(m.Od, m.Do, true); }),
            System.Windows.Threading.DispatcherPriority.Background);
    }

    private void RkSeznam_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        Button? tlacitko = e.Key switch
        {
            Key.Enter => RkOk,
            Key.N => RkNecitlive,
            Key.O => RkOsoba,
            Key.U => RkJiny,
            _ => null,
        };
        if (tlacitko is null || !tlacitko.IsEnabled) return;
        tlacitko.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        e.Handled = true;
    }

    private void RkBezNalezu_Click(object sender, RoutedEventArgs e)
    {
        if (_spis is null) return;
        var strany = RychlaKontrola.StranyBezNalezu(_spis, _mista).Where(s => !s.Zkontrolovano).ToList();
        if (MessageBox.Show(this,
                $"Na {strany.Count} stranách program nenašel žádný údaj ani podezřelé slovo. Označit je jako zkontrolované?\n\n"
                + "Program nenajde všechno. U důležitých stran se vyplatí přesto zapnout Náhled pro Clauda.",
                "Rychlá kontrola", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        foreach (var s in strany) s.Zkontrolovano = true;
        _spis.Uloz();
        NactiStrany();
        RkNacti();
    }

    /// <summary>Strany, které měly místa ke kontrole a všechna jsou vyřízená, se označí jako zkontrolované.</summary>
    private void OznacHotoveStrany(IEnumerable<Strana> strany)
    {
        if (_spis is null) return;
        bool zmena = false;
        foreach (var s in strany)
        {
            if (s.Zkontrolovano || _mista.Any(m => m.Strany.Contains(s) && !_vyrizeno.Contains(KlicMista(m)))) continue;
            s.Zkontrolovano = true;
            zmena = true;
        }
        if (!zmena) return;
        _spis.Uloz();
        var vybrana = _strana;
        NactiStrany();
        if (vybrana is not null)
            SeznamStran.SelectedItem = SeznamStran.Items.OfType<PolozkaStrany>().FirstOrDefault(p => p.Strana == vybrana);
    }
}
