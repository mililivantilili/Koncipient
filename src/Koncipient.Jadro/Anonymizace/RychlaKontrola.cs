using System.Text.RegularExpressions;
using Koncipient.Ocr;
using WeCantSpell.Hunspell;

namespace Koncipient.Jadro.Anonymizace;

public enum DruhMista
{
    /// <summary>Program údaj nahradí: zkontrolovat, že je to opravdu citlivý údaj.</summary>
    Nahrazeno,
    /// <summary>Slovo zůstane, ale vypadá jako jméno nebo číslo, které program přehlédl.</summary>
    Podezrele,
}

/// <summary>Jedno místo k rychlé kontrole s kouskem okolního textu.</summary>
public sealed record MistoKontroly(
    Strana Strana, int Od, int Do, DruhMista Druh, Entita? Entita,
    string Pred, string Udaj, string Nahrada, string Po, Obdelnik Ramecek, int PocetNaStrane);

/// <summary>
/// Rychlá kontrola místo procházení celých stran: ukáže jen místa, kde program údaj nahradí
/// (u každého údaje jednou za stranu), a slova, která vypadají jako přehlédnutá jména nebo čísla.
/// Advokátka je projde klávesnicí a stranu, na které má vše vyřízené, program označí jako
/// zkontrolovanou.
/// </summary>
public static class RychlaKontrola
{
    private static WordList? _slovnik;
    private static bool _slovnikNacten;

    /// <summary>Český slovník (stejný jako pro slučování OCR), null když chybí.</summary>
    private static WordList? Slovnik()
    {
        if (_slovnikNacten) return _slovnik;
        _slovnikNacten = true;
        var aff = Path.Combine(Motory.SlozkaModelu, "cs_CZ.aff");
        var dic = Path.Combine(Motory.SlozkaModelu, "cs_CZ.dic");
        if (File.Exists(aff) && File.Exists(dic)) _slovnik = WordList.CreateFromFiles(dic, aff);
        return _slovnik;
    }

    private const int Okoli = 7;

    public static List<MistoKontroly> Najdi(Spis spis, Klic klic, Func<string, bool>? veSlovniku = null)
    {
        var slovnik = veSlovniku is null ? Slovnik() : null;
        veSlovniku ??= s => slovnik is null || slovnik.Check(s) || slovnik.Check(s.ToLowerInvariant());
        // slova, která advokátka nechala (soud, úřad, obec), nejsou podezřelá
        var ponechana = klic.Entity.Where(e => e.Akce == Akce.Ponechat)
            .SelectMany(e => e.Tvary.SelectMany(t => t)).Select(Text.Normalizuj).ToHashSet();

        var mista = new List<MistoKontroly>();
        foreach (var strana in spis.Strany)
        {
            var v = Motory.ProZobrazeni(spis, strana);
            if (v is null) continue;
            var slovaOcr = v.Slova.ToList();
            var slova = Anonymizator.Slova(v);
            var vyskyty = Anonymizator.Vyskyty(slova, klic);

            MistoKontroly Misto(int od, int @do, DruhMista druh, Entita? e, string nahrada, int pocet) => new(
                strana, od, @do, druh, e,
                string.Join(" ", slova.Skip(Math.Max(0, od - Okoli)).Take(Math.Min(Okoli, od))),
                string.Join(" ", slova.Skip(od).Take(@do - od + 1)),
                nahrada,
                string.Join(" ", slova.Skip(@do + 1).Take(Okoli)),
                Obdelnik.ZBodu(slovaOcr.Skip(od).Take(@do - od + 1).SelectMany(s => new[]
                    { ((double)s.Ramecek.X, (double)s.Ramecek.Y), ((double)s.Ramecek.X + s.Ramecek.Sirka, (double)s.Ramecek.Y + s.Ramecek.Vyska) })),
                pocet);

            foreach (var skupina in vyskyty.GroupBy(x => x.Entita))
            {
                var prvni = skupina.First();
                mista.Add(Misto(prvni.Od, prvni.Do, DruhMista.Nahrazeno, skupina.Key,
                    Anonymizator.NahradaVyskytu(skupina.Key, slova, prvni.Od, prvni.Do), skupina.Count()));
            }

            var zakryte = new bool[slova.Count];
            foreach (var x in vyskyty) for (int i = x.Od; i <= x.Do; i++) zakryte[i] = true;
            var nahlasene = new HashSet<string>();
            for (int i = 0; i < slova.Count; i++)
            {
                if (zakryte[i]) continue;
                var w = Text.Ocisti(slova[i]);
                var n = Text.Normalizuj(w);
                if (n.Length < 3 || klic.Vyjimky.Contains(n) || ponechana.Contains(n) || Anonymizator.JePseudonym(klic, w)) continue;
                if (!JePodezrele(slova, i, w, veSlovniku)) continue;
                if (!nahlasene.Add(n)) continue; // stejné slovo na straně jen jednou
                mista.Add(Misto(i, i, DruhMista.Podezrele, null, w, 1));
            }
        }
        return mista.OrderBy(m => spis.Poradi(m.Strana)).ThenBy(m => m.Od).ToList();
    }

    /// <summary>
    /// Slovo, které vypadá jako přehlédnutý údaj: známé křestní jméno, slovo s velkým písmenem
    /// mimo slovník uprostřed věty, nebo číslo s aspoň pěti číslicemi (letopočty a částky ne).
    /// </summary>
    private static bool JePodezrele(IReadOnlyList<string> slova, int i, string w, Func<string, bool> veSlovniku)
    {
        if (w.Any(char.IsDigit))
        {
            var cislice = Text.JenCislice(w);
            if (cislice.Length < 5) return false;
            if (Regex.IsMatch(w, @"^\d{1,3}(?:[ .]\d{3})+(?:,\d+)?(?:,-)?$")) return false; // částka 12.500,-
            return !Regex.IsMatch(w, @"^\d{1,2}\.\d{1,2}\.\d{4}$"); // datum se nechává
        }
        if (!w.All(z => char.IsLetter(z) || z == '-') || !char.IsUpper(w[0])) return false;
        if (slova[i].EndsWith('.') && w.Length <= 5 && !Detektor.JeKrestniJmeno(w)) return false; // zkratky a tituly (Ing., Mgr., odst.)
        if (Detektor.JeKrestniJmeno(w)) return true;
        bool zacatekVety = i == 0 || Regex.IsMatch(slova[i - 1], @"[.:;!?]$|^[-–•]$|^\d+[.)]$");
        if (zacatekVety) return false;
        bool velkymi = w.Length > 1 && w.All(z => !char.IsLetter(z) || char.IsUpper(z));
        if (velkymi && w.Length <= 4) return false; // zkratky (ČR, OSŘ, ČSN)
        return !veSlovniku(w);
    }

    /// <summary>Strany, na kterých program nenašel nic ke kontrole.</summary>
    public static List<Strana> StranyBezNalezu(Spis spis, IReadOnlyCollection<MistoKontroly> mista) =>
        spis.Strany.Where(s => Motory.ProZobrazeni(spis, s) is not null && mista.All(m => m.Strana != s)).ToList();
}
