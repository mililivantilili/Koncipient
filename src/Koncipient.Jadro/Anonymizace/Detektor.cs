using System.Reflection;
using System.Text.RegularExpressions;

namespace Koncipient.Jadro.Anonymizace;

/// <summary>Nalezený údaj: slova Od až Do (včetně) v pořadí slov strany.</summary>
public readonly record struct Nalez(int Od, int Do, TypUdaje Typ);

/// <summary>
/// Hledá citlivé údaje pravidly a seznamem křestních jmen, bez modelů a bez sítě.
/// Nikdy nenajde všechno, proto každou stranu kontroluje advokátka a může
/// údaje doplnit ručně.
/// </summary>
public static class Detektor
{
    private static readonly Lazy<HashSet<string>> KrestniJmena = new(NactiJmena);

    private static readonly HashSet<string> Tituly = new(StringComparer.OrdinalIgnoreCase)
    {
        "ing", "mgr", "judr", "mudr", "mvdr", "bc", "phdr", "rndr", "paeddr", "doc", "prof", "ph.d", "csc", "dis",
        "pan", "paní", "pana", "panu", "panem", "slečna", "p", "sp",
        "npor", "por", "kpt", "prap", "pprap", "mjr", "pplk", "plk", "nstržm", "stržm", "ppor", "šstržm",
        "rozená", "rozený", "roz", "rozené", "rozenou", "rozeného",
        "doktor", "doktore", "doktorka", "doktorko", "inženýr", "inženýre", "magistr", "magistře", "magistro", "kolega", "kolego", "kolegyně",
    };

    private static readonly HashSet<string> PravniFormy = new(StringComparer.OrdinalIgnoreCase)
    {
        "s.r.o", "s. r. o", "a.s", "a. s", "spol", "v.o.s", "k.s", "z.s", "o.p.s", "z.ú",
    };

    // Slova psaná s velkým písmenem, která nejsou jména (začátky vět, úřady, měsíce).
    private static readonly HashSet<string> NeJmena = new(StringComparer.OrdinalIgnoreCase)
    {
        "ČSN", "EN", "Sb", "Policie", "České", "Česká", "Český", "republiky", "Krajské", "Územní", "Městský",
        "Okresní", "Nejvyšší", "Ústavní", "Krajský", "Obecní", "Stránka", "Strana", "Za", "Ve", "V", "Na", "Pro",
        "Do", "Od", "Při", "Dle", "Podle", "Datum", "Telefon", "Tel", "E-mail", "Email", "Fax", "IČ", "IČO", "DIČ",
        "Viz", "Rozhodnutí", "Usnesení", "Rozsudek", "Žalobce", "Žalovaný", "Navrhovatel", "Odpůrce",
    };

