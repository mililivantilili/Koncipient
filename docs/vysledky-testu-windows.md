# Výsledky testů na Windows z 2026-11-03

Testy 0, 3 (dílčí kontrola), 4, 5, 6 a 7 z [testovani.md](testovani.md) na Matějově notebooku, sestavení Release z větve `claude/friendly-franklin-k4ma6l` (commit 5993e52). Hlavní závěr: program funguje od importu po Word, ale **anonymizace pustila do exportu skutečné údaje v 6 situacích a v jedné další export zablokovala tak, že ho běžně nejde odblokovat**. Nejdřív je potřeba opravit je.

Testy běžely na fotkách z Matějova archivu. Ten do repozitáře nepatří, proto jsou všechny příklady níže přepsané na vymyšlené. Tvar chyby je zachovaný.

## Přehled

| Test | Výsledek | Poznámka |
| --- | --- | --- |
| 0: automatické testy | Prošlo | 370 z 370, sestavení bez upozornění |
| 3: otočení tam a zpět | Prošlo | Text i oprava se vrátí, značka ☑ se ztratí |
| 4: anonymizace a export | Prošlo s chybami | Odmítnutí exportu i obsah exportu v pořádku, úniky viz níže |
| 5: vrácení jmen a Word | Prošlo s výhradou | Pády v pořádku, barvy jsem neviděl, viz Co neověřeno |
| 6: měření | 1 únik z 87 | Windows OCR a PaddleOCR, nahrazeno navíc 2 |
| 7: dokumenty a složky | Prošlo s chybami | Přesuny v pořádku, únik v názvu dokumentu, slabé návrhy názvů |

## Úniky do exportu (přednostně opravit)

### 1. Datum narození spolkne příjmení (příčina nalezena, ověřeno)

Výsledek testu 6: `Žalobce: Ing. Petr Pokusný, nar. 12.3.1975, bytem X` se změní na `Žalobce: Ing. Petr [ODSTRANĚNO: datum narození], bytem X`. Křestní jméno unikne a příjmení zmizí.

Příčina je v `Anonymizator.StejnyTvar` (`Anonymizator.cs:42`). Druhý motor přečte datum jako tři slova `12 3 1975`, takže v klíči vznikne tvar o třech slovech. Číselné údaje se porovnávají jen podle číslic, proto na něj sedí i okno `Pokusný, nar. 12.3.1975,`. `Vyskyty` pak dá přednost delšímu výskytu a celé jméno zahodí. Se samotným tvarem `12.3.1975` je výsledek správný, ověřeno samostatným pokusem.

Do exportu se tenhle únik nedostane, závěrečná kontrola ho zachytí a export odmítne (`zůstal nerozpoznaný údaj (osoba): „Petr“`). Běžnou cestou ale nejde opravit: ruční označení `Petr Pokusný` jako osoby nepomůže, protože delší výskyt data dál vyhrává. Pomohlo až smazání položky s tvarem `12 3 1975` z klíče, pak je výsledek správný a export projde.

Návrh opravy: v číselné větvi vyžadovat číslici v každém slově okna. Totéž se může týkat telefonů a účtů.

### 2. Příjmení v názvu dokumentu

Název `Smlouva Jan Pokusný` skončil v exportu jako `Smlouva Bukáč Pokusný`, a to v nadpisu, v `manifest.json` i v názvu souboru. Závěrečná kontrola to nezachytí.

Příčina je pravděpodobně v `AktualizujKlic` (`Anonymizator.cs:148`): u názvů se nový tvar k nalezené entitě nepřidá. Ve spisu už byla entita s tvarem `Jan` a `NajdiEntitu` na ni celé jméno přiřadila. Když taková entita ve spisu není, název se anonymizuje správně, ověřeno přes `koncipient-cli` na vymyšlené sadě. Kontrolu před exportem by bylo dobré rozšířit i na názvy dokumentů.

### 3. Nerozpoznaná jména

Ověřeno přímo proti `Detektor.Najdi`:

| V textu | Detektor |
| --- | --- |
| `petra smyšlená` (malými, hlavička e-mailu) | nic |
| `To: 15 - Xenie Vymyšlenů` (jméno mimo seznam, příjmení na -ů) | nic |

Ze skutečného exportu navíc: samotné křestní jméno velkými písmeny na samostatném řádku (`JANA`).

### 4. Zbytky jména po chybě OCR

- Useknuté křestní jméno zůstane vedle náhrady: `etr Pokusný` se změní na `etr Topolák`.
- Zkomolené příjmení zůstane: `Stavebník: Jana Pokusn011a` se změní na `Akátníková Velislava Pokusn011a`.

### 5. Adresa bez ulice

`Přístavba RD č.p 158 v Lhotě u Nikde` detektor nenajde, takže číslo popisné i obec v exportu zůstanou. Ve stavebních věcech tím jde přímo určit nemovitost. Totéž platí pro samotný název obce, který se v textu opakuje.

### 6. Doména v odkazu

E-mail `info@fiktivni.cz` se odstraní, ale `http://mail.fiktivni.cz/?_task=mail` v patičce vytištěného e-mailu zůstane a prozradí firmu.

### 7. Firma v jiném tvaru

