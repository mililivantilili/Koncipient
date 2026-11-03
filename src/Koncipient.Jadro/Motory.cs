using Koncipient.Ocr;

namespace Koncipient.Jadro;

/// <summary>Modely OCR a vytváření motorů.</summary>
public static class Motory
{
    public const string Paddle = "paddle-cs";
    public const string Tesseract = "tesseract-cs";

    /// <summary>Pořadí, ve kterém se bere hlavní výsledek pro zobrazení.</summary>
    public static readonly string[] PoradiZobrazeni = { Spis.Opraveno, Paddle, Tesseract, "windows-ocr" };

    private static readonly (string Soubor, string Url)[] Soubory =
    {
        ("det.onnx", "https://huggingface.co/PaddlePaddle/PP-OCRv6_medium_det_onnx/resolve/main/inference.onnx"),
        ("rec.onnx", "https://huggingface.co/PaddlePaddle/PP-OCRv6_medium_rec_onnx/resolve/main/inference.onnx"),
        ("rec.yml", "https://huggingface.co/PaddlePaddle/PP-OCRv6_medium_rec_onnx/resolve/main/inference.yml"),
        ("ces.traineddata", "https://github.com/tesseract-ocr/tessdata/raw/main/ces.traineddata"),
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
    public static IReadOnlyList<Func<IOcrMotor>> ProDavku() => new Func<IOcrMotor>[]
    {
        () => new PaddleMotor(SlozkaModelu),
        () => new TesseractMotor(SlozkaModelu),
    };

    /// <summary>Určení otočení fotek pomocí modelů PaddleOCR.</summary>
    public static Func<IUrceniOtoceni> Narovnani() => () => new PaddleMotor(SlozkaModelu);

    /// <summary>
    /// Výsledek strany pro prohlížeč: ručně opravený, jinak PaddleOCR, jinak co je.
    /// Slova, která druhý motor přečetl jinak, jsou označená jako sporná.
    /// </summary>
    public static VysledekOcr? ProZobrazeni(Spis spis, Strana strana)
    {
        var hlavni = PoradiZobrazeni.Select(m => spis.NactiOcr(strana, m)).FirstOrDefault(v => v is not null);
        if (hlavni is null) return null;
        if (hlavni.Motor != Tesseract)
        {
            var kontrolni = spis.NactiOcr(strana, Tesseract);
            if (kontrolni is not null) Porovnani.OznacSporna(hlavni, kontrolni);
        }
        return hlavni;
    }
}