    private static HashSet<string> NactiJmena()
    {
        using var proud = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Koncipient.Jadro.Anonymizace.jmena.txt")!;
        using var cteni = new StreamReader(proud);
        return cteni.ReadToEnd().Split('\n')
            .Where(r => !r.TrimStart().StartsWith('#'))
            .SelectMany(r => r.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(Text.Normalizuj)
            .ToHashSet();
    }

    /// <summary>Je normalizované slovo přímo v seznamu jmen (tedy v 1. pádě)?</summary>
    public static bool JeVSeznamuJmen(string slovo) => KrestniJmena.Value.Contains(Text.Normalizuj(slovo));

    /// <summary>Označení role před jménem v hlavičce, např. „Žalobce:“.</summary>
    public static string? Role(string slovo)
    {
        var n = Text.Normalizuj(slovo).TrimEnd(':');
        return Role_.TryGetValue(n, out var r) ? r : null;
    }

    private static readonly Dictionary<string, string> Role_ = new()
    {
        ["zalobce"] = "žalobce", ["zalobkyne"] = "žalobkyně", ["zalovany"] = "žalovaný", ["zalovana"] = "žalovaná",
        ["navrhovatel"] = "navrhovatel", ["navrhovatelka"] = "navrhovatelka", ["odpurce"] = "odpůrce", ["odpurkyne"] = "odpůrkyně",
        ["svedek"] = "svědek", ["svedkyne"] = "svědkyně", ["obvineny"] = "obviněný", ["obvinena"] = "obviněná",
        ["obzalovany"] = "obžalovaný", ["obzalovana"] = "obžalovaná", ["poskozeny"] = "poškozený", ["poskozena"] = "poškozená",
        ["opravneny"] = "oprávněný", ["opravnena"] = "oprávněná", ["povinny"] = "povinný", ["povinna"] = "povinná",
        ["kupujici"] = "kupující", ["prodavajici"] = "prodávající", ["pronajimatel"] = "pronajímatel", ["najemce"] = "nájemce",
        ["dluznik"] = "dlužník", ["dluznice"] = "dlužnice", ["veritel"] = "věřitel", ["veritelka"] = "věřitelka",
        ["zamestnavatel"] = "zaměstnavatel", ["zamestnanec"] = "zaměstnanec", ["zamestnankyne"] = "zaměstnankyně",
        ["zastupce"] = "zástupce", ["zastoupen"] = "zástupce", ["zastoupena"] = "zástupce", ["investor"] = "investor",
        ["stavebnik"] = "stavebník", ["zadatel"] = "žadatel", ["zadatelka"] = "žadatelka", ["projektant"] = "projektant",
        ["jednatel"] = "jednatel", ["jednatelka"] = "jednatelka", ["spolecnik"] = "společník", ["spolecnice"] = "společnice",
        ["zmocnitel"] = "zmocnitel", ["zmocnenec"] = "zmocněnec",
        // veřejné osoby: advokátka je obvykle nechá neanonymizované
        ["samosoudkyni"] = "samosoudkyně (veřejná osoba)", ["samosoudkyne"] = "samosoudkyně (veřejná osoba)",
        ["samosoudcem"] = "samosoudce (veřejná osoba)", ["samosoudce"] = "samosoudce (veřejná osoba)",
        ["soudkyne"] = "soudkyně (veřejná osoba)", ["soudce"] = "soudce (veřejná osoba)",
        ["vyrizuje"] = "úřední osoba (veřejná osoba)", ["referent"] = "referent (veřejná osoba)", ["referentka"] = "referentka (veřejná osoba)",
    };

    /// <summary>Rod osoby podle slova těsně před jménem (paní, rozená, svědkyně…), jinak null.</summary>
    public static Rod? RodZOkoli(string slovo) => Text.Normalizuj(slovo).TrimEnd(':') switch
    {
        "pani" or "rozena" or "rozene" or "rozenou" or "doktorko" or "doktorka" or "svedkyne" or "zalovana" or "zalobkyne" or "navrhovatelka" or "odpurkyne" or "dluznice"
            or "veritelka" or "obvinena" or "poskozena" or "opravnena" or "povinna" or "zadatelka" or "jednatelka"
            or "spolecnice" or "zmocnenkyne" or "advokatka" or "manzelka" or "dcera" or "matka" => Rod.Zena,
        "pan" or "pana" or "panu" or "panem" or "rozeny" or "svedek" or "zalovany" or "zalobce" or "navrhovatel"
            or "odpurce" or "dluznik" or "veritel" or "obvineny" or "jednatel" or "spolecnik" or "zmocnenec"
            or "advokat" or "manzel" or "syn" or "otec" => Rod.Muz,
        _ => null,
    };

    /// <summary>Je slovo (v libovolném pádě) běžné české křestní jméno?</summary>
    public static bool JeKrestniJmeno(string slovo)
    {
        if (!Text.ZacinaVelkym(slovo)) return false;
        var n = Text.Normalizuj(slovo);
        if (n.Length < 2) return false;
        if (KrestniJmena.Value.Contains(n)) return true;
        foreach (var kmen in Text.Kmeny(n))
            if (kmen != n && (KrestniJmena.Value.Contains(kmen) || KrestniJmena.Value.Contains(kmen + "a") || KrestniJmena.Value.Contains(kmen + "e")))
                return true;
        return false;
    }

    private static bool MuzeBytPrijmeni(string slovo)
    {
        var c = Text.Ocisti(slovo);
        return c.Length >= 2 && char.IsUpper(c[0]) && c.Skip(1).Any(char.IsLower)
            && c.All(z => char.IsLetter(z) || z == '-') && !NeJmena.Contains(c) && !Tituly.Contains(c);
    }

    private static bool JeTitul(string slovo) => Tituly.Contains(Text.Ocisti(slovo).TrimEnd('.'));

    // ---------- Pravidla nad celým textem strany ----------

    /// <summary>Slova, kterými začínají víceslovné názvy obcí (pro obec rozdělenou koncem řádku).</summary>
    private static readonly HashSet<string> PrvniSlovaObci = new(StringComparer.Ordinal)
    {
        "České", "Český", "Česká", "Hradec", "Nové", "Nový", "Nová", "Ústí", "Frýdek", "Valašské", "Valašský", "Rožnov", "Kostelec",
        "Brandýs", "Mladá", "Dvůr", "Karlovy", "Mariánské", "Kutná", "Velké", "Velký", "Velká", "Horní", "Dolní", "Staré", "Starý",
        "Stará", "Uherské", "Uherský", "Havlíčkův", "Jindřichův", "Králův", "Moravské", "Moravský", "Moravská", "Bystřice", "Lhota",
        "Kamenice", "Žďár", "Dobrá", "Hořovice", "Sušice", "Vysoké", "Malé", "Bílé", "Červený", "Lázně", "Praha", "Brno",
    };

    private const string Mesice = "ledna|února|března|dubna|května|června|července|srpna|září|října|listopadu|prosince";

    private static readonly (Regex Vzor, TypUdaje Typ)[] Pravidla =
    {
        (new Regex(@"\b\d{2}[0156]\d[0-3]\d\s?/\s?\d{3,4}\b"), TypUdaje.RodneCislo),
        (new Regex(@"(?:nar\.|narozen\w*|datum narození:?|dat\. ?nar\.:?)\s*(?:dne\s*)?(?<u>\d{1,2}\.\s?(?:\d{1,2}\.|" + Mesice + @")\s?\d{4})", RegexOptions.IgnoreCase), TypUdaje.DatumNarozeni),
        (new Regex(@"\b(?:IČO?|DIČ)\s*:?\s*(?<u>(?:CZ)?\d{8,10})\b"), TypUdaje.Ico),
        (new Regex(@"\b(?:\d{1,6}-)?\d{6,10}/\d{4}\b"), TypUdaje.Ucet),
        (new Regex(@"(?:\+420 ?)?\b[1-9]\d{2} ?\d{3} ?\d{3}\b"), TypUdaje.Telefon),
        (new Regex(@"[\w.+-]+@[\w-]+\.[\w.-]*\w"), TypUdaje.Email),
        (new Regex(@"\b\d{1,3}\s?[A-Z][A-Za-z]{0,3}\s?\d{1,6}/\d{4}\b"), TypUdaje.SpisovaZnacka),
        (new Regex(@"(?:č\.\s?j\.|čj\.|sp\.\s?zn\.|spis\.\s?zn\.|spisová značka|značka)\s*:?\s*(?<u>(?-i:(?:\p{Lu}[\p{Lu}-]* )?)[\w/-]*\d[\w/-]*(?: [\w/-]*\d[\w/-]*)?)", RegexOptions.IgnoreCase), TypUdaje.SpisovaZnacka),
        (new Regex(@"\bvložka\s*(?<u>\d+)", RegexOptions.IgnoreCase), TypUdaje.SpisovaZnacka),
        (new Regex(@"\bev\.\s?č\.(?:\s?ČAK)?\s*(?<u>\d+)", RegexOptions.IgnoreCase), TypUdaje.Jiny),
        (new Regex(@"\b(?:RZ|SPZ)\s*:?\s*(?<u>\d[A-Z][A-Z0-9] ?\d{4})\b"), TypUdaje.Jiny),
        (new Regex(@"\b\d[A-Z][A-Z0-9] ?\d{4}\b"), TypUdaje.Jiny),                          // SPZ i bez označení (1AB 2345)
        (new Regex(@"\b(?=[A-HJ-NPR-Z0-9]*\d)(?=[A-HJ-NPR-Z0-9]*[A-Z])[A-HJ-NPR-Z0-9]{17}\b"), TypUdaje.Jiny), // VIN
        (new Regex(@"\bCZ\d{2}(?:[ ]?\d{4}){5}\b"), TypUdaje.Ucet),                          // IBAN
        (new Regex(@"(?:se sídlem|sídlem|bytem|bydlištěm)\s+v\s+(?<u>\p{Lu}\p{Ll}+(?:[ -]\p{Lu}\p{Ll}+){0,2})(?=[,.;\s]|$)"), TypUdaje.Adresa),
        (new Regex(@"(?:číslo|č\.)\s+(?:pojistné\s+smlouvy|pojistné\s+události|smlouvy|dokladu|klienta|zákazníka|karty|pojištěnce)\s*:?\s*(?<u>[\w/-]*\d[\w/-]*)", RegexOptions.IgnoreCase), TypUdaje.Jiny),
        // osmimístné číslo hned za firmou je IČO, i když chybí slovo „IČO“ (… a.s., 12345678)
        (new Regex(@"(?:s\.\s?r\.\s?o\.|a\.\s?s\.|spol\. s r\.\s?o\.|v\.\s?o\.\s?s\.|k\.\s?s\.|družstvo(?: \p{Lu}\p{L}+)?)\.?,?\s+\(?(?:IČO?:?\s*)?(?<u>\d{8})\b"), TypUdaje.Ico),
        (new Regex(@"(?:variabilní(?:m|ho)? symbol(?:em|u)?|var\.\s?symb(?:ol)?\.?|\bVS)\s*:?\s*(?<u>\d{2,10})\b", RegexOptions.IgnoreCase), TypUdaje.Jiny),
        (new Regex(@"(?<u>(?:\p{Lu}\p{Ll}+ )+(?:družstvo|spolek|nadace|ústav)(?: \p{Lu}[\p{L}-]+)*)"), TypUdaje.Firma),
        (new Regex(@"(?:občansk\w+ průkaz\w*|\bOP|cestovní\w* pas\w*)\s*(?:č\.)?\s*(?<u>\d{6,10})\b", RegexOptions.IgnoreCase), TypUdaje.Jiny),
        (new Regex(@"parc\.?\s*č\.?\s*(?<u>\d+(?:/\d+)?)", RegexOptions.IgnoreCase), TypUdaje.Nemovitost),
        (new Regex(@"\bLV\s*č\.?\s*(?<u>\d+)"), TypUdaje.Nemovitost),
        // U adres se smí přejít přes konec řádku tam, kde je to jednoznačné:
        // mezi PSČ a obcí, za „k. ú.“ a mezi ulicí a číslem domu (zalomení na fotce).
        (new Regex(@"\bk\. ?ú\.[ \n]*(?<u>\p{Lu}[\p{L}-]*(?: (?:u|nad|pod|v|na)? ?\p{Lu}[\p{L}-]*){0,3})"), TypUdaje.Adresa),
        (new Regex(@"(?<u>\b\d{3}[ \n]?\d{2}[ \n](?<obec>\p{Lu}[\p{L}]+)(?: \d{1,2})?(?:(?: (?:u|nad|pod|v|na))?[ -]\p{Lu}[\p{L}]+){0,3}(?: ?[-–] ?\p{Lu}\p{L}+(?: \p{Lu}\p{L}+)?)?)"), TypUdaje.Adresa),
        // ulice s číslem a za ní PSČ (Lipová 9, 999 04 Smyšlov), nebo ulice s typickou koncovkou
        (new Regex(@"(?<u>\b(?<obec>(?:(?:Na|U|V|Pod|Nad|K) )?(?:\p{Lu}\p{Ll}+ ){0,2}\p{Lu}\p{Ll}+(?: \p{Ll}+)?)[ \n]\d{1,5}(?:/\d{1,5})?[a-z]?,[ \n]?\d{3}[ \n]?\d{2}[ \n]\p{Lu}\p{L}+(?: \d{1,2})?(?:(?: (?:u|nad|pod|v|na))?[ -]\p{Lu}\p{L}+){0,3}(?: ?[-–] ?\p{Lu}\p{L}+(?: \p{Lu}\p{L}+)?)?)"), TypUdaje.Adresa),
        (new Regex(@"(?<u>\b(?<obec>\p{Lu}\p{Ll}+(?:ská|cká|ová|ova|ovo|ní|ící|ého|ká|ná))[ \n]\d{1,5}(?:/\d{1,5})?[a-z]?\b)"), TypUdaje.Adresa),
        // za slovy bytem, sídlem apod. až do čárky před dalším údajem
        (new Regex(@"(?:bytem|sídlem|bydlištěm|bydliště:|trvale bytem|trvalý pobyt:?|se sídlem|místem podnikání)\s+(?:v\s+)?(?<u>(?-i:\p{Lu}[^\n,]{0,30}?[ \n]\d{1,5}(?:/\d{1,5})?[a-z]?(?:,[ \n]?\d{3}[ \n]?\d{2}[ \n]\p{Lu}\p{L}+(?: \d{1,2})?(?:(?: (?:u|nad|pod|v|na))?[ -]\p{Lu}\p{L}+){0,3}(?: ?[-–] ?\p{Lu}\p{L}+(?: \p{Lu}\p{L}+)?)?)?))", RegexOptions.IgnoreCase), TypUdaje.Adresa),
    };

    /// <param name="radky">Číslo řádku každého slova. Jména a adresy nepřecházejí přes konec řádku.</param>
    /// <summary>
    /// Spisová značka Nejvyššího soudu, Nejvyššího správního soudu nebo Ústavního soudu
    /// (citovaná judikatura, veřejná): agendy Cdo, Odo, Tdo, ICdo, NSČR, Cul, As, Ads, Afs, Azs, Ars, ÚS, Pl. ÚS.
    /// </summary>
    public static bool JeJudikatura(string znacka) =>
        Regex.IsMatch(znacka, @"\b(?:I?C(?:do|don)|Odo|Tdo|Tz|NSČR|Cul|Ncu|Nd|A(?:s|ds|fs|zs|rs|o)|Pl\.?\s?ÚS|[IV]+\.?\s?ÚS|ÚS)\b");

    /// <summary>Kontrolní součet IČO (vážený součet prvních sedmi číslic mod 11).</summary>
    public static bool PlatneIco(string ico)
    {
        if (ico.Length != 8 || !ico.All(char.IsDigit)) return false;
        int soucet = 0;
        for (int i = 0; i < 7; i++) soucet += (ico[i] - '0') * (8 - i);
        int zbytek = soucet % 11;
        int kontrola = zbytek == 0 ? 1 : zbytek == 1 ? 0 : 11 - zbytek;
        return ico[7] - '0' == kontrola;
    }

    public static List<Nalez> Najdi(IReadOnlyList<string> slova, IReadOnlyList<int>? radky = null)
    {
        var nalezy = new List<Nalez>();
        radky ??= new int[slova.Count];

        // Text strany se začátky slov, aby šlo výsledek regulárního výrazu převést zpět na slova.
        // Řádky odděluje konec řádku, pravidla uvnitř údaje počítají jen s mezerou.
        var zacatky = new int[slova.Count];
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < slova.Count; i++)
        {
            if (i > 0) text.Append(radky[i] != radky[i - 1] ? '\n' : ' ');
            zacatky[i] = text.Length;
            text.Append(slova[i]);
        }
        var s = text.ToString();
        int SlovoNa(int znak)
        {
            int idx = Array.BinarySearch(zacatky, znak);
            return idx >= 0 ? idx : ~idx - 1;
        }

        // osmimístné číslo s platným kontrolním součtem IČO je IČO i bez slova „IČO“
        foreach (Match m in Regex.Matches(s, @"(?<![\d/.-])\d{8}(?![\d/])"))
            if (PlatneIco(m.Value)) nalezy.Add(new Nalez(SlovoNa(m.Index), SlovoNa(m.Index + m.Length - 1), TypUdaje.Ico));

        foreach (var (vzor, typ) in Pravidla)
            foreach (Match m in vzor.Matches(s))
            {
                var g = m.Groups["u"].Success ? m.Groups["u"] : m.Groups[0];
                if (g.Length == 0) continue;
                if (m.Groups["obec"].Success && NeJmena.Contains(m.Groups["obec"].Value)) continue;
                nalezy.Add(new Nalez(SlovoNa(g.Index), SlovoNa(g.Index + g.Length - 1), typ));
            }

        // obec z více slov rozdělená koncem řádku (České / Budějovice, Hradec / Králové)
        for (int k = 0; k < nalezy.Count; k++)
        {
            var n = nalezy[k];
            if (n.Typ != TypUdaje.Adresa) continue;
            int konec = n.Do;
            while (konec + 1 < slova.Count && radky[konec + 1] != radky[konec]
                   && PrvniSlovaObci.Contains(Text.Ocisti(slova[konec])) && Text.ZacinaVelkym(slova[konec + 1]))
                konec++;
            if (konec != n.Do) nalezy[k] = n with { Do = konec };
        }

        NajdiOsoby(slova, radky, nalezy);
        NajdiFirmy(slova, radky, nalezy);
        return Sluc(nalezy);
    }

