using OpenCvSharp;

namespace Koncipient.Ocr;

/// <summary>Úprava fotky před OCR.</summary>
public static class UpravaFotky
{
    /// <summary>
    /// Odstraní stín a nerovnoměrné světlo: odhadne pozadí papíru a odečte ho.
    /// Stejný postup jako v ocr_test/spolecne.py. Na testovacích fotkách snížil
    /// chyby Tesseractu z 16–25 % na 1–3 % znaků.
    /// </summary>
    public static Mat VyrovnatOsvetleni(Mat barevna)
    {
        using var seda = new Mat();
        Cv2.CvtColor(barevna, seda, ColorConversionCodes.BGR2GRAY);
        using var jadro = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(15, 15));
        using var roztazena = new Mat();
        Cv2.Dilate(seda, roztazena, jadro);
        using var pozadi = new Mat();
        Cv2.MedianBlur(roztazena, pozadi, 51);
        using var rozdil = new Mat();
        Cv2.Absdiff(seda, pozadi, rozdil);
        using var obracena = new Mat();
        Cv2.BitwiseNot(rozdil, obracena);
        var vysledek = new Mat();
        Cv2.Normalize(obracena, vysledek, 0, 255, NormTypes.MinMax);
        return vysledek;
    }

    /// <summary>Uloží upravenou fotku jako PNG do zadané složky a vrátí cestu k ní.</summary>
    public static string UlozUpravenou(string cestaKFotce, string slozka)
    {
        Directory.CreateDirectory(slozka);
        var cil = Path.Combine(slozka, Path.GetFileNameWithoutExtension(cestaKFotce) + ".png");
        if (!File.Exists(cil))
        {
            using var puvodni = Cv2.ImRead(cestaKFotce, ImreadModes.Color);
            if (puvodni.Empty())
                throw new InvalidOperationException($"Fotku {cestaKFotce} nejde načíst.");
            using var upravena = VyrovnatOsvetleni(puvodni);
            Cv2.ImWrite(cil, upravena);
        }
        return cil;
    }
}
