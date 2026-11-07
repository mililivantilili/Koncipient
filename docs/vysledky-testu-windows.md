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

`Přístavba RD č.p 123 v Lhotě u Nikde` detektor nenajde, takže číslo popisné i obec v exportu zůstanou. Ve stavebních věcech tím jde přímo určit nemovitost. Totéž platí pro samotný název obce, který se v textu opakuje.

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

## Stav oprav

Opraveno v cloudové relaci 2026-11-03, každá oprava má automatický test na vymyšleném příkladu (`tests/Koncipient.Testy/WindowsNalezyTesty.cs`), celkem 405 testů.

| Nález | Oprava |
| --- | --- |
| 1. Datum spolkne příjmení | Číselný údaj sedí jen na slova s číslicí (nebo názvem měsíce), datum `12 3 1975` už nepřekryje jméno |
| 2. Příjmení v názvu dokumentu | Samotné křestní jméno už osoby nespojuje, tvar z názvu se přidá k entitě a závěrečná kontrola ho najde |
| 3. Nerozpoznaná jména | Jména za `Od:`, `Komu:`, `To:` v hlavičce e-mailu (i malými a mimo seznam), jméno a příjmení velkými písmeny |
| 4. Zbytky po OCR | Useknutý začátek křestního jména před příjmením a příjmení s číslicemi z OCR (`Pokusn011a`) patří k osobě |
| 5. Adresa bez ulice | `č.p.`/`č. ev.` s číslem a obcí, obec z více slov se pak najde i samostatně |
| 6. Doména v odkazu | Webové odkazy (`http://`, `www.`) se odstraňují |
| 7. Firma v jiném tvaru | Firma se hledá i bez právní formy, slova jako Dodavatel, Správě, společnost už nejsou součástí názvu |
| Ruční oprava jména | Opravené jméno se označí a program ho už neodvozuje znovu |
| Rod neznámého jména | U dvouslovného jména mimo seznam je první slovo křestní a rod se určí z něj (Xenie → žena) |
| Ručně označená osoba | Hned dostane vymyšlené jméno |
| Není citlivé u víceslovného údaje | Údaje zasažené výběrem se nastaví na Ponechat, program to oznámí |
| Iniciály | Vymyšlené jméno se od skutečného liší aspoň o tři písmena |
| Falešné adresy z výkresů | Pětimístné číslo se slovem VELKÝMI písmeny se nebere jako PSČ s obcí |
| Matka a syn sloučení | Různá křestní jména u stejného příjmení znamenají různé osoby |
| Skloňování křestního jména | Karla → Karel podle příjmení, pohyblivé e i u -el (Karel, Pavel, Havel) |
| Hláška kontroly | Ukazuje jen skutečný zbytek, ne vymyšlené jméno vedle něj |
| Konec dávky | Titulek okna ukáže „přepis dokončen“ s časem |
| Hláška po importu | Počet stran a dokumentů, zmínka o černých fotkách |
| Nový dokument na konci | Zařadí se hned za původní dokument strany |
| ☑ po otočení | Kontrola se pamatuje pro každé natočení a vrátí se s přepisem |
| Návrhy názvů | Víc typů dokumentů (stanovisko, povolení, technická zpráva…), krátké řádky hlavičky se přeskakují |
| Zastaralý návod | Opraveno v `testovani.md` |

Neřešeno: falešná spisová značka `347` (bez kontextu nejde určit pravidlo) a role zkomolená OCR ve jméně. Oboje zachytí kontrola v tabulce Osoby a údaje.


## Dotestování 2026-11-04 (commit da67223)

Opravy z minula fungují, ale na archivu zůstaly 3 druhy úniků, jedna nová chyba a nepoznaná černá fotka. Testováno přes `koncipient-cli` v oddělené složce spisů, 17 stran z archivu a vymyšlená sada. Příklady jsou opět přepsané na vymyšlené.

| Test | Výsledek |
| --- | --- |
| Automatické testy | 412 z 412 |
| Měření na vymyšlené sadě s Windows OCR | 0 úniků z 87 (minule 1), nahrazeno navíc 2 |
| Datum a jméno, název dokumentu, jména v hlavičce za `To:`, zbytky po OCR, odkazy, adresa `č.p 123 v Obci` | Opraveno, v exportu z archivu se už neobjevily |
| Vrácení jmen a `.docx` | Prošlo, ženské jméno mimo seznam má správný rod a správně se vrátilo ve všech zkoušených pádech |
| Závěrečná kontrola | Správně zablokovala export kvůli zbytkům (`Lhota`, `JANA`) a hláška už ukazuje jen zbytek. Po ručním označení export prošel. |

