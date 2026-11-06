using Koncipient.Ocr;
using OpenCvSharp;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Outline;
using UglyToad.PdfPig.Outline.Destinations;
using UglyToad.PdfPig.Writer;

namespace Koncipient.Jadro;

/// <summary>
/// Prohledatelné PDF spisu pro archiv advokátky: fotky stran a pod nimi neviditelný text
/// z přepisu, takže v PDF funguje hledání (Ctrl+F) i kopírování. Dokumenty jsou záložky.
/// Obsahuje skutečné údaje, patří jen do jejího počítače, nikdy do ProClaude.
/// </summary>
public static class ArchivPdf
{
    /// <summary>Šířka strany v bodech (A4), výška podle poměru stran fotky.</summary>
    private const double Sirka = 595;

    private static readonly string[] Pisma =
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf"),
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
    };

    /// <summary>
    /// Uloží PDF celého spisu, nebo jen vybraných dokumentů (Id). Strany bez přepisu
    /// jsou v PDF jen jako obrázek. Vrátí počet stran.
    /// </summary>
    public static int Uloz(Spis spis, string cil, IReadOnlyCollection<string>? dokumenty = null)
    {
        var pismo = Pisma.FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException("Nenašel jsem písmo pro text v PDF (Arial).");
        var builder = new PdfDocumentBuilder();
        var font = builder.AddTrueTypeFont(File.ReadAllBytes(pismo));
        var zalozky = new List<BookmarkNode>();
        int pocet = 0;

        foreach (var (dokument, strany) in spis.StranyPoDokumentech())
        {
            if (dokumenty is not null && !dokumenty.Contains(dokument.Id)) continue;
            bool prvni = true;
            foreach (var strana in strany)
            {
                var cesta = spis.CestaFotky(strana);
                byte[] jpeg;
                int sirkaFotky, vyskaFotky;
                using (var obrazek = Cv2.ImRead(cesta, ImreadModes.Color))
                {
                    sirkaFotky = obrazek.Width;
                    vyskaFotky = obrazek.Height;
                    // fotky ve spisu jsou JPEG; přeuložení zaručí formát, který PDF umí vložit
                    Cv2.ImEncode(".jpg", obrazek, out jpeg, new ImageEncodingParam(ImwriteFlags.JpegQuality, 85));
                }
                double k = Sirka / sirkaFotky, vyska = vyskaFotky * k;
                var stranka = builder.AddPage(Sirka, vyska);
                stranka.AddJpeg(jpeg, new PdfRectangle(0, 0, Sirka, vyska));
                pocet++;

                var v = Motory.ProZobrazeni(spis, strana);
                if (v is not null)
                {
                    stranka.SetTextRenderingMode(TextRenderingMode.Neither);
                    double kv = v.Sirka > 0 ? Sirka / v.Sirka : k;
                    // všechna slova řádku na společném účaří a se stejnou výškou, jinak je prohlížeč PDF
                    // bere jako různé řádky a kopírovaný text vyjde přeházený
                    foreach (var radek in v.Radky.Where(r => r.Slova.Count > 0))
                    {
                        var spodky = radek.Slova.Select(s => s.Ramecek.Y + s.Ramecek.Vyska).OrderBy(x => x).ToList();
                        var vysky = radek.Slova.Select(s => s.Ramecek.Vyska).OrderBy(x => x).ToList();
                        int spodek = spodky[spodky.Count / 2], vyskaRadku = Math.Max(1, vysky[vysky.Count / 2]);
                        foreach (var slovo in radek.Slova.OrderBy(s => s.Ramecek.X))
                            VlozSlovo(stranka, font, slovo, kv, vyska, spodek, vyskaRadku);
                    }
                }
                if (prvni)
                {
                    zalozky.Add(new DocumentBookmarkNode(dokument.Nazev, 0,
                        new ExplicitDestination(pocet, ExplicitDestinationType.FitPage, ExplicitDestinationCoordinates.Empty),
                        Array.Empty<BookmarkNode>()));
                    prvni = false;
                }
            }
        }
        if (pocet == 0) throw new InvalidOperationException("Ve výběru nejsou žádné strany.");
        builder.Bookmarks = new Bookmarks(zalozky);
        File.WriteAllBytes(cil, builder.Build());
        return pocet;
    }

    /// <summary>Neviditelné slovo přes jeho místo na fotce, velikost písma podle šířky rámečku.</summary>
    private static void VlozSlovo(PdfPageBuilder stranka, PdfDocumentBuilder.AddedFont font, SlovoOcr slovo, double k, double vyska,
        int spodekRadku, int vyskaRadku)
    {
        var text = slovo.Platny.Trim();
        if (text.Length == 0) return;
        var r = slovo.Ramecek;
        double vyskaSlova = Math.Max(1, vyskaRadku * k);
        var zaklad = new PdfPoint(r.X * k, vyska - spodekRadku * k + vyskaSlova * 0.2);
        try
        {
            // velikost tak, aby text zabral šířku slova na fotce (označení v prohlížeči PDF sedí)
            double velikost = vyskaSlova * 0.9;
            var pismena = stranka.MeasureText(text, 10, zaklad, font);
            double sirka10 = pismena.Count == 0 ? 0 : pismena.Max(p => p.EndBaseLine.X) - zaklad.X;
            if (sirka10 > 0) velikost = Math.Clamp(10 * r.Sirka * k / sirka10, velikost * 0.5, velikost * 1.6);
            stranka.AddText(text + " ", velikost, zaklad, font);
        }
        catch (Exception)
        {
            // znak, který písmo nemá: slovo zůstane jen na obrázku
        }
    }
}
