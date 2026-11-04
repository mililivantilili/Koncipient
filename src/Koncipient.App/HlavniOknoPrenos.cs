using System.IO;
using System.Windows;
using Koncipient.Jadro;

namespace Koncipient.App;

/// <summary>Přenos přes flash disk: export na disk a výstupy od Clauda zpátky.</summary>
public partial class HlavniOkno
{
    /// <summary>Vybere flash disk; když je jen jeden, nevyptává se. Null = žádný nebo zrušeno.</summary>
    private DriveInfo? VyberFlashDisk(string ucel)
    {
        var disky = Prenos.FlashDisky();
        if (disky.Count == 0)
        {
            MessageBox.Show(this, "Nevidím žádný flash disk. Zasuň ho a zkus to znovu.", "Flash disk",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }
        if (disky.Count == 1) return disky[0];
        return VyberDialog.Vyber(this, "Flash disk", ucel, disky,
            d => $"{d.Name}  {(string.IsNullOrWhiteSpace(d.VolumeLabel) ? "" : d.VolumeLabel)}  ({d.AvailableFreeSpace / 1_000_000} MB volno)");
    }

    private void NaFlashDisk_Click(object sender, RoutedEventArgs e) => ZkopirujNaFlashDisk();

    private void ZkopirujNaFlashDisk()
    {
        var disk = VyberFlashDisk("Na který disk zkopírovat export pro Clauda?");
        if (disk is null) return;
        try
        {
            int pocet = Prenos.NaDisk(Prenos.MistniSlozka, disk.RootDirectory.FullName);
            Stav.Text = $"Na {disk.Name} zkopírováno {pocet} souborů pro Clauda.";
            MessageBox.Show(this,
                $"Na disk {disk.Name} jsem zkopíroval složku ProClaude ({pocet} souborů). Fotky ani klíč v ní nejsou, ověřil jsem to.\n\n"
                + "Na počítači s Claudem ji zkopíruj na stejné místo jako minule. Výstupy od Clauda ukládej zpátky na disk do ProClaude\\S-…\\vystup.",
                "Flash disk", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Flash disk", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ZFlashDisku_Click(object sender, RoutedEventArgs e)
    {
        var disk = VyberFlashDisk("Ze kterého disku načíst výstupy od Clauda?");
        if (disk is null) return;
        List<VystupOdClauda> nove;
        try { nove = Prenos.ZDisku(disk.RootDirectory.FullName, Prenos.MistniSlozka); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Flash disk", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (nove.Count == 0)
        {
            MessageBox.Show(this, $"Na disku {disk.Name} nejsou žádné nové výstupy od Clauda (ProClaude\\S-…\\vystup\\*.md).",
                "Flash disk", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Stav.Text = $"Načteno {nove.Count} výstupů od Clauda.";
        var vybrany = nove.Count == 1 ? nove[0]
            : VyberDialog.Vyber(this, "Výstupy od Clauda", $"Načteno {nove.Count} nových výstupů. Který otevřít a vrátit do něj jména?",
                nove, v => $"{v.Spis}:  {Path.GetFileName(v.Soubor)}");
        if (vybrany is null) return;
        var spis = _uloziste.Spisy().FirstOrDefault(s => s.Id == vybrany.Spis);
        if (spis is null)
        {
            MessageBox.Show(this, $"Spis {vybrany.Spis} v tomto počítači není. Výstup je uložený v {vybrany.Soubor}.",
                "Flash disk", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var okno = new ZpetneOkno(spis, Koncipient.Jadro.Anonymizace.Anonymizator.NactiKlic(spis)) { Owner = this };
        okno.Show();
        okno.OtevriSoubor(vybrany.Soubor);
    }
}

/// <summary>Stránky k přefocení: tlačítko a nabídka po přepisu.</summary>
public partial class HlavniOkno
{
    private void KPrefoceni_Click(object sender, RoutedEventArgs e)
    {
        if (_spis is null) return;
        var strany = KvalitaFotek.Najdi(_spis);
        if (strany.Count == 0)
        {
            MessageBox.Show(this, "Všechny strany vypadají dobře, není co přefotit.", "Stránky k přefocení",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        UkazPrefoceni(_spis, strany);
    }

    private void UkazPrefoceni(Spis spis, IReadOnlyList<StranaKPrefoceni> strany)
    {
        var (nahrazeno, otevrit) = PrefoceniDialog.Ukaz(this, spis, strany);
        if (nahrazeno > 0) Stav.Text = $"Nahrazeno {nahrazeno} fotek. Přepíšou se tlačítkem Spustit přepis.";
        if (_spis?.Id != spis.Id) return;
        var vybrana = otevrit ?? _strana;
        NactiStrany();
        if (vybrana is not null)
            SeznamStran.SelectedItem = SeznamStran.Items.OfType<PolozkaStrany>().FirstOrDefault(p => p.Strana == vybrana);
        if (otevrit is not null) Zalozky.SelectedIndex = 0;
    }

    /// <summary>Po přepisu: když některé strany vyšly špatně, nabídne je k přefocení.</summary>
    private void NabidniPrefoceni()
    {
        if (_spis is null) return;
        var strany = KvalitaFotek.Najdi(_spis);
        if (strany.Count == 0) return;
        if (MessageBox.Show(this,
                $"{strany.Count} stran ve spisu vyšlo špatně (rozmazané, tmavé, uříznuté nebo s nejistým přepisem). "
                + "Ukázat je, ať je jde přefotit, dokud máš spis v ruce?",
                "Stránky k přefocení", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            UkazPrefoceni(_spis, strany);
    }
}
