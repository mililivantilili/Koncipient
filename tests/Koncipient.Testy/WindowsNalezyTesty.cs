using Koncipient.Jadro;
using Koncipient.Jadro.Anonymizace;
using Koncipient.Ocr;
using OpenCvSharp;
using Xunit;

namespace Koncipient.Testy;

/// <summary>Chyby z testů na Windows (docs/vysledky-testu-windows.md), napodobené na vymyšlených příkladech.</summary>
public class WindowsNalezyTesty
{
    private static Klic KlicZ(params string[] texty)
    {
        var klic = new Klic { Styl = StylPseudonymu.Kody };
        var vsechna = new List<string>();
        foreach (var text in texty)
        {
            var ocr = Vzor.Ocr(text);
            var slova = Anonymizator.Slova(ocr);
            vsechna.AddRange(slova);
            foreach (var n in Detektor.Najdi(slova, Anonymizator.Radky(ocr)))
            {
                var usek = slova.Skip(n.Od).Take(n.Do - n.Od + 1).ToList();
                var e = Anonymizator.NajdiEntitu(klic, n.Typ, slova, n.Od, n.Do);
                if (e is null) klic.Nova(n.Typ, usek);
                else e.Tvary.Add(usek.Select(Text.Ocisti).ToList());
            }
        }
        Anonymizator.DoplnOsoby(klic, vsechna);
        return klic;
    }

    private static string Anonym(string text, Klic klic) => Anonymizator.AnonymizovanyText(Vzor.Ocr(text), klic);

    [Fact]
    public void DatumRozdeleneOcrNespolkneJmeno()
    {
        // druhý motor přečetl datum jako tři slova „12 3 1975“
        var klic = KlicZ("Žalobce: Ing. Petr Pokusný, nar. 12.3.1975, bytem Fiktivní 1, 999 01 Lhota");
        klic.Nova(TypUdaje.DatumNarozeni, new[] { "12", "3", "1975" });
        var text = Anonym("Žalobce: Ing. Petr Pokusný, nar. 12.3.1975, bytem Fiktivní 1, 999 01 Lhota", klic);
        Assert.Contains("[OSOBA_001]", text);
        Assert.DoesNotContain("Petr", text);
        Assert.Contains("nar. [ODSTRANĚNO: datum narození]", text);
    }

    [Fact]
    public void KrestniJmenoNespojiRuzneOsobySeStejnymPrijmenim()
    {
        var klic = KlicZ("Prodávající Marie Nevšední a její syn Jiří Nevšední podepsali.");
        Assert.Equal(2, klic.Entity.Count(e => e.Typ == TypUdaje.Osoba));
    }

    [Fact]
    public void JmenoSamotneKrestniNestrhneCeleJmeno()
    {
        // ve spisu je entita jen s tvarem „Jan“; „Jan Pokusný“ se k ní nesmí přiřadit bez příjmení
        var klic = new Klic { Styl = StylPseudonymu.Kody };
        klic.Nova(TypUdaje.Osoba, new[] { "Jan" });
        var slova = new[] { "Smlouva", "Jan", "Pokusný" };
        Assert.Null(Anonymizator.NajdiEntitu(klic, TypUdaje.Osoba, slova, 1, 2));
    }

    [Fact]
    public void NazevDokumentuSeJmenemSeAnonymizujeCely()
    {
        using var d = new DocasnaSlozka();
        var spis = new Uloziste(d.Cesta).Zaloz("vzor");
        using (var m = new Mat(40, 40, MatType.CV_8UC3, Scalar.White)) Cv2.ImWrite(Path.Combine(d.Cesta, "1.png"), m);
        var strana = spis.PridejFotky(new[] { Path.Combine(d.Cesta, "1.png") }, "Smlouva Jan Pokusný")[0];
        spis.UlozOcr(strana, Vzor.Ocr("Kupující Jan podepsal. Ve věci rozhodl Jan Smyšlený."));
        var klic = new Klic { Styl = StylPseudonymu.Kody };
        Anonymizator.AktualizujKlic(spis, klic);
        var nazev = Anonymizator.AnonymizujText("Smlouva Jan Pokusný", klic);
        Assert.DoesNotContain("Pokusn", nazev);
        Assert.DoesNotContain("Jan", nazev);
    }

    [Theory]
    [InlineData("Od: petra smyšlená <petra@example.cz>", "petra smyšlená")]
    [InlineData("To: 15 - Xenie Vymyšlenů", "Xenie Vymyšlenů")]
    [InlineData("S pozdravem\nJANA POKUSNÁ\nreferentka", "JANA POKUSNÁ")]
    public void JmenaVHlavickachAVelkymi(string text, string jmeno)
    {
        var ocr = Vzor.Ocr(text);
        var slova = Anonymizator.Slova(ocr);
        var n = Detektor.Najdi(slova, Anonymizator.Radky(ocr));
        Assert.Contains(n, x => x.Typ == TypUdaje.Osoba && string.Join(" ", slova.Skip(x.Od).Take(x.Do - x.Od + 1)).Contains(jmeno.Split(' ')[^1]));
    }

