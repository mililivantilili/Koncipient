using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Koncipient.Jadro.Anonymizace;

public sealed record VysledekExportu(bool Uspech, string Slozka, IReadOnlyList<string> Problemy);

/// <summary>
/// Zápis anonymizovaného spisu do složky ProClaude, kterou advokátka přenese
/// na počítač s Claude Code. Fotky ani klíč pseudonymů se nikdy nezapisují.
/// </summary>
public static class Export
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string VychoziSlozka => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Koncipient", "ProClaude");

    /// <summary>
    /// Ověří a zapíše export. Nic nezapíše, když některá strana není zkontrolovaná
    /// nebo když závěrečná kontrola najde v anonymizovaném textu známý či nový údaj.
    /// </summary>
    public static VysledekExportu Proved(Spis spis, Klic klic, string? slozkaProClaude = null)
    {
        var koren = slozkaProClaude ?? VychoziSlozka;
        var problemy = new List<string>();
        var dokumenty = new List<(int Poradi, string Nazev, string Soubor, List<(int Strana, string Text)> Strany)>();

        int k = 0;
        foreach (var (dokument, strany) in spis.StranyPoDokumentech())
        {
            k++;
            // název dokumentu (ze složky nebo od advokátky) může obsahovat jméno, anonymizuje se taky
            var nazev = Anonymizator.AnonymizujText(dokument.Nazev, klic);
            problemy.AddRange(Kontrola(nazev, klic).Select(p => $"Název dokumentu {k}: {p}"));
            var obsah = new List<(int, string)>();
            foreach (var strana in strany)
            {
                int poradi = spis.Poradi(strana);
                var v = Motory.ProZobrazeni(spis, strana);
                if (v is null) { problemy.Add($"Strana {poradi} nemá přepis."); continue; }
                if (!strana.Zkontrolovano) problemy.Add($"Strana {poradi} není označená jako zkontrolovaná.");
                var text = Anonymizator.AnonymizovanyText(v, klic);
                problemy.AddRange(Kontrola(text, klic).Select(p => $"Strana {poradi}: {p}"));
                obsah.Add((poradi, text));
            }
            dokumenty.Add((k, nazev, $"dokumenty/{k:D2}-{Slug(nazev)}.md", obsah));
        }
        if (problemy.Count > 0) return new VysledekExportu(false, koren, problemy);

        var cil = Path.Combine(koren, spis.Id);
        var docasna = cil + ".zapisuje";
        // výstupy od Clauda z minula zůstanou
        var vystupy = Path.Combine(cil, "vystup");
        if (Directory.Exists(docasna)) Directory.Delete(docasna, recursive: true);
        Directory.CreateDirectory(Path.Combine(docasna, "dokumenty"));

        foreach (var d in dokumenty)
        {
            var sb = new StringBuilder();
            var rozsah = d.Strany.Count == 0 ? "" : d.Strany.Count == 1 ? $", strana {d.Strany[0].Strana}" : $", strany {d.Strany[0].Strana}–{d.Strany[^1].Strana}";
            sb.Append($"# {d.Nazev}\n\n_{spis.Id}, dokument {d.Poradi}{rozsah}_\n");
            foreach (var (strana, text) in d.Strany) sb.Append($"\n## Strana {strana}\n\n{text}");
            File.WriteAllText(Path.Combine(docasna, d.Soubor), sb.ToString(), new UTF8Encoding(false));
        }

        // jen náhrady a role, nikdy skutečné hodnoty (kromě údajů, které advokátka ponechala)
        var entity = klic.Entity.Where(e => e.Akce != Akce.Odstranit)
            .Select(e => new
            {
                nahrada = e.TextNahrady,
                typ = Nazvy.Typ(e.Typ),
                rod = e.Typ == TypUdaje.Osoba && e.Rod is { } r ? (r == Rod.Zena ? "žena" : "muž") : null,
                role = e.Role,
                patri_k = klic.Podle(e.PatriK)?.TextNahrady,
                ponechano = e.Akce == Akce.Ponechat ? true : (bool?)null,
            });
        File.WriteAllText(Path.Combine(docasna, "entity.json"),
            JsonSerializer.Serialize(new { spis = spis.Id, entity }, Json), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(docasna, "manifest.json"),
            JsonSerializer.Serialize(new
            {
                spis = spis.Id,
                exportovano = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                dokumenty = dokumenty.Select(d => new { poradi = d.Poradi, nazev = d.Nazev, soubor = d.Soubor, strany = d.Strany.Select(s => s.Strana) }),
            }, Json), new UTF8Encoding(false));

        // celý spis se vymění najednou, aby v ProClaude nezůstaly staré strany
        if (Directory.Exists(vystupy)) Directory.Move(vystupy, Path.Combine(docasna, "vystup"));
        else Directory.CreateDirectory(Path.Combine(docasna, "vystup"));
        if (Directory.Exists(cil)) Directory.Delete(cil, recursive: true);
        Directory.Move(docasna, cil);
        ZapisPravidla(koren);
        return new VysledekExportu(true, cil, Array.Empty<string>());
    }

    /// <summary>Název souboru z názvu dokumentu: malá písmena bez diakritiky, pomlčky místo mezer.</summary>
    public static string Slug(string nazev)
    {
        var s = System.Text.RegularExpressions.Regex.Replace(Anonymizace.Text.Normalizuj(nazev.Replace("/", " ")), @"[^a-z0-9]+", "-").Trim('-');
        if (s.Length > 50) s = s[..50].TrimEnd('-');
        return s.Length == 0 ? "dokument" : s;
    }

    /// <summary>
    /// Závěrečná kontrola anonymizovaného textu: znovu hledá údaje pravidly
    /// a všechny tvary entit z klíče. Cokoli nalezeného zastaví export.
    /// </summary>
    public static List<string> Kontrola(string anonymizovanyText, Klic klic)
    {
        var problemy = new List<string>();
        var nahlasene = new List<(int Od, int Do)>();
        bool Nove(int od, int @do)
        {
            if (nahlasene.Any(x => od <= x.Do && x.Od <= @do)) return false;
            nahlasene.Add((od, @do));
            return true;
        }
        // pseudonymy se nekontrolují, nahradí se neutrálním slovem, aby se nespojila okolní slova
        var bezPseudonymu = System.Text.RegularExpressions.Regex.Replace(anonymizovanyText, @"\[[^\]\n]*\]", "_");
        var slova = new List<string>();
        var radky = new List<int>();
        foreach (var (radek, i) in bezPseudonymu.Split('\n').Select((r, i) => (r, i)))
            foreach (var s in radek.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                slova.Add(s);
                radky.Add(i);
            }

        foreach (var n in Detektor.Najdi(slova, radky))
        {
            var usekSlova = slova.Skip(n.Od).Take(n.Do - n.Od + 1).ToList();
            var usek = string.Join(" ", usekSlova);
            if (n.Do == n.Od && klic.Vyjimky.Contains(Text.Normalizuj(usek))) continue;
            if (klic.Entity.Any(e => e.Akce == Akce.Ponechat && e.Tvary.Any(t => Text.Normalizuj(string.Join(" ", t)) == Text.Normalizuj(usek))))
                continue;
            // vymyšlená jména a vlastní náhrady jsou v pořádku
            if (usekSlova.All(s => Anonymizator.JePseudonym(klic, s) || klic.Vyjimky.Contains(Text.Normalizuj(s)))) continue;
            if (Nove(n.Od, n.Do)) problemy.Add($"zůstal nerozpoznaný údaj ({Nazvy.Typ(n.Typ)}): „{usek}“");
        }
        foreach (var x in Anonymizator.Vyskyty(slova, klic))
            if (Nove(x.Od, x.Do)) problemy.Add($"zůstal údaj {x.Entita.Id}: „{string.Join(" ", slova.Skip(x.Od).Take(x.Do - x.Od + 1))}“");
        return problemy;
    }

    /// <summary>
    /// Zapíše pravidla pro Clauda. PRAVIDLA-KONCIPIENT.md se přepisuje při každém
    /// exportu, CLAUDE.md jen vytvoří a odkáže na ně, aby si ho advokátka mohla doplnit.
    /// </summary>
    private static void ZapisPravidla(string koren)
    {
        File.WriteAllText(Path.Combine(koren, "PRAVIDLA-KONCIPIENT.md"), Pravidla, new UTF8Encoding(false));
        var claude = Path.Combine(koren, "CLAUDE.md");
        if (!File.Exists(claude))
            File.WriteAllText(claude, """
                # Práce se spisy advokátní kanceláře

                @PRAVIDLA-KONCIPIENT.md

                Sem si můžeš dopsat vlastní pokyny (styl podání, oblíbené formulace…).
                """, new UTF8Encoding(false));
    }

    public const string Pravidla = """
        # Pravidla pro práci s anonymizovanými spisy

        Tyto soubory připravil program Koncipient. Spisy pocházejí z advokátní kanceláře
        a jsou anonymizované: skutečná jména, adresy a další údaje zůstaly na jiném
        počítači. Pracuješ jako koncipient české advokátky podle českého práva.

        ## Kde co je

        - Každá podsložka `S-0001`, `S-0002`… je jeden spis. Číslo spisu je pseudonym.
        - `dokumenty/01-nazev.md` je jeden dokument spisu (smlouva, rozsudek…), strany jsou
          oddíly „## Strana N“ číslované v rámci celého spisu. Text je přepsaný z fotek
          automaticky (OCR), může obsahovat překlepy a chybějící diakritiku. Když je místo
          nečitelné nebo nejasné, napiš to, nedomýšlej si ho. Názvy dokumentů jsou navržené
          programem nebo advokátkou a jsou také anonymizované.
        - `manifest.json` vyjmenovává dokumenty a jejich strany.
        - `entity.json` vyjmenovává náhrady použité ve spisu, jejich typ, rod a roli ve věci
          (žalobce, svědek…) a ke komu patří adresy a další údaje.
        - Výstupy ukládej do `vystup/` v příslušném spisu jako Markdown (`.md`). Odtud si je
          advokátka přenese zpět a program do nich vrátí skutečná jména a uloží je jako Word.

        ## Náhrady jmen a údajů

        - Osoby mají vymyšlená jména (např. Bohdan Jasaník, Radmila Habráková). Jsou to
          pseudonymy, ne skutečná jména. Používej je přesně v této podobě a normálně je
          skloňuj podle češtiny, rod odpovídá skutečné osobě.
        - Některé údaje jsou nahrazené kódy v hranatých závorkách, např. `[ADRESA_001]`,
          `[FIRMA_002]` nebo `[OSOBA_003]`. Kódy přepisuj přesně, včetně závorek, a neskloňuj
          je. Stejný kód znamená stejnou věc ve všech stranách spisu.
        - `[ODSTRANĚNO: telefon]` a podobně znamená, že údaj byl úplně vynechán. Nevymýšlej ho.
        - Údaje, které anonymizované nejsou (soudy, úřady, obce, data, částky, paragrafy),
          jsou skutečné.
        - Nepokoušej se zjistit, kdo se za pseudonymem skrývá, a nespojuj pseudonymy
          z různých spisů. Stejné vymyšlené jméno ve dvou spisech nemusí být stejný člověk.

        ## Nové osoby a údaje

        Když potřebuješ zmínit osobu, místo nebo údaj, který ve spisu není (advokátka ti ho
        řekla v zadání, nebo jde o místo k doplnění), nepiš vymyšlené jméno. Napiš značku
        `[NOVÁ OSOBA: popis]`, `[NOVÉ MÍSTO: popis]` nebo `[DOPLNIT: co]`, např.
        `[NOVÁ OSOBA: znalec z oboru stavebnictví]`. Advokátka je při zpětném zpracování
        doplní.

        ## Odkazy na spis

        Při odkazu na podklady uváděj spis a stranu, např. „(S-0001, s. 3)“, aby advokátka
        místo rychle našla na původní fotce.
        """;
}
