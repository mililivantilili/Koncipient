using DocumentFormat.OpenXml.Packaging;
using Koncipient.Jadro;
using Koncipient.Jadro.Anonymizace;
using Xunit;
using VzorSklonovani = Koncipient.Jadro.Anonymizace.Vzor;

namespace Koncipient.Testy;

// Všechna jména jsou vymyšlená.

public class SklonovaniTesty
{
    [Theory]
    [InlineData("Novák", Rod.Muz, Pad.G, "Nováka")]
    [InlineData("Novák", Rod.Muz, Pad.D, "Novákovi")]
    [InlineData("Novák", Rod.Muz, Pad.I, "Novákem")]
    [InlineData("Vymyšlíček", Rod.Muz, Pad.G, "Vymyšlíčka")]
    [InlineData("Tomáš", Rod.Muz, Pad.G, "Tomáše")]
    [InlineData("Karel", Rod.Muz, Pad.G, "Karla")]
    [InlineData("Pavel", Rod.Muz, Pad.D, "Pavlovi")]
    [InlineData("Daniel", Rod.Muz, Pad.G, "Daniela")]
    [InlineData("Nováková", Rod.Zena, Pad.A, "Novákovou")]
    [InlineData("Nováková", Rod.Zena, Pad.G, "Novákové")]
    [InlineData("Vymyšlený", Rod.Muz, Pad.G, "Vymyšleného")]
    [InlineData("Vymyšlený", Rod.Muz, Pad.D, "Vymyšlenému")]
    [InlineData("Smyšlená", Rod.Zena, Pad.I, "Smyšlenou")]
    [InlineData("Jana", Rod.Zena, Pad.D, "Janě")]
    [InlineData("Petra", Rod.Zena, Pad.D, "Petře")]
    [InlineData("Lenka", Rod.Zena, Pad.L, "Lence")]
    [InlineData("Radmila", Rod.Zena, Pad.D, "Radmile")]
    [InlineData("Marie", Rod.Zena, Pad.D, "Marii")]
    [InlineData("Svoboda", Rod.Muz, Pad.D, "Svobodovi")]
    [InlineData("Krejčí", Rod.Muz, Pad.G, "Krejčího")]
    public void Tvar(string zaklad, Rod rod, Pad pad, string ocekavany) =>
        Assert.Equal(ocekavany, VzorSklonovani.Urci(zaklad, rod).Tvar(pad));

    [Fact]
    public void UrciPadIZOcr()
    {
        var v = VzorSklonovani.Urci("Vymyšlíček", Rod.Muz);
        Assert.Equal(Pad.G, v.UrciPad("Vymyslicka")); // bez diakritiky
        Assert.Null(v.UrciPad("Vymyšlíčková"));
    }

    [Fact]
    public void ZakladniTvar()
    {
        Assert.Equal("Novák", Sklonovani.ZakladniTvar(new[] { "Nováka", "Novák", "Novákovi" }, Rod.Muz));
        Assert.Equal("Vymyšlíček", Sklonovani.ZakladniTvar(new[] { "Vymyšlíčka", "Vymyšlíček" }, Rod.Muz));
        Assert.Equal("Nováková", Sklonovani.ZakladniTvar(new[] { "Novákovou", "Nováková" }, Rod.Zena));
    }
}

public class VymyslenaJmenaTesty
{
    public static IEnumerable<object[]> Semena() => Enumerable.Range(0, 300).Select(i => new object[] { i * 7919 });