    [Fact]
    public void ZbytkyJmenaPoChybeOcr()
    {
        var klic = KlicZ("Stavebník: Jana Pokusná, bytem Fiktivní 1, 999 01 Lhota");
        var text = Anonym("podpis: etr Pokusná a dále Jana Pokusn011a", klic);
        Assert.DoesNotContain("Pokusn011a", text);
        var klic2 = KlicZ("Ing. Petr Pokusný podepsal.");
        Assert.DoesNotContain("etr", Anonym("Za správnost etr Pokusný.", klic2));
    }

    [Fact]
    public void CisloPopisneSObci()
    {
        var klic = KlicZ("Přístavba RD č.p 158 v Lhotě u Nikde, stavebník podal žádost.");
        var text = Anonym("Přístavba RD č.p 158 v Lhotě u Nikde. Dům v Lhotě u Nikde stojí.", klic);
        Assert.DoesNotContain("158", text);
        Assert.DoesNotContain("Nikde", text);
    }

    [Fact]
    public void OdkazSDomenouFirmy()
    {
        var klic = KlicZ("Kontakt: info@fiktivni.cz\nhttp://mail.fiktivni.cz/?_task=mail");
        var text = Anonym("Kontakt: info@fiktivni.cz\nhttp://mail.fiktivni.cz/?_task=mail", klic);
        Assert.DoesNotContain("fiktivni", text);
    }

    [Fact]
    public void FirmaBezPravniFormyAVZavorce()
    {
        var klic = KlicZ("Dodavatel FIKTIVNET a.s. dodal zboží.");
        Assert.DoesNotContain("FIKTIVNET", Anonym("Reklamaci řešil FIKTIVNET, ale bez výsledku.", klic));
        var ocr = Vzor.Ocr("odběratel (Smyšlená 02, a.s) zaplatil");
        var slova = Anonymizator.Slova(ocr);
        Assert.Contains(Detektor.Najdi(slova, Anonymizator.Radky(ocr)), x => x.Typ == TypUdaje.Firma);
    }

    [Fact]
    public void FalesnaAdresaZVykresu()
    {
        var ocr = Vzor.Ocr("12345 VSTUPY\n54321 NOVE SITE");
        Assert.DoesNotContain(Detektor.Najdi(Anonymizator.Slova(ocr), Anonymizator.Radky(ocr)), x => x.Typ == TypUdaje.Adresa);
    }

    [Fact]
    public void RucniOpravaJmenaSeNeztrati()
    {
        var klic = KlicZ("Žalobce Jan Pokusni podal žalobu.");
        var e = klic.Entity.Single(x => x.Typ == TypUdaje.Osoba);
        e.Prijmeni = "Pokusný"; e.JmenoRucne = true;
        Anonymizator.DoplnOsoby(klic, new[] { "Jan", "Pokusni" });
        Assert.Equal("Pokusný", e.Prijmeni);
    }

    [Fact]
    public void RodNeznamehoJmenaPodleKrestnihoNaE()
    {
        var klic = KlicZ("To: Xenie Vymyšlenů");
        Assert.Equal(Rod.Zena, klic.Entity.Single(x => x.Typ == TypUdaje.Osoba).Rod);
    }

    [Fact]
    public void KrestniJmenoPodleRoduPrijmeni()
    {
        // jen „Karla Vzorečka“: Karla je i ženské jméno, ale příjmení není -ová/-á, tedy muž Karel
        var klic = KlicZ("výslech Karla Vzorečka");
        var e = klic.Entity.Single(x => x.Typ == TypUdaje.Osoba);
        Assert.Equal("Karel", e.Jmeno);
        Assert.Equal(Rod.Muz, e.Rod);
    }

    [Fact]
    public void InicialyNevyberouPodobneJmeno()
    {
        for (int semeno = 0; semeno < 50; semeno++)
        {
            var (jmeno, prijmeni) = Pseudonymy.Vyber(StylPseudonymu.Stromy, Rod.Zena, "Radmila", "Pokusná", true, _ => false, semeno);
            Assert.True(Text.Levenshtein(Text.Normalizuj(jmeno!), "radmila") > 2, jmeno);
        }
    }
    [Theory]
    [InlineData("provozovatelem vozidla RZ 3H6\n9882 od roku 2018", "9882")]
    [InlineData("vozidla registrační značky 7H\n1024 dne", "1024")]
    [InlineData("vlastníkem vozidla s 4E6\n3893 v době", "3893")]
    [InlineData("kupní smlouvu podepsala Marie\nMarková dne 1. 1. 2020", "Marie")]
    [InlineData("technického průkazu CZ64 0800\n0000 2040 3170 9173, který byl", "9173")]
    [InlineData("společnost Fiktivní s.r.o., tel. +420 743\n748 240, uzavřela smlouvu", "748")]
    public void UdajRozdelenyKoncemRadkuSeNajde(string text, string zbytek)
    {
        var klic = KlicZ(text);
        var vystup = Anonym(text, klic);
        Assert.DoesNotContain(zbytek, vystup);
    }

