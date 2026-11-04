using System.Text.RegularExpressions;
using Koncipient.Ocr;
using OpenCvSharp;

namespace Koncipient.Jadro;

/// <summary>Dokument ve spisu: skupina po sobě jdoucích stran (smlouva, rozsudek…).</summary>
public sealed class Dokument
{
    public required string Id { get; set; }

    /// <summary>Název od advokátky, ze složky, nebo navržený z textu. Do exportu jde anonymizovaný.</summary>
    public string Nazev { get; set; } = "";

    /// <summary>Název zatím nikdo nezadal, program ho po přepisu navrhne z prvního textu.</summary>
    public bool NavrhnoutNazev { get; set; }

    /// <summary>Kdy dokument naposledy odešel v exportu pro Clauda (null = ještě nikdy).</summary>
    public DateTime? Exportovano { get; set; }
}

/// <summary>Řazení názvů souborů s čísly: IMG_2 před IMG_10.</summary>
public sealed class PrirozeneRazeni : IComparer<string?>
{
    public static readonly PrirozeneRazeni Instance = new();
    private static readonly Regex Casti = new(@"\d+|\D+");

    public int Compare(string? x, string? y)
    {
        var a = Casti.Matches(x ?? ""); var b = Casti.Matches(y ?? "");
        for (int i = 0; i < Math.Min(a.Count, b.Count); i++)
        {
            int c = char.IsDigit(a[i].Value[0]) && char.IsDigit(b[i].Value[0])
                ? decimal.Parse(a[i].Value).CompareTo(decimal.Parse(b[i].Value))
                : string.Compare(a[i].Value, b[i].Value, StringComparison.CurrentCultureIgnoreCase);
            if (c != 0) return c;
        }
        return a.Count.CompareTo(b.Count);
    }
}

public sealed partial class Spis
{
    public List<Dokument> Dokumenty { get; set; } = new();

    private static readonly string[] PriponyFotek = { ".jpg", ".jpeg", ".png", ".tif", ".tiff" };

    /// <summary>
    /// Černá fotka (zakrytý objektiv) slouží jako oddělovač dokumentů: tmavší než papír
    /// a bez jakékoli kresby (mobil ji může zesvětlit do červena nebo šeda).
    /// </summary>
    public static bool JeCerna(Mat obrazek)
    {
        using var seda = new Mat();
        Cv2.CvtColor(obrazek, seda, ColorConversionCodes.BGR2GRAY);
        Cv2.MeanStdDev(seda, out var prumer, out var odchylka);
        // telefon ve tmě obraz zesvětlí a přidá šum, stránka papíru je ale vždy světlejší a má kontrast textu
        if (prumer.Val0 < 50 && odchylka.Val0 < 25) return true;
        // Zakrytý objektiv může mobil zesvětlit do červena nebo do šedé mlhy (noční a makro režim).
        // Rozhoduje, že na snímku není žádná kresba: hrany pod 0,05 % (dokumenty mají aspoň 3,9 %,
        // prázdný papír na stole 0,5 %). Mez jasu chrání bílou stránku bez okrajů.
        if (prumer.Val0 >= 130) return false;
        using var mala = new Mat();
        double meritko = 800.0 / seda.Width;
        Cv2.Resize(seda, mala, new Size(800, Math.Max(1, (int)Math.Round(seda.Height * meritko))), 0, 0, InterpolationFlags.Area);
        using var hrany = new Mat();
        Cv2.Canny(mala, hrany, 60, 180);
        return Cv2.CountNonZero(hrany) < 0.0005 * hrany.Rows * hrany.Cols;
    }

    private Dokument NovyDokument(string? nazev)
    {
        int n = 1;
        while (Dokumenty.Any(d => d.Id == $"D{n:D3}")) n++;
        var d = new Dokument
        {
            Id = $"D{n:D3}",
            Nazev = string.IsNullOrWhiteSpace(nazev) ? $"Dokument {Dokumenty.Count + 1}" : nazev.Trim(),
            NavrhnoutNazev = string.IsNullOrWhiteSpace(nazev),
        };
        Dokumenty.Add(d);
        return d;
    }

    /// <summary>
    /// Přidá složku s fotkami. Fotky přímo ve složce tvoří dokument pojmenovaný podle
    /// složky, každá podsložka (i vnořená) další dokument pojmenovaný podle své cesty.
    /// Černé fotky uvnitř dokument dál rozdělují.
    /// </summary>
    /// <summary>Každé PDF ve složce je samostatný dokument pojmenovaný podle souboru.</summary>
    private static IEnumerable<string> PdfVeSlozce(string slozka) =>
        Directory.GetFiles(slozka).Where(f => Path.GetExtension(f).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName, PrirozeneRazeni.Instance);

