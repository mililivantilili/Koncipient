using Koncipient.Jadro;
using Xunit;

namespace Koncipient.Testy;

public class PrenosTesty
{
    private static string Soubor(string cesta, string obsah = "x")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(cesta)!);
        File.WriteAllText(cesta, obsah);
        return cesta;
    }

    [Fact]
    public void NaDiskZkopirujeExportANechaVystupyOdClauda()
    {
        using var d = new DocasnaSlozka();
        var pc = Path.Combine(d.Cesta, "ProClaude");
        Soubor(Path.Combine(pc, "S-0001", "dokumenty", "01-smlouva.md"));
        Soubor(Path.Combine(pc, "S-0001", "manifest.json"), Manifest);
        Soubor(Path.Combine(pc, "S-0001", "vystup", "odvolani.docx")); // Word se skutečnými jmény na disk nejde
        Soubor(Path.Combine(pc, "PRAVIDLA-KONCIPIENT.md"));
        var disk = Path.Combine(d.Cesta, "E");
        Soubor(Path.Combine(disk, "ProClaude", "S-0001", "vystup", "navrh.md"), "od Clauda");
        Soubor(Path.Combine(disk, "ProClaude", "S-0001", "dokumenty", "02-stary.md"));

        Assert.Equal(3, Prenos.NaDisk(pc, disk));
        Assert.True(File.Exists(Path.Combine(disk, "ProClaude", "S-0001", "dokumenty", "01-smlouva.md")));
        Assert.False(File.Exists(Path.Combine(disk, "ProClaude", "S-0001", "dokumenty", "02-stary.md")));
        Assert.False(File.Exists(Path.Combine(disk, "ProClaude", "S-0001", "vystup", "odvolani.docx")));
        Assert.Equal("od Clauda", File.ReadAllText(Path.Combine(disk, "ProClaude", "S-0001", "vystup", "navrh.md")));
    }

    private const string Manifest = """{ "dokumenty": [ { "poradi": 1, "soubor": "dokumenty/01-smlouva.md" } ] }""";

    [Fact]
    public void RucneUlozenyTextNaDiskNeodejde()
    {
        // kontrola 2026-11-06: poznamky.md s přepisem se skutečnými jmény prošla na disk
        using var d = new DocasnaSlozka();
        var pc = Path.Combine(d.Cesta, "ProClaude");
        Soubor(Path.Combine(pc, "S-0001", "manifest.json"), Manifest);
        Soubor(Path.Combine(pc, "S-0001", "dokumenty", "01-smlouva.md"));
        Soubor(Path.Combine(pc, "S-0001", "poznamky.md"), "Petr Pokusný, nar. 12. 3. 1975");
        var chyba = Assert.Throws<InvalidOperationException>(() => Prenos.NaDisk(pc, Path.Combine(d.Cesta, "E")));
        Assert.Contains("poznamky.md", chyba.Message);
    }

    [Fact]
    public void ZalohaAPrenosNesmiNaStejnyDisk()
    {
        using var d = new DocasnaSlozka();
        var uloziste = new Uloziste(Path.Combine(d.Cesta, "Spisy"));
        uloziste.Zaloz("vzor");
        var disk = Path.Combine(d.Cesta, "E");
        // disk, na kterém už je ProClaude, zálohu odmítne
        Directory.CreateDirectory(Path.Combine(disk, "ProClaude"));
        Assert.Throws<InvalidOperationException>(() => Zaloha.Proved(uloziste, disk));
        Assert.Throws<InvalidOperationException>(() => Zaloha.Proved(uloziste, Path.Combine(disk, "ProClaude")));
        // a disk se zálohou odmítne přenos
        var disk2 = Path.Combine(d.Cesta, "F");
        Zaloha.Proved(uloziste, disk2);
        var pc = Path.Combine(d.Cesta, "PC");
        Soubor(Path.Combine(pc, "S-0001", "manifest.json"), Manifest);
        Assert.Throws<InvalidOperationException>(() => Prenos.NaDisk(pc, disk2));
    }

    [Theory]
    [InlineData("S-0001/fotky/0001.jpg")]
    [InlineData("S-0001/klic.json")]
    [InlineData("S-0001/spis.json")]
    [InlineData("S-0001/dokumenty/sken.pdf")]
    public void FotkaNeboKlicVExportuZastaviKopirovani(string spatny)
    {
        using var d = new DocasnaSlozka();
        var pc = Path.Combine(d.Cesta, "ProClaude");
        Soubor(Path.Combine(pc, "S-0001", "manifest.json"));
        Soubor(Path.Combine(pc, spatny));
        var disk = Path.Combine(d.Cesta, "E");
        var chyba = Assert.Throws<InvalidOperationException>(() => Prenos.NaDisk(pc, disk));
        Assert.Contains("nic jsem nezkopíroval", chyba.Message);
        Assert.False(Directory.Exists(Path.Combine(disk, "ProClaude")));
    }

    [Fact]
    public void ZDiskuNactePouzeNoveVystupy()
    {
        using var d = new DocasnaSlozka();
        var pc = Path.Combine(d.Cesta, "ProClaude");
        var disk = Path.Combine(d.Cesta, "E");
        Soubor(Path.Combine(disk, "ProClaude", "S-0002", "vystup", "zaloba.md"), "návrh");
        Soubor(Path.Combine(disk, "ProClaude", "S-0002", "vystup", "obrazek.png"));
        Soubor(Path.Combine(disk, "ProClaude", "S-0002", "dokumenty", "01-x.md"));

        var nove = Prenos.ZDisku(disk, pc);
        var v = Assert.Single(nove);
        Assert.Equal("S-0002", v.Spis);
        Assert.Equal("návrh", File.ReadAllText(v.Soubor));
        Assert.Empty(Prenos.ZDisku(disk, pc)); // podruhé už nic nového
    }
}
