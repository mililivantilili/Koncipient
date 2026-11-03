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
            bool stejne = e.Typ == TypUdaje.Osoba ? Text.StejneJmenoOsoby(tvar[k], slovo) : Text.StejneSlovo(tvar[k], slovo);
            if (!stejne) return false;
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
    /// (i v jiném pádě nebo s chybou OCR), nové založí, seskupí údaje k osobám
    /// a doplní role a vymyšlená jména. Vrátí počet nových entit.
    /// </summary>
    public static int AktualizujKlic(Spis spis, Klic klic)
    {
        int nove = 0;
        var vsechnaSlova = new List<string>();
        foreach (var strana in spis.Strany)
        {
            var v = Motory.ProZobrazeni(spis, strana);
            if (v is null) continue;
            var slova = Slova(v);
            var radky = Radky(v);
            vsechnaSlova.AddRange(slova);
            Entita? posledniOsoba = null;
            int radekOsoby = -10;
            foreach (var n in Detektor.Najdi(slova, radky))
            {
                var usek = slova.Skip(n.Od).Take(n.Do - n.Od + 1).ToList();
                if (usek.All(s => klic.Vyjimky.Contains(Text.Normalizuj(s)))) continue;
                var e = NajdiEntitu(klic, n.Typ, slova, n.Od, n.Do);
                if (e is null) { e = klic.Nova(n.Typ, usek); nove++; }
                else if (!e.Tvary.Any(t => t.Count == usek.Count && t.Zip(usek).All(p => p.First == Text.Ocisti(p.Second))))
                    e.Tvary.Add(usek.Select(Text.Ocisti).ToList());

                if (n.Typ == TypUdaje.Osoba && n.Od > 0 && Detektor.RodZOkoli(slova[n.Od - 1]) is { } rodOkoli)
                    e.RodZOkoli ??= rodOkoli;
                if (n.Typ is TypUdaje.Osoba or TypUdaje.Firma)
                {
                    // role stojí v hlavičce těsně před jménem: „Žalobce: Jan …“
                    if (e.Role is null)
                        for (int i = n.Od - 1; i >= Math.Max(0, n.Od - 3) && radky[i] == radky[n.Od]; i--)
                            if (Detektor.Role(slova[i]) is { } role) { e.Role = role; break; }
                    posledniOsoba = e;
                    radekOsoby = radky[n.Od];
                }
                else if (posledniOsoba is not null && e.PatriK is null && radky[n.Od] - radekOsoby <= 1
                    && n.Typ is not TypUdaje.SpisovaZnacka and not TypUdaje.Nemovitost)
                {
                    // údaj na stejném nebo dalším řádku za osobou patří k ní (hlavička podání)
                    e.PatriK = posledniOsoba.Id;
                }
            }

            // Údaje i z výstupů ostatních motorů: co jeden motor zkomolí, druhý často přečte
            // správně, a oba tvary pak v klíči pomohou najít údaj v hlavním textu.
            foreach (var jiny in Motory.Ostatni(spis, strana, v.Motor))
            {
                var js = Slova(jiny);
                vsechnaSlova.AddRange(js);
                foreach (var n in Detektor.Najdi(js, Radky(jiny)))
                {
                    var usek = js.Skip(n.Od).Take(n.Do - n.Od + 1).ToList();
                    if (usek.All(s => klic.Vyjimky.Contains(Text.Normalizuj(s)))) continue;
                    var e = NajdiEntitu(klic, n.Typ, js, n.Od, n.Do);
                    if (e is null) { klic.Nova(n.Typ, usek); nove++; }
                    else if (!e.Tvary.Any(t => t.Count == usek.Count && t.Zip(usek).All(p => p.First == Text.Ocisti(p.Second))))
                        e.Tvary.Add(usek.Select(Text.Ocisti).ToList());
                }
            }
        }
        DoplnOsoby(klic, vsechnaSlova);
        return nove;
    }

    /// <summary>
    /// U osob odvodí z tvarů jméno a příjmení v 1. pádě a rod a vybere vymyšlené
    /// jméno, které se nepodobá žádnému slovu spisu ani jinému pseudonymu.
    /// </summary>
    public static void DoplnOsoby(Klic klic, IEnumerable<string> slovaSpisu)
    {
        var obsazene = new HashSet<string>();
        foreach (var s in slovaSpisu.Where(Text.ZacinaVelkym)) obsazene.UnionWith(Text.Kmeny(Text.Normalizuj(s)));
        foreach (var t in klic.Entity.SelectMany(e => e.Tvary).SelectMany(t => t)) obsazene.UnionWith(Text.Kmeny(Text.Normalizuj(t)));
        foreach (var e in klic.Entity)
            foreach (var s in new[] { e.PseudoJmeno, e.PseudoPrijmeni }.OfType<string>())
                obsazene.UnionWith(Text.Kmeny(Text.Normalizuj(s)));
        bool JeZakazane(string s) => Text.Kmeny(Text.Normalizuj(s)).Overlaps(obsazene);

        foreach (var e in klic.Entity.Where(e => e.Typ == TypUdaje.Osoba))
        {
            UrciJmeno(e);
            if (e.Prijmeni is null || e.PseudoPrijmeni is not null || klic.Styl == StylPseudonymu.Kody) continue;
            var (jmeno, prijmeni) = Pseudonymy.Vyber(klic.Styl, e.Rod ?? Rod.Muz, e.Jmeno, e.Prijmeni, e.Inicialy, JeZakazane,
                klic.Semeno ^ StabilniHash(e.Id));
            e.PseudoJmeno = jmeno;
            e.PseudoPrijmeni = prijmeni;
            foreach (var s in new[] { jmeno, prijmeni }.OfType<string>()) obsazene.UnionWith(Text.Kmeny(Text.Normalizuj(s)));
        }
    }

    /// <summary>Hash, který je stejný při každém spuštění (string.GetHashCode není).</summary>
    private static int StabilniHash(string s)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (var c in s) h = (h ^ c) * 16777619;
            return (int)h;
        }
    }

    /// <summary>Nové vymyšlené jméno pro osobu (třeba po zapnutí iniciál).</summary>
    public static void NovePseudonym(Klic klic, Entita e, IEnumerable<string> slovaSpisu)
    {
        var stare = (e.PseudoJmeno, e.PseudoPrijmeni);
        e.PseudoJmeno = e.PseudoPrijmeni = null;
        e.VlastniNahrada = null;
        // staré jméno se nesmí vybrat znovu
        DoplnOsoby(klic, slovaSpisu.Concat(new[] { stare.PseudoJmeno, stare.PseudoPrijmeni }.OfType<string>()));
    }

    private static void UrciJmeno(Entita e)
    {
        var slova = e.Tvary.SelectMany(t => t).Select(Text.Ocisti).Where(s => s.Length > 0).ToList();
        var krestni = slova.Where(Detektor.JeKrestniJmeno).ToList();
        var prijmeni = slova.Where(s => !Detektor.JeKrestniJmeno(s)).ToList();
        if (prijmeni.Count == 0 && krestni.Count > 0) { prijmeni = krestni; krestni = new(); }
        if (prijmeni.Count == 0) return;

        // rod: příjmení na -ová/-á je jasné, jinak podle křestního jména v 1. pádě ze seznamu
        string? jmeno = null;
        Rod rod = prijmeni.Any(p => p.EndsWith("ová") || (p.EndsWith("á") && p.Length > 3)) ? Anonymizace.Rod.Zena : Anonymizace.Rod.Muz;
        if (krestni.Count > 0)
        {
            var vSeznamu = krestni.Distinct().Where(Detektor.JeVSeznamuJmen).ToList();
            jmeno = vSeznamu
                .OrderByDescending(k => { var r = Sklonovani.UrciRod(k, null); var v = Vzor.Urci(k, r); return krestni.Count(x => v.UrciPad(x) is not null); })
                .ThenBy(k => k.Length)
                .FirstOrDefault() ?? Sklonovani.ZakladniTvar(krestni, rod);
            if (!prijmeni.Any(p => p.EndsWith("ová"))) rod = Sklonovani.UrciRod(jmeno, null);
        }
        else if (!prijmeni.Any(p => Text.RodPodleKoncovky(p) is not null) && e.RodZOkoli is { } okoli)
        {
            rod = okoli; // „rozená Zkušební“: koncovka -í rod neprozradí, slovo před jménem ano
        }
        e.Rod = rod;
        e.Jmeno = jmeno;
        e.Prijmeni = Sklonovani.ZakladniTvar(prijmeni, rod);
    }

    /// <summary>
    /// Převezme pseudonymy z klíče jiného spisu: stejná osoba (podle tvarů jména)
    /// dostane stejné vymyšlené jméno nebo náhradu a roli. Osoby, které ve spisu
    /// zatím nejsou, se přidají, aby je program poznal v budoucích dokumentech.
    /// Vrátí počet převzatých a přidaných entit.
    /// </summary>
    public static (int Prevzato, int Pridano) Prevezmi(Klic cil, Klic zdroj)
    {
        int prevzato = 0, pridano = 0;
        foreach (var z in zdroj.Entity.Where(z => z.Akce != Akce.Ponechat))
        {
            var shoda = cil.Entity.FirstOrDefault(c => c.Typ == z.Typ && z.Tvary.Any(tz => c.Tvary.Any(tc =>
                tz.Count == tc.Count && tz.Zip(tc).All(p => Nazvy.JeCiselny(c.Typ)
                    ? Text.JenCislice(p.First) == Text.JenCislice(p.Second)
                    : c.Typ == TypUdaje.Osoba ? Text.StejneJmenoOsoby(p.First, p.Second) : Text.StejneSlovo(p.First, p.Second)))));
            if (shoda is null)
            {
                shoda = cil.Nova(z.Typ, z.Tvary[0]);
                shoda.Tvary = z.Tvary.Select(t => t.ToList()).ToList();
                shoda.Akce = z.Akce;
                pridano++;
            }
            else prevzato++;
            shoda.PseudoJmeno = z.PseudoJmeno;
            shoda.PseudoPrijmeni = z.PseudoPrijmeni;
            shoda.VlastniNahrada = z.VlastniNahrada;
            shoda.Inicialy = z.Inicialy;
            shoda.Role ??= z.Role;
            shoda.Jmeno ??= z.Jmeno;
            shoda.Prijmeni ??= z.Prijmeni;
            shoda.Rod ??= z.Rod;
        }
        return (prevzato, pridano);
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

    /// <summary>
    /// Náhrada konkrétního výskytu. U vymyšleného jména ve stejném pádě jako v textu
    /// (Nováka → Jasaníka), jinak <see cref="Entita.TextNahrady"/>.
    /// </summary>
    public static string NahradaVyskytu(Entita e, IReadOnlyList<string> slova, int od, int @do)
    {
        if (!e.MaVymysleneJmeno) return e.TextNahrady;
        var rod = e.Rod ?? Rod.Muz;
        var skutJ = e.Jmeno is null ? null : Vzor.Urci(e.Jmeno, rod);
        var skutP = Vzor.Urci(e.Prijmeni!, rod);
        var pseudoJ = e.PseudoJmeno is null ? null : Vzor.Urci(e.PseudoJmeno, rod);
        var pseudoP = Vzor.Urci(e.PseudoPrijmeni!, rod);
        var casti = new List<string>();
        for (int i = od; i <= @do; i++)
        {
            var slovo = Text.Ocisti(slova[i]);
            if (skutJ is not null && pseudoJ is not null && skutJ.UrciPad(slovo) is Pad pj && !(skutP.UrciPad(slovo) is not null && i == @do))
                casti.Add(pseudoJ.Tvar(pj));
            else if (skutP.UrciPad(slovo) is Pad pp)
                casti.Add(pseudoP.Tvar(pp));
            else if (pseudoJ is not null && e.Jmeno is not null && Text.StejneSlovo(e.Jmeno, slovo) && i < @do)
                casti.Add(pseudoJ.Tvar(Pad.N));
            else
                casti.Add(pseudoP.Tvar(Pad.N)); // tvar neznáme, zůstane 1. pád
        }
        return string.Join(" ", casti);
    }

    /// <summary>Je slovo tvarem některého vymyšleného jména nebo vlastní náhrady z klíče?</summary>
    public static bool JePseudonym(Klic klic, string slovo)
    {
        var c = Text.Ocisti(slovo);
        foreach (var e in klic.Entity)
        {
            var rod = e.Rod ?? Rod.Muz;
            if (e.PseudoJmeno is not null && Vzor.Urci(e.PseudoJmeno, rod).UrciPad(c) is not null) return true;
            if (e.PseudoPrijmeni is not null && Vzor.Urci(e.PseudoPrijmeni, rod).UrciPad(c) is not null) return true;
            if (!string.IsNullOrWhiteSpace(e.VlastniNahrada) &&
                e.VlastniNahrada.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(s => Text.StejneSlovo(s, c))) return true;
            if (e.Akce == Akce.Ponechat && e.Tvary.Any(t => t.Any(s => Text.StejneSlovo(s, c)))) return true;
        }
        return false;
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
            // „s.r.o.,“: tečka patří ke zkratce, ne za náhradu
            if (konec.StartsWith('.') && ocistene.Contains('.')) konec = konec[1..];
            var zacatek = slova[x.Od];
            var pred = zacatek[..Math.Max(0, zacatek.IndexOf(Text.Ocisti(zacatek), StringComparison.Ordinal))];
            nahrada[x.Od] = pred + NahradaVyskytu(x.Entita, slova, x.Od, x.Do) + (konec.Length <= 2 ? konec : "");
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
