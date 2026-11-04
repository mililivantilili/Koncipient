using System.Text.RegularExpressions;
using Koncipient.Ocr;
using WeCantSpell.Hunspell;

namespace Koncipient.Jadro;

/// <summary>
/// Sloučí přepis hlavního motoru (Windows OCR) s kontrolním (PaddleOCR). Základem je
/// hlavní motor, který čte souvislý text nejlépe; z kontrolního se berou čísla, paragrafy
/// a e-maily, které čte spolehlivěji, a slova, která hlavní motor zjevně zkomolil
/// (podle českého slovníku). Útržky z okraje fotky, které čte jen kontrolní motor, se nevkládají.
///
/// Pravidla jsou ověřená v ocr_test/slouceni.py na přepisech z Matějova notebooku:
/// proti samotnému Windows OCR méně chyb znaků (0,3 % × 0,5 %), více čísel v tabulce
/// (71 × 69 ze 73) a více přesně přepsaných citlivých údajů (32 × 29 z 34).
/// </summary>
public sealed class Slouceni
{
    public const string Nazev = "slouceno";

    private const string Interpunkce = ".,;:()[]\"'„“”«»!?-–—|";
    private static readonly Regex CisteCislo = new(@"^[\d.,/:\-–%+()]+$");

    private readonly WordList? _slovnik;

    /// <param name="slozkaModelu">Složka se slovníkem cs_CZ.aff a cs_CZ.dic. Bez slovníku rozhoduje jen hlavní motor a čísla.</param>
    public Slouceni(string slozkaModelu)
    {
        var aff = Path.Combine(slozkaModelu, "cs_CZ.aff");
        var dic = Path.Combine(slozkaModelu, "cs_CZ.dic");
        if (File.Exists(aff) && File.Exists(dic)) _slovnik = WordList.CreateFromFiles(dic, aff);
    }

    private static string Jadro(string slovo) => slovo.Trim(Interpunkce.ToCharArray());

    private static bool MaCislo(string text) => text.Any(char.IsDigit) || text.Contains('§') || text.Contains('@');

    private bool Platne(string slovo)
    {
        var j = Jadro(slovo);
        if (j.Length == 0) return false;
        // platné je jen čisté číslo (2,1 / 4136/3), ne směs jako „Těs1n“
        if (j.Any(char.IsDigit)) return CisteCislo.IsMatch(j);
        if (_slovnik is null) return true;
        return _slovnik.Check(j) || _slovnik.Check(j.ToLowerInvariant());
    }

    private double PodilPlatnych(IReadOnlyList<SlovoOcr> slova) => slova.Count == 0 ? 0 : slova.Count(s => Platne(s.Text)) / (double)slova.Count;

    private static string Klic(string slovo) => Jadro(slovo).ToLowerInvariant();

    /// <summary>Výstupní slovo i s řádkem hlavního motoru, do kterého patří.</summary>
    private readonly record struct Vystup(SlovoOcr Slovo, int Radek);

    public VysledekOcr Sluc(VysledekOcr hlavni, VysledekOcr kontrolni)
    {
        // Hlavní motor nic nepřečetl (prázdná stránka): útržek z kontrolního motoru, třeba „1“
        // v záhybu papíru, nemá ke kterému řádku patřit a na prázdné stránce bývá šum.
        if (!hlavni.Radky.Any(r => r.Slova.Count > 0))
            return new VysledekOcr { Motor = Nazev, Sirka = hlavni.Sirka, Vyska = hlavni.Vyska };
        var a = hlavni.Radky.SelectMany((r, i) => r.Slova.Select(s => (Slovo: s, Radek: i))).ToList();
        var b = kontrolni.Slova.ToList();
        var shody = NejdelsiSpolecna(a.Select(x => Klic(x.Slovo.Text)).ToList(), b.Select(s => Klic(s.Text)).ToList());

        var vystup = new List<Vystup>();
        int ia = 0, ib = 0;
        foreach (var (sa, sb) in shody.Append((a.Count, b.Count)))
        {
            ZpracujMezeru(a.GetRange(ia, sa - ia), b.GetRange(ib, sb - ib), vystup, ia > 0 ? a[ia - 1].Radek : 0);
            if (sa < a.Count) vystup.Add(new Vystup(Kopie(a[sa].Slovo), a[sa].Radek));
            ia = sa + 1;
            ib = sb + 1;
        }

        var vysledek = new VysledekOcr { Motor = Nazev, Sirka = hlavni.Sirka, Vyska = hlavni.Vyska };
        foreach (var skupina in vystup.GroupBy(v => v.Radek).OrderBy(g => g.Key))
            vysledek.Radky.Add(new RadekOcr { Ramecek = hlavni.Radky[skupina.Key].Ramecek, Slova = skupina.Select(v => v.Slovo).ToList() });
        return vysledek;
    }