### Zbývající úniky v exportu z archivu

1. **Jméno ve vytištěné hlavičce e-mailu.** Popisky a hodnoty jsou ve dvou sloupcích, OCR je přečte pod sebou: `Odesílatel`, `Adresát`, `Datum`, pak `petra smyšlená`, takže jméno nemá návěští na stejném řádku a malými písmeny ho detektor nepozná. Závěrečná kontrola to nezachytí.
2. **Číslo popisné bez obce za ním nebo v jiném zápisu.** Zůstalo `V [ADRESA_012] čp. 123`, `čp.122`, `čp.123` a po chybě OCR `č,pŔ 123`. Spolu s pseudonymizovanou obcí tím v textu zůstává číslo domu.
3. **Parcelní číslo bez lomítka.** `pozemek p.č. 9999` zůstane, zatímco `p.č. 9998/2` se nahradí. Čísla parcel bez návěští zůstávají i v popisech výkresů.

Rozhodnutí pro advokátku: název orgánu s obcí, například `Územní odbor Obec`, v textu zůstává. Pravidla pro Clauda s tím počítají, ale u malé obce může obec sama prozradit místo.

### Nová chyba: firma spolkne slova věty

Hledání firmy bez právní formy bere i slova před názvem a text se tím ztrácí:

| V textu | V exportu |
| --- | --- |
| `8. Doložit vyjádření FIKTIVNET a.s k sítím` | `8. [FIRMA_001] k sítím` |
| `Přiložte vyjádření FIKTIVNET a.s` | `[FIRMA_005]` |

Věta tím přijde o požadavek, který z ní má Claude číst, a jedna firma dostane několik kódů (na 17 stranách 20 firemních položek, skutečných firem je asi 8). Důležité i proto, že chyba nic neprozradí, takže ji žádná kontrola úniků nezachytí.

### Další falešné nálezy

`12345 Počet` jako adresa (pravidlo pro velká písmena na slovo s malými nepůsobí), `Jana Velkoměsto` jako osoba, `Stavebník Jana` s rolí ve jméně jako samostatná osoba (známé, viz Neřešeno). Tím jedna osoba dostane dva pseudonymy.

### Černé fotky z mobilu

Fotky z telefonu Pixel se zakrytým objektivem, 3 snímky a automatika přepnula do nočního nebo makro režimu. Výsledek není černý, ale **červený nebo šedý rozmazaný obraz**.

| Snímek | Průměrný jas | Odchylka | Hrany | `JeCerna` |
| --- | --- | --- | --- | --- |
| Skoro černý | 0,3 | 0,5 | 0,00 % | Ano |
| Červený (světlo prstem) | 44,1 | 14,8 | 0,00 % | Ano |
| Šedý rozmazaný (makro) | 85,8 | 28,2 | 0,00 % | **Ne**, uloží se jako strana a dokument se nerozdělí |
| Fotky dokumentů v archivu (95 souborů) | od 120 | od 21 | od 3,92 % | Ne |

Hrany: `Canny(60, 180)` na obrázku zmenšeném na šířku 800 px, podíl hranových pixelů.

**Doporučení:** nerozhodovat podle tmavosti, ale podle toho, že na snímku není žádná kresba, například `hrany < 0,05 %` a zároveň `jas < 130`. Mezi oběma skupinami je velká mezera a pravidlo nezávisí na tom, jak konkrétní mobil obraz zesvětlí. Horní mez jasu chrání čistě bílou stránku (třeba prázdnou zadní stranu), kterou pravidlo jen podle hran zahodilo.

Protože mobil advokátky může dát jiný výsledek a nesprávně zahozená strana by v právním spisu vadila, navrhuji: **nemazat oddělovač potichu.** Po importu ukázat, kolik fotek se bere jako oddělovač, s náhledem a možností vrátit je mezi strany. Rozhodnutí, jestli je to potřeba, je na Matějovi.

Doplnění 2026-11-04: Matěj dodal 3 fotky prázdného papíru na stole, stejný mobil. Jas 144 až 164, odchylka 63 až 84, hrany 0,54 až 5,29 %, tedy aspoň desetkrát nad navrženou mezí. Navržené pravidlo na všech dostupných snímcích rozhoduje správně: oddělovače 3 ze 3, prázdné stránky 0 ze 3, dokumenty 0 z 95. Stávající `JeCerna` prázdné stránky také nezahazuje.

