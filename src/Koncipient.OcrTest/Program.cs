// Test OCR v C#: přepíše fotky stran všemi dostupnými motory a výsledky
// sbalí do zipu. Výstup má stejný tvar jako Python skripty v ocr_test/,
// takže jde vyhodnotit skriptem ocr_test/vyhodnoceni.py.
//
// Použití:  Koncipient.OcrTest [složka s fotkami] [--motor tesseract|paddle|windows]
// Bez parametrů se na složku zeptá.
//
// Modely se při prvním spuštění stáhnou do %LOCALAPPDATA%\Koncipient\modely.
// Fotky ani text se nikam neposílají.

using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Koncipient.Ocr;

Console.OutputEncoding = Encoding.UTF8;

string? slozka = null;
string? jenMotor = null;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--motor" && i + 1 < args.Length) jenMotor = args[++i];
    else slozka = args[i];
}

if (slozka is null)
{
    Console.WriteLine("Zadej (nebo přetáhni do okna) složku s fotkami a stiskni Enter:");
    slozka = Console.ReadLine()?.Trim().Trim('"');
}
if (string.IsNullOrWhiteSpace(slozka) || !Directory.Exists(slozka))
{
    Console.WriteLine($"Složka {slozka} neexistuje.");
    Konec();
    return 1;
}

// Fotky jsou buď přímo ve složce, nebo v podsložce fotky (jako u Python skriptů).
var slozkaFotek = Directory.Exists(Path.Combine(slozka, "fotky")) ? Path.Combine(slozka, "fotky") : slozka;
var pripony = new[] { ".jpg", ".jpeg", ".png", ".tif", ".tiff" };
var fotky = Directory.GetFiles(slozkaFotek)
    .Where(f => pripony.Contains(Path.GetExtension(f).ToLowerInvariant()))
    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
    .ToList();
if (fotky.Count == 0)
{
    Console.WriteLine($"Ve složce {slozkaFotek} nejsou žádné fotky.");
    Konec();
    return 1;
}

var vystup = Path.Combine(slozka, "vystup");
var upravene = Path.Combine(vystup, "upravene");
Console.WriteLine($"Nalezeno {fotky.Count} fotek. Výsledky půjdou do {vystup}");

var modely = await Modely.PripravAsync();

var motory = new List<Func<IOcrMotor>>();
if (jenMotor is null or "tesseract") motory.Add(() => new TesseractMotor(modely));
if (jenMotor is null or "paddle") motory.Add(() => new PaddleMotor(modely));
#if WINDOWS
if (jenMotor is null or "windows")
{
    if (WindowsOcrMotor.JeCestinaDostupna) motory.Add(() => new WindowsOcrMotor());
    else Console.WriteLine("Windows OCR přeskakuji: chybí čeština. Viz README.");
}
#endif

var protokol = new StringBuilder();
protokol.AppendLine($"Počítač: {Environment.MachineName}, {Environment.OSVersion}, jader {Environment.ProcessorCount}");

foreach (var vytvor in motory)
{
    IOcrMotor motor;
    try
    {
        motor = vytvor();
    }
    catch (Exception e)
    {
        Console.WriteLine($"Motor se nepodařilo spustit: {e.Message}");
        protokol.AppendLine($"CHYBA při spuštění motoru: {e}");
        continue;
    }

    using (motor)
    {
        var cil = Path.Combine(vystup, motor.Nazev);
        Directory.CreateDirectory(cil);
        Console.WriteLine($"\n== {motor.Nazev}");
        foreach (var fotka in fotky)
        {
            var stopky = Stopwatch.StartNew();
            try
            {
                var vstup = motor.ChceUpravenouFotku ? UpravaFotky.UlozUpravenou(fotka, upravene) : fotka;
                var text = motor.Prepis(vstup);
                File.WriteAllText(Path.Combine(cil, Path.GetFileNameWithoutExtension(fotka) + ".txt"), text, new UTF8Encoding(false));
                var radek = $"{motor.Nazev} {Path.GetFileName(fotka)}: {text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length} slov, {stopky.Elapsed.TotalSeconds:F1} s";
                Console.WriteLine(radek);
                protokol.AppendLine(radek);
            }
            catch (Exception e)
            {
                Console.WriteLine($"{Path.GetFileName(fotka)}: CHYBA {e.Message}");
                protokol.AppendLine($"CHYBA {motor.Nazev} {Path.GetFileName(fotka)}: {e}");
            }
        }
    }
}

File.WriteAllText(Path.Combine(vystup, "protokol.txt"), protokol.ToString(), new UTF8Encoding(false));

// Do zipu jen texty a protokol, žádné fotky.
var zip = Path.Combine(slozka, "vysledky-ocr.zip");
File.Delete(zip);
using (var archiv = ZipFile.Open(zip, ZipArchiveMode.Create))
{
    foreach (var soubor in Directory.GetFiles(vystup, "*.txt", SearchOption.AllDirectories))
        archiv.CreateEntryFromFile(soubor, Path.GetRelativePath(vystup, soubor).Replace('\\', '/'));
}
Console.WriteLine($"\nHotovo. Výsledky jsou v {zip}");
Konec();
return 0;

static void Konec()
{
    if (!Console.IsInputRedirected)
    {
        Console.WriteLine("Stiskni Enter pro zavření.");
        Console.ReadLine();
    }
}

/// <summary>Stažení modelů při prvním spuštění. Hotový program je bude mít v instalaci.</summary>
static class Modely
{
    private static readonly (string Soubor, string Url)[] Seznam =
    {
        ("det.onnx", "https://huggingface.co/PaddlePaddle/PP-OCRv6_medium_det_onnx/resolve/main/inference.onnx"),
        ("rec.onnx", "https://huggingface.co/PaddlePaddle/PP-OCRv6_medium_rec_onnx/resolve/main/inference.onnx"),
        ("rec.yml", "https://huggingface.co/PaddlePaddle/PP-OCRv6_medium_rec_onnx/resolve/main/inference.yml"),
        ("ces.traineddata", "https://github.com/tesseract-ocr/tessdata/raw/main/ces.traineddata"),
    };

    public static async Task<string> PripravAsync()
    {
        var slozka = Environment.GetEnvironmentVariable("KONCIPIENT_MODELY")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Koncipient", "modely");
        Directory.CreateDirectory(slozka);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        foreach (var (soubor, url) in Seznam)
        {
            var cesta = Path.Combine(slozka, soubor);
            if (File.Exists(cesta)) continue;
            Console.WriteLine($"Stahuji model {soubor}...");
            var docasny = cesta + ".stahuje";
            await using (var proud = await http.GetStreamAsync(url))
            await using (var f = File.Create(docasny))
                await proud.CopyToAsync(f);
            File.Move(docasny, cesta);
        }
        return slozka;
    }
}
