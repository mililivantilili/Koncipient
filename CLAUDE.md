# Koncipient

Desktopový program pro advokátku: převede nafocené spisy na text přímo v počítači, zobrazí vlevo text a vpravo fotku a vytvoří anonymizovanou kopii, se kterou pracuje její Claude Code. Cílem je zefektivnit její práci a zachovat citlivá data v jejím počítači.

Komunikuj česky. Úplný návrh je v [docs/navrh.md](docs/navrh.md).

## Stav

K 2026-11-02 existuje jen návrh, žádný kód. Návrh čeká na Matějovo schválení. Po schválení následuje etapa 1 (test OCR).

## Pevná pravidla

- **Skutečné spisy nikdy nečti ani nezpracovávej ve vývojové relaci.** Vývojová relace běží přes cloudový model, takže by data odešla mimo počítač. Pracuj jen s vymyšlenými vzory. Pokud někdo skutečný spis pošle, nečti ho a upozorni na to.
- **Do repozitáře patří jen kód a vymyšlené vzory.** Repozitář je veřejný. Vzory musí být zjevně vymyšlené, bez skutečných jmen, adres nebo čísel.
- **Program nesmí posílat data ven.** Žádné cloudové OCR, žádná telemetrie, žádná online NER služba (ani hostovaný NameTag). Modely se stahují při instalaci, za běhu program k síti nepřistupuje.
- **Nic se neexportuje bez schválení člověkem.** Automatika nikdy nezachytí 100 % údajů.
- **Fotky ani klíč pseudonymů nesmí do složky `ProClaude`.**
- **Nespoléhej na pravidla `deny` v Claude Code jako na jedinou ochranu.** Sandbox na nativních Windows nefunguje a příkazy tam běží nesandboxovaně.

## Omezení prostředí

Cílový systém je Windows. V cloudové relaci (Linux) nejde otestovat Windows OCR, instalátor ani chování na Windows, takže tyhle části drž za rozhraním a jejich ověření nech na Matějův počítač.

## Otevřené otázky

Odpovědi od advokátky zatím chybí:

1. Jaký má počítač (paměť, verze Windows)?
2. Jsou spisy převážně tištěné, nebo i psané rukou?
3. Znamená „přeložit“ jen převod fotky na text, nebo i překlad z cizího jazyka? Zatím se počítá jen s převodem na text.
4. Jaký má Claude Code tarif a nastavení uchovávání dat?
