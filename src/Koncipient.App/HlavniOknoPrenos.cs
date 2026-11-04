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
