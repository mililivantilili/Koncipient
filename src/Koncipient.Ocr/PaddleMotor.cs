using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using YamlDotNet.RepresentationModel;

namespace Koncipient.Ocr;

/// <summary>
/// PaddleOCR PP-OCRv6 (medium) přes ONNX Runtime, bez Pythonu.
/// Napodobuje výchozí nastavení PaddleOCR 3.7: detekce řádků (DB), výřez
/// každého řádku a rozpoznání textu (CTC). Pomalý, ale nejlepší na čísla.
/// </summary>
/// <summary>Kde PaddleOCR počítá.</summary>
public enum ZarizeniOcr
{
    /// <summary>Podle proměnné KONCIPIENT_GPU, jinak nejvýkonnější grafika.</summary>
    Automaticky,
    Procesor,
    /// <summary>Grafická karta, kterou Windows označí jako nejvýkonnější (u notebooků samostatná).</summary>
    NejvykonnejsiGrafika,
    Grafika0,
    Grafika1,
}

public sealed class PaddleMotor : IOcrMotor, IUrceniOtoceni
{
    // Výchozí hodnoty z paddlex/configs/pipelines/OCR.yaml
    private const float PrahBinarizace = 0.3f;
    private const float PrahSkore = 0.6f;
    private const float Rozsireni = 1.5f;
    private const int MinVelikost = 3;
    private const int VyskaRadku = 48;
    private const int DavkaRadku = 6;

    private static readonly float[] Prumer = { 0.485f, 0.456f, 0.406f };
    private static readonly float[] Odchylka = { 0.229f, 0.224f, 0.225f };

    private InferenceSession _detekce;
    private InferenceSession _rozpoznani;
    private readonly string _slozkaModelu;
    private readonly string[] _znaky;

    /// <param name="slozkaModelu">
    /// Složka se soubory det.onnx, rec.onnx a rec.yml
    /// (z Hugging Face: PaddlePaddle/PP-OCRv6_medium_det_onnx a _rec_onnx).
    /// </param>
    /// <param name="zarizeni">
    /// Kde počítat. Výchozí <see cref="ZarizeniOcr.Automaticky"/> zkusí na Windows nejvýkonnější
    /// grafickou kartu přes DirectML, a když to nejde, počítá na procesoru.
    /// </param>
    public PaddleMotor(string slozkaModelu, ZarizeniOcr zarizeni = ZarizeniOcr.Automaticky)
    {
        if (zarizeni == ZarizeniOcr.Automaticky) zarizeni = ZarizeniZProstredi();
        _slozkaModelu = slozkaModelu;
        _detekce = VytvorSezeni(Path.Combine(slozkaModelu, "det.onnx"), zarizeni, out var popis);
        _rozpoznani = VytvorSezeni(Path.Combine(slozkaModelu, "rec.onnx"), zarizeni, out _);
        PouziteZarizeni = popis;
        NaGrafice = !popis.StartsWith("procesor");
        _znaky = NactiZnaky(Path.Combine(slozkaModelu, "rec.yml"));
    }

    /// <summary>Popis zařízení, na kterém motor skutečně počítá (pro protokol a stavový řádek).</summary>
    public string PouziteZarizeni { get; private set; }

    /// <summary>Motor počítá na grafické kartě.</summary>
    public bool NaGrafice { get; private set; }

    /// <summary>
    /// Spustí výpočet, a když grafická karta selže (typicky jí dojde paměť), přepne motor
    /// natrvalo na procesor a výpočet zopakuje. Grafika slabého počítače tak nezastaví přepis.
    /// </summary>
    private T SGrafikouNeboBez<T>(Func<T> vypocet)
    {
        try
        {
            return vypocet();
        }
        catch (OnnxRuntimeException e) when (NaGrafice)
        {
            _detekce.Dispose();
            _rozpoznani.Dispose();
            _detekce = VytvorSezeni(Path.Combine(_slozkaModelu, "det.onnx"), ZarizeniOcr.Procesor, out _);
            _rozpoznani = VytvorSezeni(Path.Combine(_slozkaModelu, "rec.onnx"), ZarizeniOcr.Procesor, out _);
            NaGrafice = false;
            var duvod = e.Message.Split('\n')[0];
            PouziteZarizeni = $"procesor (grafická karta selhala: {duvod[(duvod.LastIndexOf(')') + 1)..].Trim()})";
            return vypocet();
        }
    }