    private static void NajdiOsoby(IReadOnlyList<string> slova, IReadOnlyList<int> radky, List<Nalez> nalezy)
    {
        for (int i = 0; i < slova.Count; i++)
        {
            bool StejnyRadek(int j) => j >= 0 && j < slova.Count && radky[j] == radky[i];

            // titul nebo oslovení, za ním jedno až tři slova jména (další tituly se přeskočí)
            if (JeTitul(slova[i]))
            {
                int j = i + 1;
                while (StejnyRadek(j) && JeTitul(slova[j])) j++;
                int od = j;
                while (StejnyRadek(j) && j - od < 3 && MuzeBytPrijmeni(slova[j])) j++;
                if (j > od) { nalezy.Add(new Nalez(od, j - 1, TypUdaje.Osoba)); i = j - 1; continue; }
            }

            // křestní jméno + příjmení, nebo příjmení + křestní jméno
            if (JeKrestniJmeno(slova[i]))
            {
                if (StejnyRadek(i + 1) && MuzeBytPrijmeni(slova[i + 1]))
                {
                    nalezy.Add(new Nalez(i, i + 1, TypUdaje.Osoba));
                    i++;
                }
                else if (StejnyRadek(i - 1) && MuzeBytPrijmeni(slova[i - 1]) && !JeKrestniJmeno(slova[i - 1]))
                {
                    nalezy.Add(new Nalez(i - 1, i, TypUdaje.Osoba));
                }
            }
        }
    }

