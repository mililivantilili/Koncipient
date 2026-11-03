using System.Text.Encodings.Web;
using System.Text.Json;
using Koncipient.Ocr;
using OpenCvSharp;

namespace Koncipient.Jadro;

/// <summary>Jedna strana spisu: fotka a k ní výsledky OCR.</summary>
public sealed class Strana
{
    public int Cislo { get; set; }

    /// <summary>Cesta k fotce relativně ke složce spisu, např. fotky/0001.jpg.</summary>
    public required string Fotka { get; set; }

    /// <summary>Název souboru, ze kterého fotka pochází. Zůstává jen v počítači se spisy.</summary>
    public string? PuvodniNazev { get; set; }
}

/// <summary>
/// Spis ve složce Spisy/S-0001. Obsahuje fotky (narovnané podle EXIF, bez metadat),
/// výsledky OCR po motorech a ruční opravy. Nic z toho se nešifruje, ochranu dává
/// oddělený počítač bez Claude Code (viz docs/navrh.md).
/// </summary>
public sealed class Spis
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Pseudonym spisu, nesmí se odvozovat ze spisové značky.</summary>
    public required string Id { get; set; }

    /// <summary>Pojmenování od advokátky, třeba spisová značka. Neexportuje se.</summary>
    public string Nazev { get; set; } = "";

    public List<Strana> Strany { get; set; } = new();

    [System.Text.Json.Serialization.JsonIgnore] public string Slozka { get; private set; } = "";

    public static Spis Nacti(string slozka)
    {
        var spis = JsonSerializer.Deserialize<Spis>(File.ReadAllText(Path.Combine(slozka, "spis.json")), Json)
            ?? throw new InvalidDataException($"Poškozený spis.json ve složce {slozka}.");
        spis.Slozka = slozka;
        return spis;
    }

    internal static Spis Zaloz(string slozka, string id, string nazev)
    {
        Directory.CreateDirectory(Path.Combine(slozka, "fotky"));
        var spis = new Spis { Id = id, Nazev = nazev, Slozka = slozka };
        spis.Uloz();
        return spis;
    }

    public void Uloz()
    {
        var cil = Path.Combine(Slozka, "spis.json");
        File.WriteAllText(cil + ".tmp", JsonSerializer.Serialize(this, Json));
        File.Move(cil + ".tmp", cil, overwrite: true);
    }

    public string CestaFotky(Strana s) => Path.Combine(Slozka, s.Fotka);

    /// <summary>
    /// Přidá fotky jako nové strany, seřazené podle názvu souboru. Fotku otočí podle
    /// EXIF a uloží bez metadat (poloha GPS, model telefonu), aby polohy slov
    /// seděly v OCR i v zobrazení.
    /// </summary>
    public IReadOnlyList<Strana> PridejFotky(IEnumerable<string> cesty)
    {
        var nove = new List<Strana>();
        foreach (var cesta in cesty.OrderBy(c => Path.GetFileName(c), StringComparer.CurrentCultureIgnoreCase))
        {
            int cislo = Strany.Count == 0 ? 1 : Strany.Max(s => s.Cislo) + 1;
            var relativni = Path.Combine("fotky", $"{cislo:D4}.jpg");
            using (var obrazek = Cv2.ImRead(cesta, ImreadModes.Color)) // ImRead otáčí podle EXIF
            {
                if (obrazek.Empty())
                    throw new InvalidDataException($"Soubor {cesta} není fotka, kterou jde načíst.");
                Cv2.ImWrite(Path.Combine(Slozka, relativni), obrazek, new ImageEncodingParam(ImwriteFlags.JpegQuality, 95));
            }
            var strana = new Strana { Cislo = cislo, Fotka = relativni.Replace('\\', '/'), PuvodniNazev = Path.GetFileName(cesta) };
            Strany.Add(strana);
            nove.Add(strana);
        }
        Uloz();
        return nove;
    }

    // ---------- Výsledky OCR ----------

    private string CestaOcr(Strana s, string motor) => Path.Combine(Slozka, "ocr", motor, $"{s.Cislo:D4}.json");

    /// <summary>Ručně opravená verze strany, vzniká první opravou v prohlížeči.</summary>
    public const string Opraveno = "opraveno";

    public bool MaOcr(Strana s, string motor) => File.Exists(CestaOcr(s, motor));

    public VysledekOcr? NactiOcr(Strana s, string motor)
    {
        var cesta = CestaOcr(s, motor);
        return File.Exists(cesta) ? JsonSerializer.Deserialize<VysledekOcr>(File.ReadAllText(cesta), Json) : null;
    }

    public void UlozOcr(Strana s, VysledekOcr vysledek, string? motor = null)
    {
        var cesta = CestaOcr(s, motor ?? vysledek.Motor);
        Directory.CreateDirectory(Path.GetDirectoryName(cesta)!);
        File.WriteAllText(cesta + ".tmp", JsonSerializer.Serialize(vysledek, Json));
        File.Move(cesta + ".tmp", cesta, overwrite: true);
    }

    /// <summary>Upravená fotka pro OCR, ukládá se mezi výsledky, aby se nepočítala znovu.</summary>
    public string UpravenaFotka(Strana s) =>
        UpravaFotky.UlozUpravenou(CestaFotky(s), Path.Combine(Slozka, "ocr", "upravene"));
}
