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

    public string Prepis(string cestaKFotce) => PrepisAsync(cestaKFotce).GetAwaiter().GetResult();

    private async Task<string> PrepisAsync(string cestaKFotce)
    {
        var soubor = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(cestaKFotce));
        using var proud = await soubor.OpenAsync(FileAccessMode.Read);
        var dekoder = await BitmapDecoder.CreateAsync(proud);
        using var bitmapa = await dekoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var vysledek = await _engine.RecognizeAsync(bitmapa);
        return string.Join("\n", vysledek.Lines.Select(r => r.Text));
    }

    public void Dispose() { }
}
#endif