    /// <summary>Zaokrouhlovat šířku řádků v dávce (na grafice zapnuto, pro srovnání jde vynutit proměnnou KONCIPIENT_ZAOKROUHLIT=1).</summary>
    private bool ZaokrouhlitSirku => NaGrafice || Environment.GetEnvironmentVariable("KONCIPIENT_ZAOKROUHLIT") == "1";

    /// <summary>
    /// Proměnná KONCIPIENT_GPU: „ne“ = jen procesor, „0“ nebo „1“ = grafická karta podle pořadí
    /// ve Windows, jinak automaticky nejvýkonnější.
    /// </summary>
    private static ZarizeniOcr ZarizeniZProstredi() => Environment.GetEnvironmentVariable("KONCIPIENT_GPU")?.Trim().ToLowerInvariant() switch
    {
        "ne" or "cpu" or "procesor" => ZarizeniOcr.Procesor,
        "0" => ZarizeniOcr.Grafika0,
        "1" => ZarizeniOcr.Grafika1,
        _ => ZarizeniOcr.NejvykonnejsiGrafika,
    };

    private static InferenceSession VytvorSezeni(string cesta, ZarizeniOcr zarizeni, out string popis)
    {
#if WINDOWS
        if (zarizeni != ZarizeniOcr.Procesor)
        {
            try
            {
                // DirectML vyžaduje sekvenční běh a vypnuté plánování paměti
                var g = new SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    EnableMemoryPattern = false,
                    ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                };
                if (zarizeni == ZarizeniOcr.NejvykonnejsiGrafika)
                {
                    try
                    {
                        g.AppendExecutionProvider("DML", new Dictionary<string, string>
                        {
                            ["performance_preference"] = "high_performance",
                            ["device_filter"] = "gpu",
                        });
                        popis = "grafická karta (nejvýkonnější, DirectML)";
                    }
                    catch (Exception)
                    {
                        g.AppendExecutionProvider_DML(0);
                        popis = "grafická karta 0 (DirectML)";
                    }
                }
                else
                {
                    int cislo = zarizeni == ZarizeniOcr.Grafika1 ? 1 : 0;
                    g.AppendExecutionProvider_DML(cislo);
                    popis = $"grafická karta {cislo} (DirectML)";
                }
                return new InferenceSession(cesta, g);
            }
            catch (Exception e)
            {
                popis = $"procesor (grafická karta nejde použít: {e.Message})";
                return new InferenceSession(cesta, new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL });
            }
        }
