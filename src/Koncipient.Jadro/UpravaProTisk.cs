using OpenCvSharp;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace Koncipient.Jadro;

/// <summary>
/// Úprava nafocených stran k tisku: najde list papíru, ořízne ho a narovná perspektivu
/// a převede na čistou černobílou (bílý papír, černý text, bez stínů a šedého pozadí).
///
/// Černobílá vychází z Matějova nápadu (práh počítaný po okolí velikosti písmene), jen ve dvou
/// krocích, které se ustálily jako nejspolehlivější pro fotky z mobilu:
/// 1. Odhad pozadí: z obrázku se morfologickým uzavřením „smažou“ tmavá písmena, zůstane jen
///    papír se stíny a přechody osvětlení. Vydělením tímto pozadím je papír všude stejně bílý.
/// 2. Sauvolův lokální práh (1997): v okně zhruba o velikosti několika písmen se z průměru
///    a rozptylu jasu spočítá hranice, T = m · (1 + k · (s / R − 1)). Kde je jen papír (malý
///    rozptyl), práh klesne pod jas papíru a šum zmizí; u písmen leží mezi písmem a papírem.
/// Hledání dvou píků v histogramu (Matějova metoda) je blízké Bernsenově a Otsuově metodě;
/// Sauvola s vyrovnaným pozadím je proti nim odolnější na slabém textu a v rozích se stínem.
/// </summary>
public static class UpravaProTisk
{
    /// <summary>Upraví fotku strany. Vrátí nový obrázek (barevný, nebo jednokanálový černobílý).</summary>
    public static Mat Uprav(Mat fotka, bool orezat = true, bool cernobile = true)
    {
        var vysledek = orezat ? OrizniPapir(fotka) : fotka.Clone();
        if (!cernobile) return vysledek;
        using (vysledek) return Cernobile(vysledek);
    }

