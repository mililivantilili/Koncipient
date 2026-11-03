using System.Text;
using System.Text.Json;
using Koncipient.Ocr;

namespace Koncipient.Jadro.Anonymizace;

/// <summary>
/// Anonymizace spisu: najde údaje, vede klíč pseudonymů a z přepisu udělá text,
/// ve kterém jsou údaje nahrazené pseudonymy nebo odstraněné.
/// </summary>
public static class Anonymizator
{
    private static string CestaKlice(Spis spis) => Path.Combine(spis.Slozka, "klic.json");

    public static Klic NactiKlic(Spis spis)
    {
        var cesta = CestaKlice(spis);
        return File.Exists(cesta)
            ? JsonSerializer.Deserialize<Klic>(File.ReadAllText(cesta), Spis.Json) ?? new Klic()
            : new Klic();
    }

    public static void UlozKlic(Spis spis, Klic klic)
    {
        var cesta = CestaKlice(spis);
        File.WriteAllText(cesta + ".tmp", JsonSerializer.Serialize(klic, Spis.Json));
        File.Move(cesta + ".tmp", cesta, overwrite: true);
    }

    /// <summary>Platná slova strany v pořadí čtení.</summary>
    public static List<string> Slova(VysledekOcr v) => v.Slova.Select(s => s.Platny).ToList();

    /// <summary>Číslo řádku pro každé slovo strany.</summary>
    public static List<int> Radky(VysledekOcr v) =>
        v.Radky.SelectMany((r, i) => Enumerable.Repeat(i, r.Slova.Count)).ToList();

    // ---------- Porovnání tvarů ----------

    private static bool StejnyTvar(Entita e, IReadOnlyList<string> tvar, IReadOnlyList<string> slova, int od)
    {
        if (od + tvar.Count > slova.Count) return false;
        if (Nazvy.JeCiselny(e.Typ))
        {
            // čísla mohou být v OCR rozdělená mezerou jinak, porovnají se jen číslice
            var hledane = Text.JenCislice(string.Concat(tvar));
            return hledane.Length > 0 && Text.JenCislice(string.Concat(slova.Skip(od).Take(tvar.Count))) == hledane;
        }
        for (int k = 0; k < tvar.Count; k++)
        {
            var slovo = slova[od + k];
            // jména osob a první slovo firmy se píšou velkým písmenem (Malý vs. malý)
            if ((e.Typ == TypUdaje.Osoba || (e.Typ == TypUdaje.Firma && k == 0)) && !Text.ZacinaVelkym(slovo)) return false;
            if (!Text.StejneSlovo(tvar[k], slovo)) return false;
        }
        return true;
    }

    /// <summary>
    /// Tvary, podle kterých se entita hledá. U osoby i samotné příjmení
    /// (poslední slovo, pokud není křestní jméno), protože v textu bývá „pan Novák“.
    /// </summary>
    private static IEnumerable<List<string>> HledaneTvary(Entita e)
    {
        foreach (var t in e.Tvary.Where(t => t.Count > 0)) yield return t;
        if (e.Typ != TypUdaje.Osoba) yield break;
        foreach (var t in e.Tvary.Where(t => t.Count >= 2))
        {
            var prijmeni = t[^1];
            if (Text.Ocisti(prijmeni).Length >= 3 && !Detektor.JeKrestniJmeno(prijmeni))
                yield return new List<string> { prijmeni };
        }
    }

    public static Entita? NajdiEntitu(Klic klic, TypUdaje typ, IReadOnlyList<string> slova, int od, int @do)
    {
        var usek = slova.Skip(od).Take(@do - od + 1).ToList();
        return klic.Entity.FirstOrDefault(e => e.Typ == typ && HledaneTvary(e).Any(t => t.Count == usek.Count && StejnyTvar(e, t, usek, 0)))
            ?? klic.Entity.FirstOrDefault(e => e.Typ == typ && e.Typ == TypUdaje.Osoba
                && HledaneTvary(e).Any(t => t.Count == 1 && usek.Any(s => StejnyTvar(e, t, new[] { s }, 0))));
    }

    // ---------- Klíč ----------