Omezení: oddělovače i prázdné stránky jsou z jednoho mobilu a mobil advokátky se zatím testovat nebude. Na všech třech snímcích prázdného papíru je vidět okraj papíru a stůl. Prázdný papír přes celý záběr by hrany mít nemusel, před zahozením ho chrání jen mez jasu, a ta zatím není ověřená. I proto dává smysl oddělovače po importu ukázat a nemazat je potichu.

### Co tentokrát neověřeno

Změny v okně: hláška po importu, konec dávky v titulku, Není citlivé u víceslovného údaje, ruční oprava jména. Ty mají automatické testy nebo jsou v kódu, ale v okně jsem je znovu neprocházel. Vzhled okna vrácení jmen a otevření `.docx` ve Wordu dál zbývá na Matějovi.

## Stav oprav 2026-11-04

Opraveno v cloudové relaci, každá oprava má automatický test v `WindowsNalezyTesty.cs` (celkem 426 testů):

- **Hlavička e-mailu ve dvou sloupcích:** po bloku popisků pod sebou (Odesílatel, Adresát, Datum…) se hodnoty přiřadí podle pořadí, jméno se pozná i malými písmeny. Malými písmeny se pak nahrazuje jen celé jméno, ne samotné příjmení.
- **Číslo popisné:** `čp. 123`, `čp.122` i zkomolené `č,pŔ 123`.
- **Parcelní číslo:** `p.č. 9999` bez lomítka. Čísla parcel bez návěští v popisech výkresů zůstávají, bez nápovědy je nejde odlišit od jiných čísel.
- **Firma nespolkne slova věty:** malé slovo patří k názvu jen za přídavným jménem (`Dopravní služby Kraus s.r.o.` zůstane celá, z `Doložit vyjádření FIKTIVNET a.s` se nahradí jen firma).
- **`12345 Počet`** a podobná záhlaví tabulek (Celkem, Cena, Plocha, Výměra…) už nejsou adresa.
- **Černé fotky:** k původnímu pravidlu přibylo navržené `hrany < 0,05 % a jas < 130` (Canny 60/180 na šířce 800 px). Šedá mlha z makro režimu se tak bere jako oddělovač, bílá stránka ne.

Měření na korpusu (vývojová část 1439 údajů) se nezhoršilo: přes OCR 11 úniků, z textu 12, kontrolní část 11 z 600, vymyšlená sada 0 z 87.

Neřešeno: `Jana Velkoměsto` jako osoba a role ve jméně (`Stavebník Jana`), obojí zachytí tabulka Osoby a údaje. Náhled oddělovačů po importu: Matěj souhlasil, hotovo (okno Oddělovače dokumentů, v CLI `oddelovace` a `vrat-oddelovac`).

## Opakování testu 7, 2026-11-04 (commit 769a13e)

Oddělovače fungují, ale test našel **novou blokující chybu: jedna prázdná stránka zastaví celý přepis všech spisů**. Automatické testy 428 z 428. Testováno v oddělené složce spisů přes `koncipient-cli`, okno Oddělovače i v aplikaci.

| Krok testu 7 | Výsledek |
| --- | --- |
| Složka s podsložkami, pořadí `IMG_2` před `IMG_10` | Prošlo |
| Tři zakryté fotky z Pixelu | Prošlo, všechny 3 poznané včetně šedé, dokument se rozdělí |
| 3 fotky prázdného papíru a fotky dokumentů | Prošlo, žádná není oddělovač |
| Několik oddělovačů za sebou, na začátku a na konci | Prošlo, prázdný dokument nevznikne, všechny jsou v seznamu oddělovačů |
| Okno Oddělovače po importu, Hotovo, hláška s počtem | Prošlo |
| Tlačítko Oddělovače, Je to stránka, vrátit | Prošlo, fotka je na svém místě a dokumenty se spojí |
| Přesuny, nový dokument, spojení, přejmenování, návrh názvu (přes CLI) | Prošlo |
| Export se jménem v názvu dokumentu i složky | Prošlo, jméno je nahrazené v názvu souboru i v manifestu, fotky ani klíč v exportu nejsou |
| Přepis spisu s prázdnou stránkou | **Selhalo**, viz níže |
| Navržené názvy dokumentů | Částečně: `Účel stanoviska: Povolení stavby stavební režim` je použitelný, věta z reklamního sloganu na první straně ne |

### Blokující: prázdná stránka zastaví přepis

Výsledek na 1 ze 3 fotek prázdného papíru. Windows OCR nepřečetlo nic a PaddleOCR přečetlo v záhybu papíru `1`.

