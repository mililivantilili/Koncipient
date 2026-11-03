using Koncipient.Jadro;
using Koncipient.Ocr;
using Xunit;

namespace Koncipient.Testy;

public class UpravyTesty
{
    private static VysledekOcr Strana() => new()
    {
        Motor = Motory.Windows,
        Radky =
        {
            Radek(100, "Smlouva o dílo"),
            Radek(200, "uzavřená mezi stranami"),
            Radek(300, "Vymyšlený a Smyšlenka"),
        },
    };

    private static RadekOcr Radek(int y, string text)
    {
        int x = 10;
        var slova = text.Split(' ').Select(s => { var o = new SlovoOcr { Text = s, Ramecek = new Obdelnik(x, y, s.Length * 10, 20) }; x += s.Length * 10 + 10; return o; }).ToList();
        return new RadekOcr { Slova = slova, Ramecek = new Obdelnik(10, y, x - 20, 20) };
    }

    private static string[] Radky(VysledekOcr v) => v.Text.Split('\n');

    [Fact]
    public void VlozZaSlovo()
    {
        var v = Strana();
        Assert.Equal(2, UpravyStrany.VlozZa(v, 1, "č. 12"));
        Assert.Equal("Smlouva o č. 12 dílo", Radky(v)[0]);
        var doplnene = v.Slova.ElementAt(2);
        Assert.Equal("", doplnene.Text);
        Assert.Equal("č.", doplnene.Platny);
    }

    [Fact]
    public void VlozRadekPodlePolohy()
    {
        var v = Strana();
        int prvni = UpravyStrany.VlozRadek(v, new Obdelnik(10, 250, 200, 20), "dne 1. 1. 2026");
        Assert.Equal(new[] { "Smlouva o dílo", "uzavřená mezi stranami", "dne 1. 1. 2026", "Vymyšlený a Smyšlenka" }, Radky(v));
        Assert.Equal(6, prvni);
        Assert.Equal("dne", v.SlovoNa(15, 255)?.Platny);
    }

    [Fact]
    public void SmazaniZalomeniSpojeniAPosun()
    {
        var v = Strana();
        UpravyStrany.Smaz(v, 1, 2);                  // „o dílo“
        Assert.Equal("Smlouva", Radky(v)[0]);
        UpravyStrany.SpojSDalsim(v, 0);
        Assert.Equal("Smlouva uzavřená mezi stranami", Radky(v)[0]);
        UpravyStrany.Zalom(v, 2);                    // před „mezi“
        Assert.Equal(new[] { "Smlouva uzavřená", "mezi stranami", "Vymyšlený a Smyšlenka" }, Radky(v));
        UpravyStrany.Posun(v, 4, -1);                // řádek s „Vymyšlený“ nahoru
        Assert.Equal(new[] { "Smlouva uzavřená", "Vymyšlený a Smyšlenka", "mezi stranami" }, Radky(v));
        UpravyStrany.Smaz(v, 0, 1);
        Assert.Equal(2, v.Radky.Count);              // prázdný řádek zmizel
    }
}
