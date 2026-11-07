using Koncipient.Ocr;
using OpenCvSharp;

namespace Koncipient.Jadro;

/// <summary>Strana, kterou je lepší přefotit, a proč.</summary>
public sealed record StranaKPrefoceni(Strana Strana, IReadOnlyList<string> Duvody);

/// <summary>
/// Po přepisu najde strany, které vyšly špatně: rozmazané, tmavé, s textem uříznutým
/// u okraje nebo s nejistým přepisem. Advokátka je přefotí, dokud má spis v ruce.
/// </summary>
public static class KvalitaFotek
{
    /// <summary>
    /// Ostrost (rozptyl Laplaceova operátoru na šířce 1000 px), pod kterou je fotka rozmazaná.
    /// Fotky z Matějova archivu (mobil) mají 445 až 1583, vymyšlená sada 204 až 614, rozmazané pod 20.
    /// </summary>
    public const double MinOstrost = 60;

    /// <summary>Posoudí všechny strany a výsledek si strany zapamatují (barva v seznamu stran, průvodce).</summary>
    public static List<StranaKPrefoceni> Najdi(Spis spis)
    {
        var vysledek = new List<StranaKPrefoceni>();
        foreach (var s in spis.Strany)
        {
            var duvody = Posud(spis, s);
            s.DuvodyPrefoceni = duvody.Count > 0 ? duvody : null;
            if (duvody.Count > 0) vysledek.Add(new StranaKPrefoceni(s, duvody));
        }
        spis.Uloz();
        return vysledek;
    }

    public static List<string> Posud(Spis spis, Strana strana)
    {
        var duvody = new List<string>();
        var v = Motory.ProZobrazeni(spis, strana);
        if (v?.Motor == Motory.PdfText) return duvody; // text z PDF je přesný
        using (var obrazek = Cv2.ImRead(spis.CestaFotky(strana), ImreadModes.Grayscale))
        {
            if (obrazek.Empty()) { duvody.Add("fotku nejde načíst"); return duvody; }
            using var mala = new Mat();
            Cv2.Resize(obrazek, mala, new Size(1000, Math.Max(1, (int)Math.Round(obrazek.Height * 1000.0 / obrazek.Width))), 0, 0, InterpolationFlags.Area);
            Cv2.MeanStdDev(mala, out var prumer, out var odchylka);
            if (odchylka.Val0 < 12) return duvody; // prázdná stránka, není co číst
            if (prumer.Val0 < 80) duvody.Add("fotka je tmavá");
            using var lap = new Mat();
            Cv2.Laplacian(mala, lap, MatType.CV_64F);
            Cv2.MeanStdDev(lap, out _, out var lapOdchylka);
            // prázdný papír na stole je „neostrý“, i když je fotka v pořádku: strana bez textu se nehlásí
            bool bezTextu = v is not null && v.Slova.Count() < 5;
            double ostrost = lapOdchylka.Val0 * lapOdchylka.Val0;
            // když motory text přečetly dobře (hodně slov, málo sporných), mírná neostrost nevadí
            bool dobrePrecteno = v is not null && v.Slova.Count() >= 30 && v.Slova.Count(s => s.Sporne) < 0.2 * v.Slova.Count();
            if (!bezTextu && !dobrePrecteno && ostrost < MinOstrost) duvody.Add("fotka je rozmazaná");
            // úplně rozmazaná fotka nemá ani slovo a tvářila by se jako prázdná; prázdný papír má ostrost aspoň 51
            else if (bezTextu && ostrost < MinOstrost / 3) duvody.Add("fotka je nečitelná (rozmazaná), program z ní nepřečetl nic");
        }
        if (v is null) return duvody; // bez přepisu jde posoudit jen fotku

        var slova = v.Slova.ToList();
        if (v.Sirka > 0 && v.Vyska > 0)
        {
            double okrajX = v.Sirka * 0.006, okrajY = v.Vyska * 0.006;
            int uOkraje = slova.Count(s => s.Ramecek.X <= okrajX || s.Ramecek.X + s.Ramecek.Sirka >= v.Sirka - okrajX
                                           || s.Ramecek.Y <= okrajY || s.Ramecek.Y + s.Ramecek.Vyska >= v.Vyska - okrajY);
            if (uOkraje >= 3) duvody.Add("text je u okraje uříznutý");
        }
        if (slova.Count >= 20)
        {
            double sporne = slova.Count(s => s.Sporne) / (double)slova.Count;
            if (sporne > 0.35) duvody.Add($"přepis je nejistý ({sporne:P0} slov přečetly motory různě)");
        }
        return duvody;
    }
}
