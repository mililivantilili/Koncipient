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

    public string Prepis(string cestaKFotce)
    {
        using var obrazek = Pix.LoadFromFile(cestaKFotce);
        using var strana = _engine.Process(obrazek, PageSegMode.Auto);
        return strana.GetText();
    }

    public void Dispose() => _engine.Dispose();
}
