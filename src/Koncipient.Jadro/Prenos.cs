using Koncipient.Jadro.Anonymizace;

namespace Koncipient.Jadro;

/// <summary>Výstup od Clauda načtený z flash disku.</summary>
public sealed record VystupOdClauda(string Spis, string Soubor);

/// <summary>
/// Přenos mezi počítačem s Koncipientem a počítačem s Claudem přes flash disk.
/// Na disk jde jen obsah složky ProClaude a před kopírováním se ověří, že v ní nejsou
/// fotky, PDF ani klíč pseudonymů. Zpátky se berou jen výstupy od Clauda (.md, .txt).
/// </summary>
public static class Prenos
{
    /// <summary>Soubory, které do exportu nikdy nepatří (fotky, PDF, data spisu, klíč).</summary>
    private static readonly HashSet<string> ZakazanePripony = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".bmp", ".heic", ".webp", ".pdf", ".docx" };
    private static readonly HashSet<string> ZakazaneNazvy = new(StringComparer.OrdinalIgnoreCase)
        { "klic.json", "spis.json" };

    public const string NazevSlozky = "ProClaude";

    /// <summary>Výměnné disky připravené k zápisu (flash disky, karty).</summary>
    public static List<DriveInfo> FlashDisky() =>
        DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Removable && d.IsReady).ToList();

    /// <summary>
    /// Co v exportu nemá co dělat. Prázdný seznam = export je čistý. Na disk smí jen soubory,
    /// které vytvořil export (podle manifest.json), pravidla pro Clauda a výstupy od Clauda.
    /// Textové soubory, které mohl upravit člověk (CLAUDE.md, výstupy), projdou závěrečnou
    /// kontrolou proti klíči spisu, aby na disk neodešel třeba ručně uložený přepis se jmény.
    /// </summary>
    public static List<string> Zkontroluj(string slozkaProClaude, IEnumerable<Spis>? spisy = null)
    {
        var problemy = new List<string>();
        var klice = (spisy ?? Enumerable.Empty<Spis>()).ToDictionary(s => s.Id, Anonymizator.NactiKlic, StringComparer.OrdinalIgnoreCase);
        void ZkontrolujText(string soubor, string rel, IEnumerable<Klic> proti)
        {
            var text = File.ReadAllText(soubor);
            foreach (var klic in proti)
                problemy.AddRange(Export.Kontrola(text, klic).Take(3).Select(p => $"{rel}: {p}"));
        }

        foreach (var soubor in Directory.EnumerateFiles(slozkaProClaude, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(slozkaProClaude, soubor);
            var casti = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var nazev = Path.GetFileName(soubor);
            var pripona = Path.GetExtension(soubor);
            if (casti.Length == 1)
            {
                if (nazev.Equals("PRAVIDLA-KONCIPIENT.md", StringComparison.OrdinalIgnoreCase)) continue;
                if (nazev.Equals("CLAUDE.md", StringComparison.OrdinalIgnoreCase)) { ZkontrolujText(soubor, rel, klice.Values); continue; }
                problemy.Add($"{rel}: soubor, který nevytvořil export");
                continue;
            }
            var spis = casti[0];
            klice.TryGetValue(spis, out var klicSpisu);
            if (casti.Length == 2 && (nazev is "manifest.json" or "entity.json" or "UKOL.md")) continue;
            if (casti.Length == 3 && casti[1].Equals("dokumenty", StringComparison.OrdinalIgnoreCase)
                && SouboryZManifestu(Path.Combine(slozkaProClaude, spis)).Contains($"dokumenty/{nazev}")) continue;
            if (casti.Length >= 3 && casti[1].Equals("vystup", StringComparison.OrdinalIgnoreCase))
            {
                // Word se skutečnými jmény (zpětné dosazení) se na disk nekopíruje, smí tu zůstat
                if (pripona.Equals(".docx", StringComparison.OrdinalIgnoreCase)) continue;
                if (pripona.Equals(".md", StringComparison.OrdinalIgnoreCase) || pripona.Equals(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    if (klicSpisu is not null) ZkontrolujText(soubor, rel, new[] { klicSpisu });
                    continue;
                }
            }
            problemy.Add(ZakazaneNazvy.Contains(nazev) ? $"{rel}: soubor ze spisu (klíč nebo data spisu)"
                : ZakazanePripony.Contains(pripona) ? $"{rel}: fotka, PDF nebo dokument, do exportu nepatří"
                : $"{rel}: soubor, který nevytvořil export (třeba ručně uložený přepis)");
        }
        return problemy;
    }

    /// <summary>Soubory dokumentů, které podle manifest.json vytvořil export.</summary>
    private static HashSet<string> SouboryZManifestu(string slozkaSpisu)
    {
        var vysledek = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var manifest = Path.Combine(slozkaSpisu, "manifest.json");
        if (!File.Exists(manifest)) return vysledek;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifest));
            if (doc.RootElement.TryGetProperty("dokumenty", out var dokumenty))
                foreach (var d in dokumenty.EnumerateArray())
                    if (d.TryGetProperty("soubor", out var soubor) && soubor.GetString() is { } cesta) vysledek.Add(cesta.Replace('\\', '/'));
        }
        catch (System.Text.Json.JsonException) { }
        return vysledek;
    }

    /// <summary>Disk se zálohou spisů (fotky, klíč) nesmí sloužit k přenosu na počítač s Claudem.</summary>
    public static bool JeZalohovaciDisk(string koren) => Directory.Exists(Path.Combine(koren, Zaloha.NazevSlozky));

    /// <summary>
    /// Zkopíruje ProClaude na disk (do složky ProClaude v jeho kořeni). Soubory exportu se
    /// přepíšou, výstupy od Clauda, které na disku už jsou, zůstanou. Vrátí počet souborů.
    /// Výstupy od Clauda (.docx se skutečnými jmény) se na disk nekopírují.
    /// </summary>
    public static int NaDisk(string slozkaProClaude, string korenDisku, IEnumerable<Spis>? spisy = null)
    {
        if (!Directory.Exists(slozkaProClaude))
            throw new InvalidOperationException("Zatím není nic exportované. Nejdřív exportuj spis pro Clauda.");
        if (JeZalohovaciDisk(korenDisku))
            throw new InvalidOperationException("Na tomto disku je záloha spisů (fotky a skutečná jména). Ten patří ke spisům a nikdy se nesmí "
                + "zasunout do počítače s Claudem. Pro přenos použij jiný flash disk.");
        var problemy = Zkontroluj(slozkaProClaude, spisy);
        if (problemy.Count > 0)
            throw new InvalidOperationException("Ve složce ProClaude je něco, co nepatří Claudovi, nic jsem nezkopíroval:\n"
                + string.Join("\n", problemy.Take(10)));

        var cil = Path.Combine(korenDisku, NazevSlozky);
        int pocet = 0;
        foreach (var soubor in Directory.EnumerateFiles(slozkaProClaude, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(slozkaProClaude, soubor);
            if (Path.GetExtension(soubor).Equals(".docx", StringComparison.OrdinalIgnoreCase)) continue; // Word se skutečnými jmény
            var kam = Path.Combine(cil, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(kam)!);
            File.Copy(soubor, kam, overwrite: true);
            pocet++;
        }
        // dokumenty, které ze spisu zmizely, smazat i na disku (Claude by pracoval se starou verzí)
        foreach (var spis in Directory.EnumerateDirectories(slozkaProClaude))
        {
            var dokumenty = Path.Combine(cil, Path.GetFileName(spis), "dokumenty");
            if (!Directory.Exists(dokumenty)) continue;
            foreach (var stary in Directory.GetFiles(dokumenty))
                if (!File.Exists(Path.Combine(spis, "dokumenty", Path.GetFileName(stary)))) File.Delete(stary);
        }
        // záznam do každého spisu, který na disk odešel
        foreach (var spis in spisy ?? Enumerable.Empty<Spis>())
            if (Directory.Exists(Path.Combine(slozkaProClaude, spis.Id)))
                ZaznamyExportu.Pridej(spis, new ZaznamExportu { Kdy = DateTime.Now, Akce = "flash disk", Kdo = Environment.UserName, Cil = cil });
        return pocet;
    }

    /// <summary>
    /// Načte z disku výstupy od Clauda (ProClaude\S-…\vystup\*.md), které ještě nejsou
    /// v počítači nebo jsou na disku novější. Zkopíruje je do místní složky ProClaude.
    /// </summary>
    public static List<VystupOdClauda> ZDisku(string korenDisku, string slozkaProClaude)
    {
        var zdroj = Path.Combine(korenDisku, NazevSlozky);
        var nove = new List<VystupOdClauda>();
        if (!Directory.Exists(zdroj)) return nove;
        foreach (var spis in Directory.EnumerateDirectories(zdroj))
        {
            var id = Path.GetFileName(spis);
            var vystup = Path.Combine(spis, "vystup");
            if (!Directory.Exists(vystup)) continue;
            foreach (var soubor in Directory.EnumerateFiles(vystup))
            {
                var pripona = Path.GetExtension(soubor);
                if (!pripona.Equals(".md", StringComparison.OrdinalIgnoreCase) && !pripona.Equals(".txt", StringComparison.OrdinalIgnoreCase)) continue;
                var kam = Path.Combine(slozkaProClaude, id, "vystup", Path.GetFileName(soubor));
                if (File.Exists(kam) && File.GetLastWriteTimeUtc(kam) >= File.GetLastWriteTimeUtc(soubor)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(kam)!);
                File.Copy(soubor, kam, overwrite: true);
                nove.Add(new VystupOdClauda(id, kam));
            }
        }
        return nove;
    }

    /// <summary>Výchozí místní složka ProClaude (jako u exportu).</summary>
    public static string MistniSlozka => Export.VychoziSlozka;
}
