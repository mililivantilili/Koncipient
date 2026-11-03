using Koncipient.Ocr;

namespace Koncipient.Jadro;

public sealed record StavDavky(int Hotovo, int Celkem, string Popis);

/// <summary>
/// Dávkové OCR, třeba přes noc. Přeskočí strany, které už výsledek mají, takže
/// po přerušení stačí spustit znovu a pokračuje se.
/// </summary>
public static class Davka
{
    /// <param name="narovnani">
    /// Určení otočení pro strany, které ještě nejsou narovnané (fotky z foťáku bez EXIF).
    /// Proběhne před přepisem. Null znamená neotáčet.
    /// </param>
    public static Task ZpracujAsync(
        IReadOnlyList<Spis> spisy,
        IReadOnlyList<Func<IOcrMotor>> tovarny,
        IProgress<StavDavky>? prubeh = null,
        CancellationToken zruseni = default,
        Func<IUrceniOtoceni>? narovnani = null)
    {
        // OCR je náročné na procesor, poběží mimo vlákno okna
        return Task.Run(() => Zpracuj(spisy, tovarny, prubeh, zruseni, narovnani), zruseni);
    }

    private static void Zpracuj(IReadOnlyList<Spis> spisy, IReadOnlyList<Func<IOcrMotor>> tovarny,
        IProgress<StavDavky>? prubeh, CancellationToken zruseni, Func<IUrceniOtoceni>? narovnani)
    {
        var nenarovnane = spisy.SelectMany(sp => sp.Strany.Select(st => (sp, st)))
            .Where(x => !x.st.Narovnano).ToList();
        if (narovnani is not null && nenarovnane.Count > 0)
        {
            using var urceni = narovnani();
            for (int i = 0; i < nenarovnane.Count; i++)
            {
                zruseni.ThrowIfCancellationRequested();
                var (spis, strana) = nenarovnane[i];
                prubeh?.Report(new StavDavky(i, nenarovnane.Count, $"Narovnávám: {spis.Id} strana {strana.Cislo}"));
                var stupne = urceni.ZjistiOtoceni(spis.CestaFotky(strana));
                if (stupne != 0) spis.OtocStranu(strana, stupne);
                else { strana.Narovnano = true; spis.Uloz(); }
            }
        }

        foreach (var tovarna in tovarny)
        {
            using var motor = tovarna();
            var zbyva = spisy.SelectMany(sp => sp.Strany.Select(st => (sp, st)))
                .Where(x => !x.sp.MaOcr(x.st, motor.Nazev)).ToList();
            for (int i = 0; i < zbyva.Count; i++)
            {
                zruseni.ThrowIfCancellationRequested();
                var (spis, strana) = zbyva[i];
                var kde = motor is PaddleMotor pm ? $" na {pm.PouziteZarizeni}" : "";
                prubeh?.Report(new StavDavky(i, zbyva.Count, $"{motor.Nazev}{kde}: {spis.Id} strana {strana.Cislo}"));
                var vstup = motor.ChceUpravenouFotku ? spis.UpravenaFotka(strana) : spis.CestaFotky(strana);
                spis.UlozOcr(strana, motor.Rozpoznej(vstup));
            }
            prubeh?.Report(new StavDavky(zbyva.Count, zbyva.Count, $"{motor.Nazev}: hotovo"));
        }

        // Sloučení Windows OCR (hlavní) a PaddleOCR (kontrolní) do jednoho textu
        var kSlouceni = spisy.SelectMany(sp => sp.Strany.Select(st => (sp, st)))
            .Where(x => !x.sp.MaOcr(x.st, Slouceni.Nazev) && x.sp.MaOcr(x.st, Motory.Windows) && x.sp.MaOcr(x.st, Motory.Paddle))
            .ToList();
        if (kSlouceni.Count > 0)
        {
            var slouceni = new Slouceni(Motory.SlozkaModelu);
            for (int i = 0; i < kSlouceni.Count; i++)
            {
                zruseni.ThrowIfCancellationRequested();
                var (spis, strana) = kSlouceni[i];
                prubeh?.Report(new StavDavky(i, kSlouceni.Count, $"Slučuji přepisy: {spis.Id} strana {strana.Cislo}"));
                spis.UlozOcr(strana, slouceni.Sluc(spis.NactiOcr(strana, Motory.Windows)!, spis.NactiOcr(strana, Motory.Paddle)!));
            }
        }
    }
}
