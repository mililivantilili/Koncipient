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

    [JsonIgnore] public string Popis => string.Join(" ", Tvary.FirstOrDefault() ?? new List<string>());

    [JsonIgnore] public string Nahrada => Akce == Akce.Odstranit ? $"[ODSTRANĚNO: {Nazvy.Typ(Typ)}]" : $"[{Id}]";
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

    public Entita Nova(TypUdaje typ, IEnumerable<string> tvar, bool rucne = false)
    {
        var prefix = Nazvy.Prefix(typ);
        int cislo = Entity.Count(e => e.Id.StartsWith(prefix + "_")) + 1;
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
