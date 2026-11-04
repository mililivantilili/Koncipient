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

    /// <summary>Co v exportu nemá co dělat. Prázdný seznam = export je čistý.</summary>
    public static List<string> Zkontroluj(string slozkaProClaude)
    {
        var problemy = new List<string>();
        foreach (var soubor in Directory.EnumerateFiles(slozkaProClaude, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(slozkaProClaude, soubor);
            var casti = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            // výstup od Clauda jako Word může vzniknout zpětným dosazením, i ten má zůstat tady
            bool veVystupu = casti.Length >= 3 && casti[1].Equals("vystup", StringComparison.OrdinalIgnoreCase);
            if (ZakazaneNazvy.Contains(Path.GetFileName(soubor))) problemy.Add($"{rel}: soubor ze spisu (klíč nebo data spisu)");
            else if (ZakazanePripony.Contains(Path.GetExtension(soubor)) && !(veVystupu && Path.GetExtension(soubor).Equals(".docx", StringComparison.OrdinalIgnoreCase)))
                problemy.Add($"{rel}: fotka, PDF nebo dokument, do exportu nepatří");
            else if (casti.Any(c => c.Equals("fotky", StringComparison.OrdinalIgnoreCase) || c.Equals("ocr", StringComparison.OrdinalIgnoreCase)))
                problemy.Add($"{rel}: složka ze spisu");
        }
        return problemy;
    }

    /// <summary>
    /// Zkopíruje ProClaude na disk (do složky ProClaude v jeho kořeni). Soubory exportu se
    /// přepíšou, výstupy od Clauda, které na disku už jsou, zůstanou. Vrátí počet souborů.
    /// Výstupy od Clauda (.docx se skutečnými jmény) se na disk nekopírují.
    /// </summary>
    public static int NaDisk(string slozkaProClaude, string korenDisku)
    {
        if (!Directory.Exists(slozkaProClaude))
            throw new InvalidOperationException("Zatím není nic exportované. Nejdřív exportuj spis pro Clauda.");
        var problemy = Zkontroluj(slozkaProClaude);
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
