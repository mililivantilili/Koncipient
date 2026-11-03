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
        var strany = new List<(Strana Strana, string Text)>();

        foreach (var strana in spis.Strany)
        {
            var v = Motory.ProZobrazeni(spis, strana);
            if (v is null) { problemy.Add($"Strana {strana.Cislo} nemá přepis."); continue; }
            if (!strana.Zkontrolovano) problemy.Add($"Strana {strana.Cislo} není označená jako zkontrolovaná.");
            var text = Anonymizator.AnonymizovanyText(v, klic);
            problemy.AddRange(Kontrola(text, klic).Select(p => $"Strana {strana.Cislo}: {p}"));
            strany.Add((strana, text));
        }
        if (problemy.Count > 0) return new VysledekExportu(false, koren, problemy);

        var cil = Path.Combine(koren, spis.Id);
        var docasna = cil + ".zapisuje";
        if (Directory.Exists(docasna)) Directory.Delete(docasna, recursive: true);
        Directory.CreateDirectory(Path.Combine(docasna, "strany"));

        foreach (var (strana, text) in strany)
            File.WriteAllText(Path.Combine(docasna, "strany", $"{strana.Cislo:D4}.md"),
                $"# {spis.Id}, strana {strana.Cislo}\n\n{text}", new UTF8Encoding(false));

        var entity = klic.Entity.Where(e => e.Akce == Akce.Pseudonym)
            .Select(e => new { id = e.Id, typ = Nazvy.Typ(e.Typ), role = e.Role });
        File.WriteAllText(Path.Combine(docasna, "entity.json"),
            JsonSerializer.Serialize(new { spis = spis.Id, entity }, Json), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(docasna, "manifest.json"),
            JsonSerializer.Serialize(new
            {
                spis = spis.Id,
                exportovano = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                strany = strany.Select(s => new { cislo = s.Strana.Cislo, soubor = $"strany/{s.Strana.Cislo:D4}.md" }),
            }, Json), new UTF8Encoding(false));

        // celý spis se vymění najednou, aby v ProClaude nezůstaly staré strany
        if (Directory.Exists(cil)) Directory.Delete(cil, recursive: true);
        Directory.Move(docasna, cil);
        ZapisPravidla(koren);
        return new VysledekExportu(true, cil, Array.Empty<string>());
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
            var usek = string.Join(" ", slova.Skip(n.Od).Take(n.Do - n.Od + 1));
            if (n.Do == n.Od && klic.Vyjimky.Contains(Text.Normalizuj(usek))) continue;
            if (klic.Entity.Any(e => e.Akce == Akce.Ponechat && e.Tvary.Any(t => Text.Normalizuj(string.Join(" ", t)) == Text.Normalizuj(usek))))
                continue;
            if (Nove(n.Od, n.Do)) problemy.Add($"zůstal nerozpoznaný údaj ({Nazvy.Typ(n.Typ)}): „{usek}“");
        }
        foreach (var x in Anonymizator.Vyskyty(slova, klic))
            if (Nove(x.Od, x.Do)) problemy.Add($"zůstal údaj {x.Entita.Id}: „{string.Join(" ", slova.Skip(x.Od).Take(x.Do - x.Od + 1))}“");
        return problemy;
    }

    private static void ZapisPravidla(string koren)
    {
        var cesta = Path.Combine(koren, "CLAUDE.md");
        if (File.Exists(cesta)) return;
        File.WriteAllText(cesta, """
            # Anonymizované spisy

            Každá podsložka `S-0001` je jeden spis, každá strana je v `strany/0001.md`.

            - Osoby, firmy, adresy, nemovitosti a spisové značky jsou nahrazené pseudonymy
              v hranatých závorkách, např. `[OSOBA_001]`. Stejný pseudonym znamená stejnou
              osobu ve všech stranách spisu. Role osob jsou v `entity.json`, pokud je
              advokátka vyplnila.
            - `[ODSTRANĚNO: telefon]` a podobně znamená, že údaj byl úplně vynechán.
            - Pseudonymy ve výstupu zachovej přesně, včetně závorek. Advokátka je pak
              v programu Koncipient nahradí skutečnými jmény. Skloňuj okolní text,
              pseudonym samotný neměň.
            - Nepokoušej se zjistit, kdo se za pseudonymem skrývá.
            - Text vznikl z fotek automatickým přepisem (OCR), může obsahovat překlepy.
            """, new UTF8Encoding(false));
    }
}
