using Koncipient.Jadro;
using Koncipient.Ocr;
using OpenCvSharp;
using Xunit;

namespace Koncipient.Testy;

public class KvalitaTesty
{
    private static string Stranka(string slozka, string nazev, bool rozmazat = false, int pozadi = 245)
    {
        Directory.CreateDirectory(slozka);
        using var m = new Mat(1400, 1000, MatType.CV_8UC3, new Scalar(pozadi, pozadi, pozadi));
        for (int y = 80; y < 1350; y += 36)
            Cv2.PutText(m, "Kupni smlouva uzavrena dne 1. 1. 2020 mezi stranami", new Point(60, y), HersheyFonts.HersheySimplex, 0.8, Scalar.Black, 2);
        if (rozmazat) Cv2.GaussianBlur(m, m, new Size(21, 21), 0);
        var cesta = Path.Combine(slozka, nazev);
        Cv2.ImWrite(cesta, m);
        return cesta;
    }

    [Fact]
    public void RozmazanaATmavaFotkaJeKPrefoceni()
    {
        using var d = new DocasnaSlozka();
        var z = Path.Combine(d.Cesta, "z");
        var spis = new Uloziste(Path.Combine(d.Cesta, "Spisy")).Zaloz("vzor");
        spis.PridejFotky(new[] { Stranka(z, "1.jpg"), Stranka(z, "2.jpg", rozmazat: true), Stranka(z, "3.jpg", pozadi: 70) }, "Smlouva");
        var vysledek = KvalitaFotek.Najdi(spis);
        Assert.DoesNotContain(vysledek, x => x.Strana == spis.Strany[0]);
        Assert.Contains("fotka je rozmazaná", Assert.Single(vysledek, x => x.Strana == spis.Strany[1]).Duvody);
        Assert.Contains("fotka je tmavá", Assert.Single(vysledek, x => x.Strana == spis.Strany[2]).Duvody);
    }

