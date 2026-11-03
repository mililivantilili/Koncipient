# Koncipient v C#

Řešení `Koncipient.sln` v kořeni repozitáře se otevírá ve Visual Studiu 2022 (s .NET 8 SDK).

| Projekt | Co je |
| --- | --- |
| `Koncipient.App` | Program s oknem (WPF): spisy, import fotek, noční přepis, text vlevo a fotka vpravo |
| `Koncipient.Jadro` | Spisy na disku, dávkový přepis, porovnání motorů (sporná místa), stažení modelů |
| `Koncipient.Ocr` | Knihovna OCR: úprava fotky, Tesseract, PaddleOCR přes ONNX Runtime a na Windows i Windows OCR. Vrací slova i s polohou na fotce |
| `Koncipient.OcrTest` | Konzolový test: přepíše fotky ve složce všemi motory a výsledky sbalí do zipu |
| `tests/Koncipient.Testy` | Automatické testy na vymyšlených datech (Test Explorer ve Visual Studiu) |

## Prohlížeč (Koncipient.App)

1. Nastav `Koncipient.App` jako spouštěný projekt a stiskni F5.
2. **Nový spis**, pojmenuj ho. Název zůstává jen v počítači, spis dostane pseudonym `S-0001`.
3. **Přidat fotky…** a vyber fotky stran. Seřadí se podle názvu souboru, otočí podle údajů z telefonu a uloží bez metadat (poloha GPS, model telefonu).
4. **Spustit přepis.** Poprvé se zeptá na stažení modelů. Přepíše všechny strany bez přepisu PaddleOCR i Tesseractem. Počítač mezitím neusne, přerušený přepis po dalším spuštění pokračuje.
5. Klikni na stranu vlevo. Klik na slovo v textu ukáže jeho místo na fotce, klik do fotky najde slovo v textu. Oranžově jsou místa, která Tesseract přečetl jinak než PaddleOCR. Dvojklik na slovo ho opraví, opravy se ukládají zvlášť a jsou zeleně. Ctrl + kolečko mění zvětšení fotky.

Anonymizace: **Najít údaje** (po přepisu proběhne samo) označí fialově jména, adresy, telefony, e-maily, účty, IČO, spisové značky a parcely. Pravým tlačítkem na slově se údaj doplní nebo označí jako necitlivý, v seznamu vlevo dole jde u údaje změnit akci a roli. Po zaškrtnutí **Strana zkontrolována** u všech stran zapíše **Exportovat pro Clauda…** text do `Dokumenty\Koncipient\ProClaude`. Postup testování je v [docs/testovani.md](../docs/testovani.md).

Spisy leží v `Dokumenty\Koncipient\Spisy`. Každý spis má `spis.json`, fotky v `fotky/`, přepisy v `ocr/<motor>/0001.json`, ruční opravy v `ocr/opraveno/` a klíč pseudonymů v `klic.json`. Nic se nešifruje, ochranu dává oddělený počítač bez Claude Code.

## Test OCR na Matějově notebooku

1. Otevři `Koncipient.sln` ve Visual Studiu a nahoře nastav `Koncipient.OcrTest` jako spouštěný projekt (pravým tlačítkem, Set as Startup Project).
2. Stiskni F5.
3. Program se zeptá na složku s fotkami. Přetáhni ji do okna a stiskni Enter.
4. Při prvním spuštění si stáhne modely (asi 140 MB) do `%LOCALAPPDATA%\Koncipient\modely`. Fotky ani text se nikam neposílají.
5. Na konci vznikne ve složce s fotkami soubor `vysledky-ocr.zip` jen s texty a časy. Ten pošli Claudovi.

Windows OCR se spustí, jen když má Windows nainstalovanou češtinu pro rozpoznávání textu. Pokud program napíše, že chybí, přidej češtinu v Nastavení, Čas a jazyk, Jazyk a oblast, nebo v PowerShellu spuštěném jako správce:

```powershell
Add-WindowsCapability -Online -Name "Language.OCR~~~cs-CZ~0.0.1.0"
```

Tesseract potřebuje Microsoft Visual C++ Redistributable (x64). S Visual Studiem bývá nainstalovaný.

## Ověřeno v cloudové relaci (Linux)

2026-11-03 dal C# PaddleOCR i C# Tesseract na pěti testovacích fotkách stejné výsledky jako Python skripty v `ocr_test/` (stejné chyby znaků, 73 z 73 čísel, 32 a 29 z 34 citlivých údajů). PaddleOCR v C# trval 9–16 s na stranu proti 60–80 s v Pythonu. Windows OCR a běh na Windows se v cloudu ověřit nedají.

Celé řešení se v cloudu překládá oficiálním SDK od Microsoftu (`dotnet-install.sh`), Ubuntu balíček `dotnet-sdk-8.0` neumí WPF. Okno se dá v cloudu jen přeložit, spustit až na Windows. Jádro bylo 2026-11-03 ověřeno naostro: import pěti fotek, dávkový přepis oběma motory za 79 s a označení sporných míst.

Tesseract z NuGetu obsahuje knihovny jen pro Windows. Pro test na Linuxu bylo nutné do `bin/.../x64` nalinkovat systémové `libtesseract.so.5`, `liblept.so.5` a `libdl.so.2` pod jmény, která wrapper hledá.

Hotový program nebude modely stahovat za běhu, budou součástí instalace.
