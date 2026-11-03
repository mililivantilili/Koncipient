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

    private static readonly (Regex Vzor, TypUdaje Typ)[] Pravidla =
    {
        (new Regex(@"\b\d{2}[0156]\d[0-3]\d\s?/\s?\d{3,4}\b"), TypUdaje.RodneCislo),
        (new Regex(@"(?:nar\.|narozen[aáý]?|datum narození:?)\s*(?<u>\d{1,2}\.\s?\d{1,2}\.\s?\d{4})", RegexOptions.IgnoreCase), TypUdaje.DatumNarozeni),
        (new Regex(@"\b(?:IČO?|DIČ)\s*:?\s*(?<u>(?:CZ)?\d{8,10})\b"), TypUdaje.Ico),
        (new Regex(@"\b(?:\d{1,6}-)?\d{6,10}/\d{4}\b"), TypUdaje.Ucet),
        (new Regex(@"(?:\+420 ?)?\b[1-9]\d{2} ?\d{3} ?\d{3}\b"), TypUdaje.Telefon),
        (new Regex(@"[\w.+-]+@[\w-]+\.[\w.-]*\w"), TypUdaje.Email),
        (new Regex(@"\b\d{1,3}\s?[A-Z][A-Za-z]{0,3}\s?\d{1,6}/\d{4}\b"), TypUdaje.SpisovaZnacka),
        (new Regex(@"(?:č\.\s?j\.|čj\.)\s*:?\s*(?<u>[\w/-]*\d[\w/-]*)", RegexOptions.IgnoreCase), TypUdaje.SpisovaZnacka),
        (new Regex(@"parc\.?\s*č\.?\s*(?<u>\d+(?:/\d+)?)", RegexOptions.IgnoreCase), TypUdaje.Nemovitost),
        (new Regex(@"\bLV\s*č\.?\s*(?<u>\d+)"), TypUdaje.Nemovitost),
        (new Regex(@"\bk\. ?ú\. *(?<u>\p{Lu}[\p{L}-]*(?: (?:u|nad|pod|v|na)? ?\p{Lu}[\p{L}-]*){0,3})"), TypUdaje.Adresa),
        (new Regex(@"(?<u>\b\d{3} ?\d{2} (?<obec>\p{Lu}[\p{L}]+)(?:(?: (?:u|nad|pod|v|na))?[ -]\p{Lu}[\p{L}]+){0,3})"), TypUdaje.Adresa),
        (new Regex(@"(?<u>\b(?<obec>\p{Lu}\p{Ll}+(?:ská|cká|ova|ovo|ní|ého|ká)) \d{1,5}(?:/\d{1,5})?[a-z]?\b)"), TypUdaje.Adresa),
    };

    /// <param name="radky">Číslo řádku každého slova. Jména a adresy nepřecházejí přes konec řádku.</param>
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

        foreach (var (vzor, typ) in Pravidla)
            foreach (Match m in vzor.Matches(s))
            {
                var g = m.Groups["u"].Success ? m.Groups["u"] : m.Groups[0];
                if (g.Length == 0) continue;
                if (m.Groups["obec"].Success && NeJmena.Contains(m.Groups["obec"].Value)) continue;
                nalezy.Add(new Nalez(SlovoNa(g.Index), SlovoNa(g.Index + g.Length - 1), typ));
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
            // Název firmy: až čtyři slova před právní formou, začíná velkým písmenem
            // („Smyšlené stavby s.r.o.“), končí na dvojtečce nebo čárce před ním.
            int od = i;
            for (int j = i - 1; j >= 0 && i - j <= 4 && radky[j] == radky[i]; j--)
            {
                var w = slova[j];
                if (w.EndsWith(':') || w.EndsWith(';') || (w.EndsWith(',') && j < i - 1) || JeTitul(w)) break;
                if (Text.ZacinaVelkym(w) && !NeJmena.Contains(Text.Ocisti(w))) od = j;
                if (w.EndsWith(',')) break;
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
