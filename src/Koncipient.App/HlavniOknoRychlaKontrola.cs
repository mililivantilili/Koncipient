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
/// Záložka Rychlá kontrola: nejdřív místa, kde program údaj nahradí, a slova, která vypadají
/// jako přehlédnutý údaj. Když má strana všechna místa vyřízená, ukáže se celá tak, jak ji
/// dostane Claude, a teprve potvrzením celé strany je strana zkontrolovaná (Matěj 2026-11-04).
/// </summary>
public partial class HlavniOkno
{
    private List<MistoKontroly> _mista = new();
    private readonly HashSet<string> _vyrizeno = new();
    private string? _rkSpis;
    private (int Cislo, BitmapSource Obrazek)? _rkFotka;

    private static string KlicMista(MistoKontroly m) => m.Klic;

    private void RkNacist_Click(object sender, RoutedEventArgs e) => RkNacti();

    private void RkNacti(string? vybrat = null)
    {
        if (_spis is null) return;
        if (_rkSpis != _spis.Id) { _vyrizeno.Clear(); _rkSpis = _spis.Id; _rkFotka = null; }
        _mista = RychlaKontrola.Najdi(_spis, _klic);

        // pořadí podle stran: místa strany, pak celá strana k potvrzení, jakmile jsou její místa vyřízená
        var polozky = new List<(int Poradi, int Druh, ListBoxItem Polozka)>();
        foreach (var m in _mista.Where(m => !_vyrizeno.Contains(KlicMista(m))))
            polozky.Add((_spis.Poradi(m.Strana), 0, new ListBoxItem { Content = PopisMista(m), Tag = m }));
        foreach (var s in _spis.Strany.Where(PripravenaKPotvrzeni))
            polozky.Add((_spis.Poradi(s), 1, new ListBoxItem
            {
                Content = $"s. {_spis.Poradi(s)}   ☐ celá strana k potvrzení (jak ji dostane Claude)",
                Tag = new PotvrzeniStrany(s), FontWeight = FontWeights.SemiBold,
            }));
        RkSeznam.Items.Clear();
        foreach (var p in polozky.OrderBy(p => p.Poradi).ThenBy(p => p.Druh)) RkSeznam.Items.Add(p.Polozka);

        int mist = polozky.Count(p => p.Druh == 0), stran = polozky.Count(p => p.Druh == 1);
        RkSouhrn.Text = RkSeznam.Items.Count == 0
            ? "Všechny strany jsou zkontrolované."
            : $"Ke kontrole {mist} míst a {stran} stran k potvrzení. U místa: Enter = v pořádku, N = není citlivé, O = osoba, U = jiný údaj. "
              + "Celou stranu si přečti a potvrď Enterem, teprve pak je zkontrolovaná.";

        var vyber = RkSeznam.Items.Cast<ListBoxItem>().FirstOrDefault(i => i.Tag is MistoKontroly m && KlicMista(m) == vybrat
                                                                       || i.Tag is PotvrzeniStrany p && $"S:{p.Strana.Cislo}" == vybrat)
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

    /// <summary>Položka seznamu: celá strana k potvrzení.</summary>
    private sealed record PotvrzeniStrany(Strana Strana);

    private PotvrzeniStrany? VybranaStrana => (RkSeznam.SelectedItem as ListBoxItem)?.Tag as PotvrzeniStrany;

    /// <summary>Strana čeká na potvrzení, když není zkontrolovaná a nemá žádné nevyřízené místo.</summary>
    private bool PripravenaKPotvrzeni(Strana s) =>
        !s.Zkontrolovano && Motory.ProZobrazeni(_spis!, s) is not null
        && !_mista.Any(m => m.Strany.Contains(s) && !_vyrizeno.Contains(KlicMista(m)));

    private void RkSeznam_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VybranaStrana is { } p) RkZobrazStranu(p.Strana);
        else RkZobraz(VybraneMisto);
    }

    /// <summary>Celá strana v textu pro Clauda, fotka strany vpravo.</summary>
    private void RkZobrazStranu(Strana strana)
    {
        RkKontext.Inlines.Clear();
        RkVyrez.Source = null;
        RkVyrez.Visibility = Visibility.Collapsed;
        RkOk.IsEnabled = true;
        RkOk.Content = "Strana je v pořádku (Enter)";
        RkNecitlive.IsEnabled = RkOsoba.IsEnabled = RkJiny.IsEnabled = false;
        var v = Motory.ProZobrazeni(_spis!, strana);
        if (v is null) return;
        RkKontext.Inlines.Add(new Run($"Strana {_spis!.Poradi(strana)}, jak ji dostane Claude. Fotka je vpravo. "
            + "Když v textu zůstalo skutečné jméno nebo číslo, klikni na Otevřít stranu a označ ho.\n\n")
            { Foreground = Brushes.Gray, FontSize = 13 });
        RkKontext.Inlines.Add(new Run(Anonymizator.AnonymizovanyText(v, _klic)));
        // fotka strany vpravo (seznam stran vlevo přepne i fotku)
        var polozka = SeznamStran.Items.OfType<PolozkaStrany>().FirstOrDefault(x => x.Strana == strana);
        if (polozka is not null && !ReferenceEquals(SeznamStran.SelectedItem, polozka)) SeznamStran.SelectedItem = polozka;
    }

    private void RkZobraz(MistoKontroly? m)
    {
        RkKontext.Inlines.Clear();
        RkVyrez.Source = null;
        RkVyrez.Visibility = Visibility.Visible;
        RkOk.Content = "V pořádku (Enter)";
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
        int i = RkSeznam.SelectedIndex;
        var dalsi = RkSeznam.Items.Cast<ListBoxItem>().Skip(i + 1).Select(x => x.Tag).OfType<MistoKontroly>()
            .FirstOrDefault(x => !_vyrizeno.Contains(KlicMista(x)));
        RkNacti(dalsi is null ? null : KlicMista(dalsi));
    }

    private void RkOk_Click(object sender, RoutedEventArgs e)
    {
        if (VybraneMisto is { } m) RkDalsi(m);
        else if (VybranaStrana is { } p && _spis is not null)
        {
            p.Strana.Zkontrolovano = true;
            _spis.Uloz();
            int i = RkSeznam.SelectedIndex;
            var dalsi = RkSeznam.Items.Cast<ListBoxItem>().Skip(i + 1).Select(x => x.Tag).FirstOrDefault();
            var vybrana = _strana;
            NactiStrany();
            if (vybrana is not null)
                SeznamStran.SelectedItem = SeznamStran.Items.OfType<PolozkaStrany>().FirstOrDefault(x => x.Strana == vybrana);
            RkNacti(dalsi switch { MistoKontroly m2 => KlicMista(m2), PotvrzeniStrany p2 => $"S:{p2.Strana.Cislo}", _ => null });
        }
    }

    private void RkNecitlive_Click(object sender, RoutedEventArgs e)
    {
        if (_spis is null || VybraneMisto is not { } m) return;
        // osoba nebo číslo by šly do exportu skutečně: jedna klávesa nestačí
        // adresa soukromé osoby (patří k osobě) je stejně citlivá jako osoba
        if (m.Entita is { } citlivy && (citlivy.Typ is not (TypUdaje.Adresa or TypUdaje.Firma or TypUdaje.SpisovaZnacka)
                                        || citlivy.Typ == TypUdaje.Adresa && citlivy.PatriK is not null)
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
}