Entita `FIKTIVNET a.s` nepokryje stejnou firmu napsanou jako `FIKTIVNET,` bez právní formy. Firma zmíněná jen v závorce, například `(Smyšlená 02, a.s)`, se nenajde jako firma. Detektor z ní udělá adresu `(Smyšlená 02,`, a to se projevilo i tím, že v exportu zůstala.

## Ostatní chyby

| Závažnost | Chyba | Kde a jak se projeví |
| --- | --- | --- |
| Střední | Ruční oprava skutečného jména se ztratí | `DoplnOsoby` volá `UrciJmeno` u všech osob znovu (`Anonymizator.cs:177`). Oprava zmizí po změně stylu náhrad a pravděpodobně i po Najít údaje, jméno pak špatně vrátí zpětné dosazení. |
| Střední | Špatný rod u neznámého jména | Žena s příjmením na -ů dostala mužský rod a pseudonym. Tvar `paní Akátníkové` se pak do Wordu nevrátil a zůstal pseudonym. |
| Střední | Ručně označená osoba nemá vymyšlené jméno | `OznacRucne` nevolá `DoplnOsoby`, do dalšího Najít údaje je v náhledu `[OSOBA_004]`. |
| Střední | Není citlivé u víceslovného údaje nic neudělá | Přidá výjimku, ale údaj se dál nahrazuje a program o tom nic neřekne. Výjimky se uplatňují jen na tvary o jednom slově (`Anonymizator.cs:306`). |
| Střední | Iniciály vyberou příliš podobné jméno | Vybrané křestní jméno se od skutečného lišilo jedním písmenem. |
| Střední | Falešné adresy z výkresů a tabulek | Pětimístné číslo a slovo se berou jako PSČ a obec: `12345 VSTUPY`, `54321 NOVE SITE`. Na jediném spisu s výkresy 7 takových položek. |
| Střední | Dvě osoby sloučené do jedné | Ve vymyšlené sadě skončily `Marie Nevšední` a její syn `Jiří Nevšední` pod jedním pseudonymem. Zpětné dosazení by pak vrátilo špatné jméno. |
| Nízká | Skloňování křestního jména | `Karla Vzorečka` se změnilo na `Bohdan Modřínáka`, křestní jméno zůstalo v 1. pádě. |
| Nízká | Další falešné nálezy | Číslo `347` jako spisová značka, slovo před firmou v entitě (`SPRÁVĚ FIKTIVNET a.s`), role ve jméně (`Stavebník Jana` při zkomolené diakritice). Tím má jedna osoba dva pseudonymy. |
| Nízká | Návrhy názvů dokumentů | Berou se z hlavičkového papíru: zkomolený firemní slogan nebo věta z reklamy. Vhodnější by bylo přeskočit krátké řádky hlavičky a hledat slova jako Rozhodnutí, Smlouva, Stanovisko. |
| Nízká | Hláška o nerozpoznaném údaji mate | `Ebenáková Lhota`: skutečný zbytek je jen druhé slovo, první je pseudonym. Blokace sama je správná a našla reálný únik. |
| Nízká | Konec dávky není vidět | `Přepis dokončen` hned přepíše stav zobrazené strany. Po dávce přes noc není poznat, že doběhla. |
| Nízká | Hláška po importu | Při černé fotce píše `Přidáno 5 fotek`, přitom jsou 4 strany a 2 dokumenty. |
| Nízká | Nový dokument na konci | Přesunem do nového dokumentu se strana zařadí na konec spisu, ne vedle původního místa. |
| Nízká | Návod je zastaralý | `testovani.md` uvádí 358 testů a v testu 4 `strany/*.md`, program píše `dokumenty/*.md`. |

## Co fungovalo

- Export bez zkontrolovaných stran se odmítne a vypíše strany. V exportu nejsou fotky ani `klic.json`.
- Stejné jméno v jiném pádě a s chybou OCR dostane stejný pseudonym napříč stranami.
- Tohle neanonymizovat, Jiné vymyšlené jméno, přepnutí na kódy a zpět i Převzít z jiného spisu fungují. Stejné osoby dostaly stejná jména.
- Word: nadpisy, tučné písmo a odrážky jsou ve struktuře dokumentu správně. Skloňovaná jména se vrátila ve správném pádě a kód značky se nahradil skutečnou hodnotou.
- Import složky: podsložky jsou dokumenty, `IMG_2` je před `IMG_10`.
- Černá fotka se neuloží a rozdělí dokumenty. Přesuny stran přes hranici dokumentu, spojení a rozdělení fungují.

## Co neověřeno

- **Okno vrácení jmen jsem neviděl.** Při ovládání přes UI Automation byla všechna vedlejší okna bílá, i obyčejný dialog Nový spis, který Matějovi v testu 2 fungoval. Beru to jako vlastnost mého ovládání, ne chybu programu. Barvy a popisky proto musí ověřit Matěj.
- Dokument Word jsem četl jako XML, v samotném Wordu otevřený nebyl.
- Černá fotka byla vygenerovaná, ne vyfocená mobilem.
- Tooltipy a zvětšení Ctrl + kolečko jsem nezkoušel.
- Čísla z testu 4 a 7 jsou z 11 stran z jednoho archivu, nejde o měření úspěšnosti.