- Chyba: `Slouceni.Sluc` (`Slouceni.cs:73`) sáhne na `hlavni.Radky[skupina.Key]`, ale hlavní motor nemá žádný řádek. Krátké číslo jen z kontrolního motoru se přitom vkládá na řádek 0 (`ZpracujMezeru`, řádek 111).
- Dopad: `Davka` se zastaví na první takové straně. Další strany **ve všech spisech** se nesloučí, nenavrhnou se názvy dokumentů a nevznikne klíč (po pádu byly bez klíče i spisy, které samy prázdnou stranu nemají).
- V okně: hláška `Koncipient: chyba` s anglickým textem `Index was out of range…`. Titulek okna zůstane na `přepis běží…` a stav na `Slučuji přepisy…`, i když je tlačítko zase aktivní. Opakované spuštění skončí stejně, spis nejde dokončit.
- Ochrana dat funguje: export takového spisu se odmítne a vypíše nenahrazené údaje, takže jde o funkční chybu, ne o únik.

Návrh opravy:
1. V `Sluc` zacházet s prázdným hlavním motorem: vrátit prázdný výsledek, nebo vytvořit řádek z rámečku slova kontrolního motoru.
2. V dávce zachytit chybu jedné strany, označit ji a pokračovat dalšími, aby jedna strana nezastavila noční dávku.
3. V okně po chybě vrátit titulek a stav a hlášku napsat česky s číslem spisu a strany.
4. Přidat test s hlavním přepisem bez řádků a kontrolním `1`.

### Co tentokrát neověřeno

Přesuny stran přes pravé tlačítko jsem v okně neopakoval, jen přes CLI. Mobil advokátky a prázdný papír přes celý záběr dál chybí.

### Stav oprav po opakování testu 7

Opraveno 2026-11-04 podle návrhu, s automatickými testy (celkem 431):

1. `Slouceni.Sluc` vrátí pro stránku, kde hlavní motor nic nepřečetl, prázdný výsledek. Útržek jen z kontrolního motoru (`1` v záhybu) je na prázdné stránce šum.
2. Dávka zachytí chybu jedné strany (narovnání, každý motor, sloučení, návrh názvů), zapíše ji a pokračuje. Vrací seznam chyb, strana bez výsledku se při dalším spuštění zkusí znovu. `koncipient-cli prepis` je vypíše v poli `chyby`. Hledání údajů běží pro každý spis zvlášť, takže chyba jednoho spisu nezastaví klíč ostatních.
3. Okno: po chybách česká hláška se spisem a stranou, titulek „přepis dokončen s chybami“. Po neočekávané chybě se titulek a stav vrátí a hláška je česky (podrobnost anglicky).
4. Tři fotky prázdného papíru od Matěje jsou zmenšené ve `vzory/prazdne/` a test hlídá, že se nezahodí jako oddělovač (hrany 0,40 až 2,19 %, jas 143 až 164).

Navrhování názvů z reklamního sloganu opraveno 2026-11-04: hlavička papíru, slogan a oslovení se přeskočí, přednost má řádek „Věc:“.

## Kontrola nových funkcí, 2026-11-04 (commit 9d8bf75)

Instalátor a oprava prázdné stránky fungují. **Před předáním advokátce je potřeba vyřešit 2 věci: u skenu s textovou doložkou PDF potichu zmizí celý obsah strany a rychlá ani závěrečná kontrola neodhalí všechny údaje, zatímco návod tvrdí opak.** Automatické testy 437 z 437. Příklady jsou přepsané na vymyšlené.

| Co | Výsledek |
| --- | --- |
| Přepis spisu s prázdnou stránkou | Opraveno, dávka doběhne, export prošel |
| Test 8: instalátor | Prošel, 189 MB, instalace 32 s, bez práv správce, zástupce, odinstalace nechá spisy |
| Přepis z nainstalovaného programu | Prošel s modely z instalace (`modely`), žádné spojení TCP ani s blokovanou sítí přes proxy |
| PDF s textem a sken | Text se převezme, přepíše se jen sken |
| Název PDF se jménem | Nahrazený v názvu souboru i manifestu |
| Prázdná strana v PDF | Přepíše se, nespadne |
| Rychlá kontrola v okně | OK, N, O, U a Strany bez nálezů fungují, strany se označují správně |

### 1. Závažné: sken s doložkou v textové vrstvě ztratí obsah strany

