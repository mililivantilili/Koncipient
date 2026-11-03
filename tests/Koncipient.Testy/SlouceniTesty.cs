using Koncipient.Jadro;
using Koncipient.Ocr;
using OpenCvSharp;
using Xunit;

namespace Koncipient.Testy;

public class SlouceniTesty
{
    private static VysledekOcr Text(string motor, string text) => new()
    {
        Motor = motor,
        Radky = text.Split('\n').Select(r => new RadekOcr
        {
            Slova = r.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => new SlovoOcr { Text = s }).ToList(),
        }).ToList(),
    };

    // bez slovníku: rozhoduje hlavní motor a čísla
    private static readonly Slouceni Bez = new(Path.Combine(Path.GetTempPath(), "neexistuje-" + Guid.NewGuid()));

    [Fact]
    public void ParagrafACisloZKontrolnihoTextZHlavniho()
    {
        var w = Text(Motory.Windows, "ve smyslu S 9 odst. 4 lze stavbu\nplocha 2.1 m");
        var p = Text(Motory.Paddle, "ve smyslu § 9 odst. 4 Ize stavbu\nplocha 2,1 m");
        var s = Bez.Sluc(w, p);
        Assert.Equal("ve smyslu § 9 odst. 4 lze stavbu\nplocha 2,1 m", s.Text);
        Assert.Equal(2, s.Radky.Count);
    }

    [Fact]
    public void UtrzkyZOkrajeSeNevkladajiAleEmailAno()
    {
        var w = Text(Motory.Windows, "Smlouva byla podepsána.\nKontakt: Tel.");
        var p = Text(Motory.Paddle, "PEN OST Smlouva byla EBNI podepsána.\nKontakt: vymysleny@example.cz Tel.");
        Assert.Equal("Smlouva byla podepsána.\nKontakt: vymysleny@example.cz Tel.", Bez.Sluc(w, p).Text);
    }

    [Fact]
    public async Task DavkaSlouciAZobraziSloucenyText()
    {
        using var d = new DocasnaSlozka();
        var spis = new Uloziste(d.Cesta).Zaloz("vzor");
        using (var m = new Mat(50, 50, MatType.CV_8UC3, Scalar.White)) Cv2.ImWrite(Path.Combine(d.Cesta, "1.png"), m);
        var strana = spis.PridejFotky(new[] { Path.Combine(d.Cesta, "1.png") })[0];
        spis.UlozOcr(strana, Text(Motory.Windows, "dle S 9"));
        spis.UlozOcr(strana, Text(Motory.Paddle, "dle § 9"));

        await Davka.ZpracujAsync(new[] { spis }, Array.Empty<Func<IOcrMotor>>());

        var v = Motory.ProZobrazeni(spis, strana)!;
        Assert.Equal(Slouceni.Nazev, v.Motor);
        Assert.Equal("dle § 9", v.Text);
    }
}
