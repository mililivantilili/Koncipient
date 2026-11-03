using System.Text;
using System.Text.RegularExpressions;

namespace Koncipient.Jadro.Anonymizace;

public enum DruhUseku
{
    /// <summary>Text od Clauda beze změny.</summary>
    Text,
    /// <summary>Vrácený skutečný údaj, tvar je jistý.</summary>
    Vraceno,
    /// <summary>Vrácený údaj, ale advokátka má zkontrolovat pád nebo jde o jméno, které v klíči není.</summary>
    Zkontrolovat,
    /// <summary>Program neví, co sem patří (odstraněný údaj, nová osoba). Musí doplnit advokátka.</summary>
    Doplnit,
}

public sealed record Usek(string Text, DruhUseku Druh, string? Poznamka = null);

/// <summary>
/// Vrátí do výstupu od Clauda skutečná jména a údaje. Nikdy nenahrazuje naslepo:
/// co si program není jistý, označí ke kontrole.
/// </summary>
public static class ZpetneDosazeni
{
    private static readonly Regex Kod = new(@"\[(?<id>[A-ZÁ-Ž]+_\d{3})\]");
    private static readonly Regex Odstraneno = new(@"\[ODSTRANĚNO:[^\]\n]*\]");
    private static readonly Regex Nove = new(@"\[(?:NOVÁ OSOBA|NOVÉ MÍSTO|DOPLNIT)\s*:[^\]\n]*\]", RegexOptions.IgnoreCase);
    private static readonly Regex SlovoVzor = new(@"\p{L}[\p{L}\-]*");

    private readonly record struct Nahrazeni(int Od, int Delka, string Text, DruhUseku Druh, string? Poznamka);

    public static List<Usek> Vrat(string text, Klic klic)
    {
        var nahrazeni = new List<Nahrazeni>();
        bool Volne(int od, int delka) => !nahrazeni.Any(n => od < n.Od + n.Delka && n.Od < od + delka);

        foreach (Match m in Nove.Matches(text))
            nahrazeni.Add(new(m.Index, m.Length, m.Value, DruhUseku.Doplnit, "Nový údaj od Clauda, doplň skutečnou hodnotu."));
        foreach (Match m in Odstraneno.Matches(text))
            nahrazeni.Add(new(m.Index, m.Length, m.Value, DruhUseku.Doplnit, "Tento údaj byl při anonymizaci odstraněn, doplň ho ručně."));
        foreach (Match m in Kod.Matches(text))
        {
            var e = klic.Podle(m.Groups["id"].Value);
            if (e is null)
                nahrazeni.Add(new(m.Index, m.Length, m.Value, DruhUseku.Doplnit, "Kód, který v klíči není."));
            else
                nahrazeni.Add(new(m.Index, m.Length, e.Popis,
                    e.Typ == TypUdaje.Osoba ? DruhUseku.Zkontrolovat : DruhUseku.Vraceno,
                    e.Typ == TypUdaje.Osoba ? $"{e.Id}: doplněno v 1. pádě, zkontroluj tvar." : e.Id));
        }

        // vlastní náhrady, které napsala advokátka
        foreach (var e in klic.Entity.Where(e => e.Akce == Akce.Pseudonym && !string.IsNullOrWhiteSpace(e.VlastniNahrada)))
            foreach (Match m in Regex.Matches(text, Regex.Escape(e.VlastniNahrada!)))
                if (Volne(m.Index, m.Length))
                    nahrazeni.Add(new(m.Index, m.Length, e.Popis, DruhUseku.Zkontrolovat, $"{e.Id}: doplněno v 1. pádě, zkontroluj tvar."));

        // vymyšlená jména ve všech pádech
        var osoby = klic.Entity.Where(e => e.MaVymysleneJmeno).ToList();
        foreach (Match m in SlovoVzor.Matches(text))
        {
            if (!Volne(m.Index, m.Length)) continue;
            foreach (var e in osoby)
            {
                var rod = e.Rod ?? Rod.Muz;
                (Vzor Pseudo, Vzor? Skutecny)[] dvojice =
                {
                    (Vzor.Urci(e.PseudoPrijmeni!, rod), Vzor.Urci(e.Prijmeni!, rod)),
                    (e.PseudoJmeno is null ? null! : Vzor.Urci(e.PseudoJmeno, rod), e.Jmeno is null ? null : Vzor.Urci(e.Jmeno, rod)),
                };
                var nalez = dvojice.Where(d => d.Pseudo is not null)
                    .Select(d => (d.Skutecny, Pad: d.Pseudo.UrciPad(m.Value)))
                    .FirstOrDefault(x => x.Pad is not null);
                if (nalez.Pad is not Pad pad) continue;
                if (nalez.Skutecny is null)
                {
                    nahrazeni.Add(new(m.Index, m.Length, m.Value, DruhUseku.Doplnit, $"{e.Id}: skutečné křestní jméno není známé."));
                }
                else
                {
                    var tvar = nalez.Skutecny.Tvar(pad);
                    bool jiste = pad == Pad.N || !nalez.Skutecny.Nesklonne;
                    nahrazeni.Add(new(m.Index, m.Length, tvar, jiste ? DruhUseku.Vraceno : DruhUseku.Zkontrolovat,
                        jiste ? $"{e.Id} ({m.Value})" : $"{e.Id}: neumím skloňovat, zkontroluj tvar."));
                }
                break;
            }
        }

        // jména, která v klíči nejsou (Claude je dopsal nebo se objevila nově)
        var slova = new List<(string Slovo, int Od, int Radek)>();
        int radek = 0;
        foreach (Match m in Regex.Matches(text, @"\S+|\n"))
        {
            if (m.Value == "\n") { radek++; continue; }
            slova.Add((m.Value, m.Index, radek));
        }
        foreach (var n in Detektor.Najdi(slova.Select(s => s.Slovo).ToList(), slova.Select(s => s.Radek).ToList()))
        {
            if (n.Typ is not (TypUdaje.Osoba or TypUdaje.Firma)) continue;
            int od = slova[n.Od].Od;
            int delka = slova[n.Do].Od + slova[n.Do].Slovo.Length - od;
            var usek = text.Substring(od, delka);
            var ocistene = Text.Ocisti(usek);
            od += usek.IndexOf(ocistene, StringComparison.Ordinal);
            if (!Volne(od, ocistene.Length)) continue;
            bool zname = klic.Entity.Any(e => e.Tvary.Any(t => t.Any(s => ocistene.Split(' ').Any(x => Text.StejneSlovo(s, x)))));
            if (!zname)
                nahrazeni.Add(new(od, ocistene.Length, ocistene, DruhUseku.Zkontrolovat, "Jméno, které ve spisu není. Je skutečné, nebo ho Claude vymyslel?"));
        }

        var vysledek = new List<Usek>();
        int pozice = 0;
        foreach (var n in nahrazeni.OrderBy(n => n.Od))
        {
            if (n.Od < pozice) continue;
            if (n.Od > pozice) vysledek.Add(new Usek(text[pozice..n.Od], DruhUseku.Text));
            vysledek.Add(new Usek(n.Text, n.Druh, n.Poznamka));
            pozice = n.Od + n.Delka;
        }
        if (pozice < text.Length) vysledek.Add(new Usek(text[pozice..], DruhUseku.Text));
        return vysledek;
    }

    public static string Spoj(IEnumerable<Usek> useky) => string.Concat(useky.Select(u => u.Text));
}
