// Měření anonymizace na vymyšlených dokumentech z vzory/generuj.py.
//
// Použití:  Koncipient.MereniAnonymizace [složka z generuj.py] [--text] [--prace složka]
//   bez složky se použije vymyšlená sada vzory/sada z repozitáře
//   --prace  kam ukládat spis a výsledky (výchozí %TEMP%\koncipient-mereni), nikdy do repozitáře
//   --text  místo OCR použije referenční text (bezchybný přepis), měří jen detektor.
//   bez --text  fotky projdou celým programem: narovnání, OCR, hledání údajů, anonymizace.
//
// Měří:
//   uniklé údaje   vyznačený údaj, jehož slovo nebo číslo zůstalo v textu pro Clauda
//   navíc          údaje v klíči, které neodpovídají žádnému vyznačenému (nahrazeno zbytečně)

using System.Text;
using System.Text.Json;
using Koncipient.Jadro;
using Koncipient.Jadro.Anonymizace;
using Koncipient.Ocr;
using Text = Koncipient.Jadro.Anonymizace.Text;

Console.OutputEncoding = Encoding.UTF8;
bool rezimText = args.Contains("--text");
string? Parametr(string nazev) { int i = Array.IndexOf(args, nazev); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
var koren = args.FirstOrDefault(a => !a.StartsWith("--") && a != Parametr("--prace") && a != Parametr("--cast")) ?? NajdiSadu();
var prace = Parametr("--prace") ?? Path.Combine(Path.GetTempPath(), "koncipient-mereni");
Directory.CreateDirectory(prace);
Console.WriteLine($"Sada: {koren}\nVýsledky: {prace}");

static string NajdiSadu()
{
    for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        if (Directory.Exists(Path.Combine(d.FullName, "vzory", "sada"))) return Path.Combine(d.FullName, "vzory", "sada");
    throw new DirectoryNotFoundException("Sada vzory/sada se nenašla, zadej složku jako parametr.");
}

// --cast vyvoj|kontrola: pevné rozdělení dokumentů 70:30 podle názvu (strany téhož dokumentu zůstanou spolu)
var cast = Parametr("--cast");
bool VCasti(string nazev)
{
    if (cast is null) return true;
    var dokument = System.Text.RegularExpressions.Regex.Replace(nazev, @"_s\d+$", "");
    uint h = 2166136261; foreach (var c in dokument) h = (h ^ c) * 16777619;
    return (h % 10 < 7) == (cast == "vyvoj");
}
var reference = Directory.GetFiles(Path.Combine(koren, "reference"), "*.json").OrderBy(f => f)
    .Where(f => VCasti(Path.GetFileNameWithoutExtension(f)))
    .Select(f => (Nazev: Path.GetFileNameWithoutExtension(f), Data: JsonSerializer.Deserialize<Reference>(File.ReadAllText(f), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!))
    .ToList();

var slozkaSpisu = Path.Combine(prace, (rezimText ? "spisy-text" : "spisy-ocr") + (cast is null ? "" : "-" + cast));
if (Directory.Exists(slozkaSpisu) && rezimText) Directory.Delete(slozkaSpisu, true);
var uloziste = new Uloziste(slozkaSpisu);
var spis = uloziste.Spisy().FirstOrDefault() ?? uloziste.Zaloz("měření");
if (spis.Strany.Count == 0)
    spis.PridejFotky(reference.Select(r => Path.Combine(koren, "fotky", r.Nazev + ".jpg")));

if (rezimText)
{
    foreach (var (strana, r) in reference.Select(r => (spis.Strany.Single(s => s.PuvodniNazev == r.Nazev + ".jpg"), r)))
    {
        var v = new VysledekOcr { Motor = Motory.Windows };
        foreach (var radek in r.Data.Text.Split('\n'))
        {
            var slova = radek.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => new SlovoOcr { Text = s }).ToList();
            if (slova.Count > 0) v.Radky.Add(new RadekOcr { Slova = slova });
        }
        spis.UlozOcr(strana, v);
    }
}
else
{
    Console.WriteLine("Přepisuji fotky (nové jen ty, které ještě nemají přepis)…");
    await Motory.StahniAsync();
    await Davka.ZpracujAsync(uloziste.Spisy(), Motory.ProDavku(),
        new Progress<StavDavky>(s => { if (s.Hotovo == s.Celkem) Console.WriteLine(s.Popis); }), default, Motory.Narovnani());
    spis = uloziste.Spisy()[0];
}

var klic = new Klic();
Anonymizator.AktualizujKlic(spis, klic);
Anonymizator.UlozKlic(spis, klic);

// Slova, která se nevyznačují (titulky, předložky) a čísla, která jsou i ve veřejném textu.
var nevyznamna = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "ing", "judr", "mgr", "bc", "mudr", "phdr", "rndr", "doc", "prof", "ph.d", "parc", "č", "k", "ú", "lv", "r", "u", "v", "nad", "pod", "na",
      "nar", "c", "s.r.o", "a.s", "s", "ve", "pan", "paní", "pana", "panu", "panem", "slečna" };