    /// <summary>
    /// Najde list papíru (největší světlý čtyřúhelník) a narovná ho. Když papír nenajde
    /// (fotka je jen papír, nebo je pozadí stejně světlé), vrátí kopii celé fotky.
    /// </summary>
    public static Mat OrizniPapir(Mat fotka)
    {
        var rohy = NajdiRohyPapiru(fotka);
        if (rohy is null) return fotka.Clone();
        // pořadí: levý horní, pravý horní, pravý dolní, levý dolní
        double Vzdal(Point2f a, Point2f b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        int sirka = (int)Math.Round(Math.Max(Vzdal(rohy[0], rohy[1]), Vzdal(rohy[3], rohy[2])));
        int vyska = (int)Math.Round(Math.Max(Vzdal(rohy[0], rohy[3]), Vzdal(rohy[1], rohy[2])));
        var cil = new[] { new Point2f(0, 0), new Point2f(sirka - 1, 0), new Point2f(sirka - 1, vyska - 1), new Point2f(0, vyska - 1) };
        using var m = Cv2.GetPerspectiveTransform(rohy, cil);
        var vystup = new Mat();
        Cv2.WarpPerspective(fotka, vystup, m, new Size(sirka, vyska), InterpolationFlags.Cubic, BorderTypes.Replicate);
        return vystup;
    }

    /// <summary>Rohy papíru v souřadnicích fotky, nebo null.</summary>
    public static Point2f[]? NajdiRohyPapiru(Mat fotka)
    {
        double k = 800.0 / Math.Max(fotka.Width, fotka.Height);
        using var mala = new Mat();
        Cv2.Resize(fotka, mala, new Size(Math.Max(1, (int)(fotka.Width * k)), Math.Max(1, (int)(fotka.Height * k))), 0, 0, InterpolationFlags.Area);
        using var seda = new Mat();
        if (mala.Channels() == 3) Cv2.CvtColor(mala, seda, ColorConversionCodes.BGR2GRAY); else mala.CopyTo(seda);
        Cv2.GaussianBlur(seda, seda, new Size(7, 7), 0);
        // papír je světlejší než stůl: Otsu oddělí obě plochy, uzavření zalepí písmena
        using var maska = new Mat();
        Cv2.Threshold(seda, maska, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        using var jadro = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(15, 15));
        Cv2.MorphologyEx(maska, maska, MorphTypes.Close, jadro);
        Cv2.FindContours(maska, out var obrysy, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        if (obrysy.Length == 0) return null;
        var nejvetsi = obrysy.OrderByDescending(o => Cv2.ContourArea(o)).First();
        double plocha = Cv2.ContourArea(nejvetsi), celek = mala.Width * mala.Height;
        // papír musí být velký, ale ne celý snímek (to by nebylo co ořezávat)
        if (plocha < 0.2 * celek || plocha > 0.97 * celek) return null;

        var obvod = Cv2.ArcLength(nejvetsi, true);
        var priblizny = Cv2.ApproxPolyDP(nejvetsi, 0.02 * obvod, true);
        Point2f[] body = priblizny.Length == 4 && Cv2.IsContourConvex(priblizny)
            ? priblizny.Select(p => new Point2f(p.X, p.Y)).ToArray()
            : Cv2.MinAreaRect(nejvetsi).Points(); // zvlněný papír: aspoň obdélník kolem
        // seřadit rohy a přepočítat na původní velikost
        var podleSouctu = body.OrderBy(p => p.X + p.Y).ToArray();
        var podleRozdilu = body.OrderBy(p => p.Y - p.X).ToArray();
        var serazene = new[] { podleSouctu[0], podleRozdilu[0], podleSouctu[3], podleRozdilu[3] };
        return serazene.Select(p => new Point2f((float)(p.X / k), (float)(p.Y / k))).ToArray();
    }

    /// <summary>Černobílá: vyrovnání pozadí a Sauvolův lokální práh. Vrátí jednokanálový obrázek 0/255.</summary>
    public static Mat Cernobile(Mat obrazek, double kSauvola = 0.2)
    {
        using var seda = new Mat();
        if (obrazek.Channels() == 3) Cv2.CvtColor(obrazek, seda, ColorConversionCodes.BGR2GRAY); else obrazek.CopyTo(seda);

        // velikost písmene odhadem z rozměru stránky (A4 vyfocená celá: řádek textu ~ 1/60 výšky)
        int pismeno = Math.Max(8, Math.Max(seda.Width, seda.Height) / 60);

        // 1) pozadí: na zmenšenině uzavření jádrem větším než písmeno smaže text, pak zpět na velikost
        using var mala = new Mat();
        Cv2.Resize(seda, mala, new Size(Math.Max(1, seda.Width / 4), Math.Max(1, seda.Height / 4)), 0, 0, InterpolationFlags.Area);
        int j = Math.Max(3, pismeno / 2) | 1;
        using (var jadro = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(j, j)))
            Cv2.MorphologyEx(mala, mala, MorphTypes.Close, jadro);
        Cv2.MedianBlur(mala, mala, 5);
        using var pozadi = new Mat();
        Cv2.Resize(mala, pozadi, seda.Size(), 0, 0, InterpolationFlags.Linear);
        using var rovna = new Mat();
        Cv2.Divide(seda, pozadi, rovna, 255.0);

        // 2) Sauvola: průměr a směrodatná odchylka v okně ~ 2 písmena
        int okno = (pismeno * 2) | 1;
        using var f = new Mat();
        rovna.ConvertTo(f, MatType.CV_32F);
        using var prumer = new Mat();
        using var prumerCtvercu = new Mat();
        using var ctverce = new Mat();
        Cv2.BoxFilter(f, prumer, MatType.CV_32F, new Size(okno, okno), normalize: true, borderType: BorderTypes.Replicate);
        Cv2.Multiply(f, f, ctverce);
        Cv2.BoxFilter(ctverce, prumerCtvercu, MatType.CV_32F, new Size(okno, okno), normalize: true, borderType: BorderTypes.Replicate);
        using var rozptyl = new Mat();
        using var prumer2 = new Mat();
        Cv2.Multiply(prumer, prumer, prumer2);
        Cv2.Subtract(prumerCtvercu, prumer2, rozptyl);
        Cv2.Max(rozptyl, 0, rozptyl);
        using var odchylka = new Mat();
        Cv2.Sqrt(rozptyl, odchylka);
        // T = m * (1 + k * (s / 128 - 1))
        using var prah = new Mat();
        Cv2.AddWeighted(odchylka, kSauvola / 128.0, odchylka, 0, 1 - kSauvola, prah);
        Cv2.Multiply(prumer, prah, prah);
        using var maska = new Mat();
        Cv2.Compare(f, prah, maska, CmpTypes.GT); // světlejší než práh = papír (255)
        // osamocené tečky šumu pryč
        Cv2.MedianBlur(maska, maska, 3);
        return maska.Clone();
    }

    /// <summary>
    /// Uloží upravené strany (celý spis nebo vybrané dokumenty) do PDF formátu A4 k tisku.
    /// Obsahuje skutečné údaje, je jen pro tisk v kanceláři. Vrátí počet stran.
    /// </summary>
    public static int UlozPdf(Spis spis, string cil, IReadOnlyCollection<string>? dokumenty = null, bool orezat = true, bool cernobile = true)
    {
        const double A4Sirka = 595, A4Vyska = 842, Okraj = 18;
        var builder = new PdfDocumentBuilder();
        int pocet = 0;
        foreach (var (dokument, strany) in spis.StranyPoDokumentech())
        {
            if (dokumenty is not null && !dokumenty.Contains(dokument.Id)) continue;
            foreach (var strana in strany)
            {
                using var fotka = Cv2.ImRead(spis.CestaFotky(strana), ImreadModes.Color);
                if (fotka.Empty()) continue;
                using var upravena = Uprav(fotka, orezat, cernobile);
                // strana A4 na výšku, nebo na šířku podle tvaru fotky; obrázek se vejde s okrajem
                bool naSirku = upravena.Width > upravena.Height;
                double sirka = naSirku ? A4Vyska : A4Sirka, vyska = naSirku ? A4Sirka : A4Vyska;
                double k = Math.Min((sirka - 2 * Okraj) / upravena.Width, (vyska - 2 * Okraj) / upravena.Height);
                double w = upravena.Width * k, h = upravena.Height * k;
                var stranka = builder.AddPage(sirka, vyska);
                var obdelnik = new PdfRectangle((sirka - w) / 2, (vyska - h) / 2, (sirka + w) / 2, (vyska + h) / 2);
                if (cernobile)
                {
                    Cv2.ImEncode(".png", upravena, out var png, new ImageEncodingParam(ImwriteFlags.PngCompression, 9));
                    stranka.AddPng(png, obdelnik);
                }
                else
                {
                    Cv2.ImEncode(".jpg", upravena, out var jpg, new ImageEncodingParam(ImwriteFlags.JpegQuality, 85));
                    stranka.AddJpeg(jpg, obdelnik);
                }
                pocet++;
            }
        }
        if (pocet == 0) throw new InvalidOperationException("Ve výběru nejsou žádné strany.");
        File.WriteAllBytes(cil, builder.Build());
        return pocet;
    }
}
