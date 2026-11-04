using Koncipient.Jadro;
using Koncipient.Jadro.Anonymizace;
using Koncipient.Ocr;
using OpenCvSharp;
using Xunit;

namespace Koncipient.Testy;

public class ZaznamTesty
{
    [Fact]
    public void ExportAFlashDiskSeZapisouBezUdaju()
    {
        using var d = new DocasnaSlozka();
        var spis = new Uloziste(Path.Combine(d.Cesta, "Spisy")).Zaloz("vzor");
        var cesta = Path.Combine(d.Cesta, "1.png");
        using (var m = new Mat(100, 100, MatType.CV_8UC3, Scalar.White)) Cv2.ImWrite(cesta, m);
        var strana = spis.PridejFotky(new[] { cesta }, "Smlouva Petr Pokusný")[0];
        spis.UlozOcr(strana, new VysledekOcr { Motor = Motory.Paddle, Radky = Vzor.Ocr("Kupující Ing. Petr Pokusný, nar. 12. 3. 1975.").Radky });
        strana.Zkontrolovano = true;
        spis.Uloz();
        var klic = Anonymizator.NactiKlic(spis);
        Anonymizator.AktualizujKlic(spis, klic);
        Anonymizator.UlozKlic(spis, klic);

        var pc = Path.Combine(d.Cesta, "ProClaude");
        Assert.True(Export.Proved(spis, klic, pc).Uspech);
        Prenos.NaDisk(pc, Path.Combine(d.Cesta, "E"), new[] { spis });

        var zaznamy = ZaznamyExportu.Nacti(spis);
        Assert.Equal(new[] { "export", "flash disk" }, zaznamy.Select(z => z.Akce));
        var export = zaznamy[0];
        Assert.True(export.CelySpis && export.StranyZkontrolovane && export.KontrolaProsla);
        Assert.Equal(1, export.Stran);
        Assert.Equal(1, export.Nahrazeno["osoba"]);
        Assert.False(string.IsNullOrEmpty(export.Kdo));

        // v záznamu ani přehledu nejsou skutečné údaje ani název dokumentu se jménem
        var vse = File.ReadAllText(Path.Combine(spis.Slozka, "zaznam-exportu.json")) + ZaznamyExportu.Prehled(spis);
        Assert.DoesNotContain("Pokusný", vse);
        Assert.DoesNotContain("1975", vse);
        Assert.Contains("Export pro Clauda", ZaznamyExportu.Prehled(spis));
        // záznam nepatří do ProClaude
        Assert.Empty(Directory.GetFiles(pc, "zaznam*", SearchOption.AllDirectories));
    }
}

public class UkolTesty
{
    [Fact]
    public void UkolSeAnonymizujeAUloziDoExportu()
    {
        using var d = new DocasnaSlozka();
        var spis = new Uloziste(Path.Combine(d.Cesta, "Spisy")).Zaloz("vzor");
        var cesta = Path.Combine(d.Cesta, "1.png");
        using (var m = new Mat(100, 100, MatType.CV_8UC3, Scalar.White)) Cv2.ImWrite(cesta, m);
        var strana = spis.PridejFotky(new[] { cesta }, "Rozsudek")[0];
        spis.UlozOcr(strana, new VysledekOcr { Motor = Motory.Paddle, Radky = Vzor.Ocr("Žalobce Ing. Petr Pokusný podal žalobu.").Radky });
        strana.Zkontrolovano = true;
        spis.Uloz();
        var klic = Anonymizator.NactiKlic(spis);
        Anonymizator.AktualizujKlic(spis, klic);

        var pc = Path.Combine(d.Cesta, "ProClaude");
        var v = Export.Proved(spis, klic, pc, ukol: "Navrhni odvolání pro Petra Pokusného.");
        Assert.True(v.Uspech, string.Join("; ", v.Problemy));
        var ukol = File.ReadAllText(Path.Combine(v.Slozka, "UKOL.md"));
        Assert.Contains("Navrhni odvolání pro", ukol);
        Assert.DoesNotContain("Pokusn", ukol);
        Assert.Contains("\"ukol\": \"UKOL.md\"", File.ReadAllText(Path.Combine(v.Slozka, "manifest.json")));
        Assert.True(ZaznamyExportu.Nacti(spis).Last().SUkolem);

        // bez úkolu soubor zmizí (export vymění složku celou)
        Assert.True(Export.Proved(spis, klic, pc).Uspech);
        Assert.False(File.Exists(Path.Combine(v.Slozka, "UKOL.md")));
    }
}