    [Fact]
    public void DatumNarozeniZaRodnymPrijmenim()
    {
        const string text = "žalobkyně: Smyšlená Pokusná, narozená Zkušební 28. 5. 1990, bytem Fiktivní 1, 999 01 Lhota";
        var vystup = Anonym(text, KlicZ(text));
        Assert.DoesNotContain("1990", vystup);
    }
    // dotestování 2026-11-04

    [Fact]
    public void HlavickaEmailuVeDvouSloupcich()
    {
        const string text = "Odesílatel\nAdresát\nDatum\npetra smyšlená\njan zkušební\n3. 11. 2026\nDobrý den, posílám podklady.";
        var vystup = Anonym(text, KlicZ(text));
        Assert.DoesNotContain("smyšlená", vystup);
        Assert.DoesNotContain("zkušební", vystup);
        Assert.Contains("Dobrý den", vystup);
    }

    [Theory]
    [InlineData("Dům v Lhotě čp. 123 bude zbourán.", "123")]
    [InlineData("Dům čp.122 bude zbourán.", "122")]
    [InlineData("Dům č,pŔ 123 bude zbourán.", "123")]
    [InlineData("Stavba na pozemku p.č. 9999 v obci.", "9999")]
    public void CisloDomuAParcelyVJinemZapisu(string text, string cislo)
    {
        var vystup = Anonym(text, KlicZ(text));
        Assert.DoesNotContain(cislo, vystup);
    }

    [Theory]
    [InlineData("8. Doložit vyjádření FIKTIVNET a.s k sítím", "8. Doložit vyjádření [FIRMA_001] k sítím")]
    [InlineData("Přiložte vyjádření FIKTIVNET a.s", "Přiložte vyjádření [FIRMA_001]")]
    public void FirmaNespolkneSlovaVety(string text, string ocekavane)
    {
        Assert.Equal(ocekavane, Anonym(text, KlicZ(text)).Trim());
    }

    [Fact]
    public void SmysleneStavbyPoradZustavajiFirmou()
    {
        const string text = "Zhotovitel Dopravní služby Smyšlený s.r.o. převzal staveniště.";
        Assert.Contains("[FIRMA_001]", Anonym(text, KlicZ(text)));
        Assert.DoesNotContain("služby", Anonym(text, KlicZ(text)));
    }

    [Fact]
    public void IcoBezHackuSeNajde()
    {
        const string text = "Zhotovitel: Pokusné stavby, ICO 99887766, sídlo neuvedeno.";
        Assert.DoesNotContain("99887766", Anonym(text, KlicZ(text)));
    }

    [Fact]
    public void PozdravAPostovniProgramNejsouOsoba()
    {
        var klic = KlicZ("Od: Roundcube Webmail\nKomu: Dobrý den\nPředmět: FW: Podklady");
        Assert.DoesNotContain(klic.Entity, e => e.Typ == TypUdaje.Osoba);
    }

    [Fact]
    public void ObecVNazvuFirmyNenahradiObecVeVete()
    {
        const string text = "Technické služby města Velkoměsto a.s. převzaly dům čp. 1, 999 01 Velkoměsto.\nV obci Velkoměsto se konalo jednání.";
        var klic = KlicZ(text);
        var vystup = Anonym(text, klic);
        // obec z adresy se nahradí jako adresa, ne jako firma
        Assert.DoesNotContain("FIRMA_001] se konalo", vystup);
    }

    [Fact]
    public void ZahlaviTabulkySPetimiCisliciNeniAdresa()
    {
        var klic = KlicZ("Položka 12345 Počet kusů 3");
        Assert.DoesNotContain(klic.Entity, e => e.Typ == TypUdaje.Adresa);
    }

    [Theory]
    [InlineData(0, 0, 0, true)]       // skoro černá
    [InlineData(40, 40, 160, true)]   // červená od světla přes prst (BGR)
    [InlineData(86, 86, 86, true)]    // šedá mlha z makro režimu
    [InlineData(250, 250, 250, false)] // bílý papír přes celý záběr
    public void CernaFotkaZMobilu(int b, int g, int r, bool oddelovac)
    {
        using var obrazek = new Mat(1200, 900, MatType.CV_8UC3, new Scalar(b, g, r));
        using var sum = new Mat(1200, 900, MatType.CV_8UC3);
        Cv2.Randn(sum, new Scalar(0, 0, 0), new Scalar(6, 6, 6));
        Cv2.Add(obrazek, sum, obrazek);
        Cv2.GaussianBlur(obrazek, obrazek, new Size(31, 31), 0);
        Assert.Equal(oddelovac, Spis.JeCerna(obrazek));
    }

    [Fact]
    public void FotkaTextuNeniOddelovac()
    {
        using var obrazek = new Mat(1200, 900, MatType.CV_8UC3, new Scalar(110, 110, 110));
        for (int y = 100; y < 1100; y += 40)
            Cv2.PutText(obrazek, "Kupni smlouva c. 1", new Point(60, y), HersheyFonts.HersheySimplex, 1.2, new Scalar(20, 20, 20), 2);
        Assert.False(Spis.JeCerna(obrazek));
    }
}
