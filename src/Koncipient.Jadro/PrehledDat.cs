using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Koncipient.Jadro;

/// <summary>Datum nalezené ve spisu s okolím a nápovědou, k čemu se nejspíš vztahuje.</summary>
public sealed record DatumVeSpisu(DateOnly Datum, Strana Strana, int Od, int Do, string Pred, string Text, string Po, string? Druh);

/// <summary>
/// Přehled všech dat v textu spisu seřazený časově (doručení, jednání, splatnost…).
/// Program jen ukazuje, co v textu je, a nápovědu podle slov před datem. Lhůty nepočítá:
/// chyba ve výpočtu lhůty by byla drahá, posouzení zůstává na advokátce.
/// Obsahuje skutečné údaje, zůstává jen v počítači se spisy.
/// </summary>
public static partial class PrehledDat
{
    private static readonly string[] Mesice =
        { "ledna", "února", "března", "dubna", "května", "června", "července", "srpna", "září", "října", "listopadu", "prosince" };

    [GeneratedRegex(@"(?<![\d.])(?<d>\d{1,2})\.\s?(?<m>\d{1,2})\.\s?(?<r>\d{4})(?!\d)")]
    private static partial Regex Cislem();

    [GeneratedRegex(@"(?<![\d.])(?<d>\d{1,2})\.\s?(?<m>ledna|února|března|dubna|května|června|července|srpna|září|října|listopadu|prosince)\s(?<r>\d{4})(?!\d)", RegexOptions.IgnoreCase)]
    private static partial Regex Slovem();

    /// <summary>Nápověda podle slov těsně před datem (první shoda vyhrává).</summary>
    private static readonly (string Vzor, string Druh)[] Napovedy =
    {
        (@"\bnar(\.|oz)", "narození"),
        (@"doruč", "doručení"),
        (@"právní moc|právní mocí|nabyl", "právní moc"),
        (@"jednání|líčení|nařizuje|konat|koná|konaném|konané", "jednání"),
        (@"splatn", "splatnost"),
        (@"lhůt|nejpozději|do\s*$", "lhůta v textu"),
        (@"podán|podal|podala|podání", "podání"),
        (@"uzavř|uzavřen|smlouv", "smlouva"),
        (@"ze dne|dne\s*$|vydán", "ze dne"),
    };

    private const int Okoli = 6;

    public static List<DatumVeSpisu> Najdi(Spis spis)
    {
        var vysledek = new List<DatumVeSpisu>();
        foreach (var strana in spis.Strany)
        {
            var v = Motory.ProZobrazeni(spis, strana);
            if (v is null) continue;
            var slova = v.Slova.Select(s => s.Platny).ToList();
            // text strany se začátky slov, aby šlo nález převést na slova
            var sb = new StringBuilder();
            var zacatky = new List<int>();
            foreach (var s in slova) { if (sb.Length > 0) sb.Append(' '); zacatky.Add(sb.Length); sb.Append(s); }
            var text = sb.ToString();
            int SlovoNa(int pozice) { int i = zacatky.BinarySearch(pozice); return i >= 0 ? i : Math.Max(0, ~i - 1); }

            foreach (Match m in Cislem().Matches(text).Concat(Slovem().Matches(text)).OrderBy(m => m.Index))
            {
                if (!int.TryParse(m.Groups["d"].Value, out var den) || !int.TryParse(m.Groups["r"].Value, out var rok)) continue;
                int mesic = int.TryParse(m.Groups["m"].Value, out var mc) ? mc
                    : Array.FindIndex(Mesice, x => x.Equals(m.Groups["m"].Value, StringComparison.OrdinalIgnoreCase)) + 1;
                if (rok < 1900 || rok > 2100 || mesic < 1 || mesic > 12 || den < 1 || den > DateTime.DaysInMonth(rok, mesic)) continue;
                int od = SlovoNa(m.Index), @do = SlovoNa(m.Index + m.Length - 1);
                var pred = string.Join(" ", slova.Skip(Math.Max(0, od - Okoli)).Take(Math.Min(Okoli, od)));
                var kratce = string.Join(" ", slova.Skip(Math.Max(0, od - 3)).Take(Math.Min(3, od)));
                var druh = Napovedy.FirstOrDefault(n => Regex.IsMatch(kratce, n.Vzor, RegexOptions.IgnoreCase)).Druh;
                // termín jednání bývá dál od data („jednání, které se bude konat dne 10. 9.“)
                if (druh is null or "ze dne" && Regex.IsMatch(pred, @"jednání|líčení", RegexOptions.IgnoreCase)) druh = "jednání";
                vysledek.Add(new DatumVeSpisu(new DateOnly(rok, mesic, den), strana, od, @do, pred,
                    string.Join(" ", slova.Skip(od).Take(@do - od + 1)), string.Join(" ", slova.Skip(@do + 1).Take(Okoli)), druh));
            }
        }
        return vysledek.OrderBy(d => d.Datum).ThenBy(d => spis.Poradi(d.Strana)).ThenBy(d => d.Od).ToList();
    }

    /// <summary>Přehled jako text pro Word (jen pro advokátku, obsahuje skutečné údaje).</summary>
    public static string Text(Spis spis, IEnumerable<DatumVeSpisu> data)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Data ve spisu {(string.IsNullOrWhiteSpace(spis.Nazev) ? spis.Id : spis.Nazev)}");
        sb.AppendLine();
        sb.AppendLine("Seřazeno podle data. Druh je jen nápověda podle slov před datem, lhůty program nepočítá.");
        foreach (var d in data)
        {
            sb.AppendLine();
            sb.AppendLine($"{d.Datum.ToString("d. M. yyyy", CultureInfo.InvariantCulture)}{(d.Druh is null ? "" : $"  ({d.Druh})")}  strana {spis.Poradi(d.Strana)}");
            sb.AppendLine($"  … {d.Pred} {d.Text} {d.Po} …");
        }
        return sb.ToString();
    }
}
