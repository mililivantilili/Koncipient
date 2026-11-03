using Tesseract;

namespace Koncipient.Ocr;

/// <summary>Tesseract s češtinou. Rychlý, nejméně chyb v souvislém textu, horší na čísla.</summary>
public sealed class TesseractMotor : IOcrMotor
{
    private readonly TesseractEngine _engine;

    /// <param name="slozkaTessdata">Složka se souborem ces.traineddata.</param>
    public TesseractMotor(string slozkaTessdata)
    {
        _engine = new TesseractEngine(slozkaTessdata, "ces", EngineMode.Default);
    }

    public string Nazev => "tesseract-cs";
    public bool ChceUpravenouFotku => true;

    public VysledekOcr Rozpoznej(string cestaKFotce)
    {
        using var obrazek = Pix.LoadFromFile(cestaKFotce);
        using var strana = _engine.Process(obrazek, PageSegMode.Auto);
        var vysledek = new VysledekOcr { Motor = Nazev, Sirka = obrazek.Width, Vyska = obrazek.Height };

        using var it = strana.GetIterator();
        it.Begin();
        RadekOcr? radek = null;
        do
        {
            if (it.IsAtBeginningOf(PageIteratorLevel.TextLine))
            {
                radek = new RadekOcr();
                if (it.TryGetBoundingBox(PageIteratorLevel.TextLine, out var r))
                    radek.Ramecek = new Obdelnik(r.X1, r.Y1, r.Width, r.Height);
                vysledek.Radky.Add(radek);
            }
            var text = it.GetText(PageIteratorLevel.Word)?.Trim();
            if (string.IsNullOrEmpty(text) || radek is null) continue;
            var slovo = new SlovoOcr { Text = text, Jistota = it.GetConfidence(PageIteratorLevel.Word) / 100f };
            if (it.TryGetBoundingBox(PageIteratorLevel.Word, out var w))
                slovo.Ramecek = new Obdelnik(w.X1, w.Y1, w.Width, w.Height);
            radek.Slova.Add(slovo);
        } while (it.Next(PageIteratorLevel.Word));

        vysledek.Radky.RemoveAll(r => r.Slova.Count == 0);
        return vysledek;
    }

    public void Dispose() => _engine.Dispose();
}
