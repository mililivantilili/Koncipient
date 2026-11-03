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
public sealed class PaddleMotor : IOcrMotor
{
    // Výchozí hodnoty z paddlex/configs/pipelines/OCR.yaml
    private const int MaxStrana = 4000;
    private const float PrahBinarizace = 0.3f;
    private const float PrahSkore = 0.6f;
    private const float Rozsireni = 1.5f;
    private const int MinVelikost = 3;
    private const int VyskaRadku = 48;
    private const int DavkaRadku = 6;

    private static readonly float[] Prumer = { 0.485f, 0.456f, 0.406f };
    private static readonly float[] Odchylka = { 0.229f, 0.224f, 0.225f };

    private readonly InferenceSession _detekce;
    private readonly InferenceSession _rozpoznani;
    private readonly string[] _znaky;

    /// <param name="slozkaModelu">
    /// Složka se soubory det.onnx, rec.onnx a rec.yml
    /// (z Hugging Face: PaddlePaddle/PP-OCRv6_medium_det_onnx a _rec_onnx).
    /// </param>
    public PaddleMotor(string slozkaModelu)
    {
        var volby = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
        _detekce = new InferenceSession(Path.Combine(slozkaModelu, "det.onnx"), volby);
        _rozpoznani = new InferenceSession(Path.Combine(slozkaModelu, "rec.onnx"), volby);
        _znaky = NactiZnaky(Path.Combine(slozkaModelu, "rec.yml"));
    }

    public string Nazev => "paddle-cs";
    public bool ChceUpravenouFotku => true;

    public string Prepis(string cestaKFotce)
    {
        using var obrazek = Cv2.ImRead(cestaKFotce, ImreadModes.Color);
        if (obrazek.Empty())
            throw new InvalidOperationException($"Fotku {cestaKFotce} nejde načíst.");

        var ramecky = SeradRamecky(NajdiRadky(obrazek));
        var vyrezy = ramecky.Select(r => Vyrizni(obrazek, r)).ToList();
        try
        {
            var texty = Rozpoznej(vyrezy);
            return string.Join("\n", texty.Where(t => t.Length > 0));
        }
        finally
        {
            foreach (var v in vyrezy) v.Dispose();
        }
    }

    // ---------- Detekce řádků ----------

    private List<Point2f[]> NajdiRadky(Mat obrazek)
    {
        int h = obrazek.Rows, w = obrazek.Cols;
        double pomer = 1.0;
        if (Math.Max(h, w) > MaxStrana) pomer = (double)MaxStrana / Math.Max(h, w);
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

    private static Mat Vyrizni(Mat obrazek, Point2f[] b)
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
            return otoceny;
        }
        return vyrez;
    }

    // ---------- Rozpoznání textu ----------

    private string[] Rozpoznej(List<Mat> vyrezy)
    {
        var texty = new string[vyrezy.Count];
        var poradi = Enumerable.Range(0, vyrezy.Count)
            .OrderBy(i => (double)vyrezy[i].Cols / vyrezy[i].Rows).ToArray();

        for (int start = 0; start < poradi.Length; start += DavkaRadku)
        {
            var davka = poradi.Skip(start).Take(DavkaRadku).ToArray();
            double maxPomer = 320.0 / VyskaRadku;
            foreach (var i in davka)
                maxPomer = Math.Max(maxPomer, (double)vyrezy[i].Cols / vyrezy[i].Rows);
            int sirka = (int)(VyskaRadku * maxPomer);

            var vstup = new DenseTensor<float>(new[] { davka.Length, 3, VyskaRadku, sirka });
            for (int k = 0; k < davka.Length; k++)
                VlozRadek(vyrezy[davka[k]], vstup, k, sirka);

            using var vysledky = _rozpoznani.Run(new[] { NamedOnnxValue.CreateFromTensor(_rozpoznani.InputMetadata.Keys.First(), vstup) });
            var p = vysledky.First().AsTensor<float>();
            for (int k = 0; k < davka.Length; k++)
                texty[davka[k]] = DekodujCtc(p, k);
        }
        return texty;
    }

    private static void VlozRadek(Mat vyrez, DenseTensor<float> vstup, int index, int maxSirka)
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
    }

    private string DekodujCtc(Tensor<float> p, int index)
    {
        int kroky = p.Dimensions[1], tridy = p.Dimensions[2];
        var sb = new System.Text.StringBuilder();
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
                sb.Append(nejlepsi - 1 < _znaky.Length ? _znaky[nejlepsi - 1] : " ");
            predchozi = nejlepsi;
        }
        return sb.ToString().Trim();
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
