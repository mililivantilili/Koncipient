using System.Text.Json;
using Koncipient.Jadro;
using Koncipient.Jadro.Anonymizace;
using Koncipient.Ocr;
using Xunit;
using Text = Koncipient.Jadro.Anonymizace.Text;

namespace Koncipient.Testy;

/// <summary>
/// Hlídá, že z vymyšlené sady dokumentů (vzory/sada, viz vzory/generuj.py) při bezchybném
/// přepisu neunikne žádný vyznačený údaj. Měření přes OCR dělá nástroj Koncipient.MereniAnonymizace.
/// </summary>
public class SadaVzoruTesty
{
    private sealed record Udaj(string Typ, string Hodnota, int Od, int Do);
    private sealed record Reference(string Text, List<Udaj> Udaje);

    private static string Sada()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "vzory", "sada"))) return Path.Combine(d.FullName, "vzory", "sada");
        throw new DirectoryNotFoundException("vzory/sada");
    }

    [Fact]
    public void ZBezchybnehoTextuNeunikneZadnyUdaj()
    {
        var sada = Sada();
        var reference = Directory.GetFiles(Path.Combine(sada, "reference"), "*.json").OrderBy(f => f)
            .Select(f => JsonSerializer.Deserialize<Reference>(File.ReadAllText(f), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!)
            .ToList();
        Assert.True(reference.Count >= 6);

        using var d = new DocasnaSlozka();
        var spis = new Uloziste(d.Cesta).Zaloz("sada");
        spis.PridejFotky(Directory.GetFiles(Path.Combine(sada, "fotky")).OrderBy(f => f));
        foreach (var (strana, r) in spis.Strany.Zip(reference))
        {
            var v = new VysledekOcr { Motor = Motory.Windows };
            foreach (var radek in r.Text.Split('\n'))
            {
                var slova = radek.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => new SlovoOcr { Text = s }).ToList();
                if (slova.Count > 0) v.Radky.Add(new RadekOcr { Slova = slova });
            }
            spis.UlozOcr(strana, v);
        }
        var klic = new Klic();
        Anonymizator.AktualizujKlic(spis, klic);

        var tituly = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ing", "judr", "mgr", "bc", "parc", "č", "k", "ú", "lv", "r", "u", "v", "c" };
        var uniky = new List<string>();
        foreach (var (strana, r) in spis.Strany.Zip(reference))
        {
            var anonym = Anonymizator.AnonymizovanyText(Motory.ProZobrazeni(spis, strana)!, klic);
            var anonymSlova = anonym.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(Text.Ocisti).ToList();
            var anonymCislice = new string(anonym.Where(char.IsDigit).ToArray());
            var verejny = r.Text.ToCharArray();
            foreach (var u in r.Udaje) for (int i = u.Od; i < u.Do; i++) verejny[i] = ' ';
            var verejnaSlova = new string(verejny).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(Text.Ocisti).ToList();
            var verejneCislice = new string(verejny.Where(char.IsDigit).ToArray());

            foreach (var u in r.Udaje)
            {
                var cislice = new string(u.Hodnota.Where(char.IsDigit).ToArray());
                if (cislice.Length >= 4 && anonymCislice.Contains(cislice) && !verejneCislice.Contains(cislice))
                    uniky.Add($"{u.Typ} „{u.Hodnota}“");
                foreach (var s in u.Hodnota.Split(' ').Select(Text.Ocisti))
                {
                    if (s.Length < 3 || s.Any(char.IsDigit) || tituly.Contains(s.TrimEnd('.'))) continue;
                    if (verejnaSlova.Any(p => Text.StejneSlovo(p, s))) continue;
                    if (anonymSlova.Any(a => Text.StejneSlovo(a, s))) uniky.Add($"{u.Typ} „{u.Hodnota}“: {s}");
                }
            }
        }
        Assert.True(uniky.Count == 0, "Uniklo:\n" + string.Join("\n", uniky));
    }
}
