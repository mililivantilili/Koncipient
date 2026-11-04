# Koncipient: návrh programu pro advokátku

Stav k 2026-11-02, zatím neschváleno. Upraveno podle prvního testu OCR a Matějova přání vyvíjet ve Visual Studiu.

## Shrnutí

Doporučuji desktopový program pro Windows v C#, vyvíjený ve Visual Studiu, který fotky spisu převede na text přímo v počítači a Claude Code předá jen zkontrolovanou anonymizovanou kopii. Advokátka v něm uvidí vlevo text a vpravo fotku, po kliknutí na slovo se na fotce zvýrazní jeho místo.

- **Přepis:** místní OCR, nic se neposílá ven. Fotky se zpracují dávkově, klidně přes noc, dvěma motory (Tesseract a PaddleOCR), jejichž výsledky se sloučí. Slovo „přeložit“ je zatím chápáno jako převod fotky na text, ne překlad do cizího jazyka.
- **Kdo text čte:** přepis čte hlavně program pro anonymizaci a pak Claude. Advokátka čte fotky, proto rozhoduje hlavně přesnost jmen, adres a čísel, ne úhlednost textu.
- **Anonymizace:** program navrhne, advokátka schválí. Hledání údajů počítá s chybami OCR, takže najde i „Miček“ místo „Míček“. Bez lidské kontroly se nic neexportuje.
- **Claude Code:** pracuje jen s kopií bez jmen a bez fotek. Klíč pseudonymů zůstává u Koncipientu.
- **Oddělené počítače:** doporučuji, aby Koncipient se spisy běžel na jiném počítači než Claude Code. Na Windows nejde Claude Code spolehlivě zamknout nastavením, takže na počítači s Claude Code by měl teoreticky přístup ke všem spisům. Šifrování spisů proto program zatím nedělá.

## Hranice citlivých dat

Co jednou Claude Code přečte, odchází k Anthropicu, proto se k němu smí dostat jen schválený export. Hranici tvoří dva počítače, ne nastavení programu.

```text
Počítač A: Koncipient (bez Claude Code)            Počítač B: Claude Code
Fotky -> místní OCR -> anonymizace a schválení -> export ProClaude -> Claude Code -> Anthropic
                                                                          |
klíč pseudonymů --> dosazení jmen <---------------- výstup s pseudonymy <-+
```

| Vrstva | Co dělá | Proti čemu nechrání |
| --- | --- | --- |
| Oddělené počítače | Spisy, fotky a klíč pseudonymů jsou jen na počítači A, kde Claude Code není nainstalovaný | Spisy uložené i na počítači B, třeba v e-mailu nebo ve sdíleném úložišti |
| Přenos jen exportu | Na počítač B se přenáší jen složka `ProClaude` (flash disk nebo sdílená složka, která obsahuje jen export) | Ruční zkopírování jiných souborů |
| Kontrola před exportem | Advokátka schválí každou stranu, program před zápisem znovu hledá známá jména | Údaje, které přehlédne program i člověk |

