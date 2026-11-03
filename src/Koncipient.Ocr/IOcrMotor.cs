namespace Koncipient.Ocr;

/// <summary>Jeden OCR motor. Dostane cestu k fotce strany a vrátí přepsaný text po řádcích.</summary>
public interface IOcrMotor : IDisposable
{
    /// <summary>Krátký název, použije se i jako název výstupní složky.</summary>
    string Nazev { get; }

    /// <summary>Jestli motor chce fotku s vyrovnaným osvětlením (viz <see cref="UpravaFotky"/>).</summary>
    bool ChceUpravenouFotku { get; }

    string Prepis(string cestaKFotce);
}
