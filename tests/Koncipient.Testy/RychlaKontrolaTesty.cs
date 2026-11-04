using Koncipient.Jadro;
using Koncipient.Jadro.Anonymizace;
using Koncipient.Ocr;
using OpenCvSharp;
using Xunit;

namespace Koncipient.Testy;

public class RychlaKontrolaTesty
{
    private static (Spis Spis, Klic Klic, DocasnaSlozka Slozka) Spis(params string[] strany)
    {
        var d = new DocasnaSlozka();
        var spis = new Uloziste(d.Cesta).Zaloz("vzor");
        using (var m = new Mat(100, 100, MatType.CV_8UC3, Scalar.White))
            for (int i = 0; i < strany.Length; i++) Cv2.ImWrite(Path.Combine(d.Cesta, $"{i + 1}.png"), m);
        var nove = spis.PridejFotky(Enumerable.Range(1, strany.Length).Select(i => Path.Combine(d.Cesta, $"{i}.png")));
        for (int i = 0; i < strany.Length; i++)
            spis.UlozOcr(nove[i], Vzor.Ocr(strany[i]) is var v ? new VysledekOcr { Motor = Motory.Paddle, Radky = v.Radky } : null!);
        var klic = Anonymizator.NactiKlic(spis);
        Anonymizator.AktualizujKlic(spis, klic);
        return (spis, klic, d);
    }

    // vymyšlený „slovník“: běžná slova ano, smyšlená příjmení ne
    private static readonly HashSet<string> Bezna = new(StringComparer.OrdinalIgnoreCase)
        { "smlouvu", "podepsal", "také", "svědek", "byl", "přítomen", "dne", "soud", "rozhodl", "Praha", "Brno" };
    private static bool VeSlovniku(string s) => Bezna.Contains(s);

    [Fact]
    public void NahrazenyUdajJednouNaStranuSPoctem()
    {
        var (spis, klic, d) = Spis("Smlouvu podepsal Ing. Petr Pokusný dne 1. 1. 2020.\nSvědek byl Petr Pokusný.\nPodepsal také Pokusný.",
                                   "Soud rozhodl.");
        using var _ = d;
        var mista = RychlaKontrola.Najdi(spis, klic, VeSlovniku);
        var osoba = Assert.Single(mista, m => m.Druh == DruhMista.Nahrazeno && m.Entita!.Typ == TypUdaje.Osoba);
        Assert.True(osoba.PocetNaStrane == 3, string.Join(" | ", mista.Select(m => $"{m.Druh} {m.Udaj} {m.PocetNaStrane} {m.Entita?.Id}")));
        Assert.Equal("Petr Pokusný", osoba.Udaj);
        Assert.Equal("Smlouvu podepsal Ing.", osoba.Pred);
        Assert.DoesNotContain(mista, m => m.Druh == DruhMista.Podezrele); // titul Ing. není podezřelý
        Assert.NotEqual(osoba.Udaj, osoba.Nahrada);
        // druhá strana nemá nic ke kontrole
        Assert.Equal(new[] { spis.Strany[1] }, RychlaKontrola.StranyBezNalezu(spis, mista));
    }

    [Fact]
    public void PrehlednuteJmenoACisloJsouPodezrela()
    {
        var (spis, klic, d) = Spis("Svědek byl také Zkušebníček a dne číslo 4471235 také Praha.");
        using var _ = d;
        var podezrela = RychlaKontrola.Najdi(spis, klic, VeSlovniku).Where(m => m.Druh == DruhMista.Podezrele).Select(m => m.Udaj).ToList();
        Assert.Contains("Zkušebníček", podezrela);
        Assert.Contains("4471235", podezrela);
        Assert.DoesNotContain("Praha", podezrela);   // ve slovníku
        Assert.DoesNotContain("Svědek", podezrela);  // začátek věty
    }

    [Fact]
    public void PonechanyANecitlivyUdajNeniPodezrely()
    {
        var (spis, klic, d) = Spis("Svědek byl také Zkušebníček přítomen.");
        using var _ = d;
        Anonymizator.OznacNecitlive(klic, "Zkušebníček");
        Assert.DoesNotContain(RychlaKontrola.Najdi(spis, klic, VeSlovniku), m => m.Udaj == "Zkušebníček");
    }
}