    private void ZpracujMezeru(List<(SlovoOcr Slovo, int Radek)> a, List<SlovoOcr> b, List<Vystup> vystup, int predchoziRadek)
    {
        if (a.Count == 0 && b.Count == 0) return;
        var slovaA = a.Select(x => x.Slovo).ToList();
        int radek = a.Count > 0 ? a[0].Radek : predchoziRadek;

        if (a.Count > 0 && a.Count == b.Count)
        {
            // stejný počet slov: rozhodne se slovo po slovu, poloha zůstává z hlavního motoru
            for (int i = 0; i < a.Count; i++)
            {
                var vybrane = Vyber(a[i].Slovo, b[i]);
                vystup.Add(new Vystup(vybrane == a[i].Slovo ? Kopie(a[i].Slovo) : KopieSPolohou(b[i], a[i].Slovo), a[i].Radek));
            }
            return;
        }
        if (a.Count > 0 && b.Count > 0)
        {
            bool vzitB =
                // kontrolní motor přečetl číslo nebo §, kde hlavní má něco jiného (S místo §)
                (b.Count <= 3 && b.All(s => MaCislo(s.Text)) && !MaCislo(string.Concat(slovaA.Select(s => s.Text))))
                // nebo hlavní úsek je zjevně zkomolený a kontrolní ne
                || PodilPlatnych(b) > PodilPlatnych(slovaA);
            if (vzitB) foreach (var s in b) vystup.Add(new Vystup(Kopie(s), radek));
            else foreach (var x in a) vystup.Add(new Vystup(Kopie(x.Slovo), x.Radek));
            return;
        }
        if (a.Count > 0)
        {
            // jen v hlavním motoru: nechat, pokud nejde většinou o nesmysly
            if (PodilPlatnych(slovaA) >= 0.5) foreach (var x in a) vystup.Add(new Vystup(Kopie(x.Slovo), x.Radek));
            return;
        }
        // jen v kontrolním motoru: vložit jen krátké číslo, § nebo e-mail, které hlavní vynechal
        if (b.Count <= 3 && b.All(s => MaCislo(s.Text)))
            foreach (var s in b) vystup.Add(new Vystup(Kopie(s), radek));
    }

    private SlovoOcr Vyber(SlovoOcr a, SlovoOcr b)
    {
        if (MaCislo(a.Text) != MaCislo(b.Text)) return MaCislo(a.Text) ? a : b;
        bool pa = Platne(a.Text), pb = Platne(b.Text);
        if (pa != pb) return pa ? a : b;
        return MaCislo(a.Text + b.Text) ? b : a;
    }

    private static SlovoOcr Kopie(SlovoOcr s) => new() { Text = s.Text, Ramecek = s.Ramecek, Jistota = s.Jistota };

    private static SlovoOcr KopieSPolohou(SlovoOcr text, SlovoOcr poloha) =>
        new() { Text = text.Text, Ramecek = poloha.Ramecek, Jistota = text.Jistota };

    /// <summary>Dvojice indexů shodných slov (nejdelší společná posloupnost), vzestupně.</summary>
    private static List<(int A, int B)> NejdelsiSpolecna(List<string> a, List<string> b)
    {
        int n = a.Count, m = b.Count;
        var d = new ushort[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
            for (int j = m - 1; j >= 0; j--)
                d[i, j] = a[i].Length > 0 && a[i] == b[j] ? (ushort)(d[i + 1, j + 1] + 1) : Math.Max(d[i + 1, j], d[i, j + 1]);
        var vysledek = new List<(int, int)>();
        for (int i = 0, j = 0; i < n && j < m;)
        {
            if (a[i].Length > 0 && a[i] == b[j]) { vysledek.Add((i, j)); i++; j++; }
            else if (d[i + 1, j] >= d[i, j + 1]) i++;
            else j++;
        }
        return vysledek;
    }
}
