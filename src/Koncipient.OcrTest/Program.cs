// Test OCR v C#: přepíše fotky stran všemi dostupnými motory a výsledky
// sbalí do zipu. Výstup má stejný tvar jako Python skripty v ocr_test/,
// takže jde vyhodnotit skriptem ocr_test/vyhodnoceni.py.
//
// Použití:  Koncipient.OcrTest [složka s fotkami] [--motor tesseract|paddle|windows] [--nahled]
// --nahled uloží k výsledkům i fotku s vyznačenými slovy (kontrola poloh).
// Bez parametrů se na složku zeptá.
//
// Modely se při prvním spuštění stáhnou do %LOCALAPPDATA%\Koncipient\modely.
// Fotky ani text se nikam neposílají.

using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using OpenCvSharp;
using Koncipient.Ocr;

Console.OutputEncoding = Encoding.UTF8;

string? slozka = null;
string? jenMotor = null;
bool nahled = false;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--motor" && i + 1 < args.Length) jenMotor = args[++i];
    else if (args[i] == "--nahled") nahled = true;
    else slozka = args[i];
}
var Json = new JsonSerializerOptions { WriteIndented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

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

await Koncipient.Jadro.Motory.StahniAsync(new Progress<string>(Console.WriteLine));
var modely = Koncipient.Jadro.Motory.SlozkaModelu;

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
                var vysledek = motor.Rozpoznej(vstup);
                var text = vysledek.Text;
                var jmeno = Path.GetFileNameWithoutExtension(fotka);
                File.WriteAllText(Path.Combine(cil, jmeno + ".txt"), text, new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(cil, jmeno + ".json"), JsonSerializer.Serialize(vysledek, Json), new UTF8Encoding(false));
                if (nahled) Nahled.Uloz(fotka, vysledek, Path.Combine(cil, jmeno + ".nahled.jpg"));
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

/// <summary>Fotka s vyznačenými rámečky slov, pro kontrolu, že polohy sedí.</summary>
static class Nahled
{
    public static void Uloz(string fotka, Koncipient.Ocr.VysledekOcr vysledek, string cil)
    {
        using var obrazek = Cv2.ImRead(fotka, ImreadModes.Color);
        foreach (var s in vysledek.Slova)
        {
            var r = s.Ramecek;
            var barva = s.Jistota is < 0.8f ? Scalar.Red : Scalar.LimeGreen;
            Cv2.Rectangle(obrazek, new OpenCvSharp.Rect(r.X, r.Y, r.Sirka, r.Vyska), barva, 2);
        }
        Cv2.ImWrite(cil, obrazek);
    }
}
