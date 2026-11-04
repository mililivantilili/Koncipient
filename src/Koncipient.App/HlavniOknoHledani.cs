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
        IReadOnlyCollection<string>? dokumenty = null;
        if (_spis.Dokumenty.Count > 1)
        {
            dokumenty = DokumentyDialog.Vyber(this, _spis, "Export pro Clauda",
                "Vyber dokumenty, které dostane Claude. Kontrolují se jen strany vybraných dokumentů. "
                + "Po doplnění spisu stačí poslat nové dokumenty (tlačítko Jen nové).",
                "Exportovat", out bool zruseno);
            if (zruseno) return;
        }
        var vysledek = Export.Proved(_spis, _klic, jenDokumenty: dokumenty);
        if (!vysledek.Uspech)
        {
            var seznam = string.Join("\n", vysledek.Problemy.Take(15));
            if (vysledek.Problemy.Count > 15) seznam += $"\n… a dalších {vysledek.Problemy.Count - 15}";
            MessageBox.Show(this, "Export se neprovedl, nic nebylo zapsáno:\n\n" + seznam, "Koncipient",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        MessageBox.Show(this,
            $"Anonymizovaný spis{(dokumenty is null ? "" : $" ({dokumenty.Count} vybrané dokumenty)")} je ve složce\n{vysledek.Slozka}\n\n" +
            "Na počítač s Claude Code přenes celou složku ProClaude (flash diskem nebo sdílenou složkou, " +
            "ve které není nic jiného). Fotky ani klíč pseudonymů v ní nejsou.",
            "Koncipient", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
