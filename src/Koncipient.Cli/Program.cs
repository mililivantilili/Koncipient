// koncipient-cli: ovládání Koncipientu z příkazové řádky, hlavně pro agenty (Claude Code)
// a skripty při testování. Všechny výstupy jsou JSON na standardní výstup, chyby na
// chybový výstup s nenulovým návratovým kódem. Nápověda: koncipient-cli help
//
// POZOR: agent jako Claude Code posílá všechno, co přečte, do cloudu. Příkazy, které
// vypisují obsah spisu (text, skutečné údaje), proto běží jen s proměnnou prostředí
// KONCIPIENT_TESTOVACI_DATA=1. Nikdy je nepoužívej se skutečnými spisy advokátky.

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Koncipient.Jadro;
using Koncipient.Jadro.Anonymizace;
using Koncipient.Ocr;

Console.OutputEncoding = Encoding.UTF8;
var json = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

var argumenty = args.ToList();
string? Volba(string nazev)
{
    int i = argumenty.IndexOf(nazev);
    if (i < 0 || i + 1 >= argumenty.Count) return null;
    var v = argumenty[i + 1];
    argumenty.RemoveRange(i, 2);
    return v;
}
bool Prepinac(string nazev) => argumenty.Remove(nazev);

var slozkaSpisu = Volba("--spisy");
var uloziste = new Uloziste(slozkaSpisu);

int Vypis(object o) { Console.WriteLine(JsonSerializer.Serialize(o, json)); return 0; }
int Chyba(string zprava, int kod = 1) { Console.Error.WriteLine(JsonSerializer.Serialize(new { chyba = zprava }, json)); return kod; }

bool TestovaciData() => Environment.GetEnvironmentVariable("KONCIPIENT_TESTOVACI_DATA") == "1";
const string Ochrana = "Tento příkaz vypisuje obsah spisu. Agent (Claude Code) ho posílá do cloudu, proto běží jen s KONCIPIENT_TESTOVACI_DATA=1 a jen na testovacích nebo vymyšlených datech, nikdy na skutečných spisech advokátky.";

Spis NajdiSpis(string id) => uloziste.Spisy().FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
    ?? throw new ArgumentException($"Spis {id} neexistuje. Seznam: koncipient-cli spisy");
Strana NajdiStranu(Spis spis, string poradi) =>
    int.TryParse(poradi, out var n) && n >= 1 && n <= spis.Strany.Count ? spis.Strany[n - 1]
    : throw new ArgumentException($"Strana {poradi} neexistuje, spis má {spis.Strany.Count} stran (číslují se od 1 v pořadí spisu).");

object StavStrany(Spis spis, Strana s) => new
{
    strana = spis.Poradi(s),
    dokument = spis.DokumentStrany(s)?.Nazev,
    soubor = s.PuvodniNazev,
    prepis = Motory.ProZobrazeni(spis, s)?.Motor,
    opraveno = spis.MaOcr(s, Spis.Opraveno),
    zkontrolovano = s.Zkontrolovano,
    otoceni = s.Otoceni,
};

if (argumenty.Count == 0 || argumenty[0] is "help" or "--help" or "-h") return Napoveda();
var prikaz = argumenty[0];
argumenty.RemoveAt(0);

