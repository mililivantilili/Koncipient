namespace Koncipient.Jadro;

/// <summary>Složka se všemi spisy, výchozí Dokumenty\Koncipient\Spisy.</summary>
public sealed class Uloziste
{
    public string Slozka { get; }

    public Uloziste(string? slozka = null)
    {
        Slozka = slozka ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Koncipient", "Spisy");
        Directory.CreateDirectory(Slozka);
    }

    public IReadOnlyList<Spis> Spisy() =>
        Directory.GetDirectories(Slozka, "S-*")
            .Where(d => File.Exists(Path.Combine(d, "spis.json")))
            .OrderBy(d => d, StringComparer.Ordinal)
            .Select(Spis.Nacti)
            .ToList();

    /// <summary>Založí spis s dalším volným pořadovým číslem S-0001, S-0002 atd.</summary>
    public Spis Zaloz(string nazev)
    {
        int dalsi = Directory.GetDirectories(Slozka, "S-*")
            .Select(d => int.TryParse(Path.GetFileName(d)[2..], out var n) ? n : 0)
            .DefaultIfEmpty(0).Max() + 1;
        var id = $"S-{dalsi:D4}";
        return Spis.Zaloz(Path.Combine(Slozka, id), id, nazev);
    }
}
