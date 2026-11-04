using System.Text.RegularExpressions;
using Koncipient.Ocr;
using OpenCvSharp;
using PDFtoImage;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Koncipient.Jadro;

/// <summary>
/// Import PDF, hlavně písemností z datové schránky. Každá stránka se vykreslí jako obrázek
/// (zobrazení vpravo, případně OCR). Když PDF obsahuje text, vezme se přímo z něj: je přesný
/// a stránka se nemusí přepisovat. Naskenované PDF bez textu jde stejnou cestou jako fotky.
/// Všechno běží v počítači (PDFium a PdfPig), nic se neposílá ven.
/// </summary>
public sealed partial class Spis
{
    /// <summary>Rozlišení vykreslené stránky. 200 DPI stačí na čtení i na OCR naskenovaných stran.</summary>
    private const int DpiPdf = 200;

    /// <summary>Přidá vybrané soubory: fotky jako jeden import (černá fotka dělí dokumenty), každé PDF jako dokument.</summary>
    public IReadOnlyList<Strana> PridejSoubory(IReadOnlyCollection<string> cesty, string? nazevDokumentu = null, bool cerneOddeluji = true)
    {
        bool JePdf(string c) => Path.GetExtension(c).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
        var nove = new List<Strana>();
        var fotky = cesty.Where(c => !JePdf(c)).ToList();
        if (fotky.Count > 0) nove.AddRange(PridejFotky(fotky, nazevDokumentu, cerneOddeluji));
        foreach (var pdf in cesty.Where(JePdf).OrderBy(Path.GetFileName, PrirozeneRazeni.Instance))
            nove.AddRange(PridejPdf(pdf));
        return nove;
    }

    /// <summary>
    /// Přidá PDF jako jeden dokument pojmenovaný podle souboru (nebo <paramref name="nazevDokumentu"/>).
    /// Stránky s textem dostanou přepis rovnou z PDF, ostatní přepíše dávka.
    /// </summary>
    public IReadOnlyList<Strana> PridejPdf(string cesta, string? nazevDokumentu = null)
    {
        var bajty = File.ReadAllBytes(cesta);
        using var pdf = PdfDocument.Open(bajty);
        var dokument = NovyDokument(nazevDokumentu ?? Path.GetFileNameWithoutExtension(cesta));
        var nove = new List<Strana>();
        for (int i = 0; i < pdf.NumberOfPages; i++)
        {
            int cislo = Strany.Count == 0 ? 1 : Strany.Max(s => s.Cislo) + 1;
            var relativni = $"fotky/{cislo:D4}.jpg";
            int sirka, vyska;
            using (var obrazek = Conversion.ToImage(bajty, page: i,
                       options: new RenderOptions(Dpi: DpiPdf, WithAnnotations: true, BackgroundColor: SKColors.White)))
            {
                sirka = obrazek.Width;
                vyska = obrazek.Height;
                using var data = obrazek.Encode(SKEncodedImageFormat.Jpeg, 92);
                File.WriteAllBytes(Path.Combine(Slozka, relativni), data.ToArray());
            }
            var strana = new Strana
            {
                Cislo = cislo, Fotka = relativni, Dokument = dokument.Id,
                PuvodniNazev = $"{Path.GetFileName(cesta)}, s. {i + 1}",
            };
            var text = TextStrany(pdf.GetPage(i + 1), sirka, vyska);
            if (text is not null)
            {
                UlozOcr(strana, text);
                strana.Narovnano = true; // text z PDF je ve správné orientaci
            }
            Strany.Add(strana);
            nove.Add(strana);
        }
        Dokumenty.RemoveAll(d => !Strany.Any(s => s.Dokument == d.Id));
        Uloz();
        return nove;
    }

    /// <summary>
    /// Text stránky z PDF jako výsledek „OCR“ s polohami slov v pixelech vykreslené stránky.
    /// Null, když stránka text nemá (sken) nebo je nečitelný (rozbité kódování písma),
    /// taková stránka se přepíše přes OCR.
    /// </summary>
    internal static VysledekOcr? TextStrany(Page stranka, int sirka, int vyska)
    {
        if (stranka.Rotation.Value != 0) return null; // otočenou stránku raději přes OCR
        var slova = stranka.GetWords().Where(w => !string.IsNullOrWhiteSpace(w.Text)).ToList();
        if (slova.Sum(w => w.Text.Count(char.IsLetter)) < 20) return null;
        // rozbité kódování dává slova z nesmyslných znaků
        double citelnych = slova.Count(w => Regex.IsMatch(w.Text, @"^[\p{L}\d.,;:§()/\-–„“""'%&+*=!?@°]+$")) / (double)slova.Count;
        if (citelnych < 0.8) return null;

        var orez = stranka.CropBox.Bounds;
        double sx = sirka / orez.Width, sy = vyska / orez.Height;
        Obdelnik Ramecek(UglyToad.PdfPig.Core.PdfRectangle r) => new(
            (int)Math.Round((r.Left - orez.Left) * sx), (int)Math.Round((orez.Top - r.Top) * sy),
            Math.Max(1, (int)Math.Round(r.Width * sx)), Math.Max(1, (int)Math.Round(r.Height * sy)));

        // slova do řádků podle svislé polohy, v řádku zleva doprava
        var radky = new List<List<(UglyToad.PdfPig.Content.Word Slovo, Obdelnik R)>>();
        foreach (var w in slova.Select(w => (Slovo: w, R: Ramecek(w.BoundingBox))).OrderBy(x => x.R.Y + x.R.Vyska / 2.0))
        {
            double stred = w.R.Y + w.R.Vyska / 2.0;
            var radek = radky.LastOrDefault();
            if (radek is not null)
            {
                double stredRadku = radek.Average(x => x.R.Y + x.R.Vyska / 2.0);
                double vyskaRadku = radek.Average(x => x.R.Vyska);
                if (Math.Abs(stred - stredRadku) < 0.5 * Math.Max(vyskaRadku, w.R.Vyska)) { radek.Add(w); continue; }
            }
            radky.Add(new List<(UglyToad.PdfPig.Content.Word, Obdelnik)> { w });
        }

        var vysledek = new VysledekOcr { Motor = Motory.PdfText, Sirka = sirka, Vyska = vyska };
        foreach (var r in radky)
        {
            var serazene = r.OrderBy(x => x.R.X).ToList();
            vysledek.Radky.Add(new RadekOcr
            {
                Ramecek = Obdelnik.ZBodu(serazene.SelectMany(x => new[] { ((double)x.R.X, (double)x.R.Y), ((double)x.R.X + x.R.Sirka, (double)x.R.Y + x.R.Vyska) })),
                Slova = serazene.Select(x => new SlovoOcr { Text = x.Slovo.Text, Ramecek = x.R, Jistota = 1 }).ToList(),
            });
        }
        return vysledek;
    }
}
