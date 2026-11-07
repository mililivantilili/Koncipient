namespace Koncipient.Jadro;

/// <summary>Stav jedné strany pro barevnou značku v seznamu stran.</summary>
public enum StavStrany
{
    /// <summary>Ještě nepřepsaná (šedá).</summary>
    Neprepsana,
    /// <summary>Přepsaná, čeká na kontrolu (oranžová).</summary>
    Prepsana,
    /// <summary>Zkontrolovaná člověkem (zelená).</summary>
    Zkontrolovana,
    /// <summary>Vyšla špatně, je lepší ji přefotit (červená).</summary>
    KPrefoceni,
}

/// <summary>Co má advokátka udělat dál; průvodce nad oknem k tomu nabídne tlačítko.</summary>
public enum DalsiKrok { PridatFotky, SpustitPrepis, Prefotit, Zkontrolovat, Exportovat, PrenestNaDisk }

/// <summary>Přehled spisu pro průvodce: kolik je hotovo a co dál.</summary>
public sealed record PrehledSpisu(int Stran, int Prepsanych, int Zkontrolovanych, int KPrefoceni,
    DateTime? PosledniExport, int NeexportovanychDokumentu, DalsiKrok Dalsi)
{
    public static StavStrany Stav(Spis spis, Strana s) =>
        s.DuvodyPrefoceni is { Count: > 0 } && !s.Zkontrolovano ? StavStrany.KPrefoceni
        : s.Zkontrolovano ? StavStrany.Zkontrolovana
        : Motory.PoradiZobrazeni.Any(m => spis.MaOcr(s, m)) ? StavStrany.Prepsana
        : StavStrany.Neprepsana;

    public static PrehledSpisu Spocitej(Spis spis)
    {
        var stavy = spis.Strany.Select(s => Stav(spis, s)).ToList();
        int prepsanych = stavy.Count(x => x != StavStrany.Neprepsana);
        int zkontrolovanych = stavy.Count(x => x == StavStrany.Zkontrolovana);
        int kPrefoceni = stavy.Count(x => x == StavStrany.KPrefoceni);
        var posledni = ZaznamyExportu.Nacti(spis).LastOrDefault(z => z.Akce == "export")?.Kdy;
        var posledniDisk = ZaznamyExportu.Nacti(spis).LastOrDefault(z => z.Akce == "flash disk")?.Kdy;
        int neexportovanych = spis.Dokumenty.Count(d => d.Exportovano is null);

        DalsiKrok dalsi =
            stavy.Count == 0 ? DalsiKrok.PridatFotky
            : prepsanych < stavy.Count ? DalsiKrok.SpustitPrepis
            : kPrefoceni > 0 ? DalsiKrok.Prefotit
            : zkontrolovanych < stavy.Count ? DalsiKrok.Zkontrolovat
            : neexportovanych > 0 || posledni is null ? DalsiKrok.Exportovat
            : posledniDisk is null || posledniDisk < posledni ? DalsiKrok.PrenestNaDisk
            : DalsiKrok.PridatFotky; // vše hotovo: doplnění spisu začne znovu přidáním fotek
        return new PrehledSpisu(stavy.Count, prepsanych, zkontrolovanych, kPrefoceni, posledni, neexportovanych, dalsi);
    }

    /// <summary>Text tlačítka „co dál“.</summary>
    public string TextDalsiho => Dalsi switch
    {
        DalsiKrok.PridatFotky when Stran == 0 => "Přidat fotky nebo PDF",
        DalsiKrok.PridatFotky => "Hotovo. Doplnit spis o další fotky",
        DalsiKrok.SpustitPrepis => $"Spustit přepis ({Stran - Prepsanych} stran)",
        DalsiKrok.Prefotit => $"Přefotit {KPrefoceni} stran",
        DalsiKrok.Zkontrolovat => $"Zkontrolovat ({Stran - Zkontrolovanych} stran)",
        DalsiKrok.Exportovat => NeexportovanychDokumentu > 0 && PosledniExport is not null
            ? $"Exportovat nové dokumenty ({NeexportovanychDokumentu})" : "Exportovat pro Clauda",
        DalsiKrok.PrenestNaDisk => "Zkopírovat na flash disk",
        _ => "",
    };
}