    private static void NajdiFirmy(IReadOnlyList<string> slova, IReadOnlyList<int> radky, List<Nalez> nalezy)
    {
        for (int i = 1; i < slova.Count; i++)
        {
            var forma = Text.Ocisti(slova[i]).TrimEnd('.');
            int konec = i;
            // „s. r. o.“ může být rozdělené do tří slov
            if (forma.Equals("s", StringComparison.OrdinalIgnoreCase) && i + 2 < slova.Count
                && Text.Ocisti(slova[i + 1]).TrimEnd('.').Equals("r", StringComparison.OrdinalIgnoreCase))
            {
                forma = "s.r.o";
                konec = i + 2;
            }
            if (!PravniFormy.Contains(forma)) continue;
            // „spol. s r.o.“: k firmě patří i následující „s r.o.“
            if (forma.Equals("spol", StringComparison.OrdinalIgnoreCase))
                while (konec + 1 < slova.Count && radky[konec + 1] == radky[i] && konec - i < 3
                       && Regex.IsMatch(Text.Ocisti(slova[konec + 1]), @"^(s|r\.?o|r\.o\.?|s\.r\.o)$", RegexOptions.IgnoreCase))
                    konec++;
            // Název firmy: až čtyři slova před právní formou, začíná velkým písmenem
            // („Smyšlené stavby s.r.o.“), končí na dvojtečce nebo čárce před ním.
            int od = i;
            for (int j = i - 1; j >= 0 && i - j <= 4 && radky[j] == radky[i]; j--)
            {
                var w = slova[j];
                if (w.EndsWith(':') || w.EndsWith(';') || (w.EndsWith(',') && j < i - 1) || JeTitul(w)) break;
                if (Text.ZacinaVelkym(w) && !NeJmena.Contains(Text.Ocisti(w))) od = j;
                // čárka těsně před právní formou patří k názvu (Elektro-Servis Zlín, s.r.o.)
            }
            if (od < i) nalezy.Add(new Nalez(od, konec, TypUdaje.Firma));
        }
    }

    /// <summary>Překrývající se nálezy sloučí, přednost má delší.</summary>
    private static List<Nalez> Sluc(List<Nalez> nalezy)
    {
        var vysledek = new List<Nalez>();
        foreach (var n in nalezy.OrderByDescending(n => n.Do - n.Od).ThenBy(n => n.Od))
            if (!vysledek.Any(v => n.Od <= v.Do && v.Od <= n.Do))
                vysledek.Add(n);
        return vysledek.OrderBy(n => n.Od).ToList();
    }
}