    public IReadOnlyList<Strana> PridejSlozku(string slozka)
    {
        var nove = new List<Strana>();
        void Projdi(string adresar, string nazev)
        {
            var fotky = Directory.GetFiles(adresar).Where(f => PriponyFotek.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();
            if (fotky.Count > 0) nove.AddRange(PridejFotky(fotky, nazev));
            foreach (var pdf in PdfVeSlozce(adresar)) nove.AddRange(PridejPdf(pdf, $"{nazev} / {Path.GetFileNameWithoutExtension(pdf)}".TrimStart(' ', '/')));
            foreach (var pod in Directory.GetDirectories(adresar).OrderBy(Path.GetFileName, PrirozeneRazeni.Instance))
                Projdi(pod, $"{nazev} / {Path.GetFileName(pod)}".TrimStart(' ', '/'));
        }
        // fotky přímo ve vybrané složce nesou její název, podsložky jen vlastní cestu
        var koren = Path.GetFileName(Path.TrimEndingDirectorySeparator(slozka));
        var primo = Directory.GetFiles(slozka).Where(f => PriponyFotek.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();
        if (primo.Count > 0) nove.AddRange(PridejFotky(primo, koren));
        foreach (var pdf in PdfVeSlozce(slozka)) nove.AddRange(PridejPdf(pdf));
        foreach (var pod in Directory.GetDirectories(slozka).OrderBy(Path.GetFileName, PrirozeneRazeni.Instance))
            Projdi(pod, Path.GetFileName(pod));
        return nove;
    }

    private Oddelovac UlozOddelovac(Mat obrazek, string cesta)
    {
        Directory.CreateDirectory(Path.Combine(Slozka, "oddelovace"));
        int n = 1;
        while (File.Exists(Path.Combine(Slozka, "oddelovace", $"{n:D4}.jpg"))) n++;
        var relativni = $"oddelovace/{n:D4}.jpg";
        Cv2.ImWrite(Path.Combine(Slozka, relativni), obrazek, new ImageEncodingParam(ImwriteFlags.JpegQuality, 90));
        var o = new Oddelovac { Fotka = relativni, PuvodniNazev = Path.GetFileName(cesta) };
        Oddelovace.Add(o);
        return o;
    }

    /// <summary>
    /// Vrátí fotku vzatou jako oddělovač mezi strany na její původní místo. Když oddělovač
    /// začal nový dokument, rozdělení se zruší (dokument se spojí s předchozím).
    /// </summary>
    public Strana VratOddelovac(Oddelovac o)
    {
        if (!Oddelovace.Remove(o)) throw new ArgumentException("Oddělovač v tomto spisu není.");
        int cislo = Strany.Count == 0 ? 1 : Strany.Max(s => s.Cislo) + 1;
        var relativni = $"fotky/{cislo:D4}.jpg";
        File.Move(Path.Combine(Slozka, o.Fotka), Path.Combine(Slozka, relativni), overwrite: true);
        var strana = new Strana { Cislo = cislo, Fotka = relativni, PuvodniNazev = o.PuvodniNazev };

        var za = Strany.FirstOrDefault(s => s.Cislo == o.PredStranou);
        var pred = Strany.FirstOrDefault(s => s.Cislo == o.ZaStranou);
        if (za is not null)
        {
            strana.Dokument = za.Dokument;
            Strany.Insert(Strany.IndexOf(za), strana);
        }
        else if (pred is not null)
        {
            strana.Dokument = pred.Dokument;
            Strany.Insert(Strany.IndexOf(pred) + 1, strana);
        }
        else
        {
            strana.Dokument = NovyDokument(null).Id;
            Strany.Add(strana);
        }
        if (o.ZacalDokument is not null && strana.Dokument == o.ZacalDokument)
        {
            Uklid();
            SpojSPredchozim(strana);
        }
        else Uklid();
        return strana;
    }

    public Dokument? DokumentStrany(Strana s) => Dokumenty.FirstOrDefault(d => d.Id == s.Dokument);

    /// <summary>
    /// Dokumenty v pořadí stran. Strany bez dokumentu (spisy ze starší verze) dostanou
    /// jeden společný dokument „Spis“.
    /// </summary>
    public IReadOnlyList<(Dokument Dokument, List<Strana> Strany)> StranyPoDokumentech()
    {
        if (Strany.Any(s => DokumentStrany(s) is null))
        {
            var spolecny = NovyDokument("Spis");
            foreach (var s in Strany.Where(s => DokumentStrany(s) is null)) s.Dokument = spolecny.Id;
        }
        return Strany.GroupBy(s => s.Dokument!).Select(g => (DokumentStrany(g.First())!, g.ToList())).ToList();
    }

    /// <summary>Pořadí strany ve spisu od 1 (co vidí advokátka a na co odkazuje Claude).</summary>
    public int Poradi(Strana s) => Strany.IndexOf(s) + 1;

    /// <summary>Posune stranu o jedno místo; přes hranici dokumentu přejde do sousedního dokumentu.</summary>
    public void PosunStranu(Strana s, int smer)
    {
        int i = Strany.IndexOf(s), j = i + Math.Sign(smer);
        if (j < 0 || j >= Strany.Count) return;
        var soused = Strany[j];
        if (soused.Dokument != s.Dokument) s.Dokument = soused.Dokument; // nejdřív do sousedního dokumentu
        else (Strany[i], Strany[j]) = (Strany[j], Strany[i]);
        Uklid();
    }

    /// <summary>Přesune stranu na konec jiného (nebo nového, když null) dokumentu.</summary>
    public void PresunDoDokumentu(Strana s, string? idDokumentu, string? nazevNoveho = null)
    {
        var puvodni = s.Dokument;
        var cil = idDokumentu is null ? NovyDokument(nazevNoveho) : Dokumenty.First(d => d.Id == idDokumentu);
        Strany.Remove(s);
        s.Dokument = cil.Id;
        int posledni = Strany.FindLastIndex(x => x.Dokument == cil.Id);
        // nový dokument se zařadí hned za původní dokument strany, ne na konec spisu
        if (posledni < 0) posledni = Strany.FindLastIndex(x => x.Dokument == puvodni);
        Strany.Insert(posledni < 0 ? Strany.Count : posledni + 1, s);
        Uklid();
    }

    /// <summary>Od této strany dál (do konce jejího dokumentu) začne nový dokument.</summary>
    public Dokument ZacniDokument(Strana s, string? nazev = null)
    {
        var puvodni = s.Dokument;
        var novy = NovyDokument(nazev);
        foreach (var x in Strany.Skip(Strany.IndexOf(s)).TakeWhile(x => x.Dokument == puvodni)) x.Dokument = novy.Id;
        Uklid();
        return novy;
    }

    /// <summary>Připojí dokument strany k předchozímu dokumentu.</summary>
    public void SpojSPredchozim(Strana s)
    {
        int prvni = Strany.FindIndex(x => x.Dokument == s.Dokument);
        if (prvni <= 0) return;
        var predchozi = Strany[prvni - 1].Dokument;
        foreach (var x in Strany.Where(x => x.Dokument == s.Dokument).ToList()) x.Dokument = predchozi;
        Uklid();
    }

    /// <summary>Dokumenty bez stran zmizí a strany téhož dokumentu jdou za sebou.</summary>
    private void Uklid()
    {
        var poradi = Strany.Select(s => s.Dokument).Distinct().ToList();
        var serazene = poradi.SelectMany(d => Strany.Where(s => s.Dokument == d)).ToList();
        Strany.Clear();
        Strany.AddRange(serazene);
        Dokumenty.RemoveAll(d => !Strany.Any(s => s.Dokument == d.Id));
        Dokumenty.Sort((a, b) => poradi.IndexOf(a.Id).CompareTo(poradi.IndexOf(b.Id)));
        Uloz();
    }

    // ---------- Návrh názvu ----------

    private static readonly string[] TypyDokumentu =
    {
        "rozsudek", "usnesení", "platební rozkaz", "žaloba", "odvolání", "dovolání", "návrh", "vyjádření", "replika",
        "smlouva", "dohoda", "plná moc", "předvolání", "výzva", "upomínka", "rozhodnutí", "výpis", "protokol",
        "záznam", "posudek", "zpráva", "faktura", "potvrzení", "oznámení", "sdělení", "žádost", "stížnost",
        "námitky", "příkaz", "obžaloba", "svědecká výpověď", "prohlášení", "čestné prohlášení", "dopis", "list vlastnictví",
        "kupní smlouva", "nájemní smlouva", "smlouva o dílo", "darovací smlouva", "závěť", "osvědčení",
        "stanovisko", "stavební povolení", "kolaudační", "souhlas", "územní rozhodnutí", "technická zpráva", "posouzení",
        "požárně bezpečnostní", "průvodní zpráva", "souhrnná technická", "ohlášení", "povolení", "rozkaz", "předvolání",
    };

    /// <summary>
    /// Navrhne název dokumentu z prvního textu první strany: řádek, který obsahuje typ
    /// dokumentu (Kupní smlouva, Rozsudek…), jinak první řádek s alespoň třemi slovy.
    /// </summary>
    public static string? NavrhniNazev(VysledekOcr? prvniStrana)
    {
        if (prvniStrana is null) return null;
        var radky = prvniStrana.Radky.Take(25).Select(r => r.Text.Trim()).Where(r => r.Count(char.IsLetter) >= 4).ToList();
        string Uprav(string r)
        {
            r = Regex.Replace(r, @"\s+", " ").Trim(' ', '-', ':', '.', ',');
            if (r.Length > 60) r = r[..60].TrimEnd() + "…";
            // NADPIS VELKÝMI PÍSMENY → Nadpis velkými písmeny
            return r.Count(char.IsUpper) > r.Count(char.IsLower) ? char.ToUpper(r[0]) + r[1..].ToLower() : r;
        }
        // 1) dopis s „Věc:“ nebo „Předmět:“ říká, o co jde, nejpřesněji
        foreach (var r in radky)
        {
            var vec = Regex.Match(r, @"^(?:Věc|Vec|Předmět|Predmet|Ve věci)\s*:\s*(?<t>.+)$", RegexOptions.IgnoreCase);
            if (vec.Success && vec.Groups["t"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2) return Uprav(vec.Groups["t"].Value);
        }
        // hlavička papíru (firma, adresa, kontakty), slogan a oslovení název nedávají
        var obsah = radky.Where(r => !JeHlavickaNeboSlogan(r)).ToList();
        // 2) řádek s typem dokumentu (Kupní smlouva, Rozsudek…)
        var typ = obsah.FirstOrDefault(r => TypyDokumentu.Any(t => r.Contains(t, StringComparison.CurrentCultureIgnoreCase))
                                             && r.Split(' ').Length <= 8);
        if (typ is not null) return Uprav(typ);
        // 3) bez typu: první delší řádek obsahu
        var prvni = obsah.FirstOrDefault(r => r.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 4 && r.Length >= 25)
                    ?? obsah.FirstOrDefault(r => r.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 3);
        return prvni is null ? null : Uprav(prvni);
    }

    /// <summary>
    /// Řádek hlavičkového papíru (web, e-mail, telefon, IČO, adresa s PSČ, firma s právní formou),
    /// reklamní slogan (vykřičník, typická reklamní slova) nebo oslovení dopisu.
    /// </summary>
    internal static bool JeHlavickaNeboSlogan(string r) =>
        Regex.IsMatch(r, @"www\.|https?:|@|\b(?:tel|telefon|mobil|fax|IČO?|DIČ|IBAN|č\.\s?ú|účet|ID\s?DS|datová schránka)\b|\b\d{3}\s?\d{2}\s+(?-i:\p{Lu})|s\.\s?r\.\s?o|\ba\.\s?s\.|\bspol\.|\bv\.\s?o\.\s?s",
            RegexOptions.IgnoreCase)
        || r.TrimEnd().EndsWith('!') || r.Contains('™') || r.Contains('®')
        || Regex.IsMatch(r, @"\b(?:kvalit\w*|tradic\w*|spolehliv\w*|partner\w*|profesionál\w*|s láskou|pro vás|váš|vaše|vašim|od roku)\b", RegexOptions.IgnoreCase)
        || Regex.IsMatch(r, @"^(?:Vážen\w*|Dobrý den|Milý|Milá|Ahoj)\b", RegexOptions.IgnoreCase);

    /// <summary>Doplní navržené názvy u dokumentů, které je ještě nemají. Vrátí počet pojmenovaných.</summary>
    public int NavrhniNazvy()
    {
        int n = 0;
        foreach (var (d, strany) in StranyPoDokumentech())
        {
            if (!d.NavrhnoutNazev) continue;
            var nazev = NavrhniNazev(Motory.ProZobrazeni(this, strany[0]));
            if (nazev is null) continue;
            d.Nazev = nazev;
            d.NavrhnoutNazev = false;
            n++;
        }
        if (n > 0) Uloz();
        return n;
    }
}
