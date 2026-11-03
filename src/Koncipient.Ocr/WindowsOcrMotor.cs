#if WINDOWS
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;

namespace Koncipient.Ocr;

/// <summary>
/// OCR vestavěné ve Windows 10 a 11. Nic se nestahuje, ale vyžaduje
/// nainstalovanou češtinu pro rozpoznávání textu.
/// </summary>
public sealed class WindowsOcrMotor : IOcrMotor
{
    private static readonly Language Cestina = new("cs");
    private readonly OcrEngine _engine;

    public static bool JeCestinaDostupna => OcrEngine.IsLanguageSupported(Cestina);

    public WindowsOcrMotor()
    {
        _engine = OcrEngine.TryCreateFromLanguage(Cestina)
            ?? throw new InvalidOperationException("Windows OCR nemá nainstalovanou češtinu.");
    }

    public string Nazev => "windows-ocr";
    public bool ChceUpravenouFotku => true;

    public VysledekOcr Rozpoznej(string cestaKFotce) => RozpoznejAsync(cestaKFotce).GetAwaiter().GetResult();

    private async Task<VysledekOcr> RozpoznejAsync(string cestaKFotce)
    {
        var soubor = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(cestaKFotce));
        using var proud = await soubor.OpenAsync(FileAccessMode.Read);
        var dekoder = await BitmapDecoder.CreateAsync(proud);
        using var bitmapa = await dekoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var ocr = await _engine.RecognizeAsync(bitmapa);

        var vysledek = new VysledekOcr { Motor = Nazev, Sirka = bitmapa.PixelWidth, Vyska = bitmapa.PixelHeight };
        foreach (var r in ocr.Lines)
        {
            var radek = new RadekOcr();
            foreach (var w in r.Words)
            {
                var b = w.BoundingRect;
                radek.Slova.Add(new SlovoOcr
                {
                    Text = w.Text,
                    Ramecek = new Obdelnik((int)b.X, (int)b.Y, (int)Math.Ceiling(b.Width), (int)Math.Ceiling(b.Height)),
                });
            }
            if (radek.Slova.Count == 0) continue;
            radek.Ramecek = Obdelnik.ZBodu(radek.Slova.SelectMany(s => new[]
            {
                ((double)s.Ramecek.X, (double)s.Ramecek.Y),
                ((double)(s.Ramecek.X + s.Ramecek.Sirka), (double)(s.Ramecek.Y + s.Ramecek.Vyska)),
            }));
            vysledek.Radky.Add(radek);
        }
        return vysledek;
    }

    public void Dispose() { }
}
#endif
