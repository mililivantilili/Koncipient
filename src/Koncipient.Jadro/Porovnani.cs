using Koncipient.Ocr;

namespace Koncipient.Jadro;

/// <summary>Označí místa, kde dva OCR motory přečetly stranu jinak.</summary>
public static class Porovnani
{
    private const string Interpunkce = ".,;:()[]\"'„“”«»!?-–—|";

    private static string Klic(string slovo) => slovo.Trim(Interpunkce.ToCharArray()).ToLowerInvariant();

    /// <summary>
    /// Najde nejdelší společnou posloupnost slov a slova hlavního výsledku,
    /// která v ní nejsou, označí jako sporná. Vrátí počet sporných slov.
    /// Na testovacích fotkách se motory lišily v 16–110 slovech ze 400.
    /// </summary>
    public static int OznacSporna(VysledekOcr hlavni, VysledekOcr druhy)
    {
        var a = hlavni.Slova.ToList();
        var b = druhy.Slova.Select(s => Klic(s.Text)).ToList();
        var ka = a.Select(s => Klic(s.Platny)).ToList(); // ručně opravená a doplněná slova podle opravy
        int n = a.Count, m = b.Count;

        // délky LCS od konce, ushort stačí do 65 535 slov na stranu
        var d = new ushort[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
            for (int j = m - 1; j >= 0; j--)
                d[i, j] = ka[i] == b[j] ? (ushort)(d[i + 1, j + 1] + 1) : Math.Max(d[i + 1, j], d[i, j + 1]);

        foreach (var s in a) s.Sporne = true;
        for (int i = 0, j = 0; i < n && j < m;)
        {
            if (ka[i] == b[j]) { a[i].Sporne = false; i++; j++; }
            else if (d[i + 1, j] >= d[i, j + 1]) i++;
            else j++;
        }
        return a.Count(s => s.Sporne);
    }
}
