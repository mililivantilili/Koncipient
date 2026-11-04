using Koncipient.Jadro;
using Koncipient.Jadro.Anonymizace;
using Koncipient.Ocr;
using Xunit;

namespace Koncipient.Testy;

/// <summary>Import PDF (vzory/pdf: vymyšlená žaloba s textem a naskenovaná stránka bez textu).</summary>
public class PdfTesty
{
    private static string Vzor(string nazev)
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "vzory", "pdf"))) d = d.Parent;
        return Path.Combine(d!.FullName, "vzory", "pdf", nazev);
    }

    [Fact]
    public void PdfSTextemMaPresnyPrepisBezOcr()
    {
        using var d = new DocasnaSlozka();
        var spis = new Uloziste(d.Cesta).Zaloz("vzor");
        var strany = spis.PridejPdf(Vzor("s-textem.pdf"));
        var strana = Assert.Single(strany);
        Assert.Equal("s-textem", spis.DokumentStrany(strana)!.Nazev);
        Assert.True(File.Exists(spis.CestaFotky(strana)));
        Assert.True(strana.Narovnano);

        var v = Motory.ProZobrazeni(spis, strana)!;
        Assert.Equal(Motory.PdfText, v.Motor);
        Assert.StartsWith("Okresnímu soudu ve Vymyšlenicích", v.Text);
        Assert.Contains("Žalobce: Ing. Petr Pokusný, nar. 12. 3. 1975", v.Text);

        // polohy slov sedí na vykreslenou stránku: první slovo je vlevo nahoře
        var prvni = v.Slova.First();
        Assert.InRange(prvni.Ramecek.X, 50, v.Sirka / 3);
        Assert.InRange(prvni.Ramecek.Y, 50, v.Vyska / 4);
        Assert.True(v.Radky.Count >= 10);

        // údaje se najdou stejně jako z OCR
        var klic = Anonymizator.NactiKlic(spis);
        Anonymizator.AktualizujKlic(spis, klic);
        Assert.DoesNotContain("Pokusný", Anonymizator.AnonymizovanyText(v, klic));
    }

    private sealed class Pocitadlo(string nazev) : IOcrMotor
    {
        public int Volani;
        public string Nazev => nazev;
        public bool ChceUpravenouFotku => false;
        public VysledekOcr Rozpoznej(string cesta) { Volani++; return new VysledekOcr { Motor = nazev }; }
        public void Dispose() { }
    }

    [Fact]
    public async Task DavkaPrepiseJenSkenovaneStranky()
    {
        using var d = new DocasnaSlozka();
        var spis = new Uloziste(d.Cesta).Zaloz("vzor");
        spis.PridejPdf(Vzor("s-textem.pdf"));
        var sken = Assert.Single(spis.PridejPdf(Vzor("sken.pdf"), "Sken"));
        Assert.False(spis.MaOcr(sken, Motory.PdfText));
        Assert.False(sken.Narovnano);

        var motor = new Pocitadlo(Motory.Paddle);
        Assert.Empty(await Davka.ZpracujAsync(new[] { spis }, new Func<IOcrMotor>[] { () => motor }));
        Assert.Equal(1, motor.Volani);
        Assert.Equal(2, spis.Dokumenty.Count);
    }

    [Fact]
    public void PdfVeSlozceJeDokument()
    {
        using var d = new DocasnaSlozka();
        var zdroj = Path.Combine(d.Cesta, "Archiv", "Datová schránka");
        Directory.CreateDirectory(zdroj);
        File.Copy(Vzor("s-textem.pdf"), Path.Combine(zdroj, "Žaloba.pdf"));
        var spis = new Uloziste(Path.Combine(d.Cesta, "Spisy")).Zaloz("vzor");
        spis.PridejSlozku(Path.Combine(d.Cesta, "Archiv"));
        Assert.Equal("Datová schránka / Žaloba", Assert.Single(spis.Dokumenty).Nazev);
    }
}
