using Koncipient.Jadro;
using Koncipient.Jadro.Anonymizace;
using Koncipient.Ocr;
using OpenCvSharp;
using Xunit;

namespace Koncipient.Testy;

// Vzor je zjevně vymyšlený: jména, adresy i čísla neexistují.
public static class Vzor
{
    public const string Text = """
        Okresní soud ve Smyšlenicích
        sp. zn. 12 C 345/2026
        Žalobce: Jan Vymyšlený, nar. 1. 2. 1980, bytem Fiktivní 123/4, 999 99 Lhota u Nikde
        tel. +420 999 888 777, e-mail jan.vymysleny@example.cz
        Žalovaná: Smyšlené stavby s.r.o., IČO 12345678, účet 1234567890/0100
        Svědkyně Ing. Petra Nováková potvrdila, že pan Vymyšleného viděla.
        Svědek pan Novák a JUDr. Vymyšlíček uvedli, že Vymyslíček i Vymyšlíčka byli přítomni.
        Pozemek parc. č. 4321/5 v k.ú. Lhota u Nikde.
        Rodné číslo 855101/0000 je smyšlené.
        """;

    public static VysledekOcr Ocr(string text = Text) => new()
    {
        Motor = Motory.Paddle,
        Radky = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(r => new RadekOcr
            {
                Slova = r.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => new SlovoOcr { Text = s }).ToList(),
            }).ToList(),
    };
}

public class TextTesty
{
    [Theory]
    [InlineData("Vymyšlíček", "Vymyšlíčka")]      // pohyblivé e
    [InlineData("Vymyšlíček", "Vymyslíček")]      // chybějící čárka z OCR
    [InlineData("Vymyšlíček", "Vymyšlicek")]
    [InlineData("Novák", "Nováka")]
    [InlineData("Novák", "Novákovi")]
    [InlineData("Nováková", "Novákovou")]
    [InlineData("Vymyšlený", "Vymyšleného")]
    [InlineData("Smyšlenčík", "Smyšlenčik")]
    public void StejneSlovo(string a, string b) => Assert.True(Koncipient.Jadro.Anonymizace.Text.StejneSlovo(a, b));

    [Theory]
    [InlineData("Novák", "Nováková")]
    [InlineData("Novák", "Novotný")]
    [InlineData("Jan", "Jana2")]
    public void RuzneSlovo(string a, string b) => Assert.False(Koncipient.Jadro.Anonymizace.Text.StejneSlovo(a, b));
}

public class DetektorTesty
{
    private static List<(TypUdaje Typ, string Text)> Najdi(string text)
    {
        var ocr = Vzor.Ocr(text);
        var slova = Anonymizator.Slova(ocr);
        return Detektor.Najdi(slova, Anonymizator.Radky(ocr)).Select(n => (n.Typ, string.Join(" ", slova.Skip(n.Od).Take(n.Do - n.Od + 1)))).ToList();
    }

    [Fact]
    public void NajdeUdajeVeVzoru()
    {
        var n = Najdi(Vzor.Text);
        void Ma(TypUdaje typ, string obsahuje) =>
            Assert.True(n.Any(x => x.Typ == typ && x.Text.Contains(obsahuje)), $"Chybí {typ} „{obsahuje}“. Nalezeno: {string.Join("; ", n)}");

        Ma(TypUdaje.SpisovaZnacka, "12 C 345/2026");
        Ma(TypUdaje.Osoba, "Jan Vymyšlený");
        Ma(TypUdaje.DatumNarozeni, "1. 2. 1980");
        Ma(TypUdaje.Adresa, "Fiktivní 123/4");
        Ma(TypUdaje.Adresa, "Lhota");
        Ma(TypUdaje.Telefon, "999 888 777");
        Ma(TypUdaje.Email, "jan.vymysleny@example.cz");
        Ma(TypUdaje.Firma, "Smyšlené stavby s.r.o.");
        Ma(TypUdaje.Ico, "12345678");
        Ma(TypUdaje.Ucet, "1234567890/0100");
        Ma(TypUdaje.Osoba, "Petra Nováková");
        Ma(TypUdaje.Osoba, "Novák");
        Ma(TypUdaje.Osoba, "Vymyšlíček");
        Ma(TypUdaje.Nemovitost, "4321/5");
        Ma(TypUdaje.RodneCislo, "855101/0000");
    }

    [Fact]
    public void JmenoNepretekaPresKonecRadku()
    {
        var n = Najdi("Ing. Jan Vymyšlený\nVypracoval v Lhotě");
        Assert.Contains((TypUdaje.Osoba, "Jan Vymyšlený"), n);
        Assert.DoesNotContain(n, x => x.Text.Contains("Vypracoval"));
    }

