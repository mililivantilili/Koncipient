using Koncipient.Jadro;
using Koncipient.Jadro.Anonymizace;
using Koncipient.Ocr;
using OpenCvSharp;
using Xunit;

namespace Koncipient.Testy;

public class DokumentyTesty
{
    private static string Fotka(string slozka, string nazev, bool cerna = false)
    {
        Directory.CreateDirectory(slozka);
        using var m = new Mat(80, 60, MatType.CV_8UC3, cerna ? new Scalar(8, 8, 8) : Scalar.White);
        if (!cerna) Cv2.PutText(m, "VZOR", new Point(5, 40), HersheyFonts.HersheySimplex, 0.6, Scalar.Black, 2);
        var cesta = Path.Combine(slozka, nazev);
        Cv2.ImWrite(cesta, m);
        return cesta;
    }

    private static string Popis(Spis spis) => string.Join(" | ", spis.StranyPoDokumentech()
        .Select(x => $"{x.Dokument.Nazev}: {string.Join(",", x.Strany.Select(s => s.PuvodniNazev))}"));

    [Fact]
    public void CernaFotkaOddeluje()
    {
        using var d = new DocasnaSlozka();
        var z = Path.Combine(d.Cesta, "zdroj");
        var fotky = new[] { Fotka(z, "IMG_1.jpg"), Fotka(z, "IMG_2.jpg"), Fotka(z, "IMG_3.jpg", cerna: true),
                            Fotka(z, "IMG_10.jpg"), Fotka(z, "IMG_11.jpg", cerna: true) };
        var spis = new Uloziste(Path.Combine(d.Cesta, "Spisy")).Zaloz("vzor");
        spis.PridejFotky(fotky);
        // IMG_10 až za IMG_3 (přirozené řazení), černé fotky se neuloží, prázdný dokument na konci nevznikne
        Assert.Equal("Dokument 1: IMG_1.jpg,IMG_2.jpg | Dokument 2: IMG_10.jpg", Popis(spis));
        Assert.All(spis.Dokumenty, x => Assert.True(x.NavrhnoutNazev));
    }

    [Fact]
    public void SlozkaSPodslozkami()
    {
        using var d = new DocasnaSlozka();
        var koren = Path.Combine(d.Cesta, "Archiv Vymyšlený");
        Fotka(koren, "obalka.jpg");
        Fotka(Path.Combine(koren, "2 Rozsudek"), "a.jpg");
        Fotka(Path.Combine(koren, "1 Žaloba"), "b.jpg");
        Fotka(Path.Combine(koren, "1 Žaloba"), "c.jpg");
        Fotka(Path.Combine(koren, "1 Žaloba", "Přílohy"), "d.jpg");
        var spis = new Uloziste(Path.Combine(d.Cesta, "Spisy")).Zaloz("vzor");
        spis.PridejSlozku(koren);
        Assert.Equal("Archiv Vymyšlený: obalka.jpg | 1 Žaloba: b.jpg,c.jpg | 1 Žaloba / Přílohy: d.jpg | 2 Rozsudek: a.jpg", Popis(Spis.Nacti(spis.Slozka)));
    }

    [Fact]
    public void PresunyRozdeleniASpojeni()
    {
        using var d = new DocasnaSlozka();
        var z = Path.Combine(d.Cesta, "zdroj");
        var spis = new Uloziste(Path.Combine(d.Cesta, "Spisy")).Zaloz("vzor");
        spis.PridejFotky(new[] { Fotka(z, "1.jpg"), Fotka(z, "2.jpg"), Fotka(z, "3.jpg") }, "A");
        spis.PridejFotky(new[] { Fotka(z, "4.jpg") }, "B");
        Strana S(string n) => spis.Strany.Single(s => s.PuvodniNazev == n);

        spis.ZacniDokument(S("2.jpg"), "C");
        Assert.Equal("A: 1.jpg | C: 2.jpg,3.jpg | B: 4.jpg", Popis(spis));
        spis.PosunStranu(S("3.jpg"), 1);            // přes hranici: do dokumentu B
        Assert.Equal("A: 1.jpg | C: 2.jpg | B: 3.jpg,4.jpg", Popis(spis));
        spis.PosunStranu(S("4.jpg"), -1);           // uvnitř dokumentu: prohození
        Assert.Equal("A: 1.jpg | C: 2.jpg | B: 4.jpg,3.jpg", Popis(spis));
        spis.PresunDoDokumentu(S("1.jpg"), spis.Dokumenty.Single(x => x.Nazev == "B").Id);
        Assert.Equal("C: 2.jpg | B: 4.jpg,3.jpg,1.jpg", Popis(spis));   // prázdný A zmizel
        spis.SpojSPredchozim(S("4.jpg"));
        Assert.Equal("C: 2.jpg,4.jpg,3.jpg,1.jpg", Popis(Spis.Nacti(spis.Slozka)));
        Assert.Equal(4, spis.Poradi(S("1.jpg")));
    }

    [Theory]
    [InlineData("Advokátní kancelář\nKUPNÍ SMLOUVA\nuzavřená podle § 2079", "Kupní smlouva")]
    [InlineData("12 C 345/2026-87\nROZSUDEK\nJMÉNEM REPUBLIKY", "Rozsudek")]
    [InlineData("Vážený pane,\nzasíláme Vám vyúčtování za rok 2026", "Zasíláme Vám vyúčtování za rok 2026")]
    public void NavrhNazvu(string text, string ocekavany)
    {
        var v = new VysledekOcr
        {
            Motor = "x",
            Radky = text.Split('\n').Select(r => new RadekOcr { Slova = r.Split(' ').Select(s => new SlovoOcr { Text = s }).ToList() }).ToList(),
        };
        var navrh = Spis.NavrhniNazev(v)!;
        Assert.Equal(ocekavany, char.ToUpper(navrh[0]) + navrh[1..]);
    }

    [Fact]
    public void NazevDokumentuSeJmenemSeAnonymizuje()
    {
        using var d = new DocasnaSlozka();
        var z = Path.Combine(d.Cesta, "zdroj");
        var spis = new Uloziste(Path.Combine(d.Cesta, "Spisy")).Zaloz("vzor");
        var strana = spis.PridejFotky(new[] { Fotka(z, "1.jpg") }, "Smlouva Jan Vymyšlený")[0];
        spis.UlozOcr(strana, Vzor.Ocr("Kupující Jan Vymyšlený podepsal."));
        strana.Zkontrolovano = true;
        spis.Uloz();
        var klic = new Klic { Styl = StylPseudonymu.Kody };
        Anonymizator.AktualizujKlic(spis, klic);

        var proClaude = Path.Combine(d.Cesta, "ProClaude");
        var vysledek = Export.Proved(spis, klic, proClaude);
        Assert.True(vysledek.Uspech, string.Join("\n", vysledek.Problemy));
        var soubor = Directory.GetFiles(Path.Combine(proClaude, "S-0001", "dokumenty")).Single();
        Assert.Equal("01-smlouva-osoba-001.md", Path.GetFileName(soubor));
        var obsah = File.ReadAllText(soubor);
        Assert.StartsWith("# Smlouva [OSOBA_001]", obsah);
        Assert.Contains("## Strana 1", obsah);
        Assert.DoesNotContain("Vymyšlen", string.Concat(Directory.GetFiles(Path.Combine(proClaude, "S-0001"), "*", SearchOption.AllDirectories).Select(File.ReadAllText)));
    }
}
