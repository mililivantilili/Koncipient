using Koncipient.Ocr;

namespace Koncipient.Jadro;

/// <summary>Modely OCR a vytváření motorů.</summary>
public static class Motory
{
    public const string Paddle = "paddle-cs";
    public const string Tesseract = "tesseract-cs";
    public const string Windows = "windows-ocr";

    /// <summary>
    /// Pořadí, ve kterém se bere hlavní výsledek pro zobrazení. Windows OCR čte souvislý
    /// text nejlépe a nejrychleji (test na Matějově notebooku 2026-11-03), PaddleOCR
    /// je nejlepší na čísla a jména, Tesseract je záloha.
    /// </summary>
    public static readonly string[] PoradiZobrazeni = { Spis.Opraveno, Slouceni.Nazev, Windows, Paddle, Tesseract };

    /// <summary>Výsledky všech motorů, které se používají ke kontrole a hledání údajů.</summary>
    public static readonly string[] VsechnyMotory = { Windows, Paddle, Tesseract };

    private static readonly (string Soubor, string Url)[] Soubory =
    {
        ("det.onnx", "https://huggingface.co/PaddlePaddle/PP-OCRv6_medium_det_onnx/resolve/main/inference.onnx"),
        ("rec.onnx", "https://huggingface.co/PaddlePaddle/PP-OCRv6_medium_rec_onnx/resolve/main/inference.onnx"),
        ("rec.yml", "https://huggingface.co/PaddlePaddle/PP-OCRv6_medium_rec_onnx/resolve/main/inference.yml"),
        ("ces.traineddata", "https://github.com/tesseract-ocr/tessdata/raw/main/ces.traineddata"),
        // český slovník pro sloučení motorů (LibreOffice, GPL/LGPL/MPL)
        ("cs_CZ.aff", "https://raw.githubusercontent.com/LibreOffice/dictionaries/master/cs_CZ/cs_CZ.aff"),
        ("cs_CZ.dic", "https://raw.githubusercontent.com/LibreOffice/dictionaries/master/cs_CZ/cs_CZ.dic"),
    };

    /// <summary>Složka s modely, výchozí %LOCALAPPDATA%\Koncipient\modely, jde změnit proměnnou KONCIPIENT_MODELY.</summary>
    public static string SlozkaModelu =>
        Environment.GetEnvironmentVariable("KONCIPIENT_MODELY")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Koncipient", "modely");

    public static bool ModelyJsou => Soubory.All(s => File.Exists(Path.Combine(SlozkaModelu, s.Soubor)));

    /// <summary>
    /// Jednorázové stažení modelů (asi 140 MB). V hotovém programu bude součástí
    /// instalace, za běhu se k síti nepřistupuje. Stahují se jen modely, žádná data.
    /// </summary>
    public static async Task StahniAsync(IProgress<string>? prubeh = null, CancellationToken zruseni = default)
    {
        Directory.CreateDirectory(SlozkaModelu);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        foreach (var (soubor, url) in Soubory)
        {
            var cesta = Path.Combine(SlozkaModelu, soubor);
            if (File.Exists(cesta)) continue;
            prubeh?.Report($"Stahuji model {soubor}…");
            var docasny = cesta + ".stahuje";
            await using (var proud = await http.GetStreamAsync(url, zruseni))
            await using (var f = File.Create(docasny))
                await proud.CopyToAsync(f, zruseni);
            File.Move(docasny, cesta, overwrite: true);
        }
    }

    /// <summary>Motory pro noční zpracování: PaddleOCR (hlavní) a Tesseract (kontrolní).</summary>
    /// <summary>Má Windows OCR nainstalovanou češtinu (jen na Windows).</summary>
    public static bool WindowsOcrJe =>
#if WINDOWS
        WindowsOcrMotor.JeCestinaDostupna;
#else
        false;
#endif

    /// <summary>
    /// Motory pro noční zpracování: Windows OCR jako hlavní a PaddleOCR jako kontrolní.
    /// Bez češtiny ve Windows OCR nastoupí místo něj Tesseract.
    /// </summary>
    public static IReadOnlyList<Func<IOcrMotor>> ProDavku()
    {
        var motory = new List<Func<IOcrMotor>>();
#if WINDOWS
        if (WindowsOcrMotor.JeCestinaDostupna) motory.Add(() => new WindowsOcrMotor());
#endif
        motory.Add(() => new PaddleMotor(SlozkaModelu));
        if (!WindowsOcrJe) motory.Add(() => new TesseractMotor(SlozkaModelu));
        return motory;
    }

    /// <summary>Určení otočení fotek pomocí modelů PaddleOCR.</summary>
    public static Func<IUrceniOtoceni> Narovnani() => () => new PaddleMotor(SlozkaModelu);

    /// <summary>
    /// Výsledek strany pro prohlížeč: ručně opravený, jinak Windows OCR, PaddleOCR,
    /// Tesseract. Slova, která kontrolní motor přečetl jinak, jsou označená jako sporná.
    /// Kontrolní je PaddleOCR, a když je hlavní on, tak další dostupný.
    /// </summary>
    public static VysledekOcr? ProZobrazeni(Spis spis, Strana strana)
    {
        var hlavni = PoradiZobrazeni.Select(m => spis.NactiOcr(strana, m)).FirstOrDefault(v => v is not null);
        if (hlavni is null) return null;
        var kontrolni = new[] { Paddle, Windows, Tesseract }
            .Where(m => m != hlavni.Motor)
            .Select(m => spis.NactiOcr(strana, m))
            .FirstOrDefault(v => v is not null);
        if (kontrolni is not null) Porovnani.OznacSporna(hlavni, kontrolni);
        return hlavni;
    }

    /// <summary>Výsledky ostatních motorů pro stranu (kromě hlavního), k hledání údajů.</summary>
    public static IEnumerable<VysledekOcr> Ostatni(Spis spis, Strana strana, string hlavniMotor) =>
        VsechnyMotory.Where(m => m != hlavniMotor).Select(m => spis.NactiOcr(strana, m)).OfType<VysledekOcr>();
}