    [Theory]
    [MemberData(nameof(Semena))]
    public void OsobyDostanouVymyslenaJmenaSeStejnymRodemAPadem(int semeno)
    {
        var (_, klic, d) = AnonymizatorTesty.SpisSeVzorem(StylPseudonymu.Stromy, semeno);
        using var _ = d;
        var vymysleny = klic.Entity.Single(e => e.Typ == TypUdaje.Osoba && e.Prijmeni == "Vymyšlený");
        var novakova = klic.Entity.Single(e => e.Typ == TypUdaje.Osoba && e.Prijmeni == "Nováková");
        Assert.Equal("Jan", vymysleny.Jmeno);
        Assert.Equal(Rod.Muz, vymysleny.Rod);
        Assert.Equal(Rod.Zena, novakova.Rod);
        Assert.EndsWith("ký", vymysleny.PseudoPrijmeni); // přídavné příjmení dostane přídavné
        Assert.EndsWith("ová", novakova.PseudoPrijmeni);
        Assert.Equal(Rod.Zena, Sklonovani.UrciRod(novakova.PseudoJmeno, null));

        var text = Anonymizator.AnonymizovanyText(Vzor.Ocr(), klic);
        var genitiv = VzorSklonovani.Urci(vymysleny.PseudoPrijmeni!, Rod.Muz).Tvar(Pad.G);
        Assert.Contains($"pan {genitiv} viděla", text);
        Assert.Contains($"{vymysleny.PseudoJmeno} {vymysleny.PseudoPrijmeni},", text);
        foreach (var citlive in new[] { "Vymyšlen", "Vymyšlíč", "Vymyslíč", "Nováková", "999 888" })
            Assert.DoesNotContain(citlive, text);
        var kontrola = Export.Kontrola(text, klic);
        Assert.True(kontrola.Count == 0, string.Join("\n", kontrola) + "\n---\n" + text);
    }

    [Fact]
    public void RoleASeskupeniZHlavicky()
    {
        var (_, klic, d) = AnonymizatorTesty.SpisSeVzorem(StylPseudonymu.Stromy);
        using var _ = d;
        var zalobce = klic.Entity.Single(e => e.Typ == TypUdaje.Osoba && e.Prijmeni == "Vymyšlený");
        Assert.Equal("žalobce", zalobce.Role);
        var firma = klic.Entity.Single(e => e.Typ == TypUdaje.Firma);
        Assert.Equal("žalovaná", firma.Role);
        Assert.Contains(klic.Entity, e => e.Typ == TypUdaje.Adresa && e.PatriK == zalobce.Id);
        Assert.Contains(klic.Entity, e => e.Typ == TypUdaje.Telefon && e.PatriK == zalobce.Id);
        Assert.Contains(klic.Entity, e => e.Typ == TypUdaje.Ico && e.PatriK == firma.Id);
    }

    [Fact]
    public void ManzeleSeNeslouci()
    {
        var ocr = Vzor.Ocr("Žalobce Ing. Petr Pokusný a jeho manželka Jana Pokusná.\nPan Pokusný a paní Pokusná, svědkyně Jany Pokusné.\nJednatel Karel Vzoreček, panu Vzorečkovi, společnice Eva Vzorečková.");
        var klic = new Klic();
        var slova = Anonymizator.Slova(ocr);
        foreach (var n in Detektor.Najdi(slova, Anonymizator.Radky(ocr)))
            if (Anonymizator.NajdiEntitu(klic, n.Typ, slova, n.Od, n.Do) is null)
                klic.Nova(n.Typ, slova.Skip(n.Od).Take(n.Do - n.Od + 1));
        Anonymizator.DoplnOsoby(klic, slova);
        var osoby = klic.Entity.Where(e => e.Typ == TypUdaje.Osoba).Select(e => e.Popis).OrderBy(p => p).ToList();
        Assert.Equal(new[] { "Eva Vzorečková", "Jana Pokusná", "Karel Vzoreček", "Petr Pokusný" }, osoby);

        var text = Anonymizator.AnonymizovanyText(ocr, klic);
        var petr = klic.Entity.Single(e => e.Prijmeni == "Pokusný");
        Assert.Contains($"Ing. {petr.PseudoJmeno} {petr.PseudoPrijmeni} a", text);
        Assert.DoesNotContain("Pokusn", text);
        Assert.DoesNotContain("Vzoreč", text);
    }

    [Fact]
    public void IniciályZustanou()
    {
        var klic = new Klic();
        var e = klic.Nova(TypUdaje.Osoba, new[] { "Jan", "Vymyšlený" });
        e.Inicialy = true;
        Anonymizator.DoplnOsoby(klic, new[] { "Jan", "Vymyšlený" });
        Assert.StartsWith("J", e.PseudoJmeno);
        Assert.StartsWith("V", e.PseudoPrijmeni);
        Assert.NotEqual("Vymyšlený", e.PseudoPrijmeni);
    }

