using Koncipient.Jadro;
using OpenCvSharp;
using UglyToad.PdfPig;
using Xunit;

namespace Koncipient.Testy;

public class UpravaProTiskTesty
{
    /// <summary>Vymyšlená fotka: hnědý stůl, šikmo položený list s textem, stín zleva doprava.</summary>
    private static Mat Fotka()
    {
        using var list = new Mat(1414, 1000, MatType.CV_8UC3, new Scalar(235, 240, 245));
        for (int y = 120; y < 1300; y += 45)
            Cv2.PutText(list, "Kupni smlouva c. 1 uzavrena mezi stranami", new Point(70, y), HersheyFonts.HersheySimplex, 1.0, new Scalar(30, 30, 30), 2);
        var fotka = new Mat(1600, 1300, MatType.CV_8UC3, new Scalar(40, 70, 120));
        var zdroj = new[] { new Point2f(0, 0), new Point2f(999, 0), new Point2f(999, 1413), new Point2f(0, 1413) };
        var cil = new[] { new Point2f(180, 140), new Point2f(1120, 200), new Point2f(1080, 1480), new Point2f(140, 1420) };
        using var m = Cv2.GetPerspectiveTransform(zdroj, cil);
        Cv2.WarpPerspective(list, fotka, m, fotka.Size(), InterpolationFlags.Linear, BorderTypes.Transparent);
        // stín: levá část o třetinu tmavší
        for (int x = 0; x < fotka.Width; x++)
        {
            double f = 0.65 + 0.35 * x / fotka.Width;
            using var sloupec = fotka.ColRange(x, x + 1);
            sloupec.ConvertTo(sloupec, -1, f, 0);
        }
        return fotka;
    }

    [Fact]
    public void NajdePapirAOrizneHo()
    {
        using var fotka = Fotka();
        var rohy = UpravaProTisk.NajdiRohyPapiru(fotka)!;
        Assert.NotNull(rohy);
        Assert.InRange(rohy[0].X, 150, 210); Assert.InRange(rohy[0].Y, 110, 170);   // levý horní
        Assert.InRange(rohy[2].X, 1050, 1110); Assert.InRange(rohy[2].Y, 1450, 1510); // pravý dolní
        using var orez = UpravaProTisk.OrizniPapir(fotka);
        double pomer = orez.Height / (double)orez.Width;
        Assert.InRange(pomer, 1.30, 1.50); // A4 na výšku
    }

    [Fact]
    public void CernobileMaBilyPapirICernyTextIVeStinu()
    {
        using var fotka = Fotka();
        using var bw = UpravaProTisk.Uprav(fotka);
        Assert.Equal(1, bw.Channels());
        double cernych = 1 - Cv2.CountNonZero(bw) / (double)(bw.Width * bw.Height);
        Assert.InRange(cernych, 0.02, 0.30);   // text je, ale papír není zašedlý
        // ve stínu (levý okraj papíru bez textu) je bílo
        using var levyOkraj = new Mat(bw, new Rect(5, bw.Height / 3, 40, bw.Height / 3));
        Assert.True(Cv2.CountNonZero(levyOkraj) > 0.97 * levyOkraj.Width * levyOkraj.Height);
        // řádek textu má černé body i vlevo ve stínu
        using var radek = new Mat(bw, new Rect(60, 90, 300, 60));
        Assert.True(Cv2.CountNonZero(radek) < 0.95 * radek.Width * radek.Height);
    }

    [Fact]
    public void FotkaBezStoluSeNeorizne()
    {
        using var list = new Mat(1400, 1000, MatType.CV_8UC3, new Scalar(240, 240, 240));
        Cv2.PutText(list, "Text", new Point(100, 200), HersheyFonts.HersheySimplex, 2, Scalar.Black, 3);
        Assert.Null(UpravaProTisk.NajdiRohyPapiru(list));
        using var orez = UpravaProTisk.OrizniPapir(list);
        Assert.Equal(list.Size(), orez.Size());
    }

    [Fact]
    public void PdfKTiskuMaStranyA4()
    {
        using var d = new DocasnaSlozka();
        var spis = new Uloziste(Path.Combine(d.Cesta, "Spisy")).Zaloz("vzor");
        var cesta = Path.Combine(d.Cesta, "1.jpg");
        using (var f = Fotka()) Cv2.ImWrite(cesta, f);
        spis.PridejFotky(new[] { cesta, cesta.Replace("1.jpg", "1.jpg") });
        var pdf = Path.Combine(d.Cesta, "tisk.pdf");
        Assert.Equal(2, UpravaProTisk.UlozPdf(spis, pdf));
        using var dokument = PdfDocument.Open(pdf);
        Assert.Equal(2, dokument.NumberOfPages);
        Assert.InRange(dokument.GetPage(1).Width, 594, 596);
        Assert.InRange(dokument.GetPage(1).Height, 841, 843);
    }
}