#endif
        popis = "procesor";
        return new InferenceSession(cesta, new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL });
    }

    /// <summary>
    /// Největší strana obrázku pro hledání řádků. Fotky z telefonu (4000 bodů) jsou
    /// na hledání řádků zbytečně velké a na slabém notebooku pomalé; samotné čtení
    /// textu dál používá plné rozlišení výřezů.
    /// </summary>
    public int MaxStranaDetekce { get; init; } =
        int.TryParse(Environment.GetEnvironmentVariable("KONCIPIENT_DETEKCE"), out var n) && n >= 640 ? n : 2000;

    /// <summary>Název výstupní složky; pro srovnání zařízení v konzolovém testu jde změnit.</summary>
    public string Nazev { get; init; } = "paddle-cs";
    public bool ChceUpravenouFotku => true;

    public VysledekOcr Rozpoznej(string cestaKFotce) => SGrafikouNeboBez(() => RozpoznejJednou(cestaKFotce));

    private VysledekOcr RozpoznejJednou(string cestaKFotce)
    {
        using var obrazek = Cv2.ImRead(cestaKFotce, ImreadModes.Color);
        if (obrazek.Empty())
            throw new InvalidOperationException($"Fotku {cestaKFotce} nejde načíst.");

        var ramecky = SeradRamecky(NajdiRadky(obrazek));
        var vyrezy = ramecky.Select(r => Vyrizni(obrazek, r)).ToList();
        try
        {
            var znaky = RozpoznejVyrezy(vyrezy.Select(v => v.Obrazek).ToList());
            var vysledek = new VysledekOcr { Motor = Nazev, Sirka = obrazek.Cols, Vyska = obrazek.Rows };
            for (int i = 0; i < ramecky.Count; i++)
            {
                var radek = SlozRadek(ramecky[i], vyrezy[i], znaky[i]);
                if (radek.Slova.Count > 0) vysledek.Radky.Add(radek);
            }
            return vysledek;
        }
        finally
        {
            foreach (var v in vyrezy) v.Obrazek.Dispose();
        }
    }

    /// <summary>
    /// Zjistí, o kolik stupňů po směru hodinových ručiček je potřeba fotku otočit,
    /// aby text stál rovně (0, 90, 180 nebo 270). Nepotřebuje údaje z fotoaparátu:
    /// podle tvaru řádků pozná, jestli text běží vodorovně nebo svisle, a nejdelší
    /// řádky zkusí přečíst normálně i vzhůru nohama. Vyhraje směr s vyšší jistotou.
    /// </summary>
    public int ZjistiOtoceni(string cestaKFotce) => SGrafikouNeboBez(() => ZjistiOtoceniJednou(cestaKFotce));

    private int ZjistiOtoceniJednou(string cestaKFotce)
    {
        using var puvodni = Cv2.ImRead(cestaKFotce, ImreadModes.Color);
        if (puvodni.Empty())
            throw new InvalidOperationException($"Fotku {cestaKFotce} nejde načíst.");
        // na určení směru stačí menší obrázek, je to rychlejší
        double pomer = Math.Min(1.0, 1000.0 / Math.Max(puvodni.Rows, puvodni.Cols));
        using var obrazek = new Mat();
        Cv2.Resize(puvodni, obrazek, new Size(), pomer, pomer);

        var ramecky = NajdiRadky(obrazek);
        if (ramecky.Count < 3) return 0;
        int vodorovne = 0, svisle = 0;
        foreach (var b in ramecky)
        {
            double w = Vzdalenost(b[0], b[1]), h = Vzdalenost(b[0], b[3]);
            if (w > 1.5 * h) vodorovne++;
            else if (h > 1.5 * w) svisle++;
        }
        bool jeSvisle = svisle > vodorovne;

        // nejdelší řádky ve směru textu
        var vybrane = ramecky
            .OrderByDescending(b => Math.Max(Vzdalenost(b[0], b[1]), Vzdalenost(b[0], b[3])))
            .Take(10).ToList();
        var vyrezy = vybrane.Select(b => Vyrizni(obrazek, b).Obrazek).ToList();
        var obracene = vyrezy.Select(v => { var o = new Mat(); Cv2.Rotate(v, o, RotateFlags.Rotate180); return o; }).ToList();
        try
        {
            double rovne = Jistota(RozpoznejVyrezy(vyrezy));
            double vzhuruNohama = Jistota(RozpoznejVyrezy(obracene));
            // Svislé výřezy Vyrizni otáčí o 90° proti směru hodinových ručiček.
            // Čte-li se takový výřez dobře, fotku je třeba otočit stejně, tj. o 270° po směru.
            if (jeSvisle) return rovne >= vzhuruNohama ? 270 : 90;
            return rovne >= vzhuruNohama ? 0 : 180;
        }
        finally
        {
            foreach (var m in vyrezy.Concat(obracene)) m.Dispose();
        }
    }

    /// <summary>Součet průměrné jistoty znaků přes řádky, delší řádky váží víc.</summary>
    private static double Jistota(List<Znak>[] radky) =>
        radky.Sum(r => r.Count == 0 ? 0 : r.Average(z => z.Jistota) * Math.Min(r.Count, 40));

    /// <summary>Rozpoznaný znak a jeho vodorovná poloha ve výřezu řádku (v pixelech výřezu).</summary>
    private readonly record struct Znak(string Text, double X, float Jistota);

    private sealed record Vyrez(Mat Obrazek, bool Otoceny);

    /// <summary>
    /// Rozdělí řádek na slova podle mezer. Hranice slova leží v půli mezi
    /// posledním znakem a mezerou, poloha se přepočte z výřezu zpět na fotku.
    /// </summary>
    private static RadekOcr SlozRadek(Point2f[] b, Vyrez vyrez, List<Znak> znaky)
    {
        int sirka = vyrez.Obrazek.Cols;
        var radek = new RadekOcr { Ramecek = Obdelnik.ZBodu(b.Select(p => ((double)p.X, (double)p.Y))) };

        // Úseky znaků mezi mezerami
        var useky = new List<(int Od, int Do)>();
        int start = -1;
        for (int i = 0; i <= znaky.Count; i++)
        {
            bool mezera = i == znaky.Count || string.IsNullOrWhiteSpace(znaky[i].Text);
            if (!mezera && start < 0) start = i;
            if (mezera && start >= 0) { useky.Add((start, i - 1)); start = -1; }
        }

        foreach (var (od, @do) in useky)
        {
            var text = string.Concat(znaky.Skip(od).Take(@do - od + 1).Select(z => z.Text));
            var jistota = znaky.Skip(od).Take(@do - od + 1).Min(z => z.Jistota);
            Obdelnik ramecek;
            if (vyrez.Otoceny)
            {
                ramecek = radek.Ramecek; // svislý řádek, slova nerozdělujeme
            }
            else
            {
                double x0 = od > 0 ? (znaky[od - 1].X + znaky[od].X) / 2 : 0;
                double x1 = @do < znaky.Count - 1 ? (znaky[@do].X + znaky[@do + 1].X) / 2 : sirka;
                double u0 = Math.Clamp(x0 / sirka, 0, 1), u1 = Math.Clamp(x1 / sirka, 0, 1);
                (double, double) Bod(double u, double v) => (
                    b[0].X + u * (b[1].X - b[0].X) + v * (b[3].X - b[0].X),
                    b[0].Y + u * (b[1].Y - b[0].Y) + v * (b[3].Y - b[0].Y));
                ramecek = Obdelnik.ZBodu(new[] { Bod(u0, 0), Bod(u1, 0), Bod(u1, 1), Bod(u0, 1) });
            }
            radek.Slova.Add(new SlovoOcr { Text = text, Ramecek = ramecek, Jistota = jistota });
        }
        return radek;
    }

    // ---------- Detekce řádků ----------

    private List<Point2f[]> NajdiRadky(Mat obrazek)
    {
        int h = obrazek.Rows, w = obrazek.Cols;
        double pomer = 1.0;
        if (Math.Max(h, w) > MaxStranaDetekce) pomer = (double)MaxStranaDetekce / Math.Max(h, w);
        int nh = Math.Max((int)Math.Round(h * pomer / 32) * 32, 32);
        int nw = Math.Max((int)Math.Round(w * pomer / 32) * 32, 32);

        using var zmenseny = new Mat();
        Cv2.Resize(obrazek, zmenseny, new Size(nw, nh));
        var vstup = NaTenzor(zmenseny, Prumer, Odchylka);

        using var vysledky = _detekce.Run(new[] { NamedOnnxValue.CreateFromTensor(_detekce.InputMetadata.Keys.First(), vstup) });
        var mapa = vysledky.First().AsTensor<float>();
        int mh = mapa.Dimensions[2], mw = mapa.Dimensions[3];

        using var pravdepodobnost = new Mat(mh, mw, MatType.CV_32FC1);
        var data = mapa.ToArray();
        pravdepodobnost.SetArray(data);
        using var binarni = new Mat();
        Cv2.Threshold(pravdepodobnost, binarni, PrahBinarizace, 255, ThresholdTypes.Binary);
        binarni.ConvertTo(binarni, MatType.CV_8UC1);

        Cv2.FindContours(binarni, out var obrysy, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);
        var ramecky = new List<Point2f[]>();
        foreach (var obrys in obrysy.Take(1000))
        {
            var (body, kratsi) = MinimalniRamecek(Cv2.MinAreaRect(obrys));
            if (kratsi < MinVelikost) continue;
            if (SkoreRamecku(pravdepodobnost, body) < PrahSkore) continue;

            var rozsireny = Rozsir(body);
            var (body2, kratsi2) = MinimalniRamecek(rozsireny);
            if (kratsi2 < MinVelikost + 2) continue;

            for (int i = 0; i < 4; i++)
            {
                body2[i].X = Math.Clamp((float)Math.Round(body2[i].X / mw * w), 0, w - 1);
                body2[i].Y = Math.Clamp((float)Math.Round(body2[i].Y / mh * h), 0, h - 1);
            }
            var sirka = (int)Vzdalenost(body2[0], body2[1]);
            var vyska = (int)Vzdalenost(body2[0], body2[3]);
            if (sirka <= 3 || vyska <= 3) continue;
            ramecky.Add(body2);
        }
        return ramecky;
    }

    /// <summary>Body obdélníku seřazené jako levý horní, pravý horní, pravý dolní, levý dolní.</summary>
    private static (Point2f[] body, float kratsi) MinimalniRamecek(RotatedRect r)
    {
        var b = r.Points().OrderBy(p => p.X).ToArray();
        Point2f lh, ld, ph, pd;
        if (b[1].Y > b[0].Y) { lh = b[0]; ld = b[1]; } else { lh = b[1]; ld = b[0]; }
        if (b[3].Y > b[2].Y) { ph = b[2]; pd = b[3]; } else { ph = b[3]; pd = b[2]; }
        return (new[] { lh, ph, pd, ld }, Math.Min(r.Size.Width, r.Size.Height));
    }

    private static float SkoreRamecku(Mat mapa, Point2f[] body)
    {
        int x0 = Math.Clamp((int)Math.Floor(body.Min(p => p.X)), 0, mapa.Cols - 1);
        int x1 = Math.Clamp((int)Math.Ceiling(body.Max(p => p.X)), 0, mapa.Cols - 1);
        int y0 = Math.Clamp((int)Math.Floor(body.Min(p => p.Y)), 0, mapa.Rows - 1);
        int y1 = Math.Clamp((int)Math.Ceiling(body.Max(p => p.Y)), 0, mapa.Rows - 1);
        using var maska = new Mat(y1 - y0 + 1, x1 - x0 + 1, MatType.CV_8UC1, Scalar.All(0));
        var posunute = body.Select(p => new Point((int)(p.X - x0), (int)(p.Y - y0))).ToArray();
        Cv2.FillPoly(maska, new[] { posunute }, Scalar.All(1));
        using var vyrez = new Mat(mapa, new Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1));
        return (float)Cv2.Mean(vyrez, maska).Val0;
    }

    /// <summary>
    /// Rozšíří rámeček o vzdálenost plocha × 1,5 / obvod na každou stranu.
    /// U obdélníku dává stejný výsledek jako pyclipper v PaddleOCR.
    /// </summary>
    private static RotatedRect Rozsir(Point2f[] body)
    {
        var plocha = Math.Abs(Cv2.ContourArea(body));
        var obvod = Cv2.ArcLength(body, true);
        var d = (float)(plocha * Rozsireni / obvod);
        var r = Cv2.MinAreaRect(body);
        return new RotatedRect(r.Center, new Size2f(r.Size.Width + 2 * d, r.Size.Height + 2 * d), r.Angle);
    }

    /// <summary>Pořadí čtení: shora dolů, na stejném řádku (do 10 px) zleva doprava.</summary>
    private static List<Point2f[]> SeradRamecky(List<Point2f[]> ramecky)
    {
        var s = ramecky.OrderBy(r => r[0].Y).ThenBy(r => r[0].X).ToList();
        for (int i = 0; i < s.Count - 1; i++)
        {
            for (int j = i; j >= 0; j--)
            {
                if (Math.Abs(s[j + 1][0].Y - s[j][0].Y) < 10 && s[j + 1][0].X < s[j][0].X)
                    (s[j], s[j + 1]) = (s[j + 1], s[j]);
                else
                    break;
            }
        }
        return s;
    }

    private static Vyrez Vyrizni(Mat obrazek, Point2f[] b)
    {
        int sirka = (int)Math.Max(Vzdalenost(b[0], b[1]), Vzdalenost(b[2], b[3]));
        int vyska = (int)Math.Max(Vzdalenost(b[0], b[3]), Vzdalenost(b[1], b[2]));
        var cil = new[] { new Point2f(0, 0), new Point2f(sirka, 0), new Point2f(sirka, vyska), new Point2f(0, vyska) };
        using var m = Cv2.GetPerspectiveTransform(b, cil);
        var vyrez = new Mat();
        Cv2.WarpPerspective(obrazek, vyrez, m, new Size(sirka, vyska), InterpolationFlags.Cubic, BorderTypes.Replicate);
        if ((double)vyrez.Rows / vyrez.Cols >= 1.5)
        {
            var otoceny = new Mat();
            Cv2.Rotate(vyrez, otoceny, RotateFlags.Rotate90Counterclockwise);
            vyrez.Dispose();
            return new Vyrez(otoceny, true);
        }
        return new Vyrez(vyrez, false);
    }

    // ---------- Rozpoznání textu ----------

    private List<Znak>[] RozpoznejVyrezy(List<Mat> vyrezy)
    {
        var vysledek = new List<Znak>[vyrezy.Count];
        var poradi = Enumerable.Range(0, vyrezy.Count)
            .OrderBy(i => (double)vyrezy[i].Cols / vyrezy[i].Rows).ToArray();

        for (int start = 0; start < poradi.Length; start += DavkaRadku)
        {
            var davka = poradi.Skip(start).Take(DavkaRadku).ToArray();
            double maxPomer = 320.0 / VyskaRadku;
            foreach (var i in davka)
                maxPomer = Math.Max(maxPomer, (double)vyrezy[i].Cols / vyrezy[i].Rows);
            int sirka = (int)(VyskaRadku * maxPomer);
            // Grafická karta překládá výpočet pro každý nový rozměr vstupu znovu, proto se
            // šířka zaokrouhlí nahoru na násobek 160 bodů (přidané místo je prázdné jako v PaddleOCR).
            if (ZaokrouhlitSirku) sirka = (sirka + 159) / 160 * 160;

            var vstup = new DenseTensor<float>(new[] { davka.Length, 3, VyskaRadku, sirka });
            var sirkyRadku = new int[davka.Length];
            for (int k = 0; k < davka.Length; k++)
                sirkyRadku[k] = VlozRadek(vyrezy[davka[k]], vstup, k, sirka);

            using var vysledky = _rozpoznani.Run(new[] { NamedOnnxValue.CreateFromTensor(_rozpoznani.InputMetadata.Keys.First(), vstup) });
            var p = vysledky.First().AsTensor<float>();
            for (int k = 0; k < davka.Length; k++)
            {
                // časový krok t odpovídá úseku šířky sirka/kroky ve vstupu modelu;
                // přepočet na pixely výřezu přes poměr šířky výřezu a zmenšeného řádku
                double meritko = (double)vyrezy[davka[k]].Cols / sirkyRadku[k];
                vysledek[davka[k]] = DekodujCtc(p, k, sirka, meritko);
            }
        }
        return vysledek;
    }

    /// <summary>Vloží řádek do dávky a vrátí jeho šířku po zmenšení.</summary>
    private static int VlozRadek(Mat vyrez, DenseTensor<float> vstup, int index, int maxSirka)
    {
        double pomer = (double)vyrez.Cols / vyrez.Rows;
        int sirka = Math.Min(maxSirka, (int)Math.Ceiling(VyskaRadku * pomer));
        using var zmenseny = new Mat();
        Cv2.Resize(vyrez, zmenseny, new Size(sirka, VyskaRadku));
        var px = Bajty(zmenseny);
        for (int y = 0; y < VyskaRadku; y++)
            for (int x = 0; x < sirka; x++)
            {
                int o = (y * sirka + x) * 3;
                for (int c = 0; c < 3; c++)
                    vstup[index, c, y, x] = (px[o + c] / 255f - 0.5f) / 0.5f;
            }
        // zbytek šířky zůstává 0, stejně jako v PaddleOCR
        return sirka;
    }

    private List<Znak> DekodujCtc(Tensor<float> p, int index, int sirkaVstupu, double meritko)
    {
        int kroky = p.Dimensions[1], tridy = p.Dimensions[2];
        double krok = (double)sirkaVstupu / kroky;
        var znaky = new List<Znak>();
        int predchozi = -1;
        for (int t = 0; t < kroky; t++)
        {
            int nejlepsi = 0;
            float max = float.MinValue;
            for (int c = 0; c < tridy; c++)
            {
                var v = p[index, t, c];
                if (v > max) { max = v; nejlepsi = c; }
            }
            if (nejlepsi != 0 && nejlepsi != predchozi)
            {
                var text = nejlepsi - 1 < _znaky.Length ? _znaky[nejlepsi - 1] : " ";
                znaky.Add(new Znak(text, (t + 0.5) * krok * meritko, max));
            }
            predchozi = nejlepsi;
        }
        // mezery na začátku a konci řádku nemají význam
        while (znaky.Count > 0 && string.IsNullOrWhiteSpace(znaky[0].Text)) znaky.RemoveAt(0);
        while (znaky.Count > 0 && string.IsNullOrWhiteSpace(znaky[^1].Text)) znaky.RemoveAt(znaky.Count - 1);
        return znaky;
    }

    // ---------- Pomocné ----------

    private static DenseTensor<float> NaTenzor(Mat bgr, float[] prumer, float[] odchylka)
    {
        int h = bgr.Rows, w = bgr.Cols;
        var t = new DenseTensor<float>(new[] { 1, 3, h, w });
        var px = Bajty(bgr);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int o = (y * w + x) * 3;
                for (int c = 0; c < 3; c++)
                    t[0, c, y, x] = (px[o + c] / 255f - prumer[c]) / odchylka[c];
            }
        return t;
    }

    /// <summary>Pixely obrázku BGR jako pole bajtů, řádek po řádku.</summary>
    private static byte[] Bajty(Mat bgr)
    {
        using var souvisly = bgr.Clone(); // Clone vrací vždy souvislou paměť
        var data = new byte[souvisly.Rows * souvisly.Cols * 3];
        System.Runtime.InteropServices.Marshal.Copy(souvisly.Data, data, 0, data.Length);
        return data;
    }

    private static float Vzdalenost(Point2f a, Point2f b) =>
        (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>Načte seznam znaků (PostProcess.character_dict) z inference.yml modelu rozpoznání.</summary>
    private static string[] NactiZnaky(string cestaYml)
    {
        var yaml = new YamlStream();
        using (var r = new StreamReader(cestaYml)) yaml.Load(r);
        var koren = (YamlMappingNode)yaml.Documents[0].RootNode;
        var post = (YamlMappingNode)koren.Children[new YamlScalarNode("PostProcess")];
        var seznam = (YamlSequenceNode)post.Children[new YamlScalarNode("character_dict")];
        return seznam.Children.Select(n => ((YamlScalarNode)n).Value ?? "").ToArray();
    }

    public void Dispose()
    {
        _detekce.Dispose();
        _rozpoznani.Dispose();
    }
}
