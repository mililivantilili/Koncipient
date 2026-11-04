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
            int pocet = Prenos.NaDisk(Prenos.MistniSlozka, disk.RootDirectory.FullName, _uloziste.Spisy());
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

/// <summary>Záznam o exportech (doklad pro GDPR).</summary>
public partial class HlavniOkno
{
    private void ZaznamExportu_Click(object sender, RoutedEventArgs e)
    {
        if (_spis is null) return;
        var spis = _spis;
        var text = ZaznamyExportu.Prehled(spis);
        var pole = new System.Windows.Controls.TextBox
        {
            Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontSize = 13,
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
        };
        var ulozit = new System.Windows.Controls.Button { Content = "Uložit jako Word…", Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 8, 0) };
        var zavrit = new System.Windows.Controls.Button { Content = "Zavřít", IsCancel = true, Padding = new Thickness(10, 3, 10, 3) };
        var dole = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        dole.Children.Add(ulozit);
        dole.Children.Add(zavrit);
        var obsah = new System.Windows.Controls.DockPanel { Margin = new Thickness(12) };
        System.Windows.Controls.DockPanel.SetDock(dole, System.Windows.Controls.Dock.Bottom);
        obsah.Children.Add(dole);
        obsah.Children.Add(pole);
        var okno = new Window
        {
            Title = $"Záznam o exportech, spis {spis.Id}", Content = obsah, Owner = this, Width = 680, Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false,
        };
        ulozit.Click += (_, _) =>
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Uložit záznam o exportech", Filter = "Dokument Word|*.docx",
                FileName = $"Záznam o exportech {spis.Id}.docx",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            };
            if (dialog.ShowDialog(okno) != true) return;
            Koncipient.Jadro.Word.Uloz(dialog.FileName, text);
            Stav.Text = "Záznam uložen: " + dialog.FileName;
        };
        okno.ShowDialog();
    }
}

/// <summary>Záloha složky Spisy jedním klikem a připomenutí.</summary>
public partial class HlavniOkno
{
    private async void Zalohovat_Click(object sender, RoutedEventArgs e) => await Zalohuj();

    private async Task Zalohuj()
    {
        var disky = Prenos.FlashDisky().Concat(DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType is DriveType.Fixed or DriveType.Network
                && !_uloziste.Slozka.StartsWith(d.RootDirectory.FullName, StringComparison.OrdinalIgnoreCase))).ToList();
        if (disky.Count == 0)
        {
            MessageBox.Show(this, "Nevidím žádný jiný disk pro zálohu. Připoj externí disk nebo flash disk a zkus to znovu.",
                "Záloha", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var disk = disky.Count == 1 ? disky[0] : VyberDialog.Vyber(this, "Záloha",
            "Kam zálohovat spisy? Nejlépe na externí disk, který pak uložíš stejně bezpečně jako papírové spisy.",
            disky, d => $"{d.Name}  {d.VolumeLabel}  ({d.DriveType switch { DriveType.Removable => "výměnný", DriveType.Network => "síťový", _ => "pevný" }}, {d.AvailableFreeSpace / 1_000_000_000} GB volno)");
        if (disk is null) return;
        Stav.Text = "Zálohuji spisy…";
        IsEnabled = false;
        try
        {
            var (cil, souboru) = await Task.Run(() => Zaloha.Proved(_uloziste, disk.RootDirectory.FullName));
            Stav.Text = $"Záloha hotová a ověřená: {souboru} souborů v {cil}";
            MessageBox.Show(this, $"Záloha je hotová a ověřená ({souboru} souborů):\n{cil}\n\n"
                + "Jsou v ní fotky i skutečná jména. Disk ulož bezpečně a nedávej ho k počítači s Claudem.",
                "Záloha", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            Stav.Text = "Záloha se nepodařila.";
            MessageBox.Show(this, ex.Message, "Záloha", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { IsEnabled = true; }
    }

    /// <summary>Po spuštění: když záloha dlouho neproběhla, nabídne ji.</summary>
    private async void PripomenZalohu()
    {
        if (!Zaloha.Pripomenout(_uloziste)) return;
        var posledni = Zaloha.Posledni(_uloziste);
        if (MessageBox.Show(this,
                (posledni is { } kdy ? $"Spisy jsi naposledy zálohovala {kdy:d. M. yyyy}." : "Spisy zatím nikdy nebyly zálohované.")
                + " Fotky a skutečná jména jsou jen v tomto počítači. Zálohovat teď?",
                "Záloha", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            await Zalohuj();
    }
}
