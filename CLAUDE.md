# Koncipient

Desktopový program pro advokátku: převede nafocené spisy na text přímo v počítači, zobrazí vlevo text a vpravo fotku a vytvoří anonymizovanou kopii, se kterou pracuje její Claude Code. Cílem je zefektivnit její práci a zachovat citlivá data v jejím počítači.

Komunikuj česky. Úplný návrh je v [docs/navrh.md](docs/navrh.md).

## Stav

K 2026-11-02 proběhlo první kolo testu OCR (skripty v `ocr_test/`, výsledky v návrhu). Doporučení: vyrovnání osvětlení, Tesseract a PaddleOCR sloučené přes český slovník, dávkové zpracování přes noc. Program bude v C# ve Visual Studiu. K 2026-11-03 existuje knihovna `src/Koncipient.Ocr` a konzolový test `src/Koncipient.OcrTest`, ověřené v cloudu proti Python skriptům. Od 2026-11-03 existuje i první verze prohlížeče `src/Koncipient.App` (WPF), jádro `src/Koncipient.Jadro` s automatickým otočením fotek a anonymizací (etapa 3) a testy v `tests/`. Co má Matěj vyzkoušet na Windows, je v [docs/testovani.md](docs/testovani.md). Čeká se na jeho výsledky a na vzorky rukopisu.

Test běžel na fotkách z Matějova vlastního archivu stavebního úřadu, které dal výslovně k dispozici. Nejsou to spisy advokátky a do repozitáře nepatří ani ony, ani jejich přepisy.

## Pevná pravidla

- **Skutečné spisy nikdy nečti ani nezpracovávej ve vývojové relaci.** Vývojová relace běží přes cloudový model, takže by data odešla mimo počítač. Pracuj jen s vymyšlenými vzory. Pokud někdo skutečný spis pošle, nečti ho a upozorni na to.
- **Do repozitáře patří jen kód a vymyšlené vzory.** Repozitář je veřejný. Vzory musí být zjevně vymyšlené, bez skutečných jmen, adres nebo čísel.
- **Program nesmí posílat data ven.** Žádné cloudové OCR, žádná telemetrie, žádná online NER služba (ani hostovaný NameTag). Modely se stahují při instalaci, za běhu program k síti nepřistupuje.
- **Nic se neexportuje bez schválení člověkem.** Automatika nikdy nezachytí 100 % údajů.
- **Fotky ani klíč pseudonymů nesmí do složky `ProClaude`.**
- **Nespoléhej na pravidla `deny` v Claude Code jako na jedinou ochranu.** Sandbox na nativních Windows nefunguje a příkazy tam běží nesandboxovaně.
- **Hranici tvoří oddělené počítače.** Koncipient se spisy má běžet na jiném počítači než Claude Code, přenáší se jen složka `ProClaude`. Šifrování spisů se zatím nedělá (Matějovo rozhodnutí z 2026-11-03).

## Omezení prostředí

Cílový systém je Windows. V cloudové relaci (Linux) nejde otestovat Windows OCR, instalátor ani chování na Windows, takže tyhle části drž za rozhraním a jejich ověření nech na Matějův počítač.

V cloudu překládej řešení oficiálním .NET 8 SDK z `dotnet-install.sh`, protože Ubuntu balíček neumí WPF. Windows variantu ověříš příkazem `dotnet build Koncipient.sln -p:OS=Windows_NT -p:EnableWindowsTargeting=true`.

## Otevřené otázky

Odpovědi od advokátky zatím chybí:

1. Jaký má počítač (paměť, verze Windows)? Víme jen, že není silný.
2. Jsou spisy převážně tištěné, nebo i psané rukou?
3. Znamená „přeložit“ jen převod fotky na text, nebo i překlad z cizího jazyka? Zatím se počítá jen s převodem na text.
4. Jaký má Claude Code tarif a nastavení uchovávání dat?