`SpisPdf.TextStrany` bere text z PDF, kdykoli má stránka aspoň 20 písmen. Vyzkoušeno na PDF, kde je celá strana naskenovaný obrázek a dole je textová doložka o elektronickém podpisu nebo konverzi. Výsledek: přepis `pdf-text` obsahuje jen doložku, OCR se pro stranu nespustí a Claude dostane jen tu větu. Program nic nehlásí a rychlá kontrola stranu nabídne mezi **Strany bez nálezů**. Fotka vpravo přitom ukazuje celý text, takže si toho člověk snadno nevšimne.

U dokumentů z datové schránky jde o realistický případ, ověřit na skutečném PDF je ale ještě potřeba. Návrh: vzít text z PDF, jen když stránka nemá velký obrázek přes většinu plochy, nebo text porovnat s pokrytím stránky. Jinak stranu poslat přes OCR.

### 2. Závažné: co kontroly nezachytí

Na 17 stranách z archivu po celé kontrole programu zůstalo v exportu:

| Zůstalo v exportu | Závěrečná kontrola | Rychlá kontrola |
| --- | --- | --- |
| Jméno malými písmeny v hlavičce vytištěného e-mailu (`petra smyšlená`, sloupce popisků a hodnot) | Nezastaví | **Nenabídne**, hlídá jen velké počáteční písmeno |
| Název firmy bez právní formy velkými písmeny (`FIKTIVA`) | Nezastaví | Nabídne jako podezřelé |
| Samostatný název obce z adresy (`Lhota`) | Nezastaví, minule zastavila | Nabídne jako podezřelé |
| Parcelní čísla ve výkresech (`9999/1`) | Nezastaví | Nabídne jako podezřelé |

Rychlá kontrola schválně neukazuje zbytek textu, takže jméno v obecném tvaru nebo příjmení, které je ve slovníku (Malý, Černý), nemusí nikdo vidět. Návod přitom říká `Pokud v něm zůstalo skutečné jméno nebo číslo, export zastaví`, což neplatí. Doporučení:

- Upravit návod: export zastaví jen to, co program pozná. Před exportem je potřeba projít Náhled pro Clauda, aspoň u důležitých stran.
- Přidat do rychlé kontroly slova malými písmeny, která nejsou ve slovníku, a dvojice slov vypadající jako jméno v hlavičkách.
- Rozhodnutí pro Matěje a advokátku: smí se strana považovat za zkontrolovanou, když člověk viděl jen vybraná místa? Pravidlo projektu `Nic se neexportuje bez schválení člověkem` to splňuje jen částečně.

### Střední chyby

| Chyba | Projev |
| --- | --- |
| Falešné osoby z hlaviček e-mailu | `Dobrý den`, `FW`, `Roundcube Webmail` se berou jako osoby. Pozdrav pak v textu pro Clauda nahradí vymyšlené jméno, text je zavádějící. |
| Jedna klávesa N odkryje osobu ve všech stranách | N u nahrazené osoby nastaví Ponechat bez potvrzení. Skutečné jméno pak jde do exportu a kontrola neprotestuje. U osob a čísel by mělo být potvrzení. |
| Příliš mnoho míst na výkresech | 293 míst na 17 stranách (144 nahrazených, 149 podezřelých), hlavně čísla parcel a popisy z výkresů. Tím se kontrola mění v Enter, Enter, Enter. Pomohlo by sloučit stejné údaje napříč stranami. |
| `ICO 99887766` bez háčku | V PDF bez diakritiky zůstane číslo v exportu. Rychlá kontrola ho nabídne. |
| Město jako firma | `Velkoměsto a.s` z delší firmy pak nahradí i samotné město ve větě. |
| Rozbité PDF | Hláška `Koncipient: chyba` anglicky a stav zůstane na `Přidávám 1 fotek…`. Program nespadne. Heslem chráněné PDF jsem nezkoušel. CLI v tomto případě skončí neošetřenou výjimkou. |

### Návod pro advokátku

Je srozumitelný a odpovídá programu, až na tvrzení o exportu výše. Dále: `Nechte počítač zapnutý v síti` může znít jako připojení k internetu, přitom jde o nabíječku. Správně by mělo být `zapojený do zásuvky`.

### Instalátor: co neověřeno

- Síť jsem nevypínal, protože by se přerušila tato relace. Běh jsem hlídal blokací přes proxy a sledováním spojení procesu `koncipient-cli`, okno jsem jen spustil.
- Čistý počítač bez Visual Studia zbývá, stejně jako Windows bez české sady OCR. Ty program řeší přechodem na Tesseract, ale nezkoušel jsem to.
- Velikost po instalaci je 465 MB.
- Skutečné PDF z datové schránky jsem neměl k dispozici.

### Stav oprav po kontrole nových funkcí

