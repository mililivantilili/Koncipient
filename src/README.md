# Koncipient v C#

Řešení `Koncipient.sln` v kořeni repozitáře se otevírá ve Visual Studiu 2022 (s .NET 8 SDK).

| Projekt | Co je |
| --- | --- |
| `Koncipient.Ocr` | Knihovna OCR, kterou použije i hlavní program: úprava fotky, Tesseract, PaddleOCR přes ONNX Runtime a na Windows i Windows OCR |
| `Koncipient.OcrTest` | Konzolový test: přepíše fotky ve složce všemi motory a výsledky sbalí do zipu |

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

Tesseract z NuGetu obsahuje knihovny jen pro Windows. Pro test na Linuxu bylo nutné do `bin/.../x64` nalinkovat systémové `libtesseract.so.5`, `liblept.so.5` a `libdl.so.2` pod jmény, která wrapper hledá.

Hotový program nebude modely stahovat za běhu, budou součástí instalace.
