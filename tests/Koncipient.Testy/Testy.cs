using Koncipient.Jadro;
using Koncipient.Ocr;
using OpenCvSharp;
using Xunit;

namespace Koncipient.Testy;

// Všechna data v testech jsou vymyšlená.

public sealed class DocasnaSlozka : IDisposable
{
    public string Cesta { get; } = Directory.CreateTempSubdirectory("koncipient-test-").FullName;
    public void Dispose() => Directory.Delete(Cesta, recursive: true);
}

public class PorovnaniTesty
{
    private static VysledekOcr Vysledek(string motor, params string[] radky) => new()
    {
        Motor = motor,
        Radky = radky.Select(r => new RadekOcr
        {
            Slova = r.Split(' ').Select(s => new SlovoOcr { Text = s }).ToList(),
        }).ToList(),
    };

    [Fact]
    public void ShodnaSlovaNejsouSporna()
    {
        var a = Vysledek("a", "Jan Vymyšlený bydlí v Horní Lhotě 12.");
        var b = Vysledek("b", "Jan Vymyšlený bydlí v Horni Lhotě 12");
        Assert.Equal(1, Porovnani.OznacSporna(a, b));
        Assert.Equal(new[] { "Horní" }, a.Slova.Where(s => s.Sporne).Select(s => s.Text));
    }

    [Fact]
    public void ChybejiciSlovoVDruhemJeSporne()
    {
        var a = Vysledek("a", "plocha 2,1 m", "výška 1,35 m");
        var b = Vysledek("b", "plocha 21 m", "výška m");
        Porovnani.OznacSporna(a, b);
        Assert.Equal(new[] { "2,1", "1,35" }, a.Slova.Where(s => s.Sporne).Select(s => s.Text));
    }

    [Fact]
    public void PrazdnyDruhyOznaciVse()
    {
        var a = Vysledek("a", "jedna dvě");
        Assert.Equal(2, Porovnani.OznacSporna(a, Vysledek("b")));
    }
}

public class ModelTesty
{
    [Fact]
    public void SlovoNaBodeNajdeSpravneSlovo()
    {
        var v = new VysledekOcr
        {
            Motor = "x",
            Radky = { new RadekOcr { Slova =
            {
                new SlovoOcr { Text = "první", Ramecek = new Obdelnik(10, 10, 50, 20) },
                new SlovoOcr { Text = "druhé", Ramecek = new Obdelnik(70, 10, 50, 20) },
            } } },
        };
        Assert.Equal("druhé", v.SlovoNa(80, 15)?.Text);
        Assert.Null(v.SlovoNa(65, 15));
    }

    [Fact]
    public void OpravaMaPrednostPredTextem()
    {
        var s = new SlovoOcr { Text = "Vymyslíček", Oprava = "Vymyšlíček" };
        Assert.Equal("Vymyšlíček", s.Platny);
    }
}

public class SpisTesty
{
    private static string VymyslenaFotka(string slozka, string nazev, int sirka = 300, int vyska = 400)
    {
        using var m = new Mat(vyska, sirka, MatType.CV_8UC3, Scalar.White);
        Cv2.PutText(m, "VZOR", new Point(20, 60), HersheyFonts.HersheySimplex, 1.5, Scalar.Black, 3);
        var cesta = Path.Combine(slozka, nazev);
        Cv2.ImWrite(cesta, m);
        return cesta;
    }

    [Fact]
    public void ZalozeniCislujeSpisyPopořadě()
    {
        using var d = new DocasnaSlozka();
        var u = new Uloziste(d.Cesta);
        Assert.Equal("S-0001", u.Zaloz("vymyšlený spis A").Id);
        Assert.Equal("S-0002", u.Zaloz("vymyšlený spis B").Id);
        Assert.Equal(new[] { "S-0001", "S-0002" }, u.Spisy().Select(s => s.Id));
    }