try
{
    switch (prikaz)
    {
        case "stahni-modely":
        {
            // pro sestavení instalátoru: stáhne modely do zadané složky (instalace/sestav.ps1)
            var kam = Volba("--kam") ?? Motory.SlozkaModelu;
            await Motory.StahniAsync(new Progress<string>(t => Console.Error.WriteLine(t)), default, Path.GetFullPath(kam));
            return Vypis(new { slozka = Path.GetFullPath(kam), soubory = Directory.GetFiles(kam).Select(Path.GetFileName) });
        }

        case "spisy":
            return Vypis(new { slozka = uloziste.Slozka, spisy = uloziste.Spisy().Select(s => new { id = s.Id, nazev = s.Nazev, stran = s.Strany.Count, dokumentu = s.Dokumenty.Count }) });

        case "zaloz":
        {
            var spis = uloziste.Zaloz(argumenty.Count > 0 ? string.Join(" ", argumenty) : "");
            return Vypis(new { id = spis.Id, slozka = spis.Slozka });
        }

        case "pridej-fotky":
        {
            var spis = NajdiSpis(argumenty[0]);
            var nazev = Volba("--dokument");
            bool bezCernych = Prepinac("--bez-cernych");
            var nove = spis.PridejSoubory(argumenty.Skip(1).ToList(), nazev, cerneOddeluji: !bezCernych);
            return Vypis(new { pridano = nove.Count, dokumentu = spis.Dokumenty.Count, oddelovacu = spis.Oddelovace.Count });
        }

        case "oddelovace":
        {
            // fotky vzaté jako oddělovače dokumentů; vypisuje jen názvy souborů a místo ve spisu
            var spis = NajdiSpis(argumenty[0]);
            int? Poradi(int? cislo) => spis.Strany.FirstOrDefault(s => s.Cislo == cislo) is { } s ? spis.Poradi(s) : null;
            return Vypis(new
            {
                spis = spis.Id,
                oddelovace = spis.Oddelovace.Select((o, k) => new
                {
                    cislo = k + 1, soubor = o.PuvodniNazev, fotka = Path.Combine(spis.Slozka, o.Fotka),
                    zaStranou = Poradi(o.ZaStranou), predStranou = Poradi(o.PredStranou), rozdelilDokument = o.ZacalDokument is not null,
                }),
            });
        }

        case "vrat-oddelovac":
        {
            var spis = NajdiSpis(argumenty[0]);
            if (!int.TryParse(argumenty.ElementAtOrDefault(1), out var n) || n < 1 || n > spis.Oddelovace.Count)
                return Chyba($"Oddělovač {argumenty.ElementAtOrDefault(1)} neexistuje, spis jich má {spis.Oddelovace.Count} (koncipient-cli oddelovace {spis.Id}).");
            var strana = spis.VratOddelovac(spis.Oddelovace[n - 1]);
            return Vypis(StavStrany(spis, strana));
        }

        case "pridej-slozku":
        {
            var spis = NajdiSpis(argumenty[0]);
            var nove = spis.PridejSlozku(argumenty[1]);
            return Vypis(new { pridano = nove.Count, dokumenty = spis.StranyPoDokumentech().Select(x => new { nazev = x.Dokument.Nazev, stran = x.Strany.Count }) });
        }

        case "prepis":
        {
            // stejně jako tlačítko Spustit přepis: narovnání, OCR, sloučení, názvy dokumentů, hledání údajů
            var spisy = argumenty.Count > 0 ? new List<Spis> { NajdiSpis(argumenty[0]) } : uloziste.Spisy().ToList();
            await Motory.StahniAsync(new Progress<string>(t => Console.Error.WriteLine(t)));
            var posledni = "";
            var chyby = await Davka.ZpracujAsync(spisy, Motory.ProDavku(), new Progress<StavDavky>(s =>
            {
                var r = $"{s.Popis} ({s.Hotovo}/{s.Celkem})";
                if (r != posledni) Console.Error.WriteLine(r);
                posledni = r;
            }), default, Motory.Narovnani());
            foreach (var s in uloziste.Spisy().Where(x => spisy.Any(y => y.Id == x.Id)))
            {
                var klic = Anonymizator.NactiKlic(s);
                Anonymizator.AktualizujKlic(s, klic);
                Anonymizator.UlozKlic(s, klic);
            }
            return Vypis(new { hotovo = chyby.Count == 0, spisy = spisy.Select(s => s.Id), motory = Motory.ProDavku().Count, windowsOcr = Motory.WindowsOcrJe,
                chyby = chyby.Select(c => new { spis = c.Spis, strana = c.Strana, krok = c.Krok, zprava = c.Zprava }) });
        }

        case "kontrola":
        {
            // rychlá kontrola: místa s nahrazenými údaji a podezřelá slova (vypisuje obsah spisu)
            if (!TestovaciData()) return Chyba(Ochrana, 3);
            var spis = NajdiSpis(argumenty[0]);
            var mista = RychlaKontrola.Najdi(spis, Anonymizator.NactiKlic(spis));
            return Vypis(new
            {
                mista = mista.Select(m => new
                {
                    strana = spis.Poradi(m.Strana), druh = m.Druh.ToString(), udaj = m.Udaj, nahrada = m.Druh == DruhMista.Nahrazeno ? m.Nahrada : null,
                    kod = m.Entita?.Id, pocet = m.Pocet, stran = m.Strany.Select(spis.Poradi), kontext = $"{m.Pred} [{m.Udaj}] {m.Po}",
                }),
                stranyBezNalezu = RychlaKontrola.StranyBezNalezu(spis, mista).Select(spis.Poradi),
            });
        }

        case "strany":
        {
            var spis = NajdiSpis(argumenty[0]);
            return Vypis(new
            {
                spis = spis.Id,
                dokumenty = spis.StranyPoDokumentech().Select((x, k) => new
                {
                    poradi = k + 1, nazev = x.Dokument.Nazev, strany = x.Strany.Select(s => StavStrany(spis, s)),
                }),
            });
        }

        case "text":
        {
            if (!TestovaciData()) return Chyba(Ochrana, 3);
            var spis = NajdiSpis(argumenty[0]);
            var strana = NajdiStranu(spis, argumenty[1]);
            var v = Motory.ProZobrazeni(spis, strana);
            if (v is null) return Chyba("Strana zatím nemá přepis, spusť: koncipient-cli prepis " + spis.Id);
            bool anonym = Prepinac("--anonymizovany");
            if (anonym) return Vypis(new { strana = spis.Poradi(strana), text = Anonymizator.AnonymizovanyText(v, Anonymizator.NactiKlic(spis)) });
            bool slova = Prepinac("--slova");
            return Vypis(new
            {
                strana = spis.Poradi(strana), motor = v.Motor, text = v.Text,
                slova = slova ? v.Slova.Select((s, i) => new { i, text = s.Platny, sporne = s.Sporne, ramecek = s.Ramecek }) : null,
            });
        }

        case "udaje":
        {
            if (!TestovaciData()) return Chyba(Ochrana, 3);
            var spis = NajdiSpis(argumenty[0]);
            var klic = Anonymizator.NactiKlic(spis);
            return Vypis(new
            {
                styl = klic.Styl.ToString(),
                udaje = klic.Entity.Select(e => new
                {
                    id = e.Id, typ = Nazvy.Typ(e.Typ), akce = e.Akce.ToString(), skutecnost = e.Popis, nahrada = e.TextNahrady,
                    role = e.Role, patriK = e.PatriK, tvary = e.Tvary.Select(t => string.Join(" ", t)),
                }),
            });
        }

        case "udaj":
        {
            // koncipient-cli udaj S-0001 OSOBA_001 --akce ponechat --role žalobce --nahrada "Bohdan Jasaník"
            var spis = NajdiSpis(argumenty[0]);
            var klic = Anonymizator.NactiKlic(spis);
            var e = klic.Podle(argumenty[1]) ?? throw new ArgumentException($"Údaj {argumenty[1]} v klíči není.");
            if (Volba("--akce") is { } akce) e.Akce = akce.ToLowerInvariant() switch
            {
                "nahradit" or "pseudonym" => Akce.Pseudonym,
                "odstranit" => Akce.Odstranit,
                "ponechat" or "neanonymizovat" => Akce.Ponechat,
                _ => throw new ArgumentException("Akce: nahradit, odstranit, ponechat."),
            };
            if (Volba("--role") is { } role) e.Role = role.Length == 0 ? null : role;
            if (Volba("--nahrada") is { } nahrada) e.VlastniNahrada = nahrada.Length == 0 ? null : nahrada;
            if (Prepinac("--smazat")) klic.Entity.Remove(e);
            Anonymizator.UlozKlic(spis, klic);
            return Vypis(new { id = e.Id, akce = e.Akce.ToString(), role = e.Role, nahrada = e.TextNahrady });
        }

        case "oznac":
        {
            // koncipient-cli oznac S-0001 3 12 13 osoba   (slova 12–13 strany 3, čísla z: text --slova)
            var spis = NajdiSpis(argumenty[0]);
            var strana = NajdiStranu(spis, argumenty[1]);
            var v = Motory.ProZobrazeni(spis, strana) ?? throw new ArgumentException("Strana nemá přepis.");
            var typ = Enum.GetValues<TypUdaje>().First(t => Nazvy.Typ(t).Equals(argumenty[4], StringComparison.OrdinalIgnoreCase)
                                                          || t.ToString().Equals(argumenty[4], StringComparison.OrdinalIgnoreCase));
            var klic = Anonymizator.NactiKlic(spis);
            var e = Anonymizator.OznacRucne(klic, typ, Anonymizator.Slova(v), int.Parse(argumenty[2]), int.Parse(argumenty[3]));
            Anonymizator.DoplnOsoby(klic, Anonymizator.Slova(v));
            Anonymizator.UlozKlic(spis, klic);
            return Vypis(new { id = e.Id, typ = Nazvy.Typ(e.Typ), nahrada = e.TextNahrady });
        }

        case "necitlive":
        {
            var spis = NajdiSpis(argumenty[0]);
            var klic = Anonymizator.NactiKlic(spis);
            foreach (var slovo in argumenty.Skip(1)) Anonymizator.OznacNecitlive(klic, slovo);
            Anonymizator.UlozKlic(spis, klic);
            return Vypis(new { vyjimek = klic.Vyjimky.Count });
        }

        case "styl":
        {
            var spis = NajdiSpis(argumenty[0]);
            var klic = Anonymizator.NactiKlic(spis);
            klic.Styl = Enum.Parse<StylPseudonymu>(argumenty[1], ignoreCase: true);
            foreach (var o in klic.Entity.Where(o => o.Typ == TypUdaje.Osoba && o.VlastniNahrada is null)) o.PseudoJmeno = o.PseudoPrijmeni = null;
            Anonymizator.DoplnOsoby(klic, spis.Strany.Select(s => Motory.ProZobrazeni(spis, s)).OfType<VysledekOcr>().SelectMany(Anonymizator.Slova));
            Anonymizator.UlozKlic(spis, klic);
            return Vypis(new { styl = klic.Styl.ToString() });
        }

        case "zkontroluj":
        {
            var spis = NajdiSpis(argumenty[0]);
            bool zrusit = Prepinac("--zrusit");
            var strany = argumenty.Count < 2 || argumenty[1] == "vse" ? spis.Strany : argumenty.Skip(1).Select(p => NajdiStranu(spis, p)).ToList();
            foreach (var s in strany) s.Zkontrolovano = !zrusit;
            spis.Uloz();
            return Vypis(new { zkontrolovano = spis.Strany.Count(s => s.Zkontrolovano), celkem = spis.Strany.Count });
        }

        case "otoc":
        {
            var spis = NajdiSpis(argumenty[0]);
            var obnoveno = spis.OtocStranu(NajdiStranu(spis, argumenty[1]), int.Parse(argumenty[2]));
            return Vypis(new { obnovenPrepis = obnoveno });
        }

        case "dokument":
        {
            // koncipient-cli dokument S-0001 <strana> posun|presun|novy|spoj|prejmenuj|navrhni [hodnota]
            var spis = NajdiSpis(argumenty[0]);
            var strana = NajdiStranu(spis, argumenty[1]);
            var hodnota = argumenty.Count > 3 ? string.Join(" ", argumenty.Skip(3)) : null;
            switch (argumenty[2])
            {
                case "posun": spis.PosunStranu(strana, int.Parse(hodnota ?? "1")); break;
                case "presun":
                    var cil = spis.Dokumenty.FirstOrDefault(d => d.Id == hodnota || d.Nazev == hodnota);
                    spis.PresunDoDokumentu(strana, cil?.Id, cil is null ? hodnota : null);
                    break;
                case "novy": spis.ZacniDokument(strana, hodnota); break;
                case "spoj": spis.SpojSPredchozim(strana); break;
                case "prejmenuj":
                    var d = spis.DokumentStrany(strana)!;
                    d.Nazev = hodnota ?? d.Nazev; d.NavrhnoutNazev = false; spis.Uloz();
                    break;
                case "navrhni": spis.DokumentStrany(strana)!.NavrhnoutNazev = true; spis.NavrhniNazvy(); break;
                default: return Chyba("Akce: posun, presun, novy, spoj, prejmenuj, navrhni.");
            }
            return Vypis(new { dokumenty = spis.StranyPoDokumentech().Select(x => new { nazev = x.Dokument.Nazev, strany = x.Strany.Select(spis.Poradi) }) });
        }

        case "export":
        {
            var spis = NajdiSpis(argumenty[0]);
            var cil = Volba("--cil");
            var ukol = Volba("--ukol");
            var vysledek = Export.Proved(spis, Anonymizator.NactiKlic(spis), cil, Dokumenty(spis, Volba("--dokumenty")), ukol);
            return vysledek.Uspech
                ? Vypis(new { uspech = true, slozka = vysledek.Slozka })
                : Vypis(new { uspech = false, problemy = vysledek.Problemy });
        }

        case "zaloha":
        {
            var (cil, souboru) = Zaloha.Proved(uloziste, Path.GetFullPath(argumenty[0]));
            return Vypis(new { cil, souboru });
        }

        case "data":
        {
            // přehled dat ve spisu (vypisuje obsah spisu)
            if (!TestovaciData()) return Chyba(Ochrana, 3);
            var spis = NajdiSpis(argumenty[0]);
            return Vypis(new { data = PrehledDat.Najdi(spis).Select(x => new { datum = x.Datum.ToString("yyyy-MM-dd"), druh = x.Druh, strana = spis.Poradi(x.Strana), kontext = $"{x.Pred} [{x.Text}] {x.Po}" }) });
        }

        case "zaznam":
        {
            // záznam o exportech (jen počty, žádné údaje)
            var spis = NajdiSpis(argumenty[0]);
            if (Prepinac("--text")) { Console.WriteLine(ZaznamyExportu.Prehled(spis)); return 0; }
            return Vypis(new { spis = spis.Id, zaznamy = ZaznamyExportu.Nacti(spis) });
        }

        case "kvalita":
        {
            // strany k přefocení (jen důvody, žádný obsah)
            var spis = NajdiSpis(argumenty[0]);
            return Vypis(new { kPrefoceni = KvalitaFotek.Najdi(spis).Select(k => new { strana = spis.Poradi(k.Strana), soubor = k.Strana.PuvodniNazev, duvody = k.Duvody }) });
        }

        case "nahrad-fotku":
        {
            var spis = NajdiSpis(argumenty[0]);
            var strana = NajdiStranu(spis, argumenty[1]);
            spis.NahradFotku(strana, argumenty[2]);
            return Vypis(StavStrany(spis, strana));
        }

        case "ponechat-fotku":
        {
            // stranu nejde přefotit: ponechat a přestat ji nabízet
            var spis = NajdiSpis(argumenty[0]);
            var strana = NajdiStranu(spis, argumenty[1]);
            spis.PonechatBezPrefoceni(strana);
            return Vypis(StavStrany(spis, strana));
        }

        case "na-disk":
        {
            // kopie ProClaude na flash disk (nebo do zadané složky) s kontrolou, že tam nejsou fotky ani klíč
            var zdroj = Volba("--proclaude") ?? Prenos.MistniSlozka;
            return Vypis(new { cil = Path.Combine(Path.GetFullPath(argumenty[0]), Prenos.NazevSlozky), souboru = Prenos.NaDisk(zdroj, Path.GetFullPath(argumenty[0]), uloziste.Spisy()) });
        }

        case "z-disku":
        {
            var cil = Volba("--proclaude") ?? Prenos.MistniSlozka;
            var nove = Prenos.ZDisku(Path.GetFullPath(argumenty[0]), cil);
            return Vypis(new { nacteno = nove.Select(v => new { spis = v.Spis, soubor = v.Soubor }) });
        }

        case "pdf":
        {
            // prohledatelné PDF pro archiv advokátky (obsahuje skutečné údaje, zůstává v počítači)
            var spis = NajdiSpis(argumenty[0]);
            var dokumenty = Dokumenty(spis, Volba("--dokumenty"));
            var cil = Path.GetFullPath(argumenty[1]);
            return Vypis(new { soubor = cil, stran = ArchivPdf.Uloz(spis, cil, dokumenty) });
        }

        case "tisk":
        {
            // upravené fotky k tisku (oříznutí, narovnání, černobílá) do PDF A4; skutečné údaje, jen pro tisk
            var spis = NajdiSpis(argumenty[0]);
            var dokumenty = Dokumenty(spis, Volba("--dokumenty"));
            bool barevne = Prepinac("--barevne"), neorezavat = Prepinac("--neorezavat");
            var cil = Path.GetFullPath(argumenty[1]);
            return Vypis(new { soubor = cil, stran = UpravaProTisk.UlozPdf(spis, cil, dokumenty, !neorezavat, !barevne) });
        }

        case "hledej":
        {
            if (!TestovaciData()) return Chyba(Ochrana, 3);
            var spis = NajdiSpis(argumenty[0]);
            var nalezy = Hledani.Najdi(spis, string.Join(" ", argumenty.Skip(1)));
            return Vypis(new { nalezu = nalezy.Count, nalezy = nalezy.Select(n => new { strana = spis.Poradi(n.Strana), od = n.Od, @do = n.Do, kontext = $"{n.Pred} [{n.Text}] {n.Po}" }) });
        }

        case "vrat-jmena":
        {
            if (!TestovaciData()) return Chyba(Ochrana, 3);
            var spis = NajdiSpis(argumenty[0]);
            var useky = ZpetneDosazeni.Vrat(File.ReadAllText(argumenty[1]).Replace("\r\n", "\n"), Anonymizator.NactiKlic(spis));
            var text = ZpetneDosazeni.Spoj(useky);
            if (Volba("--docx") is { } docx) Word.Uloz(docx, text);
            return Vypis(new
            {
                text,
                kontrolovat = useky.Where(u => u.Druh == DruhUseku.Zkontrolovat).Select(u => new { u.Text, u.Poznamka }),
                doplnit = useky.Where(u => u.Druh == DruhUseku.Doplnit).Select(u => new { u.Text, u.Poznamka }),
            });
        }

        default:
            return Chyba($"Neznámý příkaz {prikaz}. Nápověda: koncipient-cli help");
    }
}
catch (Exception e) when (e is ArgumentException or ArgumentOutOfRangeException or FileNotFoundException or DirectoryNotFoundException or InvalidOperationException or InvalidDataException)
{
    return Chyba(e.Message);
}