    /// <summary>
    /// Projde všechny přepsané strany, nalezené údaje přiřadí k existujícím entitám
    /// (i v jiném pádě nebo s chybou OCR), nové založí. Vrátí počet nových entit.
    /// </summary>
    public static int AktualizujKlic(Spis spis, Klic klic)
    {
        int nove = 0;
        foreach (var strana in spis.Strany)
        {
            var v = Motory.ProZobrazeni(spis, strana);
            if (v is null) continue;
            var slova = Slova(v);
            foreach (var n in Detektor.Najdi(slova, Radky(v)))
            {
                var usek = slova.Skip(n.Od).Take(n.Do - n.Od + 1).ToList();
                if (usek.All(s => klic.Vyjimky.Contains(Text.Normalizuj(s)))) continue;
                var e = NajdiEntitu(klic, n.Typ, slova, n.Od, n.Do);
                if (e is null) { klic.Nova(n.Typ, usek); nove++; }
                else if (!e.Tvary.Any(t => t.Count == usek.Count && t.Zip(usek).All(p => p.First == Text.Ocisti(p.Second))))
                    e.Tvary.Add(usek.Select(Text.Ocisti).ToList());
            }
        }
        return nove;
    }

    /// <summary>Ruční označení slov jako údaje. Připojí je k existující entitě, nebo založí novou.</summary>
    public static Entita OznacRucne(Klic klic, TypUdaje typ, IReadOnlyList<string> slova, int od, int @do)
    {
        var usek = slova.Skip(od).Take(@do - od + 1).ToList();
        foreach (var s in usek) klic.Vyjimky.Remove(Text.Normalizuj(s));
        var e = NajdiEntitu(klic, typ, slova, od, @do);
        if (e is not null)
        {
            e.Tvary.Add(usek.Select(Text.Ocisti).ToList());
            return e;
        }
        return klic.Nova(typ, usek, rucne: true);
    }

    /// <summary>Slovo není citlivé: přidá ho mezi výjimky a odebere z tvarů entit.</summary>
    public static void OznacNecitlive(Klic klic, string slovo)
    {
        var n = Text.Normalizuj(slovo);
        klic.Vyjimky.Add(n);
        foreach (var e in klic.Entity)
            e.Tvary.RemoveAll(t => t.Count == 1 && Text.Normalizuj(t[0]) == n);
        klic.Entity.RemoveAll(e => e.Tvary.Count == 0);
    }

    // ---------- Výskyty a anonymizovaný text ----------

    /// <summary>Kde na straně se vyskytují entity z klíče. Překryvy řeší ve prospěch delšího výskytu.</summary>
    public static List<Vyskyt> Vyskyty(IReadOnlyList<string> slova, Klic klic)
    {
        var kandidati = new List<Vyskyt>();
        foreach (var e in klic.Entity.Where(e => e.Akce != Akce.Ponechat))
            foreach (var t in HledaneTvary(e))
                for (int i = 0; i + t.Count <= slova.Count; i++)
                {
                    if (t.Count == 1 && klic.Vyjimky.Contains(Text.Normalizuj(slova[i]))) continue;
                    if (StejnyTvar(e, t, slova, i)) kandidati.Add(new Vyskyt(i, i + t.Count - 1, e));
                }

        var vysledek = new List<Vyskyt>();
        foreach (var k in kandidati.OrderByDescending(k => k.Do - k.Od).ThenBy(k => k.Od))
            if (!vysledek.Any(v => k.Od <= v.Do && v.Od <= k.Do))
                vysledek.Add(k);
        return vysledek.OrderBy(v => v.Od).ToList();
    }

    /// <summary>Anonymizovaný text strany, řádky zůstávají zachované.</summary>
    public static string AnonymizovanyText(VysledekOcr v, Klic klic)
    {
        var slova = Slova(v);
        var vyskyty = Vyskyty(slova, klic);
        var nahrada = new string?[slova.Count];
        var vynechat = new bool[slova.Count];
        foreach (var x in vyskyty)
        {
            // interpunkce za údajem zůstane (čárka, tečka)
            var posledni = slova[x.Do];
            var ocistene = Text.Ocisti(posledni);
            var konec = posledni[(Math.Max(0, posledni.IndexOf(ocistene, StringComparison.Ordinal)) + ocistene.Length)..];
            var zacatek = slova[x.Od];
            var pred = zacatek[..Math.Max(0, zacatek.IndexOf(Text.Ocisti(zacatek), StringComparison.Ordinal))];
            nahrada[x.Od] = pred + x.Entita.Nahrada + (konec.Length <= 2 ? konec : "");
            for (int i = x.Od + 1; i <= x.Do; i++) vynechat[i] = true;
        }

        var sb = new StringBuilder();
        int index = 0;
        foreach (var radek in v.Radky)
        {
            var casti = new List<string>();
            foreach (var _ in radek.Slova)
            {
                if (!vynechat[index]) casti.Add(nahrada[index] ?? slova[index]);
                index++;
            }
            if (casti.Count > 0) sb.AppendLine(string.Join(" ", casti));
        }
        return sb.ToString();
    }
}
