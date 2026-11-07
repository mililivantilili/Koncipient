using Koncipient.Jadro;
using Koncipient.Jadro.Anonymizace;
using Koncipient.Ocr;
using OpenCvSharp;
using Xunit;

namespace Koncipient.Testy;

public class PrehledSpisuTesty
{
    [Fact]
    public void PruvodceVedeKrokZaKrokem()
    {
        using var d = new DocasnaSlozka();
        var spis = new Uloziste(Path.Combine(d.Cesta, "Spisy")).Zaloz("vzor");
        Assert.Equal(DalsiKrok.PridatFotky, PrehledSpisu.Spocitej(spis).Dalsi);

        var cesty = new List<string>();
        for (int i = 1; i <= 2; i++)
        {
            var c = Path.Combine(d.Cesta, $"{i}.png");
            using (var m = new Mat(1400, 1000, MatType.CV_8UC3, Scalar.White))
            {
                for (int y = 80; y < 1350; y += 36)
                    Cv2.PutText(m, "Kupni smlouva uzavrena dne 1. 1. 2020", new Point(60, y), HersheyFonts.HersheySimplex, 0.8, Scalar.Black, 2);
                Cv2.ImWrite(c, m);
            }
            cesty.Add(c);
        }
        var strany = spis.PridejFotky(cesty, "Smlouva");
        var p = PrehledSpisu.Spocitej(spis);
        Assert.Equal((2, 0, DalsiKrok.SpustitPrepis), (p.Stran, p.Prepsanych, p.Dalsi));
        Assert.Equal(StavStrany.Neprepsana, PrehledSpisu.Stav(spis, strany[0]));

        foreach (var s in strany) spis.UlozOcr(s, new VysledekOcr { Motor = Motory.Paddle, Radky = Vzor.Ocr("Kupní smlouva byla uzavřena.").Radky });
        Assert.Equal(DalsiKrok.Zkontrolovat, PrehledSpisu.Spocitej(spis).Dalsi);
        Assert.Equal(StavStrany.Prepsana, PrehledSpisu.Stav(spis, strany[0]));

        strany[1].DuvodyPrefoceni = new() { "fotka je rozmazaná" };
        Assert.Equal(DalsiKrok.Prefotit, PrehledSpisu.Spocitej(spis).Dalsi);
        Assert.Equal(StavStrany.KPrefoceni, PrehledSpisu.Stav(spis, strany[1]));
        strany[1].DuvodyPrefoceni = null;

        foreach (var s in strany) s.Zkontrolovano = true;
        spis.Uloz();
        Assert.Equal(DalsiKrok.Exportovat, PrehledSpisu.Spocitej(spis).Dalsi);
        var pc = Path.Combine(d.Cesta, "ProClaude");
        Assert.True(Export.Proved(spis, Anonymizator.NactiKlic(spis), pc).Uspech);
        Assert.Equal(DalsiKrok.PrenestNaDisk, PrehledSpisu.Spocitej(spis).Dalsi);
        Prenos.NaDisk(pc, Path.Combine(d.Cesta, "E"), new[] { spis });
        var hotovo = PrehledSpisu.Spocitej(spis);
        Assert.Equal(DalsiKrok.PridatFotky, hotovo.Dalsi);
        Assert.StartsWith("Hotovo", hotovo.TextDalsiho);
    }
}
