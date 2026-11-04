using Koncipient.Jadro.Anonymizace;

namespace Koncipient.Jadro;

/// <summary>Nález při hledání ve spisu: strana, slova od–do a okolní text.</summary>
public sealed record NalezHledani(Strana Strana, int Od, int Do, string Pred, string Text, string Po);

/// <summary>
/// Hledání slova nebo fráze ve všech stranách spisu. Nezáleží na velikosti písmen ani
/// diakritice (OCR ji občas ztratí), fráze může přecházet přes konec řádku.
/// Slovo se hledá i jako začátek delšího slova, takže „smlouv“ najde smlouvu i smlouvě.
/// </summary>
public static class Hledani
{
    private const int Okoli = 6;

    public static List<NalezHledani> Najdi(Spis spis, string dotaz, int nejvic = 500)
    {
        var hledana = dotaz.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Klic).Where(s => s.Length > 0).ToList();
        var nalezy = new List<NalezHledani>();
        if (hledana.Count == 0) return nalezy;
        foreach (var strana in spis.Strany)
        {
            var v = Motory.ProZobrazeni(spis, strana);
            if (v is null) continue;
            var slova = v.Slova.Select(s => s.Platny).ToList();
            var klice = slova.Select(Klic).ToList();
            for (int i = 0; i + hledana.Count <= slova.Count; i++)
            {
                bool shoda = true;
                for (int j = 0; j < hledana.Count && shoda; j++)
                {
                    var w = klice[i + j];
                    // uvnitř fráze celé slovo, poslední slovo fráze může být začátek (skloňování)
                    shoda = j == hledana.Count - 1 ? w.StartsWith(hledana[j]) : w == hledana[j];
                }
                if (!shoda) continue;
                int @do = i + hledana.Count - 1;
                nalezy.Add(new NalezHledani(strana, i, @do,
                    string.Join(" ", slova.Skip(Math.Max(0, i - Okoli)).Take(Math.Min(Okoli, i))),
                    string.Join(" ", slova.Skip(i).Take(hledana.Count)),
                    string.Join(" ", slova.Skip(@do + 1).Take(Okoli))));
                if (nalezy.Count >= nejvic) return nalezy;
            }
        }
        return nalezy;
    }

    private static string Klic(string slovo) => Text.Normalizuj(Text.Ocisti(slovo));
}
