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

    /// <summary>Otočení už je vyřešené (automaticky při přepisu, nebo ručně).</summary>
    public bool Narovnano { get; set; }

    /// <summary>
    /// O kolik stupňů po směru hodinových ručiček je strana otočená proti importované fotce
    /// (0, 90, 180, 270). Původní fotka zůstává beze změny, otočená kopie se dělá z ní.
    /// </summary>
    public int Otoceni { get; set; }

    /// <summary>Dokument, ke kterému strana patří (Id z <see cref="Spis.Dokumenty"/>).</summary>
    public string? Dokument { get; set; }

    /// <summary>Advokátka stranu zkontrolovala pro export. Změna údajů ve spisu kontrolu nezruší, export ji ale ověří znovu.</summary>
    public bool Zkontrolovano { get; set; }

    /// <summary>Natočení, ve kterých byla strana zkontrolovaná (kontrola se vrátí s přepisem).</summary>
    public List<int> ZkontrolovanoNatoceni { get; set; } = new();
}

/// <summary>
/// Spis ve složce Spisy/S-0001. Obsahuje fotky (narovnané podle EXIF, bez metadat),
/// výsledky OCR po motorech a ruční opravy. Nic z toho se nešifruje, ochranu dává
/// oddělený počítač bez Claude Code (viz docs/navrh.md).
/// </summary>
public sealed partial class Spis
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

    /// <summary>Fotka strany v aktuálním natočení. Otočenou kopii vytvoří z původní fotky, když chybí.</summary>
    public string CestaFotky(Strana s)
    {
        var puvodni = Path.Combine(Slozka, s.Fotka);
        if (s.Otoceni == 0) return puvodni;
        var otocena = CestaOtocene(s, s.Otoceni);
        if (!File.Exists(otocena))
        {
            var docasna = Path.ChangeExtension(otocena, ".tmp.jpg"); // OpenCV pozná formát podle přípony
            File.Copy(puvodni, docasna, overwrite: true);
            Otoceni.OtocSoubor(docasna, s.Otoceni);
            File.Move(docasna, otocena, overwrite: true);
        }
        return otocena;
    }

    private string CestaOtocene(Strana s, int stupne) =>
        Path.Combine(Slozka, Path.GetDirectoryName(s.Fotka) ?? "", $"{s.Cislo:D4}_otoceno{stupne}.jpg");

    /// <summary>
    /// Přidá fotky jako nové strany, seřazené podle názvu souboru. Fotku otočí podle
    /// EXIF a uloží bez metadat (poloha GPS, model telefonu), aby polohy slov
    /// seděly v OCR i v zobrazení.
    /// </summary>
    /// <param name="nazevDokumentu">
    /// Název dokumentu pro přidané fotky (třeba název složky). Null = nový dokument
    /// s názvem, který program navrhne po přepisu z prvního textu.
    /// </param>
    /// <param name="cerneOddeluji">Černá fotka (zakrytý objektiv) se neuloží a začne další dokument.</param>
    public IReadOnlyList<Strana> PridejFotky(IEnumerable<string> cesty, string? nazevDokumentu = null, bool cerneOddeluji = true)
    {
        var nove = new List<Strana>();
        var dokument = NovyDokument(nazevDokumentu);
        bool prazdny = true;
        foreach (var cesta in cesty.OrderBy(c => Path.GetFileName(c), PrirozeneRazeni.Instance))
        {
            int cislo = Strany.Count == 0 ? 1 : Strany.Max(s => s.Cislo) + 1;
            var relativni = Path.Combine("fotky", $"{cislo:D4}.jpg");
            using (var obrazek = Cv2.ImRead(cesta, ImreadModes.Color)) // ImRead otáčí podle EXIF
            {
                if (obrazek.Empty())
                    throw new InvalidDataException($"Soubor {cesta} není fotka, kterou jde načíst.");
                if (cerneOddeluji && JeCerna(obrazek))
                {
                    // oddělovač: další fotky patří do nového dokumentu (prázdný dokument se nezakládá)
                    if (!prazdny) { dokument = NovyDokument(null); prazdny = true; }
                    continue;
                }
                Cv2.ImWrite(Path.Combine(Slozka, relativni), obrazek, new ImageEncodingParam(ImwriteFlags.JpegQuality, 95));
            }
            var strana = new Strana { Cislo = cislo, Fotka = relativni.Replace('\\', '/'), PuvodniNazev = Path.GetFileName(cesta), Dokument = dokument.Id };
            Strany.Add(strana);
            nove.Add(strana);
            prazdny = false;
        }
        Dokumenty.RemoveAll(d => !Strany.Any(s => s.Dokument == d.Id));
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

    /// <summary>
    /// Otočí stranu. Přepisy a ruční opravy dosavadního natočení se odloží (polohy slov
    /// by neseděly) a když strana v novém natočení už přepsaná byla, vrátí se tehdejší
    /// přepis, takže otočení tam a zpět nic nepřepočítává. Vrátí, jestli se přepis obnovil.
    /// </summary>
    public bool OtocStranu(Strana s, int stupne)
    {
        int stare = s.Otoceni;
        int nove = (((stare + stupne) % 360) + 360) % 360;
        s.ZkontrolovanoNatoceni.Remove(stare);
        if (s.Zkontrolovano) s.ZkontrolovanoNatoceni.Add(stare);
        var ocr = Path.Combine(Slozka, "ocr");
        string Odlozene(int r) => Path.Combine(ocr, "otoceni", r.ToString());

        // přepisy, opravy i upravenou fotku tohoto natočení odložit, nic se nemaže
        PresunStranu(s, ocr, Odlozene(stare), vynechat: Path.Combine(ocr, "otoceni"));
        if (stare != 0 && File.Exists(CestaOtocene(s, stare))) File.Delete(CestaOtocene(s, stare));
        s.Otoceni = nove;
        // když už strana v novém natočení byla přepsaná, vrátí se tehdejší přepis
        bool obnoveno = PresunStranu(s, Odlozene(nove), ocr, vynechat: null);

        s.Narovnano = true;
        s.Zkontrolovano = obnoveno && s.ZkontrolovanoNatoceni.Contains(nove);
        Uloz();
        return obnoveno;
    }

    /// <summary>Přesune soubory strany (0001.*) ze složky do složky se zachováním podsložek. Vrátí, jestli něco přesunul.</summary>
    private static bool PresunStranu(Strana s, string odkud, string kam, string? vynechat)
    {
        if (!Directory.Exists(odkud)) return false;
        bool neco = false;
        foreach (var soubor in Directory.GetFiles(odkud, $"{s.Cislo:D4}.*", SearchOption.AllDirectories))
        {
            if (vynechat is not null && soubor.StartsWith(vynechat + Path.DirectorySeparatorChar)) continue;
            var cil = Path.Combine(kam, Path.GetRelativePath(odkud, soubor));
            Directory.CreateDirectory(Path.GetDirectoryName(cil)!);
            File.Move(soubor, cil, overwrite: true);
            neco = true;
        }
        return neco;
    }

    /// <summary>Upravená fotka pro OCR, ukládá se mezi výsledky, aby se nepočítala znovu.</summary>
    public string UpravenaFotka(Strana s) =>
        UpravaFotky.UlozUpravenou(CestaFotky(s), Path.Combine(Slozka, "ocr", "upravene"));
}
