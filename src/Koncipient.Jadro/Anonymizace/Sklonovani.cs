using System.Text.Json.Serialization;

namespace Koncipient.Jadro.Anonymizace;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Rod { Muz, Zena }

/// <summary>Pády jednotného čísla.</summary>
public enum Pad { N, G, D, A, V, L, I }

/// <summary>
/// Skloňování jmen a příjmení podle nejběžnějších vzorů. Slouží k tomu, aby vymyšlené
/// jméno stálo ve stejném pádě jako skutečné (a naopak při zpětném dosazení).
/// Neobvyklá jména zůstanou v 1. pádě a program je označí ke kontrole.
/// </summary>
public sealed class Vzor
{
    public required string Nazev { get; init; }

    /// <summary>Kmen, ke kterému se přidávají koncovky (Novák, Novákov, Novotn…).</summary>
    public required string Kmen { get; init; }

    /// <summary>Tvar 1. pádu, u pohyblivého e se liší od kmene (Vrabec × Vrabc-).</summary>
    public required string Zaklad { get; init; }

    private readonly Dictionary<Pad, string[]> _koncovky = new();

    private static readonly Pad[] Pady = Enum.GetValues<Pad>();

    private Vzor With(params (Pad Pad, string[] Koncovky)[] k)
    {
        foreach (var (p, kon) in k) _koncovky[p] = kon;
        return this;
    }

    public bool Nesklonne => _koncovky.Count == 0;

    /// <summary>Určí vzor podle 1. pádu a rodu.</summary>
    public static Vzor Urci(string zaklad, Rod rod)
    {
        var z = Text.Ocisti(zaklad);
        var n = Text.Normalizuj(z);
        Vzor V(string nazev, string kmen) => new() { Nazev = nazev, Kmen = kmen, Zaklad = z };

        if (z.Length < 2) return V("nesklonné", z);
        if (z.EndsWith("ová") || (rod == Rod.Zena && z.EndsWith("á")))
            return V("žena -á", z[..^1]).With(
                (Pad.N, new[] { "á" }), (Pad.G, new[] { "é" }), (Pad.D, new[] { "é" }), (Pad.A, new[] { "ou" }),
                (Pad.V, new[] { "á" }), (Pad.L, new[] { "é" }), (Pad.I, new[] { "ou" }));
        if (z.EndsWith("ý"))
            return V("muž -ý", z[..^1]).With(
                (Pad.N, new[] { "ý" }), (Pad.G, new[] { "ého" }), (Pad.D, new[] { "ému" }), (Pad.A, new[] { "ého" }),
                (Pad.V, new[] { "ý" }), (Pad.L, new[] { "ém" }), (Pad.I, new[] { "ým" }));
        if (z.EndsWith("í"))
            return rod == Rod.Zena ? V("nesklonné", z) : V("muž -í", z[..^1]).With(
                (Pad.N, new[] { "í" }), (Pad.G, new[] { "ího" }), (Pad.D, new[] { "ímu" }), (Pad.A, new[] { "ího" }),
                (Pad.V, new[] { "í" }), (Pad.L, new[] { "ím" }), (Pad.I, new[] { "ím" }));
        if (z.EndsWith("a"))
            return rod == Rod.Zena
                ? V("žena -a", z[..^1]).With(
                    (Pad.N, new[] { "a" }), (Pad.G, new[] { "y" }), (Pad.D, new[] { "ě" }), (Pad.A, new[] { "u" }),
                    (Pad.V, new[] { "o" }), (Pad.L, new[] { "ě" }), (Pad.I, new[] { "ou" }))
                : V("muž -a", z[..^1]).With(
                    (Pad.N, new[] { "a" }), (Pad.G, new[] { "y" }), (Pad.D, new[] { "ovi" }), (Pad.A, new[] { "u" }),
                    (Pad.V, new[] { "o" }), (Pad.L, new[] { "ovi" }), (Pad.I, new[] { "ou" }));
        if (z.EndsWith("e") && rod == Rod.Zena)
            return V("žena -e", z[..^1]).With(
                (Pad.N, new[] { "e" }), (Pad.G, new[] { "e" }), (Pad.D, new[] { "i" }), (Pad.A, new[] { "i" }),
                (Pad.V, new[] { "e" }), (Pad.L, new[] { "i" }), (Pad.I, new[] { "í" }));
        if (rod == Rod.Muz && char.IsLetter(z[^1]) && !"aeiouyáéíóúůýě".Contains(char.ToLowerInvariant(z[^1])))
        {
            // pohyblivé e: Vrabec → Vrabce, Pešek → Peška
            var kmen = z.Length >= 4 && (n.EndsWith("ek") || n.EndsWith("ec")) ? z[..^2] + z[^1] : z;
            bool mekky = "cjřšžčďťň".Contains(char.ToLowerInvariant(kmen[^1]));
            return V(kmen == z ? "muž souhláska" : "muž pohyblivé e", kmen).With(
                (Pad.N, new[] { "" }), (Pad.G, new[] { mekky ? "e" : "a" }), (Pad.D, new[] { "ovi", mekky ? "i" : "u" }),
                (Pad.A, new[] { mekky ? "e" : "a" }), (Pad.V, new[] { mekky ? "i" : "e", "u" }),
                (Pad.L, new[] { "ovi", mekky ? "i" : "u" }), (Pad.I, new[] { "em" }));
        }
        return V("nesklonné", z);
    }

