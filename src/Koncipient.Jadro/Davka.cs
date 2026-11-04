using Koncipient.Ocr;

namespace Koncipient.Jadro;

public sealed record StavDavky(int Hotovo, int Celkem, string Popis);

/// <summary>Strana, kterou dávka nezvládla. Dávka pokračuje dalšími, chyba se ukáže na konci.</summary>
public sealed record ChybaStrany(string Spis, int Strana, string Krok, string Zprava)
{
    public override string ToString() => Strana > 0 ? $"{Spis}, strana {Strana} ({Krok}): {Zprava}" : $"{Spis} {Krok}: {Zprava}".Trim();
}

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
    /// <returns>Strany, které se nepodařilo zpracovat (prázdné = vše v pořádku).</returns>
    public static Task<IReadOnlyList<ChybaStrany>> ZpracujAsync(
        IReadOnlyList<Spis> spisy,
        IReadOnlyList<Func<IOcrMotor>> tovarny,
        IProgress<StavDavky>? prubeh = null,
        CancellationToken zruseni = default,
        Func<IUrceniOtoceni>? narovnani = null)
    {
        // OCR je náročné na procesor, poběží mimo vlákno okna
        return Task.Run(() => Zpracuj(spisy, tovarny, prubeh, zruseni, narovnani), zruseni);
    }

    private static IReadOnlyList<ChybaStrany> Zpracuj(IReadOnlyList<Spis> spisy, IReadOnlyList<Func<IOcrMotor>> tovarny,
        IProgress<StavDavky>? prubeh, CancellationToken zruseni, Func<IUrceniOtoceni>? narovnani)
    {
        var chyby = new List<ChybaStrany>();
        // Jedna vadná strana nesmí zastavit noční dávku: chyba se zapíše a jde se dál.
        // Strana bez výsledku se při dalším spuštění zkusí znovu.
        void Zkus(Spis spis, Strana strana, string krok, Action akce)
        {
            try { akce(); }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) { chyby.Add(new ChybaStrany(spis.Id, spis.Poradi(strana), krok, e.Message)); }
        }

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
                Zkus(spis, strana, "narovnání", () =>
                {
                    var stupne = urceni.ZjistiOtoceni(spis.CestaFotky(strana));
                    if (stupne != 0) spis.OtocStranu(strana, stupne);
                    else { strana.Narovnano = true; spis.Uloz(); }
                });
            }
        }

        foreach (var tovarna in tovarny)
        {
            IOcrMotor motor;
            try { motor = tovarna(); }
            catch (Exception e)
            {
                // motor nejde spustit (chybí knihovna, model): ostatní motory pokračují
                chyby.Add(new ChybaStrany("", 0, "spuštění motoru OCR", e.InnerException?.Message ?? e.Message));
                continue;
            }
            using var _ = motor;
            var zbyva = spisy.SelectMany(sp => sp.Strany.Select(st => (sp, st)))
                .Where(x => !x.sp.MaOcr(x.st, motor.Nazev) && !x.sp.MaOcr(x.st, Motory.PdfText)).ToList();
            for (int i = 0; i < zbyva.Count; i++)
            {
                zruseni.ThrowIfCancellationRequested();
                var (spis, strana) = zbyva[i];
                var kde = motor is PaddleMotor pm ? $" na {pm.PouziteZarizeni}" : "";
                prubeh?.Report(new StavDavky(i, zbyva.Count, $"{motor.Nazev}{kde}: {spis.Id} strana {strana.Cislo}"));
                Zkus(spis, strana, motor.Nazev, () =>
                {
                    var vstup = motor.ChceUpravenouFotku ? spis.UpravenaFotka(strana) : spis.CestaFotky(strana);
                    spis.UlozOcr(strana, motor.Rozpoznej(vstup));
                });
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
                Zkus(spis, strana, "sloučení", () =>
                    spis.UlozOcr(strana, slouceni.Sluc(spis.NactiOcr(strana, Motory.Windows)!, spis.NactiOcr(strana, Motory.Paddle)!)));
            }
        }

        // dokumenty bez názvu (oddělené černou fotkou) dostanou název z prvního textu
        foreach (var spis in spisy)
            try { spis.NavrhniNazvy(); }
            catch (Exception e) { chyby.Add(new ChybaStrany(spis.Id, 0, "návrh názvů", e.Message)); }
        return chyby;
    }
}