    [Fact]
    public void ImportRadiFotkyPodleNazvuACisluje()
    {
        using var d = new DocasnaSlozka();
        var zdroj = Directory.CreateDirectory(Path.Combine(d.Cesta, "zdroj")).FullName;
        var fotky = new[] { "b.png", "a.png", "c.jpg" }.Select(n => VymyslenaFotka(zdroj, n)).ToList();

        var spis = new Uloziste(Path.Combine(d.Cesta, "Spisy")).Zaloz("vzor");
        spis.PridejFotky(fotky);

        var nacteny = Spis.Nacti(spis.Slozka);
        Assert.Equal(new[] { "a.png", "b.png", "c.jpg" }, nacteny.Strany.Select(s => s.PuvodniNazev));
        Assert.Equal(new[] { 1, 2, 3 }, nacteny.Strany.Select(s => s.Cislo));
        Assert.All(nacteny.Strany, s => Assert.True(File.Exists(nacteny.CestaFotky(s))));
        using var prvni = Cv2.ImRead(nacteny.CestaFotky(nacteny.Strany[0]));
        Assert.Equal(new Size(300, 400), prvni.Size());
    }

    [Fact]
    public void VysledekOcrSeUloziANacte()
    {
        using var d = new DocasnaSlozka();
        var spis = new Uloziste(d.Cesta).Zaloz("vzor");
        var strana = spis.PridejFotky(new[] { VymyslenaFotka(d.Cesta, "x.png") })[0];
        var v = new VysledekOcr
        {
            Motor = Motory.Paddle, Sirka = 300, Vyska = 400,
            Radky = { new RadekOcr { Slova = { new SlovoOcr { Text = "Žluťoučký", Jistota = 0.9f, Ramecek = new Obdelnik(1, 2, 3, 4) } } } },
        };
        spis.UlozOcr(strana, v);
        var n = spis.NactiOcr(strana, Motory.Paddle)!;
        Assert.Equal("Žluťoučký", n.Text);
        Assert.Equal(new Obdelnik(1, 2, 3, 4), n.Slova.Single().Ramecek);
        Assert.Null(spis.NactiOcr(strana, Motory.Tesseract));
    }
}

public class DavkaTesty
{
    private sealed class FalesnyMotor(string nazev) : IOcrMotor
    {
        public int Volani;
        public string Nazev => nazev;
        public bool ChceUpravenouFotku => true;
        public VysledekOcr Rozpoznej(string cesta)
        {
            Interlocked.Increment(ref Volani);
            Assert.True(File.Exists(cesta));
            return new VysledekOcr { Motor = nazev, Radky = { new RadekOcr { Slova = { new SlovoOcr { Text = nazev } } } } };
        }
        public void Dispose() { }
    }

    [Fact]
    public async Task DavkaZpracujeChybejiciStranyAPriOpakovaniNicNedela()
    {
        using var d = new DocasnaSlozka();
        var spis = new Uloziste(d.Cesta).Zaloz("vzor");
        using (var m = new Mat(100, 100, MatType.CV_8UC3, Scalar.White))
        {
            Cv2.ImWrite(Path.Combine(d.Cesta, "1.png"), m);
            Cv2.ImWrite(Path.Combine(d.Cesta, "2.png"), m);
        }
        spis.PridejFotky(new[] { Path.Combine(d.Cesta, "1.png"), Path.Combine(d.Cesta, "2.png") });

        var hlavni = new FalesnyMotor(Motory.Paddle);
        var kontrolni = new FalesnyMotor(Motory.Tesseract);
        await Davka.ZpracujAsync(new[] { spis }, new Func<IOcrMotor>[] { () => hlavni, () => kontrolni });
        Assert.Equal(2, hlavni.Volani);
        Assert.Equal(2, kontrolni.Volani);

        await Davka.ZpracujAsync(new[] { spis }, new Func<IOcrMotor>[] { () => hlavni, () => kontrolni });
        Assert.Equal(2, hlavni.Volani);

        var zobrazeni = Motory.ProZobrazeni(spis, spis.Strany[0])!;
        Assert.Equal(Motory.Paddle, zobrazeni.Motor);
        Assert.True(zobrazeni.Slova.Single().Sporne); // motory napsaly každý něco jiného
    }
}