    [Fact]
    public void VymysleneJmenoSeNepodobaSlovuZeSpisu()
    {
        // když se ve spisu vyskytují všechna stromová příjmení kromě jednoho, musí se vybrat to jedno
        var klic = new Klic();
        var e = klic.Nova(TypUdaje.Osoba, new[] { "Jan", "Vymyšlený" });
        var spis = new[] { "Jasanský", "Habrovský", "Jilmovský", "Lipovecký", "Olšanský", "Smrkovský", "Tisovský",
            "Akátský", "Platanský", "Kaštanský", "Bukovský", "Jedlovský", "Cedrovský", "Ebenský", "Hlohovský",
            "Nespolský", "Ořešanský", "Vrbovecký", "Gingkovský" };
        Anonymizator.DoplnOsoby(klic, spis);
        Assert.Equal("Topolanský", e.PseudoPrijmeni);
    }

    [Fact]
    public void PrevzetiKliceZJinehoSpisu()
    {
        var stary = new Klic();
        var a = stary.Nova(TypUdaje.Osoba, new[] { "Jan", "Vymyšlený" });
        Anonymizator.DoplnOsoby(stary, Array.Empty<string>());
        stary.Nova(TypUdaje.Osoba, new[] { "Karel", "Smyšlenec" });

        var novy = new Klic();
        novy.Nova(TypUdaje.Osoba, new[] { "Jana", "Vymyšleného" }); // jiný tvar téže osoby
        var (prevzato, pridano) = Anonymizator.Prevezmi(novy, stary);
        Assert.Equal((1, 1), (prevzato, pridano));
        Assert.Equal(a.PseudoPrijmeni, novy.Entity[0].PseudoPrijmeni);
        Assert.Equal(2, novy.Entity.Count);
    }
}

public class ZpetneDosazeniTesty
{
    [Fact]
    public void VratiJmenaVeSpravnemPadeAOznaciNejiste()
    {
        var (_, klic, d) = AnonymizatorTesty.SpisSeVzorem(StylPseudonymu.Stromy);
        using var _ = d;
        var jan = klic.Entity.Single(e => e.Prijmeni == "Vymyšlený");
        var adresa = klic.Entity.First(e => e.Typ == TypUdaje.Adresa && e.PatriK == jan.Id);
        var p = VzorSklonovani.Urci(jan.PseudoPrijmeni!, Rod.Muz);
        var j = VzorSklonovani.Urci(jan.PseudoJmeno!, Rod.Muz);

        var odClauda = $"Žalobce {j.Tvar(Pad.N)} {p.Tvar(Pad.N)} bydlí na adrese [{adresa.Id}]. " +
                       $"Doporučujeme panu {p.Tvar(Pad.D)} podat odvolání. Telefon: [ODSTRANĚNO: telefon]. " +
                       "Posudek vypracuje [NOVÁ OSOBA: znalec]. Svědkem byl Ing. Ctirad Neznámý.";
        var useky = ZpetneDosazeni.Vrat(odClauda, klic);
        var vysledek = ZpetneDosazeni.Spoj(useky);

        Assert.Contains("Žalobce Jan Vymyšlený bydlí", vysledek);
        Assert.Contains("panu Vymyšlenému podat", vysledek);
        Assert.Contains("Fiktivní 123/4", vysledek);
        Assert.DoesNotContain(jan.PseudoPrijmeni!, vysledek);
        Assert.Contains(useky, u => u.Druh == DruhUseku.Doplnit && u.Text.Contains("ODSTRANĚNO"));
        Assert.Contains(useky, u => u.Druh == DruhUseku.Doplnit && u.Text.Contains("NOVÁ OSOBA"));
        Assert.Contains(useky, u => u.Druh == DruhUseku.Zkontrolovat && u.Text.Contains("Neznámý"));
    }

    [Fact]
    public void WordObsahujeText()
    {
        using var d = new DocasnaSlozka();
        var cesta = Path.Combine(d.Cesta, "vystup.docx");
        Word.Uloz(cesta, "# Odvolání\n\nŽalobce **Jan Vymyšlený** podává odvolání.\n- první bod");
        using var doc = WordprocessingDocument.Open(cesta, false);
        var text = doc.MainDocumentPart!.Document.Body!.InnerText;
        Assert.Contains("Odvolání", text);
        Assert.Contains("Jan Vymyšlený", text);
        Assert.Contains("první bod", text);
    }
}
