using Koncipient.Jadro;
using Koncipient.Ocr;
using OpenCvSharp;
using Xunit;

namespace Koncipient.Testy;

public class PrehledDatTesty
{
    [Fact]
    public void DataSeradiSNapovedouAPlatnosti()
    {
        using var d = new DocasnaSlozka();
        var spis = new Uloziste(Path.Combine(d.Cesta, "Spisy")).Zaloz("vzor");
        var strany = new List<Strana>();
        foreach (var (i, text) in new[]
                 {
                     (1, "Rozsudek ze dne 15. 3. 2024 byl doručen dne 2.4.2024.\nŽalovaný nar. 12. 3. 1975."),
                     (2, "Jednání se koná 7. května 2024 v 9 hodin.\nČástka je splatná do 31. 12. 2023, chybně 31. 2. 2024 a 1.1.24."),
                 })
        {
            var cesta = Path.Combine(d.Cesta, $"{i}.png");
            using (var m = new Mat(100, 100, MatType.CV_8UC3, Scalar.White)) Cv2.ImWrite(cesta, m);
            var s = spis.PridejFotky(new[] { cesta })[0];
            spis.UlozOcr(s, new VysledekOcr { Motor = Motory.Paddle, Radky = Vzor.Ocr(text).Radky });
        }
        var data = PrehledDat.Najdi(spis);
        Assert.Equal(new[] { new DateOnly(1975, 3, 12), new DateOnly(2023, 12, 31), new DateOnly(2024, 3, 15), new DateOnly(2024, 4, 2), new DateOnly(2024, 5, 7) },
            data.Select(x => x.Datum));
        Assert.Equal(new[] { "narození", "splatnost", "ze dne", "doručení", "jednání" }, data.Select(x => x.Druh));
        var jednani = data.Single(x => x.Druh == "jednání");
        Assert.Equal("7. května 2024", jednani.Text);
        Assert.Equal(2, spis.Poradi(jednani.Strana));
        Assert.Contains("15. 3. 2024", PrehledDat.Text(spis, data));
    }
}
