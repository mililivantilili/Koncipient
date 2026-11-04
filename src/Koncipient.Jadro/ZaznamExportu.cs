using System.Text;
using System.Text.Json;
using Koncipient.Jadro.Anonymizace;

namespace Koncipient.Jadro;

/// <summary>
/// Jeden záznam o tom, co odešlo Claudovi (export) nebo na flash disk. Slouží advokátce
/// jako doklad, jak zachází s osobními údaji (GDPR). Obsahuje jen počty a čísla dokumentů,
/// nikdy skutečné údaje ani názvy dokumentů (můžou obsahovat jména). Zůstává ve spisu,
/// do ProClaude se nedostane.
/// </summary>
public sealed class ZaznamExportu
{
    public DateTime Kdy { get; set; }

    /// <summary>„export“ nebo „flash disk“.</summary>
    public string Akce { get; set; } = "export";

    /// <summary>Uživatel Windows, který export schválil.</summary>
    public string Kdo { get; set; } = "";

    /// <summary>Pořadí exportovaných dokumentů ve spisu (1, 2, …).</summary>
    public List<int> Dokumenty { get; set; } = new();

    public bool CelySpis { get; set; }
    public int Stran { get; set; }

    /// <summary>Všechny exportované strany měla advokátka označené jako zkontrolované.</summary>
    public bool StranyZkontrolovane { get; set; }

    /// <summary>Závěrečná kontrola nenašla v textu žádný známý skutečný údaj.</summary>
    public bool KontrolaProsla { get; set; }

    /// <summary>Počty údajů v klíči podle typu, které se nahradily vymyšleným jménem nebo kódem.</summary>
    public Dictionary<string, int> Nahrazeno { get; set; } = new();
    public int Odstraneno { get; set; }

    /// <summary>Údaje, které advokátka výslovně nechala bez anonymizace (soudy, úřady…), jen počet.</summary>
    public int Ponechano { get; set; }

    /// <summary>Kam export šel (složka nebo disk).</summary>
    public string Cil { get; set; } = "";
}

public static class ZaznamyExportu
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static string Cesta(Spis spis) => Path.Combine(spis.Slozka, "zaznam-exportu.json");

    public static List<ZaznamExportu> Nacti(Spis spis) =>
        File.Exists(Cesta(spis))
            ? JsonSerializer.Deserialize<List<ZaznamExportu>>(File.ReadAllText(Cesta(spis)), Json) ?? new()
            : new();

    public static void Pridej(Spis spis, ZaznamExportu zaznam)
    {
        var vse = Nacti(spis);
        vse.Add(zaznam);
        File.WriteAllText(Cesta(spis) + ".tmp", JsonSerializer.Serialize(vse, Json));
        File.Move(Cesta(spis) + ".tmp", Cesta(spis), overwrite: true);
    }

    /// <summary>Záznam o exportu se stavem klíče a stran v okamžiku exportu.</summary>
    public static ZaznamExportu ZExportu(Spis spis, Klic klic, IReadOnlyCollection<string>? jenDokumenty, string cil)
    {
        var poradi = spis.StranyPoDokumentech().Select((x, i) => (x.Dokument, x.Strany, Poradi: i + 1)).ToList();
        var vybrane = poradi.Where(x => jenDokumenty is null || jenDokumenty.Contains(x.Dokument.Id)).ToList();
        return new ZaznamExportu
        {
            Kdy = DateTime.Now,
            Akce = "export",
            Kdo = Environment.UserName,
            Dokumenty = vybrane.Select(x => x.Poradi).ToList(),
            CelySpis = vybrane.Count == poradi.Count,
            Stran = vybrane.Sum(x => x.Strany.Count),
            StranyZkontrolovane = vybrane.All(x => x.Strany.All(s => s.Zkontrolovano)),
            KontrolaProsla = true, // export se zapíše, jen když kontrola prošla
            Nahrazeno = klic.Entity.Where(e => e.Akce == Anonymizace.Akce.Pseudonym)
                .GroupBy(e => Nazvy.Typ(e.Typ)).ToDictionary(g => g.Key, g => g.Count()),
            Odstraneno = klic.Entity.Count(e => e.Akce == Anonymizace.Akce.Odstranit),
            Ponechano = klic.Entity.Count(e => e.Akce == Anonymizace.Akce.Ponechat),
            Cil = cil,
        };
    }

    /// <summary>Čitelný přehled pro advokátku (do Wordu nebo k tisku), bez skutečných údajů.</summary>
    public static string Prehled(Spis spis)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Záznam o předání anonymizovaných dat, spis {spis.Id}");
        sb.AppendLine();
        sb.AppendLine("Program Koncipient předává Claudovi (AI asistent) jen anonymizovaný text spisu. Skutečná jména, adresy, čísla "
            + "a fotky zůstávají v počítači se spisy. Tento záznam neobsahuje žádné osobní údaje.");
        var zaznamy = Nacti(spis);
        if (zaznamy.Count == 0) { sb.AppendLine(); sb.AppendLine("Ze spisu zatím nic neodešlo."); return sb.ToString(); }
        foreach (var z in zaznamy)
        {
            sb.AppendLine();
            sb.AppendLine($"{z.Kdy:d. M. yyyy H:mm}  {(z.Akce == "export" ? "Export pro Clauda" : "Zkopírováno na flash disk")} ({z.Kdo})");
            if (z.Akce != "export") { sb.AppendLine($"  Cíl: {z.Cil}"); continue; }
            sb.AppendLine($"  Dokumenty: {(z.CelySpis ? "celý spis" : "jen " + string.Join(", ", z.Dokumenty))}, {z.Stran} stran");
            sb.AppendLine($"  Strany zkontrolované člověkem: {(z.StranyZkontrolovane ? "ano, všechny" : "ne všechny")}");
            sb.AppendLine($"  Závěrečná kontrola známých údajů: {(z.KontrolaProsla ? "prošla" : "neprošla")}");
            sb.AppendLine($"  Nahrazeno: {(z.Nahrazeno.Count == 0 ? "nic" : string.Join(", ", z.Nahrazeno.Select(p => $"{p.Key} {p.Value}")))}"
                + $"; vypuštěno {z.Odstraneno}; ponecháno bez anonymizace (soudy, úřady…) {z.Ponechano}");
            sb.AppendLine($"  Cíl: {z.Cil}");
        }
        return sb.ToString();
    }
}
