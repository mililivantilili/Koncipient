using System.Globalization;
using System.Text;

namespace Koncipient.Jadro.Anonymizace;

/// <summary>Porovnávání slov odolné vůči skloňování a chybám OCR.</summary>
public static class Text
{
    private const string Interpunkce = ".,;:()[]{}\"'„“”‚‘«»!?–—|*";

    public static string Ocisti(string slovo) => slovo.Trim(Interpunkce.ToCharArray());

    /// <summary>Malá písmena bez diakritiky, bez okrajové interpunkce.</summary>
    public static string Normalizuj(string slovo)
    {
        var rozlozene = Ocisti(slovo).ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(rozlozene.Length);
        foreach (var z in rozlozene)
            if (CharUnicodeInfo.GetUnicodeCategory(z) != UnicodeCategory.NonSpacingMark) sb.Append(z);
        return sb.ToString();
    }

    public static string JenCislice(string s) => new(s.Where(char.IsDigit).ToArray());

    public static bool ZacinaVelkym(string slovo)
    {
        var c = Ocisti(slovo);
        return c.Length > 0 && char.IsUpper(c[0]);
    }

    // Koncovky pádů. Záměrně chybí „ovou“ a „ové“, aby se Novák nespojil s Novákovou.
    private static readonly string[] Koncovky = { "ovi", "eho", "emu", "ymi", "ych", "ech", "ami", "ou", "em", "um", "ym", "a", "u", "e", "y", "i", "o" };

    /// <summary>Možné kmeny normalizovaného slova: bez pádové koncovky a s vypuštěným pohyblivým e (Vymyšlíček, Vymyšlíčka).</summary>
    public static HashSet<string> Kmeny(string normalizovane)
    {
        var kmeny = new HashSet<string> { normalizovane };
        foreach (var k in Koncovky)
            if (normalizovane.EndsWith(k) && normalizovane.Length - k.Length >= 3)
                kmeny.Add(normalizovane[..^k.Length]);
        foreach (var kmen in kmeny.ToList())
            if (kmen.Length >= 4 && (kmen.EndsWith("ek") || kmen.EndsWith("ec") || kmen.EndsWith("el")))
                kmeny.Add(kmen[..^2] + kmen[^1]);
        return kmeny;
    }

    /// <summary>
    /// Jde o stejné slovo v jiném pádě nebo s chybou OCR? Porovnává bez diakritiky,
    /// přes kmeny a u kmenů od 5 znaků toleruje jedno chybné písmeno.
    /// </summary>
    public static bool StejneSlovo(string a, string b)
    {
        var na = Normalizuj(a);
        var nb = Normalizuj(b);
        if (na.Length == 0 || nb.Length == 0) return false;
        if (na == nb) return true;
        if (Math.Abs(na.Length - nb.Length) > 4) return false;
        var ka = Kmeny(na);
        foreach (var kb in Kmeny(nb))
            foreach (var x in ka)
                if (x == kb || (Math.Min(x.Length, kb.Length) >= 5 && Levenshtein(x, kb) <= 1))
                    return true;
        return false;
    }

    /// <summary>
    /// Rod podle koncovky příjmení nebo jména, když je jednoznačný: -ová, -ové, -ovou, -á je žena,
    /// -ý, -ého, -ému, -ým, -ovi, -em je muž. Jinak null.
    /// </summary>
    public static Rod? RodPodleKoncovky(string slovo)
    {
        var s = Ocisti(slovo).ToLowerInvariant();
        if (s.Length < 3) return null;
        if (s.EndsWith("ová") || s.EndsWith("ové") || s.EndsWith("ovou") || s.EndsWith("á")) return Rod.Zena;
        if (s.EndsWith("ý") || s.EndsWith("ého") || s.EndsWith("ému") || s.EndsWith("ým") || s.EndsWith("ovi") || s.EndsWith("em"))
            return Rod.Muz;
        return null;
    }

    /// <summary>
    /// Stejné jméno osoby: stejné slovo (viz <see cref="StejneSlovo"/>) a nesmí jít o muže a ženu,
    /// jinak by se sloučili třeba manželé Pokusný a Pokusná.
    /// </summary>
    public static bool StejneJmenoOsoby(string a, string b)
    {
        if (!StejneSlovo(a, b)) return false;
        var ra = RodPodleKoncovky(a);
        var rb = RodPodleKoncovky(b);
        return ra is null || rb is null || ra == rb;
    }

    public static int Levenshtein(string a, string b)
    {
        var predchozi = new int[b.Length + 1];
        var aktualni = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) predchozi[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            aktualni[0] = i;
            for (int j = 1; j <= b.Length; j++)
                aktualni[j] = Math.Min(Math.Min(predchozi[j] + 1, aktualni[j - 1] + 1),
                    predchozi[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            (predchozi, aktualni) = (aktualni, predchozi);
        }
        return predchozi[b.Length];
    }
}