    [Fact]
    public void PrazdneStrankyZMobiluNejsouKPrefoceni()
    {
        using var d = new DocasnaSlozka();
        var spis = new Uloziste(Path.Combine(d.Cesta, "Spisy")).Zaloz("vzor");
        var vzory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!Directory.Exists(Path.Combine(vzory.FullName, "vzory", "prazdne"))) vzory = vzory.Parent!;
        var strany = spis.PridejFotky(Directory.GetFiles(Path.Combine(vzory.FullName, "vzory", "prazdne"), "*.jpg"));
        foreach (var s in strany) spis.UlozOcr(s, new VysledekOcr { Motor = Motory.Paddle, Radky = { new RadekOcr { Slova = { new SlovoOcr { Text = "1" } } } } });
        Assert.Empty(KvalitaFotek.Najdi(spis));
    }

    [Fact]
    public void NecitelnaStranaBezSlovSeNabidne()
    {
        // kontrola 2026-11-06: úplně rozmazaná strana nemá slovo a tvářila se jako prázdná
        using var d = new DocasnaSlozka();
        var z = Path.Combine(d.Cesta, "z");
        var spis = new Uloziste(Path.Combine(d.Cesta, "Spisy")).Zaloz("vzor");
        var cesta = Stranka(z, "1.jpg");
        using (var m = Cv2.ImRead(cesta))
        {
            using var mala = new Mat();
            Cv2.Resize(m, mala, new Size(m.Width / 8, m.Height / 8));
            Cv2.Resize(mala, m, m.Size());
            Cv2.GaussianBlur(m, m, new Size(15, 15), 0);
            Cv2.ImWrite(cesta, m);
        }
        var strana = spis.PridejFotky(new[] { cesta })[0];
        spis.UlozOcr(strana, new VysledekOcr { Motor = Motory.Paddle });
        Assert.Contains(KvalitaFotek.Posud(spis, strana), x => x.StartsWith("fotka je nečitelná"));
    }

    [Fact]
    public void NecitelnouStranuJdePonechatAClaudeDostanePoznamku()
    {
        using var d = new DocasnaSlozka();
        var z = Path.Combine(d.Cesta, "z");
        var spis = new Uloziste(Path.Combine(d.Cesta, "Spisy")).Zaloz("vzor");
        var cesta = Stranka(z, "1.jpg");
        using (var m = Cv2.ImRead(cesta))
        {
            using var mala = new Mat();
            Cv2.Resize(m, mala, new Size(m.Width / 8, m.Height / 8));
            Cv2.Resize(mala, m, m.Size());
            Cv2.GaussianBlur(m, m, new Size(15, 15), 0);
            Cv2.ImWrite(cesta, m);
        }
        var strana = spis.PridejFotky(new[] { cesta }, "Dopis")[0];
        spis.UlozOcr(strana, new VysledekOcr { Motor = Motory.Paddle });
        Assert.Single(KvalitaFotek.Najdi(spis));
        Assert.Equal(DalsiKrok.Prefotit, PrehledSpisu.Spocitej(spis).Dalsi);

        spis.PonechatBezPrefoceni(strana);
        Assert.Empty(KvalitaFotek.Najdi(spis)); // už se nenabízí
        Assert.Equal(DalsiKrok.Zkontrolovat, PrehledSpisu.Spocitej(spis).Dalsi);

        strana.Zkontrolovano = true;
        spis.Uloz();
        var v = Koncipient.Jadro.Anonymizace.Export.Proved(spis, Koncipient.Jadro.Anonymizace.Anonymizator.NactiKlic(spis), Path.Combine(d.Cesta, "PC"));
        Assert.True(v.Uspech, string.Join("; ", v.Problemy));
        var dokument = File.ReadAllText(Directory.GetFiles(Path.Combine(v.Slozka, "dokumenty")).Single());
        Assert.Contains("Strana je nečitelná", dokument);
    }

    [Fact]
    public void UriznutyTextANejistyPrepis()
    {
        using var d = new DocasnaSlozka();
        var spis = new Uloziste(Path.Combine(d.Cesta, "Spisy")).Zaloz("vzor");
        var strana = spis.PridejFotky(new[] { Stranka(Path.Combine(d.Cesta, "z"), "1.jpg") })[0];
        var radky = Enumerable.Range(0, 25).Select(i => new RadekOcr
        {
            Slova = { new SlovoOcr { Text = "konec", Ramecek = new Obdelnik(900, 40 + i * 50, 100, 30) } },
        }).ToList();
        spis.UlozOcr(strana, new VysledekOcr { Motor = Motory.Paddle, Sirka = 1000, Vyska = 1400, Radky = radky });
        spis.UlozOcr(strana, new VysledekOcr { Motor = Motory.Tesseract, Sirka = 1000, Vyska = 1400,
            Radky = radky.Select(r => new RadekOcr { Slova = { new SlovoOcr { Text = "jinak", Ramecek = r.Slova[0].Ramecek } } }).ToList() });
        var duvody = KvalitaFotek.Posud(spis, strana);
        Assert.Contains("text je u okraje uříznutý", duvody);
        Assert.Contains(duvody, x => x.StartsWith("přepis je nejistý"));
    }

    [Fact]
    public void NahrazeniFotkyZahodiPrepisAKontrolu()
    {
        using var d = new DocasnaSlozka();
        var z = Path.Combine(d.Cesta, "z");
        var spis = new Uloziste(Path.Combine(d.Cesta, "Spisy")).Zaloz("vzor");
        var strana = spis.PridejFotky(new[] { Stranka(z, "1.jpg", rozmazat: true) })[0];
        spis.UlozOcr(strana, new VysledekOcr { Motor = Motory.Paddle });
        spis.OtocStranu(strana, 90);
        strana.Zkontrolovano = true;

        spis.NahradFotku(strana, Stranka(z, "1-znovu.jpg"));
        Assert.False(spis.MaOcr(strana, Motory.Paddle));
        Assert.Equal(0, strana.Otoceni);
        Assert.False(strana.Zkontrolovano);
        Assert.False(strana.Narovnano);
        Assert.Equal("1-znovu.jpg", strana.PuvodniNazev);
        Assert.Empty(KvalitaFotek.Posud(spis, strana));
    }
}