Opraveno 2026-11-04 (testy v `PdfTesty`, `RychlaKontrolaTesty`, `WindowsNalezyTesty`, celkem 447; měření na korpusu beze změny):

1. **Sken s doložkou:** stránka s obrázkem přes většinu plochy, na které text z PDF pokrývá méně než 5 % obrázku, se přepíše přes OCR. Vymyšlený vzor `vzory/pdf/sken-s-dolozkou.pdf`. Sken s plnou textovou vrstvou ze skeneru se dál bere z PDF. Na skutečném PDF z datové schránky je potřeba ověřit.
2. **Co kontroly nezachytí:** návod už netvrdí, že export zastaví každý údaj. Rychlá kontrola i návod upozorňují, že důležité strany je potřeba projít v Náhledu pro Clauda. Rychlá kontrola nově hlásí jméno malými písmeny (známé křestní jméno a za ním slovo mimo slovník, např. `petra smyšlená`).
3. **Hlavičky e-mailu:** pozdravy, `FW`, `RE` a názvy poštovních programů (`Roundcube Webmail`) se neberou jako osoba.
4. **N u osob a čísel** se ptá, protože údaj by pak šel Claudovi skutečně. U adres, firem a spisových značek se neptá.
5. **Výkresy:** stejný údaj nebo podezřelé slovo se v rychlé kontrole ukáže jen jednou za spis, s počtem výskytů a stran. Strana je hotová, až když jsou vyřízená všechna místa, která na ní jsou.
6. **`ICO` bez háčku** se najde.
7. **Město jako firma:** jednoslovný název firmy bez právní formy, který je zároveň slovem z adresy, se samostatně nehledá.
8. **Rozbité PDF:** česká hláška (poškozené nebo chráněné heslem), stav se vrátí, CLI vypíše chybu v JSON.
9. Návod: „zapojený do zásuvky, internet nepotřebuje“.

Rozhodnuto Matějem 2026-11-04: rychlá kontrola stranu sama neoznačí. Po vyřízení míst strany ukáže celou stranu v textu pro Clauda vedle fotky a zkontrolovaná je teprve po jejím potvrzení. Hromadné tlačítko Strany bez nálezů zmizelo, takové strany jdou rovnou k potvrzení.

## Kontrola nových funkcí, 2026-11-06 (commit 6a2c825)

Opravy z minula fungují a většina nových funkcí taky. **Nové jsou 3 chyby, kvůli kterým můžou skutečné údaje opustit počítač se spisy nebo zůstat nezkontrolované**: záloha na stejný flash disk, kterým se přenáší ProClaude, kontrola flash disku, která nehlídá text, a rozmazaná strana, která se nenabídne k přefocení. Automatické testy 465 z 465. Testováno přes `koncipient-cli` na archivu (17 stran), vymyšlené sadě a vymyšlených PDF, okno přes UI Automation. Příklady jsou přepsané na vymyšlené.

### Opravy z minula

| Nález | Výsledek |
| --- | --- |
| Sken s doložkou v PDF | Opraveno, strana jde přes OCR a má celý text |
| Jméno malými písmeny v hlavičce e-mailu | Rychlá kontrola ho teď nabídne |
| Potvrzení u N | Funguje u osob. U adres a firem se neptá, viz níže. |
| Sloučení míst | Archiv 194 míst (minule 293): 79 nahrazených, 115 podezřelých |
| Falešné osoby `Dobrý den`, `FW` | Na novém spisu se už neobjevily |
| Poškozené PDF | Česká hláška s názvem souboru |

### Nové funkce

| Test | Výsledek |
| --- | --- |
| 9 a 12: potvrzení celé strany | Funguje: strana dostane ☑ až po Enteru na „celá strana k potvrzení“ |
| 10: hledání | Najde bez diakritiky (`zaloba` → `Žaloba`) i víceslovnou frázi |
| 10: PDF pro archiv | Textová vrstva je na všech stranách a záložka dokumentu je vytvořená. Pořadí slov na 4 ze 6 stran je ale přeházené (`PLNÁ MOC 05 999 Dubová 22,`), kopírování textu z PDF proto dá zpřeházený text. Hledání v Edge jsem neověřoval. |
| 10: export vybraných a jen nových dokumentů | Funguje, `cely_spis: false`, čísla dokumentů jsou stálá. Viz poznámka k přenosu níže. |
| 11: přenos | Kontrola fotek a klíče funguje, výstup od Clauda se načte jen jednou a jména se vrátí. Skutečný flash disk jsem neměl, testoval jsem se složkou. |
| 13: záznam o exportech | Bez jmen a názvů dokumentů |
| 14: úkol pro Clauda | Jméno ve 2. pádě se nahradí ve stejném pádě, nové jméno a datum narození v úkolu export zastaví a bez úkolu `UKOL.md` zmizí |
| 15: přehled dat | Archiv 9 dat (prostý regex jich najde 11). Data narození jsou skrytá. |
| 16: záloha | Nabídka po spuštění funguje a po Ne se připomíná znovu. Záloha i ověření proběhnou. |
| 12: stránky k přefocení | Uříznutá strana se nahlásí. Prázdný papír ne, tmavá fotka se vezme jako oddělovač a ukáže se v okně Oddělovače, nahrazení fotky funguje. |