var vypis = new StringBuilder();
var souhrn = new Dictionary<string, (int Celkem, int Uniklo)>();
int navicCelkem = 0;

foreach (var (strana, r) in reference.Select(r => (spis.Strany.Single(s => s.PuvodniNazev == r.Nazev + ".jpg"), r)))
{
    var v = Motory.ProZobrazeni(spis, strana);
    var anonym = v is null ? "" : Anonymizator.AnonymizovanyText(v, klic);
    var anonymSlova = anonym.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(Text.Ocisti).Where(s => s.Length > 0).ToList();
    var anonymCislice = new string(anonym.Where(char.IsDigit).ToArray());

    // veřejný text: co v referenci není vyznačené (soudy, obce v názvech úřadů, částky…)
    var verejny = new StringBuilder(r.Data.Text);
    foreach (var u in r.Data.Udaje) for (int i = u.Od; i < u.Do; i++) verejny[i] = ' ';
    var verejnaSlova = verejny.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(Text.Ocisti).Where(s => s.Length > 0).ToList();

    vypis.AppendLine($"\n=== {r.Nazev} ({v?.Motor ?? "bez přepisu"})");
    foreach (var u in r.Data.Udaje)
    {
        var uniklo = new List<string>();
        var cislice = new string(u.Hodnota.Where(char.IsDigit).ToArray());
        if (cislice.Length >= 4 && anonymCislice.Contains(cislice) && !new string(verejny.ToString().Where(char.IsDigit).ToArray()).Contains(cislice))
            uniklo.Add(cislice);
        foreach (var slovo in u.Hodnota.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(Text.Ocisti))
        {
            if (slovo.Length < 3 || slovo.Any(char.IsDigit) || nevyznamna.Contains(slovo.TrimEnd('.'))) continue;
            if (verejnaSlova.Any(p => Text.StejneSlovo(p, slovo))) continue; // slovo je i ve veřejném textu
            if (anonymSlova.Any(a => Text.StejneSlovo(a, slovo))) uniklo.Add(slovo);
        }
        var s = souhrn.GetValueOrDefault(u.Typ);
        souhrn[u.Typ] = (s.Celkem + 1, s.Uniklo + (uniklo.Count > 0 ? 1 : 0));
        if (uniklo.Count > 0) vypis.AppendLine($"  UNIKLO {u.Typ,-10} „{u.Hodnota}“: {string.Join(", ", uniklo.Distinct())}");
    }
    File.WriteAllText(Path.Combine(prace, $"anonymizovano-{(rezimText ? "text" : "ocr")}-{r.Nazev}.md"), anonym);
}

// údaje v klíči, které neodpovídají ničemu vyznačenému
var vyznacena = reference.SelectMany(r => r.Data.Udaje).SelectMany(u => u.Hodnota.Split((char[]?)null)).Select(Text.Ocisti).Where(s => s.Length > 0).ToList();
var vyznacenaCisla = reference.SelectMany(r => r.Data.Udaje).Select(u => new string(u.Hodnota.Where(char.IsDigit).ToArray())).Where(s => s.Length > 0).ToList();
vypis.AppendLine("\n=== Nahrazeno navíc (v klíči, ale nevyznačeno)");
foreach (var e in klic.Entity.Where(e => e.Akce != Akce.Ponechat)) // ponechané (judikatura) se nenahrazují
{
    var tvary = e.Tvary.SelectMany(t => t).ToList();
    bool odpovida = tvary.Any(t => vyznacena.Any(z => Text.StejneSlovo(z, t)))
        || tvary.Select(t => new string(t.Where(char.IsDigit).ToArray())).Any(c => c.Length >= 3 && vyznacenaCisla.Any(z => z.Contains(c)));
    if (!odpovida) { navicCelkem++; vypis.AppendLine($"  {e.Id,-15} {Nazvy.Typ(e.Typ),-14} {string.Join(" · ", e.Tvary.Select(t => string.Join(" ", t)).Distinct())}"); }
}

Console.WriteLine($"Režim: {(rezimText ? "bezchybný text" : "OCR z fotek")}");
Console.WriteLine($"{"Typ",-12} {"Údajů",6} {"Uniklo",7}");
foreach (var (typ, (celkem, uniklo)) in souhrn.OrderBy(x => x.Key))
    Console.WriteLine($"{typ,-12} {celkem,6} {uniklo,7}");
Console.WriteLine($"{"CELKEM",-12} {souhrn.Values.Sum(x => x.Celkem),6} {souhrn.Values.Sum(x => x.Uniklo),7}   nahrazeno navíc: {navicCelkem}");
Console.WriteLine(vypis.ToString());
File.WriteAllText(Path.Combine(prace, $"vysledek-{(rezimText ? "text" : "ocr")}.txt"), vypis.ToString());
if (!Console.IsInputRedirected && args.Length == 0) { Console.WriteLine("Stiskni Enter pro zavření."); Console.ReadLine(); }
return 0;

record Udaj(string Typ, string Hodnota, int Od, int Do);
record Reference(string Text, List<Udaj> Udaje);
