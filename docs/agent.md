# Testování Koncipientu agentem (Claude Code na Windows)

Návod pro agenta, který na Matějově počítači dělá testy z [testovani.md](testovani.md). Okno se myší a čtením obrazovky ovládá špatně, proto má program příkazovou řádku `koncipient-cli` s výstupem JSON. Okno používej jen na to, co jde ověřit jen v okně.

## Bezpečnost, čti nejdřív

- Všechno, co agent přečte, odchází do cloudu. **Pracuj jen s vymyšlenými daty (`vzory/sada`) nebo s Matějovým archivem stavebního úřadu, který k tomu dal souhlas. Nikdy se spisy advokátky.**
- Příkazy, které vypisují obsah spisu (`text`, `udaje`, `vrat-jmena`), běží jen s `KONCIPIENT_TESTOVACI_DATA=1`.
- Testuj v oddělené složce spisů přes `KONCIPIENT_SPISY`, ať se nemíchá s Matějovými spisy v Dokumentech.

```powershell
$env:KONCIPIENT_TESTOVACI_DATA = "1"
$env:KONCIPIENT_SPISY = "$env:TEMP\koncipient-agent\Spisy"
```

## Spuštění

```powershell
dotnet build Koncipient.sln -c Release
```

```powershell
dotnet run --project src\Koncipient.Cli -c Release -- help
```

Nebo přímo `src\Koncipient.Cli\bin\Release\net10.0-windows10.0.19041.0\koncipient-cli.exe help`. Výstup je JSON na standardní výstup, chyba je `{"chyba": …}` na chybový výstup.

## Testy přes příkazovou řádku

| Test | Příkazy |
| --- | --- |
| 0 automatické testy | `dotnet test tests\Koncipient.Testy -c Release` |
| 1 OCR a rychlost | `dotnet run --project src\Koncipient.OcrTest -c Release -- <složka> ` a na otázku odpověz 1 (nebo přesměruj vstup) |
| 2 a 7 spis, dokumenty | `zaloz`, `pridej-slozku`, `pridej-fotky`, `prepis`, `strany`, `dokument … posun/presun/novy/spoj/prejmenuj/navrhni` |
| 3 otočení | `otoc <spis> <strana> 90`, pak `otoc … 270` a `strany`: `prepis` se nemá ztratit |
| 4 anonymizace a export | `udaje`, `text <spis> <strana> --slova`, `oznac`, `necitlive`, `udaj … --akce/--role/--nahrada`, `styl`, `zkontroluj … vse`, `export --cil <složka>` |
| 5 vrácení jmen | napiš `.md` s vymyšlenými jmény z exportu, `vrat-jmena <spis> <soubor> --docx <výstup>` |
| 6 měření anonymizace | `dotnet run --project tools\Koncipient.MereniAnonymizace -c Release` (bez parametrů použije `vzory/sada`) |

Příklad celého průchodu na vymyšlené sadě:

```powershell
koncipient-cli zaloz "test agenta"
koncipient-cli pridej-slozku S-0001 vzory\sada\fotky
koncipient-cli prepis S-0001
koncipient-cli strany S-0001
koncipient-cli udaje S-0001
koncipient-cli text S-0001 1 --anonymizovany
koncipient-cli zkontroluj S-0001 vse
koncipient-cli export S-0001 --cil $env:TEMP\koncipient-agent\ProClaude
```

## Co zůstává na okno (a na Matěje)

Vzhled a ovládání myší: propojení kliknutí v textu a na fotce, zvětšování kolečkem, tažení oblasti s Ctrl, barvy sporných a anonymizovaných slov, dialogy. Okno má u ovládacích prvků pevné identifikátory pro UI Automation (`AutomationId` stejné jako názvy v `HlavniOkno.xaml`, např. `SpustitPrepis`, `SeznamStran`, `TabulkaUdaju`) a klávesové zkratky:

| Zkratka | Akce |
| --- | --- |
| Ctrl+N | nový spis |
| Ctrl+O / Ctrl+Shift+O | přidat fotky / přidat složku |
| F5 | spustit přepis |
| Ctrl+PageDown / Ctrl+PageUp | další / předchozí strana |
| Ctrl+K | strana zkontrolována |
| Ctrl+E | export pro Clauda |

Výsledky testů zapiš do odpovědi Matějovi: co prošlo, co ne, přesný příkaz a výstup chyby. Obsah spisů do výsledků nekopíruj, stačí počty a popis problému.