### Závažné

1. **Záloha na flash disk, kterým se přenáší ProClaude.** Návod k testu 16 přímo doporučuje flash disk. Záloha s fotkami a klíčem i ProClaude pak skončí na stejném disku a ten se zasune do počítače s Claudem, což porušuje pravidlo oddělených počítačů. `Prenos.NaDisk` ani `Zaloha.Proved` to nehlídají. Ověřeno: záloha do složky, kde už je ProClaude, i následné `na-disk` do ní projdou bez varování. Záloha jde uložit i přímo do `ProClaude`. Tam ji sice zachytí kontrola při kopírování na disk, při ručním přenosu složky ale ne. Návrh: záloha odmítne disk nebo složku s ProClaude a `na-disk` odmítne disk, na kterém je `Koncipient-zaloha`. Do návodu napsat, že zálohovací disk patří ke spisům a nikdy se nezasouvá do počítače s Claudem.
2. **Kontrola flash disku nehlídá obsah textových souborů.** `Prenos.Zkontroluj` hlídá jen přípony a názvy. Soubor `poznamky.md` s přepisem strany se skutečnými jmény, uložený do `ProClaude\S-…`, se na disk zkopíroval. Návrh: kopírovat jen soubory, které vytvořil export (podle manifestu, plus `CLAUDE.md` a pravidla). Ostatní `.md` a `.txt` prověřit závěrečnou kontrolou proti klíči spisu.
3. **Nečitelná rozmazaná strana se nenabídne k přefocení.** `KvalitaFotek.Posud` přeskakuje kontrolu ostrosti, když má strana méně než 5 slov. Úplně rozmazaná fotka ale nemá žádné slovo, takže se tváří jako prázdná a v exportu zůstane bez textu. Ověřeno na vymyšlené stránce zmenšené na osminu a zpět: 0 slov, v seznamu k přefocení není. Návrh: strana bez slov, která není prázdná (odchylka jasu nad 12), je „nečitelná, přefotit“. Zároveň u uříznuté strany se přidává i důvod „rozmazaná“, který neplatí.

### Střední

| Chyba | Projev |
| --- | --- |
| N u adresy bez potvrzení | Adresa soukromé osoby se jednou klávesou odkryje na všech stranách. Potvrzení chybí u adres, firem a spisových značek. U adresy osoby (s `patri_k`) by mělo být. |
| Export „Jen nové“ nahradí předchozí export | Ve složce `ProClaude\S-…` zůstane jen nový dokument a `na-disk` smaže na disku starší dokumenty. Claude pak předchozí dokumenty spisu nemá. Pokud je to záměr, patří to do návodu, jinak by „jen nové“ mělo přidávat. |
| Důležitá tlačítka v přetečení lišty | Na monitoru o šířce 1936 px jsou `Načíst od Clauda z flash disku`, `Zálohovat…`, `Záznam o exportech…` a `PDF pro archiv…` skryté za šipkou lišty. Na menším notebooku advokátky jich bude schovaných víc. |
| Záznam o exportech přehání | U exportu jednoho dokumentu uvádí počty nahrazených údajů z celého klíče spisu (13 osob u jedné strany). Věta „neobsahuje žádné osobní údaje“ neplatí úplně, záznam obsahuje uživatelské jméno Windows a cestu. |
| Nápověda u data | `jednání, které se bude konat dne 10.9.2026` má nápovědu `[ze dne]`, ne `[jednání]`. U termínu jednání jde o podstatné. |

### Co neověřeno

Skutečný flash disk, otevření záznamu a dat ve Wordu, Ctrl+F v PDF v Edge, jestli Claude najde UKOL.md, stránky k přefocení vyfocené mobilem (testoval jsem upravené obrázky). Okno vrácení jmen dál zbývá vidět člověkem.

### Stav oprav po kontrole z 6. 11.