    [Fact]
    public void CisloSUrademNeniAdresa()
    {
        Assert.Empty(Najdi("20042 Krajské ředitelství"));
        Assert.Empty(Najdi("Nejbližší stavební objekt se nachází"));
    }

    [Fact]
    public void NeoznaciBezneVety()
    {
        var n = Najdi("Ve smyslu ČSN 73 0802 se odstupové vzdálenosti nestanovují. Stránka 9 / 10");
        Assert.Empty(n);
    }
}

public class AnonymizatorTesty
{
    internal static (Spis Spis, Klic Klic, DocasnaSlozka Slozka) SpisSeVzorem(StylPseudonymu styl = StylPseudonymu.Kody, int semeno = 1)
    {
        var d = new DocasnaSlozka();
        var spis = new Uloziste(Path.Combine(d.Cesta, "Spisy")).Zaloz("vzor");
        using (var m = new Mat(50, 50, MatType.CV_8UC3, Scalar.White)) Cv2.ImWrite(Path.Combine(d.Cesta, "1.png"), m);
        var strana = spis.PridejFotky(new[] { Path.Combine(d.Cesta, "1.png") })[0];
        spis.UlozOcr(strana, Vzor.Ocr());
        var klic = new Klic { Styl = styl, Semeno = semeno };
        Anonymizator.AktualizujKlic(spis, klic);
        return (spis, klic, d);
    }

    [Fact]
    public void SklonovaneTvaryDostanouStejnyPseudonym()
    {
        var (spis, klic, d) = SpisSeVzorem();
        using var _ = d;
        var text = Anonymizator.AnonymizovanyText(Vzor.Ocr(), klic);

        var vymysleny = klic.Entity.Single(e => e.Typ == TypUdaje.Osoba && e.Popis.Contains("Vymyšlený"));
        var micek = klic.Entity.Single(e => e.Typ == TypUdaje.Osoba && e.Popis.Contains("Vymyšlíček"));
        Assert.Contains($"pan [{vymysleny.Id}] viděla", text);
        Assert.Contains($"[{micek.Id}] uvedli, že [{micek.Id}] i [{micek.Id}] byli", text);

        // Novák a Nováková zůstanou dvě různé osoby
        var novak = klic.Entity.Single(e => e.Typ == TypUdaje.Osoba && e.Popis == "Novák");
        var novakova = klic.Entity.Single(e => e.Typ == TypUdaje.Osoba && e.Popis.Contains("Nováková"));
        Assert.NotEqual(novak.Id, novakova.Id);

        foreach (var citlive in new[] { "Vymyšlen", "Vymyšlíč", "Vymyslíč", "999 888", "example.cz", "12345678", "855101", "Fiktivní", "Smyšlené stavby" })
            Assert.DoesNotContain(citlive, text);
        Assert.Contains("[ODSTRANĚNO: telefon]", text);
        var kontrola = Export.Kontrola(text, klic);
        Assert.True(kontrola.Count == 0, string.Join("\n", kontrola) + "\n---\n" + text);
    }

    [Fact]
    public void UdajNalezenyKontrolnimMotoremSeNahradiIVHlavnimTextu()
    {
        using var d = new DocasnaSlozka();
        var spis = new Uloziste(Path.Combine(d.Cesta, "Spisy")).Zaloz("vzor");
        using (var m = new Mat(50, 50, MatType.CV_8UC3, Scalar.White)) Cv2.ImWrite(Path.Combine(d.Cesta, "1.png"), m);
        var strana = spis.PridejFotky(new[] { Path.Combine(d.Cesta, "1.png") })[0];
        // hlavní motor přečetl titul chybně, takže jméno sám nenajde
        var hlavni = Vzor.Ocr("Smlouvu podepsal lng Smyšlenčík dne 1. 1. 2026.");
        hlavni.Motor = Motory.Windows;
        spis.UlozOcr(strana, hlavni);
        var kontrolni = Vzor.Ocr("Smlouvu podepsal Ing. Smyšlenčík dne 1. 1. 2026.");
        spis.UlozOcr(strana, kontrolni); // Motor = paddle-cs

        var klic = new Klic { Styl = StylPseudonymu.Kody };
        Anonymizator.AktualizujKlic(spis, klic);
        var text = Anonymizator.AnonymizovanyText(Motory.ProZobrazeni(spis, strana)!, klic);
        Assert.DoesNotContain("Smyšlenčík", text);
        Assert.Contains("[OSOBA_001]", text);
    }

