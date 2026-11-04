using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Koncipient.Jadro;
using Koncipient.Jadro.Anonymizace;
using Microsoft.Win32;

namespace Koncipient.App;

/// <summary>Hledání ve spisu, prohledatelné PDF pro archiv a export vybraných dokumentů.</summary>
public partial class HlavniOkno
{
    // ---------- Hledání ----------

    private void HledatOtevrit()
    {
        Zalozky.SelectedIndex = 3;
        Dispatcher.BeginInvoke(new Action(() => { HledatPole.Focus(); HledatPole.SelectAll(); }),
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private void Hledat_Click(object sender, RoutedEventArgs e) => Hledej();

    private void HledatPole_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        Hledej();
        if (HledatVysledky.Items.Count > 0) { HledatVysledky.SelectedIndex = 0; (HledatVysledky.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus(); }
        e.Handled = true;
    }

    private void Hledej()
    {
        HledatVysledky.Items.Clear();
        if (_spis is null || string.IsNullOrWhiteSpace(HledatPole.Text)) { HledatSouhrn.Text = ""; return; }
        var nalezy = Hledani.Najdi(_spis, HledatPole.Text);
        foreach (var n in nalezy)
            HledatVysledky.Items.Add(new ListBoxItem
            {
                Content = $"s. {_spis.Poradi(n.Strana)}   … {n.Pred} 【{n.Text}】 {n.Po} …",
                Tag = n,
            });
        HledatSouhrn.Text = nalezy.Count switch
        {
            0 => "Nic nenalezeno. Zkus začátek slova (smlouv) nebo jiný tvar.",
            >= 500 => "Prvních 500 nálezů, upřesni hledání.",
            _ => $"{nalezy.Count} nálezů na {nalezy.Select(n => n.Strana).Distinct().Count()} stranách. Enter nebo dvojklik otevře stranu.",
        };
    }

    private void HledatVysledky_Otevrit(object sender, EventArgs e)
    {
        if (e is KeyEventArgs k && k.Key != Key.Enter) return;
        if ((HledatVysledky.SelectedItem as ListBoxItem)?.Tag is not NalezHledani n) return;
        SeznamStran.SelectedItem = SeznamStran.Items.OfType<PolozkaStrany>().FirstOrDefault(p => p.Strana == n.Strana);
        Zalozky.SelectedIndex = 0;
        Dispatcher.BeginInvoke(new Action(() => { if (n.Do < _slova.Count) Vyber(n.Od, n.Do, true); }),
            System.Windows.Threading.DispatcherPriority.Background);
        if (e is KeyEventArgs ke) ke.Handled = true;
    }

    // ---------- Prohledatelné PDF pro archiv ----------

    private async void PdfArchiv_Click(object sender, RoutedEventArgs e)
    {
        if (_spis is null) return;
        IReadOnlyCollection<string>? dokumenty = null;
        if (_spis.Dokumenty.Count > 1)
        {
            dokumenty = DokumentyDialog.Vyber(this, _spis, "PDF pro archiv",
                "Uloží fotky stran do jednoho PDF, ve kterém jde hledat (Ctrl+F) a kopírovat text. Dokumenty budou záložky. "
                + "PDF obsahuje skutečné údaje: patří jen do tohoto počítače, nikdy ho nedávej Claudovi.",
                "Uložit PDF…", out bool zruseno);
            if (zruseno) return;
        }
        var dialog = new SaveFileDialog
        {
            Title = "Uložit prohledatelné PDF", Filter = "PDF|*.pdf",
            FileName = (string.IsNullOrWhiteSpace(_spis.Nazev) ? _spis.Id : _spis.Nazev) + ".pdf",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dialog.ShowDialog(this) != true) return;
        var spis = _spis;
        Stav.Text = "Ukládám PDF…";
        IsEnabled = false;
        try
        {
            int stran = await Task.Run(() => ArchivPdf.Uloz(spis, dialog.FileName, dokumenty));
            Stav.Text = $"Uloženo PDF s {stran} stranami: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "PDF se nepodařilo uložit: " + ex.Message, "Koncipient", MessageBoxButton.OK, MessageBoxImage.Warning);
            Stav.Text = "";
        }
        finally { IsEnabled = true; }
    }

    // ---------- Export pro Clauda (celý spis nebo vybrané dokumenty) ----------

    private void Exportovat_Click(object sender, RoutedEventArgs e)
    {
        if (_spis is null) return;
        var klic = _klic;
        var dokumenty = DokumentyDialog.Vyber(this, _spis, "Export pro Clauda",
            _spis.Dokumenty.Count > 1
                ? "Vyber dokumenty, které dostane Claude. Kontrolují se jen strany vybraných dokumentů. "
                  + "Po doplnění spisu stačí poslat nové dokumenty (tlačítko Jen nové)."
                : "Spis odejde Claudovi celý.",
            "Exportovat", t => Anonymizator.AnonymizujText(t, klic), out bool zruseno, out string? ukol);
        if (zruseno) return;
        var vysledek = Export.Proved(_spis, _klic, jenDokumenty: dokumenty, ukol: ukol);
        if (!vysledek.Uspech)
        {
            var seznam = string.Join("\n", vysledek.Problemy.Take(15));
            if (vysledek.Problemy.Count > 15) seznam += $"\n… a dalších {vysledek.Problemy.Count - 15}";
            MessageBox.Show(this, "Export se neprovedl, nic nebylo zapsáno:\n\n" + seznam, "Koncipient",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (MessageBox.Show(this,
                $"Anonymizovaný spis{(dokumenty is null ? "" : $" ({dokumenty.Count} vybrané dokumenty)")} je ve složce\n{vysledek.Slozka}\n\n" +
                (ukol is null ? "" : "Úkol je v souboru UKOL.md. Na počítači s Claudem stačí napsat „udělej úkol ve spisu " + _spis.Id + "“.\n\n") +
                "Na počítač s Claude Code se přenáší jen složka ProClaude. Fotky ani klíč pseudonymů v ní nejsou.\n\n" +
                "Zkopírovat ji teď na flash disk?",
                "Koncipient", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            ZkopirujNaFlashDisk();
    }
}

/// <summary>Záložka Data: přehled dat ve spisu seřazený časově.</summary>
public partial class HlavniOkno
{
    private List<DatumVeSpisu> _data = new();

    private void DataNacti()
    {
        DataSeznam.Items.Clear();
        if (_spis is null) { DataSouhrn.Text = ""; return; }
        _data = PrehledDat.Najdi(_spis);
        bool bezNarozeni = DataBezNarozeni.IsChecked == true;
        var zobrazena = _data.Where(x => !(bezNarozeni && x.Druh == "narození")).ToList();
        foreach (var x in zobrazena)
            DataSeznam.Items.Add(new ListBoxItem
            {
                Content = $"{x.Datum:d. M. yyyy}   {(x.Druh is null ? "" : $"[{x.Druh}]   ")}s. {_spis.Poradi(x.Strana)}   … {x.Pred} 【{x.Text}】 {x.Po} …",
                Tag = x,
            });
        DataSouhrn.Text = zobrazena.Count == 0
            ? "Ve spisu nejsou žádná data (nebo ještě není přepsaný)."
            : $"{zobrazena.Count} dat. Druh v závorce je jen nápověda podle slov před datem, lhůty program nepočítá. Enter nebo dvojklik otevře stranu.";
    }

    private void DataNacist_Click(object sender, RoutedEventArgs e) => DataNacti();

    private void DataSeznam_Otevrit(object sender, EventArgs e)
    {
        if (e is KeyEventArgs k && k.Key != Key.Enter) return;
        if ((DataSeznam.SelectedItem as ListBoxItem)?.Tag is not DatumVeSpisu x) return;
        SeznamStran.SelectedItem = SeznamStran.Items.OfType<PolozkaStrany>().FirstOrDefault(p => p.Strana == x.Strana);
        Zalozky.SelectedIndex = 0;
        Dispatcher.BeginInvoke(new Action(() => { if (x.Do < _slova.Count) Vyber(x.Od, x.Do, true); }),
            System.Windows.Threading.DispatcherPriority.Background);
        if (e is KeyEventArgs ke) ke.Handled = true;
    }

    private void DataUlozit_Click(object sender, RoutedEventArgs e)
    {
        if (_spis is null) return;
        var dialog = new SaveFileDialog
        {
            Title = "Uložit přehled dat", Filter = "Dokument Word|*.docx",
            FileName = $"Data ve spisu {(string.IsNullOrWhiteSpace(_spis.Nazev) ? _spis.Id : _spis.Nazev)}.docx",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dialog.ShowDialog(this) != true) return;
        var data = DataSeznam.Items.Cast<ListBoxItem>().Select(i => (DatumVeSpisu)i.Tag);
        Koncipient.Jadro.Word.Uloz(dialog.FileName, PrehledDat.Text(_spis, data));
        Stav.Text = "Přehled dat uložen: " + dialog.FileName;
    }
}