Opraveno 2026-11-06 (testy v `PrenosTesty`, `KvalitaTesty`, `PrehledDatTesty`, `HledaniArchivTesty`, celkem 473):

1. **Záloha a přenos:** záloha odmítne disk nebo složku se složkou ProClaude, přenos na flash disk odmítne disk se zálohou. Návod říká, že zálohovací disk patří ke spisům a nikdy se nezasouvá do počítače s Claudem.
2. **Kontrola flash disku:** na disk smí jen soubory, které vytvořil export (dokumenty podle manifest.json, entity, manifest, UKOL.md), pravidla a výstupy od Clauda. CLAUDE.md a výstupy projdou závěrečnou kontrolou proti klíči spisu. Ručně uložený `poznamky.md` export zastaví.
3. **Nečitelná strana:** strana bez slov s ostrostí pod 20 se nabídne k přefocení („nečitelná, program z ní nepřečetl nic“). Prázdný papír na stole má ostrost aspoň 51 a nehlásí se.
4. **N u adresy osoby** (údaj patří k osobě) se ptá jako u osoby.
5. **Export „Jen nové“ přidává:** dokumenty z dřívějšího exportu zůstanou, manifest má všechny a `naposledy_pridane`.
6. **Lišta:** méně častá tlačítka jsou v nabídkách Spis, Claude a Tisk a archiv.
7. **Záznam o exportech** počítá jen údaje z exportovaných stran a uvádí, že obsahuje jméno uživatele Windows a cestu.
8. **Nápověda u data:** termín jednání dál od data („jednání, které se bude konat dne …“) dostane nápovědu „jednání“.
9. **PDF pro archiv:** slova jednoho řádku leží na společném účaří, kopírovaný text by měl mít správné pořadí. Ověřit v Edge.

## Opakování kontroly, 2026-11-06 večer (commit 4a708e9)

Všechny tři závažné chyby i čtyři z pěti středních jsou opravené a nová funkce tisku funguje. Nic blokujícího jsem nenašel. Automatické testy 473 z 473. Testováno přes `koncipient-cli` se složkami místo flash disku, na vymyšlené sadě a archivu.

| Nález z minula | Výsledek |
| --- | --- |
| Záloha na disk s ProClaude | Opraveno: záloha na takový disk i do ProClaude a jejích podsložek se odmítne s jasnou hláškou |
| ProClaude na disk se zálohou | Opraveno: `na-disk` odmítne disk, na kterém je `Koncipient-zaloha` |
| Text ze spisu podstrčený do ProClaude | Opraveno: soubor, který nevytvořil export, se nezkopíruje. Výstup ve `vystup` se skutečným jménem zastaví závěrečná kontrola. |
| Nečitelná rozmazaná strana | Opraveno: „fotka je nečitelná (rozmazaná), program z ní nepřečetl nic“ |
| N u adresy | Opraveno: u adresy patřící osobě se program ptá |
| Export „Jen nové“ | Opraveno: přidá dokument, předchozí zůstanou ve složce i na disku, `cely_spis: true` |
| Tlačítka v přetečení lišty | Opraveno: přesunuté do nabídky Spis a Claude |
| Záznam o exportu | Opraveno: počty jen z exportovaných dokumentů |
| Nápověda u termínu jednání | Opraveno: `[jednání]` |
| Pořadí slov v PDF pro archiv | Opraveno: text jde po řádcích (`PLNÁ MOC Já, níže podepsaný…`) |

Zbývá drobnost: uříznutá strana má v seznamu k přefocení i důvod „fotka je rozmazaná“, který neplatí. (Opraveno 2026-11-07: když motory text přečetly dobře, rozmazání se nehlásí.)

### Test 17: upravené fotky k tisku

`tisk` vytvořil PDF (vymyšlená sada 9 stran, 1,1 MB, archiv 6 stran, 5,9 MB). Stránky A4 jsou oříznuté, narovnané a černobílé. Vytištěný text vymyšlené sady je dobře čitelný, písmo je jen trochu zubaté. U fotky z archivu v průhledné fólii se odlesky změnily v černé skvrny kolem textu, tištěný text zůstal čitelný. Na takové stránky je vhodná volba `--barevne`, kterou jsem neověřoval. PDF k tisku nemá textovou vrstvu a jde jen do počítače se spisy.

### Co neověřeno

Skutečný flash disk (program hledá výměnné disky, s USB diskem hlášeným jako pevný ho neuvidí), skutečný tisk na papír, nabídka v okně (testoval jsem jen přes CLI a čtení XAML) a dál okno vrácení jmen a Word.
