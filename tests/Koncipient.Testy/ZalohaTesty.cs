using Koncipient.Jadro;
using OpenCvSharp;
using Xunit;

namespace Koncipient.Testy;

public class ZalohaTesty
{
    [Fact]
    public void ZalohaZkopirujeSpisyAZapamatujeSiCas()
    {
        using var d = new DocasnaSlozka();
        var uloziste = new Uloziste(Path.Combine(d.Cesta, "Spisy"));
        var spis = uloziste.Zaloz("vzor");
        var cesta = Path.Combine(d.Cesta, "1.png");
        using (var m = new Mat(50, 50, MatType.CV_8UC3, Scalar.White)) Cv2.ImWrite(cesta, m);
        spis.PridejFotky(new[] { cesta });
        Assert.True(Zaloha.Pripomenout(uloziste));

        var (cil, souboru) = Zaloha.Proved(uloziste, Path.Combine(d.Cesta, "E"));
        Assert.True(souboru >= 2);
        Assert.True(File.Exists(Path.Combine(cil, spis.Id, "spis.json")));
        Assert.True(File.Exists(Path.Combine(cil, spis.Id, "fotky", "0001.jpg")));
        Assert.NotNull(Zaloha.Posledni(uloziste));
        Assert.False(Zaloha.Pripomenout(uloziste));

        // do složky se spisy zálohovat nejde
        Assert.Throws<InvalidOperationException>(() => Zaloha.Proved(uloziste, Path.Combine(d.Cesta, "Spisy", "x")));
    }
}