    [Fact]
    public void KontrolaNajdeZapomenutyUdaj()
    {
        var klic = new Klic();
        klic.Nova(TypUdaje.Osoba, new[] { "Jan", "Vymyšlený" });
        var problemy = Export.Kontrola("Svědek [OSOBA_001] a pan Vymyšleného, tel. 999 888 777.", klic);
        Assert.Equal(2, problemy.Count);
    }

    [Fact]
    public void RucniOznaceniAVyjimka()
    {
        var klic = new Klic();
        var slova = new[] { "Smlouvu", "podepsal", "Kvapil", "na", "Bystřině" };
        var e = Anonymizator.OznacRucne(klic, TypUdaje.Osoba, slova, 2, 2);
        Assert.Equal(new[] { new Vyskyt(2, 2, e) }, Anonymizator.Vyskyty(slova, klic));

        Anonymizator.OznacNecitlive(klic, "Kvapil");
        Assert.Empty(Anonymizator.Vyskyty(slova, klic));
        Assert.Empty(klic.Entity);
    }

    [Fact]
    public void ExportVyzadujeKontroluAZapiseJenText()
    {
        var (spis, klic, d) = SpisSeVzorem();
        using var _ = d;
        var proClaude = Path.Combine(d.Cesta, "ProClaude");

        var neuspech = Export.Proved(spis, klic, proClaude);
        Assert.False(neuspech.Uspech);
        Assert.Contains(neuspech.Problemy, p => p.Contains("zkontrolovan"));
        Assert.False(Directory.Exists(Path.Combine(proClaude, spis.Id)));

        spis.Strany[0].Zkontrolovano = true;
        var uspech = Export.Proved(spis, klic, proClaude);
        Assert.True(uspech.Uspech, string.Join("\n", uspech.Problemy));

        var soubory = Directory.GetFiles(proClaude, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(proClaude, f).Replace('\\', '/')).OrderBy(f => f).ToList();
        Assert.Equal(new[] { "CLAUDE.md", "PRAVIDLA-KONCIPIENT.md", "S-0001/entity.json", "S-0001/manifest.json", "S-0001/strany/0001.md" }, soubory);
        Assert.True(Directory.Exists(Path.Combine(proClaude, "S-0001", "vystup")));
        var vse = string.Concat(Directory.GetFiles(Path.Combine(proClaude, "S-0001"), "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.DoesNotContain("Vymyšlen", vse);
        Assert.DoesNotContain("vzor", File.ReadAllText(Path.Combine(proClaude, "S-0001", "manifest.json")));
    }
}

public class OtoceniTesty
{
    [Fact]
    public void OtoceniStranyOtociFotkuASmazePrepis()
    {
        using var d = new DocasnaSlozka();
        var spis = new Uloziste(d.Cesta).Zaloz("vzor");
        using (var m = new Mat(400, 300, MatType.CV_8UC3, Scalar.White)) Cv2.ImWrite(Path.Combine(d.Cesta, "1.png"), m);
        var strana = spis.PridejFotky(new[] { Path.Combine(d.Cesta, "1.png") })[0];
        spis.UlozOcr(strana, new VysledekOcr { Motor = Motory.Paddle });
        spis.UlozOcr(strana, new VysledekOcr { Motor = Motory.Paddle }, Spis.Opraveno);

        var puvodniFotka = File.ReadAllBytes(spis.CestaFotky(strana));

        Assert.False(spis.OtocStranu(strana, 90));
        using (var fotka = Cv2.ImRead(spis.CestaFotky(strana)))
            Assert.Equal(new Size(400, 300), fotka.Size());
        Assert.False(spis.MaOcr(strana, Motory.Paddle));
        Assert.False(spis.MaOcr(strana, Spis.Opraveno));
        Assert.True(Spis.Nacti(spis.Slozka).Strany[0].Narovnano);

        // přepis otočené strany, pak zpět: vrátí se původní přepis i oprava a fotka je původní
        spis.UlozOcr(strana, new VysledekOcr { Motor = Motory.Windows });
        Assert.True(spis.OtocStranu(strana, 270));
        Assert.True(spis.MaOcr(strana, Motory.Paddle));
        Assert.True(spis.MaOcr(strana, Spis.Opraveno));
        Assert.False(spis.MaOcr(strana, Motory.Windows));
        Assert.Equal(puvodniFotka, File.ReadAllBytes(spis.CestaFotky(strana)));

        // a znovu na 90°: vrátí se přepis otočené strany
        Assert.True(spis.OtocStranu(strana, 90));
        Assert.True(spis.MaOcr(strana, Motory.Windows));
        Assert.Equal(90, Spis.Nacti(spis.Slozka).Strany[0].Otoceni);
    }
}