    /// <summary>Tvar v daném pádě (první z možných koncovek).</summary>
    public string Tvar(Pad pad)
    {
        if (Nesklonne || pad == Pad.N) return Zaklad;
        var koncovka = _koncovky[pad][0];
        return Pripoj(Kmen, koncovka);
    }

    /// <summary>
    /// Připojí koncovku. U „ě“ (Janě) a „e“ za tvrdou souhláskou provede českou
    /// změnu kmene: Petra → Petře, Lenka → Lence, Olga → Olze, Radmila → Radmile.
    /// </summary>
    private static string Pripoj(string kmen, string koncovka)
    {
        if (koncovka != "ě" || kmen.Length == 0) return kmen + koncovka;
        var posledni = char.ToLowerInvariant(kmen[^1]);
        var bez = kmen[..^1];
        return posledni switch
        {
            'r' => bez + "ře",
            'k' => bez + "ce",
            'h' => bez + "ze",
            'g' => bez + "ze",
            'l' or 's' or 'z' or 'c' or 'j' or 'ž' or 'š' or 'č' or 'ř' => kmen + "e",
            _ => kmen + "ě",
        };
    }

    /// <summary>
    /// Ve kterém pádě je slovo, nebo null, když to není tvar tohoto jména.
    /// Porovnává bez diakritiky, aby prošly i drobné chyby OCR.
    /// </summary>
    public Pad? UrciPad(string slovo)
    {
        var n = Text.Normalizuj(slovo);
        if (n.Length == 0) return null;
        if (Nesklonne) return n == Text.Normalizuj(Zaklad) ? Pad.N : null;
        foreach (var pad in Pady)
            foreach (var k in _koncovky[pad])
            {
                var tvar = pad == Pad.N ? Zaklad : Pripoj(Kmen, k);
                if (Text.Normalizuj(tvar) == n) return pad;
            }
        return null;
    }
}

public static class Sklonovani
{
    private static readonly HashSet<string> ZenskaNaSouhlasku = new(StringComparer.OrdinalIgnoreCase)
        { "Dagmar", "Ester", "Miriam", "Karin", "Ingrid", "Ruth", "Edit" };

    /// <summary>Rod podle křestního jména, případně podle příjmení (-ová, -á).</summary>
    public static Rod UrciRod(string? jmeno, string? prijmeni)
    {
        if (!string.IsNullOrEmpty(jmeno))
        {
            var j = Text.Ocisti(jmeno);
            if (ZenskaNaSouhlasku.Contains(j)) return Rod.Zena;
            var n = Text.Normalizuj(j);
            return n.EndsWith('a') || n.EndsWith('e') ? Rod.Zena : Rod.Muz;
        }
        if (!string.IsNullOrEmpty(prijmeni))
        {
            var p = Text.Ocisti(prijmeni);
            if (p.EndsWith("ová") || p.EndsWith("á")) return Rod.Zena;
        }
        return Rod.Muz;
    }

    /// <summary>
    /// Ze skloňovaných tvarů vybere nejpravděpodobnější 1. pád: ten, ze kterého
    /// jdou odvodit ostatní tvary. Při shodě vyhraje kratší.
    /// </summary>
    public static string ZakladniTvar(IReadOnlyCollection<string> tvary, Rod rod)
    {
        var kandidati = tvary.Select(Text.Ocisti).Where(t => t.Length > 0).Distinct().ToList();
        if (kandidati.Count == 0) return "";
        return kandidati
            .OrderByDescending(k => { var v = Vzor.Urci(k, rod); return kandidati.Count(t => v.UrciPad(t) is not null); })
            .ThenBy(k => k.Length)
            .First();
    }
}
