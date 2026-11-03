using Koncipient.Ocr;

namespace Koncipient.Jadro;

public sealed record StavDavky(int Hotovo, int Celkem, string Popis);

/// <summary>
/// Dávkové OCR, třeba přes noc. Přeskočí strany, které už výsledek mají, takže
/// po přerušení stačí spustit znovu a pokračuje se.
/// </summary>
public static class Davka
{
    public static Task ZpracujAsync(
        IReadOnlyList<Spis> spisy,
        IReadOnlyList<Func<IOcrMotor>> tovarny,
        IProgress<StavDavky>? prubeh = null,
        CancellationToken zruseni = default)
    {
        // OCR je náročné na procesor, poběží mimo vlákno okna
        return Task.Run(() => Zpracuj(spisy, tovarny, prubeh, zruseni), zruseni);
    }

    private static void Zpracuj(IReadOnlyList<Spis> spisy, IReadOnlyList<Func<IOcrMotor>> tovarny,
        IProgress<StavDavky>? prubeh, CancellationToken zruseni)
    {
        foreach (var tovarna in tovarny)
        {
            using var motor = tovarna();
            var zbyva = spisy.SelectMany(sp => sp.Strany.Select(st => (sp, st)))
                .Where(x => !x.sp.MaOcr(x.st, motor.Nazev)).ToList();
            for (int i = 0; i < zbyva.Count; i++)
            {
                zruseni.ThrowIfCancellationRequested();
                var (spis, strana) = zbyva[i];
                prubeh?.Report(new StavDavky(i, zbyva.Count, $"{motor.Nazev}: {spis.Id} strana {strana.Cislo}"));
                var vstup = motor.ChceUpravenouFotku ? spis.UpravenaFotka(strana) : spis.CestaFotky(strana);
                spis.UlozOcr(strana, motor.Rozpoznej(vstup));
            }
            prubeh?.Report(new StavDavky(zbyva.Count, zbyva.Count, $"{motor.Nazev}: hotovo"));
        }
    }
}
