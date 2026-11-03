using System.Text.Json.Serialization;

namespace Koncipient.Jadro.Anonymizace;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TypUdaje
{
    Osoba, Firma, Adresa, Nemovitost, SpisovaZnacka, Ico,
    RodneCislo, DatumNarozeni, Ucet, Telefon, Email, Jiny,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Akce
{
    /// <summary>Nahradit pseudonymem, např. OSOBA_001.</summary>
    Pseudonym,
    /// <summary>Úplně odstranit, zůstane jen popis typu.</summary>
    Odstranit,
    /// <summary>Nechat v textu, advokátka rozhodla, že to není citlivé.</summary>
    Ponechat,
}

/// <summary>Jeden údaj ve spisu (osoba, firma, telefon…) se všemi tvary, jak se v textu objevil.</summary>
public sealed class Entita
{
    public required string Id { get; set; }
    public TypUdaje Typ { get; set; }
    public Akce Akce { get; set; }

    /// <summary>Role ve věci (žalobce, svědek…), jde do exportu, aby Claude věděl, kdo je kdo.</summary>
    public string? Role { get; set; }

    /// <summary>Tvary z textu, každý jako posloupnost slov. Nikdy se neexportují.</summary>
    public List<List<string>> Tvary { get; set; } = new();

    /// <summary>Přidala ji advokátka ručně.</summary>
    public bool Rucne { get; set; }

    /// <summary>Osoba nebo firma, ke které údaj patří (adresa, datum narození, telefon…).</summary>
    public string? PatriK { get; set; }

    // ----- jen osoby -----

    /// <summary>Skutečné křestní jméno v 1. pádě, odvozené z tvarů v textu.</summary>
    public string? Jmeno { get; set; }

    /// <summary>Skutečné příjmení v 1. pádě, odvozené z tvarů v textu.</summary>
    public string? Prijmeni { get; set; }

    public Rod? Rod { get; set; }

    /// <summary>Vymyšlené jméno a příjmení, vybrané jednou a pak stálé.</summary>
    public string? PseudoJmeno { get; set; }
    public string? PseudoPrijmeni { get; set; }

    /// <summary>Vymyšlené jméno má stejné iniciály jako skutečné. Rozhoduje advokátka.</summary>
    public bool Inicialy { get; set; }

    /// <summary>Náhrada, kterou advokátka napsala sama. Má přednost před vymyšleným jménem i kódem.</summary>
    public string? VlastniNahrada { get; set; }

    [JsonIgnore]
    public string Popis => Typ == TypUdaje.Osoba && Prijmeni is not null
        ? $"{Jmeno} {Prijmeni}".Trim()
        : string.Join(" ", Tvary.FirstOrDefault() ?? new List<string>());

    /// <summary>Text, který v anonymizovaném dokumentu nahradí údaj (v 1. pádě).</summary>
    [JsonIgnore]
    public string TextNahrady => Akce switch
    {
        Akce.Odstranit => $"[ODSTRANĚNO: {Nazvy.Typ(Typ)}]",
        Akce.Ponechat => Popis,
        _ when !string.IsNullOrWhiteSpace(VlastniNahrada) => VlastniNahrada!,
        _ when Typ == TypUdaje.Osoba && PseudoPrijmeni is not null => $"{PseudoJmeno} {PseudoPrijmeni}".Trim(),
        _ => $"[{Id}]",
    };

    /// <summary>Používá se skloňované vymyšlené jméno (ne kód ani vlastní náhrada).</summary>
    [JsonIgnore]
    public bool MaVymysleneJmeno => Typ == TypUdaje.Osoba && Akce == Akce.Pseudonym
        && string.IsNullOrWhiteSpace(VlastniNahrada) && PseudoPrijmeni is not null && Prijmeni is not null;
}

/// <summary>
/// Klíč pseudonymů spisu (klic.json ve složce spisu). Zůstává jen na počítači se spisy,
/// do složky ProClaude nesmí.
/// </summary>
public sealed class Klic
{
    public List<Entita> Entity { get; set; } = new();

    /// <summary>Normalizovaná slova, o kterých advokátka řekla, že nejsou citlivá.</summary>
    public HashSet<string> Vyjimky { get; set; } = new();

    /// <summary>Jak se nahrazují osoby, platí pro celý spis.</summary>
    public StylPseudonymu Styl { get; set; } = StylPseudonymu.Stromy;

    /// <summary>
    /// Náhodné číslo spisu pro výběr vymyšlených jmen. Díky němu dostane stejný člověk
    /// v různých spisech různá jména (pokud je advokátka nepřevezme), takže Claude
    /// nemůže spisy propojit.
    /// </summary>
    public int Semeno { get; set; } = Random.Shared.Next();

    public Entita? Podle(string? id) => id is null ? null : Entity.FirstOrDefault(e => e.Id == id);

    public Entita Nova(TypUdaje typ, IEnumerable<string> tvar, bool rucne = false)
    {
        var prefix = Nazvy.Prefix(typ);
        int cislo = 1;
        while (Entity.Any(e => e.Id == $"{prefix}_{cislo:D3}")) cislo++;
        var e = new Entita
        {
            Id = $"{prefix}_{cislo:D3}",
            Typ = typ,
            Akce = Nazvy.VychoziAkce(typ),
            Tvary = { tvar.Select(Text.Ocisti).ToList() },
            Rucne = rucne,
        };
        Entity.Add(e);
        return e;
    }
}

/// <summary>Výskyt entity na straně: slova Od až Do (včetně) v pořadí slov strany.</summary>
public readonly record struct Vyskyt(int Od, int Do, Entita Entita);

public static class Nazvy
{
    public static string Prefix(TypUdaje t) => t switch
    {
        TypUdaje.Osoba => "OSOBA",
        TypUdaje.Firma => "FIRMA",
        TypUdaje.Adresa => "ADRESA",
        TypUdaje.Nemovitost => "NEMOVITOST",
        TypUdaje.SpisovaZnacka => "ZNACKA",
        TypUdaje.Ico => "ICO",
        _ => "UDAJ",
    };

    public static string Typ(TypUdaje t) => t switch
    {
        TypUdaje.Osoba => "osoba",
        TypUdaje.Firma => "firma",
        TypUdaje.Adresa => "adresa",
        TypUdaje.Nemovitost => "nemovitost",
        TypUdaje.SpisovaZnacka => "spisová značka",
        TypUdaje.Ico => "IČO",
        TypUdaje.RodneCislo => "rodné číslo",
        TypUdaje.DatumNarozeni => "datum narození",
        TypUdaje.Ucet => "číslo účtu",
        TypUdaje.Telefon => "telefon",
        TypUdaje.Email => "e-mail",
        _ => "údaj",
    };

    /// <summary>Výchozí zacházení podle návrhu, advokátka ho může u každého údaje změnit.</summary>
    public static Akce VychoziAkce(TypUdaje t) => t switch
    {
        TypUdaje.RodneCislo or TypUdaje.DatumNarozeni or TypUdaje.Ucet or TypUdaje.Telefon
            or TypUdaje.Email or TypUdaje.Jiny => Akce.Odstranit,
        _ => Akce.Pseudonym,
    };

    /// <summary>Údaje, které se porovnávají podle číslic, ne podle slov.</summary>
    public static bool JeCiselny(TypUdaje t) =>
        t is TypUdaje.RodneCislo or TypUdaje.Ucet or TypUdaje.Telefon or TypUdaje.Ico or TypUdaje.DatumNarozeni;
}
