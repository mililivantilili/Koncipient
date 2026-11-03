using System.Text.Json.Serialization;

namespace Koncipient.Jadro.Anonymizace;

/// <summary>Jak se nahrazují osoby.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StylPseudonymu
{
    /// <summary>Vymyšlená jména, příjmení podle stromů. Claude s nimi píše přirozenou češtinu.</summary>
    Stromy,
    /// <summary>Vymyšlená jména, příjmení podle zvířat.</summary>
    Zvirata,
    /// <summary>Kódy [OSOBA_001]. Nejjistější, ale Claude je neumí skloňovat.</summary>
    Kody,
}

/// <summary>
/// Vybírá vymyšlená jména pro osoby. Jména jsou méně častá česká, příjmení jsou
/// odvozená od tématu tak, aby šla pravidelně skloňovat a nepletla se se skutečnými.
/// </summary>
public static class Pseudonymy
{
    private static readonly string[] JmenaMuzi =
    {
        "Bohdan", "Ctibor", "Dalibor", "Lubor", "Radim", "Vilém", "Bořivoj", "Mojmír", "Svatoslav",
        "Vratislav", "Budimír", "Dobromil", "Erazim", "Hostimil", "Ingomar", "Kazimír", "Ludomír",
        "Nepomuk", "Oldřich", "Přibyslav", "Rostislav", "Tichomír", "Velimír", "Zbyhněv", "Gothard",
        "Alfréd", "Jaromír", "Florián", "Medard",
    };

    private static readonly string[] JmenaZeny =
    {
        "Bohdana", "Dobroslava", "Ctislava", "Vlastimila", "Radmila", "Svatava", "Milada", "Zbyslava",
        "Bohuslava", "Jarmila", "Vladimíra", "Ludmila", "Kazimíra", "Hostislava", "Ingrida", "Lubomíra",
        "Nedomila", "Otilie", "Přibyslava", "Rostislava", "Tichomíra", "Velislava", "Gertruda", "Alfréda",
        "Ermelinda", "Florentina", "Eufrozína", "Wanda", "Uršula", "Medarda",
    };

    // Kmeny příjmení pro mužský vzor na souhlásku (ženy -ová) a přídavné (-ský, -ská).
    private static readonly Dictionary<StylPseudonymu, (string[] Souhlaska, string[] Pridavne)> Temata = new()
    {
        [StylPseudonymu.Stromy] = (
            new[] { "Jasaník", "Habrák", "Jilmák", "Lipák", "Olšák", "Smrčák", "Borovák", "Tisák", "Akátník",
                    "Platanák", "Kaštaník", "Bukáč", "Jeřabák", "Modřínák", "Topolák", "Vrbák", "Jedlák",
                    "Javořák", "Dubiňák", "Břízák", "Ořešák", "Ginkgák", "Cedrák", "Sekvojník", "Ebenák",
                    "Gaštaník", "Hlohák", "Nespolák" },
            new[] { "Jasanský", "Habrovský", "Jilmovský", "Lipovecký", "Olšanský", "Smrkovský", "Tisovský",
                    "Akátský", "Platanský", "Kaštanský", "Bukovský", "Jedlovský", "Cedrovský", "Ebenský",
                    "Hlohovský", "Nespolský", "Ořešanský", "Vrbovecký", "Gingkovský", "Topolanský" }),
        [StylPseudonymu.Zvirata] = (
            new[] { "Jezevák", "Kuňák", "Vydrák", "Srnák", "Rysák", "Bobrák", "Sysák", "Lasičák", "Tchořák",
                    "Ježák", "Losák", "Vlkoun", "Medvědík", "Zubrák", "Kamzičák", "Sviňuchák", "Hranostajník",
                    "Jelenák", "Daňák", "Muflonák", "Mývalák", "Rosomák", "Ondatrák", "Kunčák", "Plchák",
                    "Křečkák", "Sobolák", "Norkák", "Šakalák", "Pumák" },
            new[] { "Jezevský", "Kunovský", "Vydrovský", "Srnovský", "Rysovský", "Bobrovský", "Sysovský",
                    "Tchořovský", "Losovský", "Zubrovský", "Kamzický", "Jelenský", "Daňovský", "Muflonský",
                    "Mývalský", "Plchovský", "Sobolský", "Norkovský", "Šakalský", "Pumovský" }),
    };

    /// <summary>
    /// Vybere vymyšlené jméno a příjmení se stejným rodem a podobným vzorem skloňování
    /// jako skutečné. Nikdy nevybere slovo, které se podobá čemukoli v <paramref name="zakazana"/>
    /// (skutečná slova spisu) nebo jinému už použitému pseudonymu.
    /// </summary>
    public static (string? Jmeno, string Prijmeni) Vyber(
        StylPseudonymu styl, Rod rod, string? skutecneJmeno, string skutecnePrijmeni,
        bool zachovatIniciály, Func<string, bool> jeZakazane, int semeno)
    {
        var tema = Temata[styl == StylPseudonymu.Kody ? StylPseudonymu.Stromy : styl];
        var nahodne = new Random(semeno);

        string? jmeno = null;
        if (!string.IsNullOrEmpty(skutecneJmeno))
        {
            var seznam = rod == Rod.Zena ? JmenaZeny : JmenaMuzi;
            // vymyšlené jméno se nesmí podobat skutečnému (Radmila × Ramila), hlavně při stejných iniciálách
            var skutecne = Text.Normalizuj(skutecneJmeno);
            bool Zakazane(string s) => jeZakazane(s) || Text.Levenshtein(Text.Normalizuj(s), skutecne) <= 2;
            jmeno = VyberZ(seznam, zachovatIniciály ? skutecneJmeno[0] : null, Zakazane, nahodne)
                ?? VyberZ(seznam, null, Zakazane, nahodne) ?? seznam[0];
        }

        // přídavné příjmení (Novotný, Novotná) dostane přídavné, ostatní vzor -ák / -áková
        var p = Text.Ocisti(skutecnePrijmeni);
        bool pridavne = p.EndsWith("ý") || p.EndsWith("í") || (p.EndsWith("á") && !p.EndsWith("ová"));
        var zaklady = pridavne ? tema.Pridavne : tema.Souhlaska;
        string Tvar(string muzsky) => rod == Rod.Muz ? muzsky
            : pridavne ? muzsky[..^1] + "á"
            : muzsky + "ová";

        var kandidati = zaklady.Select(Tvar).ToArray();
        char? iniciala = zachovatIniciály && p.Length > 0 ? p[0] : null;
        var prijmeni = VyberZ(kandidati, iniciala, jeZakazane, nahodne);
        if (prijmeni is null && iniciala is char c)
        {
            // téma nemá příjmení na potřebné písmeno: písmeno + zbytek tématického slova (Nasaník)
            prijmeni = kandidati.Select(k => char.ToUpper(c) + k[1..].ToLowerInvariant())
                .OrderBy(_ => nahodne.Next()).FirstOrDefault(k => !jeZakazane(k));
        }
        prijmeni ??= VyberZ(kandidati, null, jeZakazane, nahodne) ?? kandidati[0] + "ek";
        return (jmeno, prijmeni);
    }

    private static string? VyberZ(string[] seznam, char? iniciala, Func<string, bool> jeZakazane, Random nahodne) =>
        seznam
            .Where(s => iniciala is null || char.ToUpperInvariant(s[0]) == char.ToUpperInvariant(iniciala.Value))
            .OrderBy(_ => nahodne.Next())
            .FirstOrDefault(s => !jeZakazane(s));
}