// --dokumenty 1,3 (pořadí dokumentů ve spisu) nebo "nove" (ještě neexportované); bez volby celý spis
static IReadOnlyCollection<string>? Dokumenty(Spis spis, string? volba)
{
    if (volba is null) return null;
    var poradi = spis.StranyPoDokumentech().Select(x => x.Dokument).ToList();
    if (volba == "nove") return poradi.Where(d => d.Exportovano is null).Select(d => d.Id).ToList();
    return volba.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(c => int.TryParse(c, out var n) && n >= 1 && n <= poradi.Count ? poradi[n - 1].Id
            : throw new ArgumentException($"Dokument {c} neexistuje, spis má {poradi.Count} dokumentů."))
        .ToList();
}

static int Napoveda()
{
    Console.WriteLine("""
        koncipient-cli: ovládání Koncipientu z příkazové řádky (výstup JSON)

        Společná volba: --spisy <složka>  (jinak Dokumenty\Koncipient\Spisy nebo KONCIPIENT_SPISY)

        Spisy a fotky
          spisy                                   seznam spisů
          zaloz <název>                           nový spis
          stahni-modely [--kam <složka>]          stáhne modely OCR (jen pro sestavení instalátoru)
          pridej-fotky <spis> <soubor>... [--dokument <název>] [--bez-cernych]   fotky i PDF
          pridej-slozku <spis> <složka>           podsložky = dokumenty, černá fotka = nový dokument
          oddelovace <spis>                       fotky vzaté jako oddělovače dokumentů (k ruční kontrole)
          vrat-oddelovac <spis> <číslo>           oddělovač byla stránka: vrátit ji na místo a spojit dokumenty
          prepis [<spis>]                         narovnání, OCR, sloučení, názvy dokumentů, hledání údajů
          strany <spis>                           dokumenty a strany se stavem
          otoc <spis> <strana> <90|180|270>
          dokument <spis> <strana> posun|presun|novy|spoj|prejmenuj|navrhni [hodnota]

        Anonymizace
          udaje <spis>                            *  údaje v klíči se skutečnými hodnotami
          kontrola <spis>                         *  rychlá kontrola: místa s nahrazenými údaji a podezřelá slova
          text <spis> <strana> [--slova] [--anonymizovany]   *  přepis strany (--slova = čísla slov pro oznac)
          oznac <spis> <strana> <od> <do> <typ>   slova od–do označit (osoba, firma, adresa, nemovitost, jiny…)
          necitlive <spis> <slovo>...             slova nejsou citlivá
          udaj <spis> <id> [--akce nahradit|odstranit|ponechat] [--role <role>] [--nahrada <text>] [--smazat]
          styl <spis> stromy|zvirata|kody         jak se nahrazují osoby
          zkontroluj <spis> [<strana>...|vse] [--zrusit]
          export <spis> [--cil <složka>] [--dokumenty 1,3|nove] [--ukol "text"]   zápis pro Clauda (jen po kontrole stran)
          pdf <spis> <soubor.pdf> [--dokumenty 1,3]   prohledatelné PDF pro archiv (skutečné údaje)
          tisk <spis> <soubor.pdf> [--dokumenty 1,3] [--barevne] [--neorezavat]   upravené fotky k tisku
          zaloha <složka nebo disk>               záloha všech spisů s ověřením
          data <spis>                             *  přehled dat ve spisu seřazený časově (lhůty nepočítá)
          zaznam <spis> [--text]                  záznam o exportech pro GDPR (jen počty, žádné údaje)
          kvalita <spis>                          strany k přefocení (rozmazané, tmavé, uříznuté, nejistý přepis)
          nahrad-fotku <spis> <strana> <soubor>   přefocená strana: nová fotka, přepis se zahodí
          ponechat-fotku <spis> <strana>          stranu nejde přefotit: ponechat, Claude dostane poznámku
          na-disk <kořen disku> [--proclaude <složka>]  ProClaude na flash disk (ověří, že tam nejsou fotky ani klíč)
          z-disku <kořen disku> [--proclaude <složka>]  nové výstupy od Clauda z disku
          hledej <spis> <slovo nebo fráze>        *  hledání ve všech stranách
          vrat-jmena <spis> <soubor.md> [--docx <výstup.docx>]   *

        * vypisuje obsah spisu: jen s KONCIPIENT_TESTOVACI_DATA=1 a jen na testovacích datech.
          Agent (Claude Code) posílá vše, co přečte, do cloudu. Na skutečné spisy advokátky nikdy.

        Strany se číslují od 1 v pořadí spisu. Chyby jdou na chybový výstup jako JSON {"chyba": …}.
        """);
    return 0;
}