Sandbox Claude Code [na nativních Windows nefunguje](https://code.claude.com/docs/en/sandboxing), pravidla `deny` tedy nejsou ochrana. Pokud advokátka už Claude Code na počítači se spisy používá, má k nim teoreticky přístup tak jako tak. Koncipient to nezhorší, jen nepřidá ochranu. Šifrování spisů a další izolace se mohou doplnit později.

## Architektura a technologie

Program bude v C# na .NET s rozhraním WPF, vyvíjený ve Visual Studiu, které Matěj používá. Výsledek se zabalí do jednoho instalátoru. OCR motor je za rozhraním, aby šel vyměnit nebo přidat další.

| Část | Návrh | Poznámka |
| --- | --- | --- |
| Úprava fotky | Vyrovnání osvětlení (odečtení odhadnutého pozadí) | V testu snížila chyby Tesseractu z 16–25 % na 1–3 % znaků |
| Otočení fotky | Podle údaje z telefonu (EXIF), a když chybí (foťák), podle textu: tvar řádků a čtení nejdelších řádků normálně a vzhůru nohama | V testu 20 z 20 otočení správně, asi 6 s na stranu, ověřeno i na Windows. Ruční otočení v prohlížeči jako záloha: původní fotka zůstává beze změny a přepis každého natočení se odkládá, takže otočení zpět nic nepřepočítává |
| OCR, motor 1 | Tesseract s češtinou | Rychlý (sekundy na stranu), nejméně chyb v souvislém textu, ztrácí desetinné čárky a levý okraj šikmých fotek |
| OCR, motor 2 | PaddleOCR PP-OCRv6, česky | Pomalejší (v C# kolem 10–15 s na stranu na 4 jádrech), přesná čísla a tabulky, občas ztratí čárku nad í |
| Sloučení | Shodná slova platí, sporná rozhodne český slovník, u čísel má přednost PaddleOCR | V testu nejvíc přesně přepsaných citlivých údajů (33 z 34) |
| Dávkové zpracování | Import fotek a OCR na pozadí, třeba přes noc | Na slabém počítači advokátky může PaddleOCR trvat i desítky sekund na stranu, čas ale nevadí |
| OCR v C# | Tesseract přes NuGet `Tesseract`, PaddleOCR přes ONNX Runtime s oficiálními ONNX modely PP-OCRv6 | Ověřeno 2026-11-03 v cloudu: stejné výsledky jako Python, PaddleOCR 9–16 s na stranu místo 60–80 s. Kód je v `src/Koncipient.Ocr` |
| Windows OCR | Možný třetí motor | Ověřit na Matějově notebooku, v cloudu ho nejde spustit |
| Rukopis | Zatím jen označení „nutné přepsat ručně“ | Vzorky rukopisu zatím chybí, spolehlivé místní řešení pro český rukopis nenalezeno |
| Rozdělené zobrazení | Text vlevo, fotka vpravo, zoom, kliknutí propojí slovo a místo na fotce | Sporná místa ze sloučení a slova s nízkou jistotou jsou podbarvená |
| Hledání údajů | Pravidla (rodné číslo, IČO, účet, telefon, e-mail) a seznam českých jmen | Funguje bez modelu a licencí |
| Odolnost vůči chybám OCR | Porovnání bez diakritiky a s tolerancí jednoho až dvou znaků | Když je jinde ve spisu „Míček“, začerní se i „Miček“ nebo „Mícek“. Zbylé chyby OCR nikdy nebudou nulové |
| Hledání jmen, rozšíření | Místní NER model, například NameTag 3 | Modely jsou nekomerční, pro CNEC 2.0 [nabízí ÚFAL komerční licenci](https://ufal.mff.cuni.cz/node/3032) |
| Sjednocení jmen | Novák, Nováka i Novákovi se sloučí na jednu osobu | Nutné kvůli českému skloňování |
| Karty osob | Údaje na stejném nebo dalším řádku za jménem (adresa, datum narození, telefon) patří k osobě, role se pozná z hlavičky („Žalobce:“) | Tabulka Osoby a údaje: vlevo skutečnost, vpravo náhrada, obojí jde upravit |

Navržené výchozí zacházení s údaji, advokátka si ho může změnit:

| Údaj | Výchozí akce |
| --- | --- |
| Jména osob | Vymyšlené jméno se stejným rodem a skloňováním (téma stromy nebo zvířata), volitelně se stejnými iniciálami, nebo kód `[OSOBA_001]` |
| Firmy | Kód `[FIRMA_001]`, advokátka může napsat vlastní náhradu |
| Rodné číslo, datum narození, doklady, účty, kontakty | Odstranit |
| Adresy | Pseudonym, volitelně ponechat obec |
| Spisové značky, IČO | Pseudonym |
| Data úkonů a lhůt, částky, paragrafy | Ponechat, jsou potřeba k právní práci |

Tlačítko **Tohle neanonymizovat** nechá údaj v textu pro Clauda tak, jak je ve spisu, bez označení (soudy, úřady, obce, veřejní úředníci). V seznamu náhrad pro Clauda je uvedený jako ponechaný.

Klíč pseudonymů má každý spis vlastní, aby Claude nemohl spisy propojit. Advokátka může náhrady převzít z jiného spisu, pak stejná osoba dostane stejné vymyšlené jméno.

### Vymyšlená jména

S kódy jako `[OSOBA_001]` Claude neumí skloňovat a výstup je toporný. Proto mají osoby vymyšlená česká jména: méně častá křestní jména a příjmení odvozená od tématu (Jasaník, Habrovská…). Vymyšlené jméno má stejný rod a podobný vzor skloňování jako skutečné, takže se dá skloňovat oběma směry. Program kontroluje, že se nepodobá žádnému slovu ve spisu ani jinému pseudonymu.

### Zpětné dosazení

Claude ukládá výstupy do `ProClaude/S-0001/vystup/`. Advokátka je přenese zpět a program do nich vrátí skutečná jména ve stejném pádě. Nikdy nenahrazuje naslepo. Co neumí skloňovat, doplní v 1. pádě a označí ke kontrole. Odstraněné údaje a nové osoby (`[NOVÁ OSOBA: …]`) označí k doplnění a jména, která v klíči nejsou, ke kontrole. Výsledek jde upravit a uložit jako Word (.docx).

## Výsledky prvního testu OCR

Test proběhl 2026-11-02 na pěti fotkách stran z Matějova vlastního archivu stavebního úřadu (tištěný text, tabulka, razítko, podpis, fotky šikmé a se stínem). Fotky ani přepisy nejsou v repozitáři. Skripty jsou v [ocr_test/](../ocr_test/). Správný text pro porovnání přepsal Claude z fotek, může obsahovat drobné chyby.

| Motor | Chyby znaků (2 strany) | Čísla v tabulce | Citlivé údaje přesně | Čas na stranu, 4 jádra |
| --- | --- | --- | --- | --- |
| Tesseract, původní fotka | 25 % a 16 % | 17 z 73 | 16 z 34 | 2 s |
| Tesseract, vyrovnané osvětlení | 1,0 % a 3,3 % | 57 z 73 | 29 z 34 | 2 s |
| PaddleOCR PP-OCRv6 | 3,1 % a 4,3 % | 73 z 73 | 32 z 34 | 60–80 s |
| docTR | 15 % a 16 % | 73 z 73 | 15 z 34 | 12–22 s |
| Sloučení Tesseract + PaddleOCR + slovník | 3,2 % a 4,1 % | 67 z 73 | 33 z 34 | součet obou |

Citlivé údaje jsou jména, adresy, čísla parcel, telefon, e-mail, data a čísla norem, celkem 34.

- Úprava fotky přinesla víc než volba motoru.
- Motory chybují na různých místech: Tesseract vynechal telefon a e-mail, PaddleOCR ztrácí čárku nad í ve jménech. Proto se vyplatí je sloučit.
- Sloučení zatím zhoršuje tabulky, protože PaddleOCR čte tabulku po buňkách v jiném pořadí. Tabulky se budou brát jen z PaddleOCR.
- docTR nezná českou diakritiku. EasyOCR se nepodařilo stáhnout (blokovaná síť) a Surya se nezkoušela, protože její balíček obsahuje klienta cizí služby a nejdřív je třeba prověřit, že nic neposílá ven.
- Oprava přepisu malým místním jazykovým modelem (Qwen3 1,7B) se nevyplatila: chyby se nesnížily, model zanesl novou chybu a na stranu potřeboval asi 7 minut. U jmen a čísel je navíc riziko, že je „opraví“ na něco jiného.
- Vzorek je malý a chybí rukopis. Závěry je třeba potvrdit na dalších fotkách.

### Druhé kolo: Matějův notebook (2026-11-03)

`Koncipient.OcrTest` na 28 fotkách z archivu (fotky z telefonu, plné rozlišení), notebook s 8 jádry. Přesnost změřená na listech, ke kterým existuje ručně přepsaný text z prvního kola:

| Motor | Chyby znaků (str. 8 / str. 9) | Čísla v tabulce | Citlivé údaje | Čas na stranu |
| --- | --- | --- | --- | --- |
| Tesseract | 4,4 % / 1,4 % | 1 ze 73 | 29 z 34 | 2–8 s |
| PaddleOCR | 4,4 % / 3,1 % | 73 ze 73 | 32 z 34 | 40–117 s |
| Windows OCR | 3,1 % / 0,5 % | 69 ze 73 | 29 z 34 | asi 1 s |

- Windows OCR čte souvislý text nejlépe a je řádově nejrychlejší, proto je hlavním motorem. Unikají mu ale některé znaky (§) a rozdělil e-mail na kousky.
- PaddleOCR zůstává kontrolním motorem: nejlépe čte čísla a jména a jeho odlišné čtení označí sporná místa. Údaje se hledají ve výstupech obou motorů.
- PaddleOCR byl na fotkách z telefonu pomalý kvůli hledání řádků v plném rozlišení. Hledání řádků teď běží na kopii zmenšené na 2000 bodů: v cloudu dvakrát rychlejší při stejné přesnosti.
- Tesseract je jen záloha pro počítač, kde Windows OCR nemá češtinu.
- Po přepisu se Windows OCR a PaddleOCR sloučí do jednoho textu (`Koncipient.Jadro/Slouceni.cs`, ověřeno v `ocr_test/slouceni.py`). Základem je Windows OCR, z PaddleOCR se berou čísla, § a e-maily a slova, která Windows OCR podle českého slovníku zkomolilo. Útržky z okraje fotky, které čte jen PaddleOCR, se nevkládají. Na přepisech z Matějova notebooku: chyby znaků 3,1 % a 0,3 % (Windows OCR samo 3,1 % a 0,5 %), čísla v tabulce 71 ze 73 (69), citlivé údaje 32 z 34 (29). Sloučený text je hlavní pro prohlížeč i export, PaddleOCR zůstává kontrolním pro sporná místa.
- PaddleOCR umí na Windows počítat na grafické kartě přes DirectML (funguje i s integrovanou grafikou). Program zkusí nejvýkonnější kartu a jinak počítá na procesoru. Na Matějově notebooku (GTX 1050 a Intel UHD 630) trvala strana na procesoru 13–22 s, na grafice 6–11 s, text se od procesoru lišil v 0,7 % slov. Jedna z grafik selhala pro nedostatek paměti až během výpočtu, proto se motor při chybě grafiky přepne na procesor a stranu zopakuje. Automatická volba použila NVIDIA GTX 1050 (podle Správce úloh naplno vytížená, Intel ne). Pořadí grafik v DirectML neodpovídá číslování ve Správci úloh, proto se má používat automatická volba, ne číslo grafiky.
- Test neotáčí fotky, takže strana vyfocená vzhůru nohama vyšla nečitelná. Prohlížeč ji před přepisem otočí.

## Měření anonymizace na vymyšlené sadě (2026-11-03)

Šest vymyšlených dokumentů (žaloba, rozsudek, kupní smlouva, plná moc, předvolání, výpis z obchodního rejstříku) s 87 vyznačenými údaji, vytištěných jako fotky z telefonu (`vzory/`). Měřeno nástrojem `tools/Koncipient.MereniAnonymizace`:

| | Uniklo údajů před úpravou | Uniklo po úpravě |
| --- | --- | --- |
| Bezchybný text (jen detektor) | 16 z 87 | 0 z 87 |
| OCR z fotek (PaddleOCR a Tesseract v cloudu) | nezměřeno | 0 z 87 |

- Úniky byly hlavně ulice s číslem domu, čísla jednací úřadů, data narození s měsícem slovy, evidenční číslo advokáta, SPZ, číslo dokladu a rodné příjmení („rozená …“).
- Vážná chyba, kterou měření našlo: manželé Pokusný a Pokusná se sloučili do jedné osoby, protože porovnání bez diakritiky nerozlišilo rod. Teď se muž a žena podle koncovky (-ý, -ého, -ovi × -á, -ová, -ovou) nikdy nesloučí.
- Přes OCR unikaly adresy rozdělené zalomením řádku na fotce. Pravidla teď přecházejí přes konec řádku mezi PSČ a obcí, za „k. ú.“ a mezi ulicí a číslem.
- Nahrazená navíc jsou jen jména soudkyně a úřednice. Program jim dá roli „veřejná osoba“, advokátka je nechá neanonymizované.
- Dokumenty i pravidla psal stejný autor, výsledek je proto spíš horní odhad. Automatický test hlídá, že se detektor na sadě nezhorší. Měření s Windows OCR musí proběhnout na Windows.

## Měření na korpusu soudních rozhodnutí (2026-11-03)

Šest vlastních vzorů nestačí, detektor byl na ně „ušitý“. Proto vznikl větší korpus (`vzory/soudni/`, data mimo repozitář):

- **161 skutečných rozhodnutí** z otevřených dat rozhodnuti.justice.cz. Jsou oficiálně anonymizovaná a každé anonymizované místo nese typ (Jméno žalovaného, Adresa žalobkyně, IČO, SPZ…). Skript na tato místa dosadí vymyšlené osoby, firmy, adresy a čísla a vyznačí je, takže výsledek má skutečný právní jazyk i přesně známé polohy údajů. Data a částky, které soudy také anonymizují, se dosazují bez vyznačení, protože je Koncipient záměrně ponechává.
- **16 vymyšlených dokumentů jiných typů** (policejní záznam, protokol o výslechu, nájemní a darovací smlouva, faktura, e-maily, lékařská zpráva, exekuční příkaz, pracovní smlouva, znalecký posudek, notářský zápis…), které napsali levnější agenti (Claude Haiku) podle zadání se značkováním údajů.
- Celkem 2039 vyznačených údajů, 363 stran vytištěných jako fotky s různými písmy. Rozdělení 70:30 na vývojovou část (na té se pravidla ladí) a kontrolní část (jen pro měření, proti přeučení).

Úniky z bezchybného textu:

| | Vývojová část (1439 údajů) | Kontrolní část (600 údajů) |
| --- | --- | --- |
| Před úpravami | 184 (12,8 %) | 61 (10,2 %) |
| Po úpravách | 25 (1,7 %) | 17 (2,8 %) |
| Po opravách z testů na Windows | 12 (0,8 %) | 11 (1,8 %) |

Přes OCR z fotek (v cloudu PaddleOCR a Tesseract, 247 stran vývojové části) uniká 11 z 1439 údajů (0,8 %), stejně jako z bezchybného textu. Pomohlo hlavně hledání údajů rozdělených koncem řádku (SPZ, IBAN, telefon, jméno a příjmení na dvou řádcích) a SPZ bez návěští. Zbývající úniky jsou hlavně samotné názvy obcí v běžném textu, údaje bez jakékoli nápovědy v okolí a chyby generátoru (údaje slepené bez mezery, „XY“ místo popisku; v `dosad.py` už opravené, sada se zatím nepřegenerovala). Nahrazeno navíc je asi 2,5 % (jména soudců a úředníků, ta program označí jako veřejné osoby). Citovaná judikatura vyšších soudů se ponechává. S Windows OCR je třeba měření zopakovat na Windows (test 6).

## Dokumenty ve spisu

Spis se skládá z dokumentů, každá strana patří k jednomu. Dokumenty vznikají:

- **ze složek:** „Přidat složku…“ udělá z každé podsložky dokument pojmenovaný podle ní, fotky přímo ve složce tvoří dokument pojmenovaný podle složky;
- **z černých fotek:** fotka se zakrytým objektivem (tmavší než papír a bez kresby, mobil ji může zesvětlit do červena nebo šeda) se neuloží mezi strany a začne další dokument. Po importu program oddělovače ukáže s náhledem a skutečnou stránku jde jedním zaškrtnutím vrátit na její místo (dokumenty se zase spojí), aby se strana spisu neztratila potichu. Oddělovače zůstávají ve složce spisu `oddelovace/` a jde se k nim vrátit tlačítkem Oddělovače…; takový dokument dostane po přepisu název navržený z prvního textu (řádek s typem dokumentu jako „Kupní smlouva“, jinak první smysluplný řádek);
- **ručně:** posunout stranu, přesunout do jiného dokumentu, začít nový dokument, spojit s předchozím, přejmenovat.

Fotky se řadí přirozeně (IMG_2 před IMG_10). Strany se číslují v pořadí spisu a na toto číslo odkazuje i Claude. Export obsahuje jeden soubor na dokument (`dokumenty/01-kupni-smlouva.md`) se stranami jako oddíly. Názvy dokumentů a složek můžou obsahovat jména, proto se anonymizují stejně jako text a projdou závěrečnou kontrolou. Automatické rozpoznání hranic dokumentů bez černé fotky (podle hlavičky nebo číslování stran) zatím není, je nespolehlivé.

## Import PDF a rychlá kontrola

**PDF** (hlavně z datové schránky) se přidává stejně jako fotky, každé PDF je dokument. Stránky se vykreslí na 200 DPI (PDFium přes PDFtoImage) pro zobrazení. Text se vezme přímo z PDF (PdfPig) i s polohami slov, takže je přesný a stránka se nepřepisuje (motor `pdf-text`, v pořadí zobrazení hned za ruční opravou). Stránka bez textu, s nečitelným kódováním písma nebo otočená jde přes OCR jako fotka. Obě knihovny běží offline.

**Rychlá kontrola** (`RychlaKontrola.cs`, záložka v okně, `koncipient-cli kontrola`) nahrazuje procházení celých stran. Ukáže každý nahrazený údaj jednou za stranu s okolím a výřezem fotky. Dál ukáže slova, která zůstanou, ale vypadají jako přehlédnutý údaj: známé křestní jméno, slovo s velkým písmenem mimo český slovník uprostřed věty, číslo s aspoň pěti číslicemi kromě dat a částek. Když má strana všechna místa vyřízená, ukáže se celá v textu pro Clauda vedle fotky a zkontrolovaná je až po potvrzení (Matějovo rozhodnutí 2026-11-04: program nenajde všechno, člověk musí vidět celou stranu).

## Hledání, PDF pro archiv a doplnění spisu

- **Hledání** (`Hledani.cs`, záložka a Ctrl+F) hledá slovo nebo frázi ve všech stranách bez ohledu na diakritiku a velikost písmen. Poslední slovo dotazu stačí jako začátek (skloňování), fráze může přecházet přes konec řádku.
- **PDF pro archiv** (`ArchivPdf.cs`) skládá fotky stran (šířka A4) s neviditelnou textovou vrstvou z přepisu (PdfPig, písmo Arial z Windows). Velikost písma každého slova odpovídá šířce slova na fotce, aby označení v prohlížeči PDF sedělo. Dokumenty jsou záložky. PDF obsahuje skutečné údaje a zůstává v počítači se spisy.
- **Export vybraných dokumentů:** export může obsahovat jen vybrané dokumenty, kontrolují se jen jejich strany. Soubory mají stálá čísla podle pořadí ve spisu, `manifest.json` má `cely_spis` a pravidla pro Clauda vysvětlují, že ostatní dokumenty nedostal. Dokument si pamatuje čas posledního exportu, takže po doplnění spisu jde vybrat jen nové.

## Stránky k přefocení a přenos přes flash disk

- **Přefocení** (`KvalitaFotek.cs`): po přepisu program nabídne strany, které vyšly špatně. Rozmazaná je fotka s ostrostí (rozptyl Laplaceova operátoru na šířce 1000 px) pod 60; fotky z mobilu v Matějově archivu mají 445 až 1583, rozmazané pod 20. Strana bez textu (prázdný papír) se na ostrost nehodnotí. Dál tmavá fotka (jas pod 80), text u okraje (aspoň 3 slova do 0,6 % od okraje) a nejistý přepis (přes 35 % sporných slov). Nová fotka nahradí starou na stejném místě, přepis i kontrola strany se zahodí.
- **Flash disk** (`Prenos.cs`): na disk jde jen obsah `ProClaude` po kontrole, že v něm nejsou fotky, PDF, Word, klíč ani data spisu. Výstupy od Clauda na disku zůstanou. Zpátky se načtou jen nové `.md` a `.txt` ze složek `vystup` a rovnou se otevřou k vrácení jmen.

## Úkol pro Clauda

Při exportu může advokátka napsat, co má Claude udělat. Text se anonymizuje stejně jako spis (včetně skloňovaných tvarů jmen), projde závěrečnou kontrolou a uloží se jako `UKOL.md` do složky spisu v exportu; `manifest.json` na něj odkazuje a pravidla pro Clauda říkají, že „udělej úkol“ znamená postupovat podle něj. Okno ukazuje živý náhled, co Claude dostane. Do záznamu o exportech se zapíše jen to, že úkol byl, ne jeho text.

## Přehled dat

Záložka Data (`PrehledDat.cs`) najde data v textu (číselně i s měsícem slovy, roky 1900–2100, neplatná data jako 31. 2. vynechá), seřadí je a k datu dá nápovědu podle tří slov před ním (narození, doručení, právní moc, jednání, splatnost, lhůta, podání, smlouva, ze dne). Lhůty se záměrně nepočítají. Data narození jsou ve výchozím stavu skrytá. Přehled obsahuje skutečné údaje a zůstává v počítači.

## Záznam o exportech

Každý export a každá kopie na flash disk se zapíše do `zaznam-exportu.json` ve složce spisu (`ZaznamExportu.cs`): čas, uživatel Windows, pořadí dokumentů, počet stran, jestli byly zkontrolované, výsledek závěrečné kontroly a počty nahrazených, vypuštěných a ponechaných údajů podle typu. Skutečné údaje ani názvy dokumentů (můžou obsahovat jména) se nezapisují. Záznam zůstává ve spisu a advokátka si ho uloží jako Word jako doklad o zacházení s osobními údaji.

## Struktura složek a formát dat

Spisy a export jsou dvě oddělené složky. Na počítač s Claude Code se přenáší jen export, Claude Code se spouští v něm. Každá strana je samostatný Markdown.

```text
Koncipient/                   na počítači A
  Spisy/                      jen na počítači A, nešifrované
    S-0001/
      fotky/                  originální fotky
      ocr/                    text a pozice slov
      klic.db                 kdo je kdo
  ProClaude/                  export, kopíruje se na počítač B, tam se spouští Claude Code
    CLAUDE.md                 pravidla práce se spisy
    .claude/settings.json     pravidla deny, jen jako doplněk
    S-0001/
      manifest.json           seznam stran a stav kontroly
      entity.json             pseudonymy a role, bez skutečných hodnot
      strany/0001.md          anonymizovaný text jedné strany
```

Tato struktura je datová složka programu u advokátky, ne složka tohoto repozitáře. Pseudonym spisu `S-0001` se nesmí odvozovat od spisové značky. Ukázka `entity.json` s vymyšlenými hodnotami:

```json
{
  "spis": "S-0001",
  "entity": [
    {"id": "OSOBA_001", "typ": "osoba", "role": "žalobce"},
    {"id": "FIRMA_001", "typ": "firma", "role": "žalovaný"},
    {"id": "ADRESA_001", "typ": "adresa", "obec": null}
  ]
}
```

## Plán po etapách

1. **Test OCR.** První kolo je hotové, viz výsledky výše. Obě OCR v C# dávají stejné výsledky jako Python. Zbývá vyzkoušet rukopis, spustit `Koncipient.OcrTest` na Matějově notebooku (rychlost na Windows, Windows OCR). Advokátka pak spustí srovnání na pár skutečných fotkách u sebe, aby se nic neposílalo ven.
2. **Prohlížeč.** Import fotek, noční OCR, rozdělené zobrazení a oprava textu. První verze je hotová (`src/Koncipient.App`), v cloudu přeložená a s otestovaným jádrem, čeká na vyzkoušení na Windows. Zatím zobrazuje výsledek PaddleOCR a oranžově místa, kde Tesseract četl jinak. Sloučení přes slovník přijde později.
3. **Anonymizace a export.** Návrhy údajů, schvalování, klíč pseudonymů, složka `ProClaude` a kontrola před zápisem. První verze je hotová (`src/Koncipient.Jadro/Anonymizace`): pravidla a seznam křestních jmen, porovnání odolné vůči skloňování a chybám OCR, ruční označení v prohlížeči, kontrola každé strany a závěrečná kontrola exportu. NER model zatím není.
4. **Propojení s Claude Code.** `CLAUDE.md`, pravidla přístupu a dosazení skutečných jmen zpět do výstupu. První verze je hotová: pravidla pro Clauda v `ProClaude/PRAVIDLA-KONCIPIENT.md`, zpětné dosazení se skloňováním a uložení jako Word.
5. **Instalátor a předání.** Instalátor je hotový (`instalace/`, sestavuje ho `instalace/sestav.ps1` přes Inno Setup): samostatný program bez nutnosti instalovat .NET, koncipient-cli a přibalené modely OCR, instalace pro uživatele bez práv správce. Nainstalovaný program modely nestahuje, za běhu k síti nepřistupuje. Zbývá: instalace na počítači se spisy, návod na přenos exportu na počítač s Claude Code, záloha složky `Spisy` a krátký návod.

## Rizika

- Pseudonymizace není anonymizace: i bez jmen může spis prozradit, o koho jde, a podle GDPR se pseudonymizovaná data stále považují za osobní údaje. Jestli takové použití obstojí z pohledu mlčenlivosti advokáta, musí posoudit ona.
- Složka `Spisy` není šifrovaná. Ochranu dává jen to, že na počítači A neběží Claude Code ani jiný cloudový nástroj. Při krádeži počítače pomůže šifrování disku ve Windows (BitLocker), které je mimo program.
- Druhý počítač je pro advokátku náklad navíc. Pokud ho mít nebude, Koncipient funguje i na jednom počítači, jen bez téhle ochrany.
- Matějův notebook má 8 GB RAM. To na lehké OCR stačí, na místní jazykový model spíš ne. Advokátka nemá silný počítač, přesné parametry nejsou známé. PaddleOCR na něm může trvat i minutu na stranu, proto dávkové zpracování přes noc.
- Žádné OCR nepřepíše všechno správně. Chybně přečtené jméno může projít anonymizací, proto hledání údajů toleruje chyby a advokátka export schvaluje.

## Zdroje

- [Claude Code: sandboxing](https://code.claude.com/docs/en/sandboxing)
- [ÚFAL: komerční licence NameTag 3](https://ufal.mff.cuni.cz/node/3032)
- [PaddleOCR-VL a PP-OCR, článek o modelech](https://arxiv.org/html/2510.14528v1)
- [Srovnání místních OCR modelů 2026](https://vdf.ai/blog/best-local-ocr-models/)
