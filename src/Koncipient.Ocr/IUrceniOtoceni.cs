using OpenCvSharp;

namespace Koncipient.Ocr;

/// <summary>Pozná, jak je fotka strany otočená, i bez údajů z fotoaparátu (EXIF).</summary>
public interface IUrceniOtoceni : IDisposable
{
    /// <summary>O kolik stupňů po směru hodinových ručiček fotku otočit: 0, 90, 180 nebo 270.</summary>
    int ZjistiOtoceni(string cestaKFotce);
}

public static class Otoceni
{
    /// <summary>Otočí fotku na disku o 90, 180 nebo 270 stupňů po směru hodinových ručiček.</summary>
    public static void OtocSoubor(string cesta, int stupne)
    {
        var smer = (((stupne % 360) + 360) % 360) switch
        {
            0 => (RotateFlags?)null,
            90 => RotateFlags.Rotate90Clockwise,
            180 => RotateFlags.Rotate180,
            270 => RotateFlags.Rotate90Counterclockwise,
            _ => throw new ArgumentException("Otočit jde jen o násobky 90 stupňů.", nameof(stupne)),
        };
        if (smer is null) return;
        using var obrazek = Cv2.ImRead(cesta, ImreadModes.Color);
        using var otoceny = new Mat();
        Cv2.Rotate(obrazek, otoceny, smer.Value);
        Cv2.ImWrite(cesta, otoceny, new ImageEncodingParam(ImwriteFlags.JpegQuality, 95));
    }
}
