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

Navrhování názvů z reklamního sloganu na první straně zatím zůstává, název jde přejmenovat ručně.

