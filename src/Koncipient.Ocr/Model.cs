using System.Text.Json.Serialization;

namespace Koncipient.Ocr;

/// <summary>Osově zarovnaný obdélník v pixelech fotky strany.</summary>
public readonly record struct Obdelnik(int X, int Y, int Sirka, int Vyska)
{
    public bool Obsahuje(double x, double y) => x >= X && x < X + Sirka && y >= Y && y < Y + Vyska;

    /// <summary>Nejmenší obdélník, který obsahuje všechny body.</summary>
    public static Obdelnik ZBodu(IEnumerable<(double X, double Y)> body)
    {
        var b = body.ToList();
        int x0 = (int)Math.Floor(b.Min(p => p.X)), y0 = (int)Math.Floor(b.Min(p => p.Y));
        int x1 = (int)Math.Ceiling(b.Max(p => p.X)), y1 = (int)Math.Ceiling(b.Max(p => p.Y));
        return new Obdelnik(x0, y0, Math.Max(x1 - x0, 1), Math.Max(y1 - y0, 1));
    }
}

/// <summary>Jedno slovo z OCR i s polohou na fotce.</summary>
public sealed class SlovoOcr
{
    public required string Text { get; set; }
    public Obdelnik Ramecek { get; set; }

    /// <summary>Jistota motoru 0–1, null když ji motor neudává (Windows OCR).</summary>
    public float? Jistota { get; set; }

    /// <summary>Ruční oprava. Když je vyplněná, platí místo <see cref="Text"/>.</summary>
    public string? Oprava { get; set; }

    /// <summary>Druhý motor přečetl toto místo jinak, takže ho stojí za to zkontrolovat.</summary>
    public bool Sporne { get; set; }

    [JsonIgnore] public string Platny => Oprava ?? Text;
}

public sealed class RadekOcr
{
    public Obdelnik Ramecek { get; set; }
    public List<SlovoOcr> Slova { get; set; } = new();

    [JsonIgnore] public string Text => string.Join(" ", Slova.Select(s => s.Platny));
}

/// <summary>Výsledek OCR jedné strany.</summary>
public sealed class VysledekOcr
{
    public required string Motor { get; set; }
    public int Sirka { get; set; }
    public int Vyska { get; set; }
    public List<RadekOcr> Radky { get; set; } = new();

    [JsonIgnore] public string Text => string.Join("\n", Radky.Select(r => r.Text).Where(t => t.Length > 0));

    [JsonIgnore] public IEnumerable<SlovoOcr> Slova => Radky.SelectMany(r => r.Slova);

    /// <summary>Slovo, na které se kliklo ve fotce, nebo null.</summary>
    public SlovoOcr? SlovoNa(double x, double y) => Slova.FirstOrDefault(s => s.Ramecek.Obsahuje(x, y));
}
