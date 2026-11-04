namespace Koncipient.Jadro;

/// <summary>
/// Záloha složky Spisy jedním klikem: zkopíruje ji do Koncipient-zaloha\RRRR-MM-DD na vybraném
/// disku, ověří velikost každého souboru a zapamatuje si čas poslední zálohy. Fotky a klíč
/// pseudonymů jsou jen v tomto počítači, porucha disku by bez zálohy znamenala ztrátu práce.
/// Záloha obsahuje skutečné údaje: disk patří ke spisům, ne ke Claudovi.
/// </summary>
public static class Zaloha
{
    public const string NazevSlozky = "Koncipient-zaloha";

    /// <summary>Po kolika dnech program připomene zálohu.</summary>
    public const int PripomenoutPoDnech = 7;

    private static string CestaZaznamu(Uloziste uloziste) => Path.Combine(uloziste.Slozka, "posledni-zaloha.txt");

    public static DateTime? Posledni(Uloziste uloziste) =>
        File.Exists(CestaZaznamu(uloziste)) && DateTime.TryParse(File.ReadAllText(CestaZaznamu(uloziste)), out var kdy) ? kdy : null;

    /// <summary>Je potřeba připomenout zálohu (spisy existují a poslední záloha je starší než týden nebo žádná).</summary>
    public static bool Pripomenout(Uloziste uloziste) =>
        uloziste.Spisy().Any() && (Posledni(uloziste) is not { } kdy || (DateTime.Now - kdy).TotalDays >= PripomenoutPoDnech);

    /// <summary>Zálohuje a ověří. Vrátí cílovou složku a počet souborů.</summary>
    public static (string Cil, int Souboru) Proved(Uloziste uloziste, string korenCile)
    {
        var zdroj = uloziste.Slozka;
        if (Path.GetFullPath(korenCile).StartsWith(Path.GetFullPath(zdroj), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Zálohu nejde uložit do složky se spisy, vyber jiný disk.");
        var cil = Path.Combine(korenCile, NazevSlozky, DateTime.Now.ToString("yyyy-MM-dd"));
        var soubory = Directory.EnumerateFiles(zdroj, "*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".tmp")).ToList();
        long potreba = soubory.Sum(f => new FileInfo(f).Length);
        var disk = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(korenCile))!);
        if (disk.IsReady && disk.AvailableFreeSpace < potreba)
            throw new InvalidOperationException($"Na disku je málo místa: záloha potřebuje {potreba / 1_000_000} MB, volno je {disk.AvailableFreeSpace / 1_000_000} MB.");

        foreach (var f in soubory)
        {
            var kam = Path.Combine(cil, Path.GetRelativePath(zdroj, f));
            Directory.CreateDirectory(Path.GetDirectoryName(kam)!);
            File.Copy(f, kam, overwrite: true);
        }
        // ověření: každý soubor je v záloze a má stejnou velikost
        var chybi = soubory.Where(f =>
        {
            var kopie = new FileInfo(Path.Combine(cil, Path.GetRelativePath(zdroj, f)));
            return !kopie.Exists || kopie.Length != new FileInfo(f).Length;
        }).ToList();
        if (chybi.Count > 0)
            throw new IOException($"Záloha není úplná, {chybi.Count} souborů se nezkopírovalo správně. Zkus jiný disk.");
        File.WriteAllText(CestaZaznamu(uloziste), DateTime.Now.ToString("o"));
        return (cil, soubory.Count);
    }
}
