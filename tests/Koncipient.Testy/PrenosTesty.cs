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
        Soubor(Path.Combine(pc, "S-0001", "manifest.json"));
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
