namespace Koncipient.Ocr;

/// <summary>Jeden OCR motor. Dostane cestu k fotce strany a vrátí řádky a slova s polohou.</summary>
public interface IOcrMotor : IDisposable
{
    /// <summary>Krátký název, použije se i jako název výstupní složky.</summary>
    string Nazev { get; }

    /// <summary>Jestli motor chce fotku s vyrovnaným osvětlením (viz <see cref="UpravaFotky"/>).</summary>
    bool ChceUpravenouFotku { get; }

    /// <summary>
    /// Přepíše fotku. Polohy slov jsou v pixelech této fotky. Upravená fotka
    /// má stejné rozměry jako původní, takže polohy platí i pro ni.
    /// </summary>
    VysledekOcr Rozpoznej(string cestaKFotce);
}
