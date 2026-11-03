using Koncipient.Ocr;

namespace Koncipient.Jadro;

/// <summary>
/// Ruční úpravy přepisu strany: doplnění vynechaného textu, smazání slov a oprava
/// zalomení a pořadí řádků. Slova se adresují pořadím na straně (jako v prohlížeči).
/// Doplněná slova mají prázdný <see cref="SlovoOcr.Text"/> a text v <see cref="SlovoOcr.Oprava"/>.
/// </summary>
public static class UpravyStrany
{
    /// <summary>Řádek a pozice v řádku pro slovo s daným pořadím na straně.</summary>
    public static (RadekOcr Radek, int Pozice) Najdi(VysledekOcr v, int indexSlova)
    {
        int zbyva = indexSlova;
        foreach (var r in v.Radky)
        {
            if (zbyva < r.Slova.Count) return (r, zbyva);
            zbyva -= r.Slova.Count;
        }
        throw new ArgumentOutOfRangeException(nameof(indexSlova));
    }

    private static List<SlovoOcr> Doplnena(string text, Obdelnik oblast)
    {
        var slova = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var vysledek = new List<SlovoOcr>();
        int celkem = Math.Max(slova.Sum(s => s.Length + 1), 1), x = oblast.X;
        foreach (var s in slova)
        {
            // šířka slova úměrná počtu písmen, ať klik do fotky aspoň přibližně sedí
            int sirka = Math.Max(1, oblast.Sirka * (s.Length + 1) / celkem);
            vysledek.Add(new SlovoOcr { Text = "", Oprava = s, Ramecek = new Obdelnik(x, oblast.Y, sirka, oblast.Vyska) });
            x += sirka;
        }
        return vysledek;
    }

    private static Obdelnik Obal(IEnumerable<SlovoOcr> slova) => Obdelnik.ZBodu(slova.SelectMany(s => new[]
    {
        ((double)s.Ramecek.X, (double)s.Ramecek.Y),
        ((double)(s.Ramecek.X + s.Ramecek.Sirka), (double)(s.Ramecek.Y + s.Ramecek.Vyska)),
    }));

    /// <summary>Doplní text za slovo na stejném řádku. Vrátí počet doplněných slov.</summary>
    public static int VlozZa(VysledekOcr v, int indexSlova, string text)
    {
        var (radek, pozice) = Najdi(v, indexSlova);
        var kotva = radek.Slova[pozice].Ramecek;
        int pocet = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        var oblast = new Obdelnik(kotva.X + kotva.Sirka, kotva.Y, Math.Max(kotva.Sirka, 10) * Math.Max(pocet, 1), kotva.Vyska);
        var nova = Doplnena(text, oblast);
        radek.Slova.InsertRange(pozice + 1, nova);
        return nova.Count;
    }

    /// <summary>
    /// Doplní text, který OCR celý vynechalo, jako nový řádek v označené oblasti fotky.
    /// Řádek se zařadí podle svislé polohy mezi ostatní. Vrátí pořadí prvního nového slova.
    /// </summary>
    public static int VlozRadek(VysledekOcr v, Obdelnik oblast, string text)
    {
        var slova = Doplnena(text, oblast);
        if (slova.Count == 0) return -1;
        var novy = new RadekOcr { Ramecek = oblast, Slova = slova };
        double stred = oblast.Y + oblast.Vyska / 2.0;
        int kam = v.Radky.FindIndex(r => r.Ramecek.Y + r.Ramecek.Vyska / 2.0 > stred);
        if (kam < 0) kam = v.Radky.Count;
        v.Radky.Insert(kam, novy);
        return v.Radky.Take(kam).Sum(r => r.Slova.Count);
    }

    /// <summary>Smaže slova od–do (včetně), prázdné řádky odstraní.</summary>
    public static void Smaz(VysledekOcr v, int od, int @do)
    {
        var mazat = v.Slova.Skip(od).Take(@do - od + 1).ToHashSet();
        foreach (var r in v.Radky) r.Slova.RemoveAll(mazat.Contains);
        v.Radky.RemoveAll(r => r.Slova.Count == 0);
    }

    /// <summary>Zalomí řádek před slovem: slovo a zbytek řádku budou na novém řádku.</summary>
    public static void Zalom(VysledekOcr v, int indexSlova)
    {
        var (radek, pozice) = Najdi(v, indexSlova);
        if (pozice == 0) return;
        var zbytek = radek.Slova.Skip(pozice).ToList();
        radek.Slova.RemoveRange(pozice, zbytek.Count);
        radek.Ramecek = Obal(radek.Slova);
        v.Radky.Insert(v.Radky.IndexOf(radek) + 1, new RadekOcr { Slova = zbytek, Ramecek = Obal(zbytek) });
    }

    /// <summary>Připojí k řádku se slovem následující řádek.</summary>
    public static void SpojSDalsim(VysledekOcr v, int indexSlova)
    {
        var (radek, _) = Najdi(v, indexSlova);
        int i = v.Radky.IndexOf(radek);
        if (i + 1 >= v.Radky.Count) return;
        radek.Slova.AddRange(v.Radky[i + 1].Slova);
        radek.Ramecek = Obal(radek.Slova);
        v.Radky.RemoveAt(i + 1);
    }

    /// <summary>Posune řádek se slovem o jedno místo nahoru (-1) nebo dolů (+1).</summary>
    public static void Posun(VysledekOcr v, int indexSlova, int smer)
    {
        var (radek, _) = Najdi(v, indexSlova);
        int i = v.Radky.IndexOf(radek), j = i + Math.Sign(smer);
        if (j < 0 || j >= v.Radky.Count) return;
        (v.Radky[i], v.Radky[j]) = (v.Radky[j], v.Radky[i]);
    }
}
