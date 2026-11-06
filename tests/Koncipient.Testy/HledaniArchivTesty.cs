using Koncipient.Jadro;
using Koncipient.Jadro.Anonymizace;
using Koncipient.Ocr;
using OpenCvSharp;
using UglyToad.PdfPig;
using Xunit;

namespace Koncipient.Testy;

public class HledaniArchivTesty
{
    private static (Spis Spis, DocasnaSlozka Slozka) SpisSDokumenty(params (string Dokument, string Text)[] strany)
    {
        var d = new DocasnaSlozka();
        var spis = new Uloziste(Path.Combine(d.Cesta, "Spisy")).Zaloz("vzor");
        int i = 0;
        foreach (var (dokument, text) in strany)
        {
            var cesta = Path.Combine(d.Cesta, $"{++i}.png");
            using (var m = new Mat(1100, 800, MatType.CV_8UC3, Scalar.White)) Cv2.ImWrite(cesta, m);
            var strana = spis.PridejFotky(new[] { cesta }, dokument)[0];
            // vymyšlený přepis s polohami slov
            var radky = text.Split('\n').Select((r, y) => new RadekOcr
            {
                Slova = r.Split(' ').Select((s, x) => new SlovoOcr { Text = s, Ramecek = new Obdelnik(60 + x * 120, 80 + y * 40, 100, 24) }).ToList(),
            }).ToList();
            spis.UlozOcr(strana, new VysledekOcr { Motor = Motory.Paddle, Sirka = 800, Vyska = 1100, Radky = radky });
            strana.Zkontrolovano = true;
        }
        spis.Uloz();
        return (spis, d);
    }

    [Fact]
    public void HledaniBezDiakritikyAPresRadek()
    {
        var (spis, d) = SpisSDokumenty(("Smlouva", "Kupní smlouva byla\nuzavřena dne 1. 1. 2020"), ("Dopis", "Ve smlouvě je chyba"));
        using var _ = d;
        var smlouva = Hledani.Najdi(spis, "smlouv");
        Assert.Equal(2, smlouva.Count);
        Assert.Equal(2, spis.Poradi(smlouva[1].Strana));
        var fraze = Assert.Single(Hledani.Najdi(spis, "byla uzavrena"));   // přes konec řádku, bez diakritiky
        Assert.Equal("byla uzavřena", fraze.Text);
        Assert.Empty(Hledani.Najdi(spis, "   "));
    }

    [Fact]
    public void ProhledatelnePdfMaTextAZalozky()
    {
        var (spis, d) = SpisSDokumenty(("Smlouva", "Kupní smlouva Pokusný"), ("Dopis", "Ve smlouvě je chyba"));
        using var _ = d;
        var cil = Path.Combine(d.Cesta, "spis.pdf");
        Assert.Equal(2, ArchivPdf.Uloz(spis, cil));
        using var pdf = PdfDocument.Open(cil);
        Assert.Equal(2, pdf.NumberOfPages);
        Assert.Contains("Pokusný", pdf.GetPage(1).Text);
        Assert.True(pdf.TryGetBookmarks(out var zalozky));
        Assert.Equal(new[] { "Smlouva", "Dopis" }, zalozky.Roots.Select(z => z.Title));

        // jen vybraný dokument
        var jen = Path.Combine(d.Cesta, "dopis.pdf");
        Assert.Equal(1, ArchivPdf.Uloz(spis, jen, new[] { spis.Dokumenty[1].Id }));
    }

    [Fact]
    public void ExportJenVybranychDokumentu()
    {
        var (spis, d) = SpisSDokumenty(("Smlouva", "Kupní smlouva"), ("Dopis", "Ve smlouvě je chyba"));
        using var _ = d;
        spis.Strany[0].Zkontrolovano = false; // nevybraný dokument nemusí být zkontrolovaný
        var klic = Anonymizator.NactiKlic(spis);
        var dopis = spis.Dokumenty[1];
        var vysledek = Export.Proved(spis, klic, Path.Combine(d.Cesta, "ProClaude"), new[] { dopis.Id });
        Assert.True(vysledek.Uspech, string.Join("; ", vysledek.Problemy));
        var soubory = Directory.GetFiles(Path.Combine(vysledek.Slozka, "dokumenty")).Select(Path.GetFileName).ToList();
        Assert.Equal(new[] { "02-dopis.md" }, soubory);   // stálé číslo podle pořadí ve spisu
        Assert.Contains("\"cely_spis\": false", File.ReadAllText(Path.Combine(vysledek.Slozka, "manifest.json")));
        Assert.NotNull(Spis.Nacti(spis.Slozka).Dokumenty[1].Exportovano);
        Assert.Null(Spis.Nacti(spis.Slozka).Dokumenty[0].Exportovano);

        // další export jen jednoho (jiného) dokumentu přidává, dřívější dokument zůstane
        spis.Strany[0].Zkontrolovano = true;
        spis.Uloz();
        var druhy = Export.Proved(spis, klic, Path.Combine(d.Cesta, "ProClaude"), new[] { spis.Dokumenty[0].Id });
        Assert.True(druhy.Uspech);
        Assert.Equal(new[] { "01-smlouva.md", "02-dopis.md" },
            Directory.GetFiles(Path.Combine(druhy.Slozka, "dokumenty")).Select(Path.GetFileName).OrderBy(x => x));
        var manifest = File.ReadAllText(Path.Combine(druhy.Slozka, "manifest.json"));
        Assert.Contains("\"cely_spis\": true", manifest);
        spis.Strany[0].Zkontrolovano = false;
        spis.Uloz();

        // celý spis neprojde, dokud není zkontrolovaná i první strana
        Assert.False(Export.Proved(spis, klic, Path.Combine(d.Cesta, "ProClaude")).Uspech);
    }
}
