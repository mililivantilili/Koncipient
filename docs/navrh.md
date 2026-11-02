# Koncipient: návrh programu pro advokátku

Stav k 2026-11-02, zatím neschváleno.

## Shrnutí

Doporučuji desktopový program pro Windows, který fotky spisu převede na text přímo v počítači a Claude Code předá jen zkontrolovanou anonymizovanou kopii. Advokátka v něm uvidí vlevo text a vpravo fotku, po kliknutí na slovo se na fotce zvýrazní jeho místo.

- **Přepis:** místní OCR, nic se neposílá ven. Slovo „přeložit“ je zatím chápáno jako převod fotky na text, ne překlad do cizího jazyka.
- **Anonymizace:** program navrhne, advokátka schválí. Bez lidské kontroly se nic neexportuje.
- **Claude Code:** pracuje jen v oddělené složce s kopií bez jmen a bez fotek. Klíč pseudonymů z počítače nikdy neodchází.
- **Hlavní riziko:** na Windows nelze Claude Code spolehlivě zamknout jen nastavením, proto je navržen šifrovaný trezor spisů.

## Hranice citlivých dat

Co jednou Claude Code přečte, odchází k Anthropicu, proto do jeho složky smí jen schválený export.

```text
Fotky -> místní OCR -> anonymizace a schválení -> složka ProClaude -> Claude Code -> Anthropic
\______ šifrovaný trezor, včetně klíče _______/                            |
                                                                           v
                     klíč z trezoru --> dosazení jmen <-- výstup s pseudonymy
```

| Vrstva | Co dělá | Proti čemu nechrání |
| --- | --- | --- |
| Šifrovaný trezor | Fotky, text i klíč pseudonymů leží šifrované, klíč je jen v paměti otevřeného programu | Soubory mimo trezor, například fotky ve Staženích |
| Pravidla Claude Code | Spouští se ve složce exportu, s pravidly `deny` a ručním schvalováním příkazů | Na nativních Windows ho nezastaví příkaz v terminálu |
| Kontrola před exportem | Advokátka schválí každou stranu, program před zápisem znovu hledá známá jména | Údaje, které přehlédne program i člověk |

Sandbox Claude Code [na nativních Windows nefunguje](https://code.claude.com/docs/en/sandboxing). Silnější izolaci by dalo spuštění ve WSL2, kde sandbox funguje, nebo pod samostatným účtem Windows. Do první verze se to nedoporučuje kvůli složitosti, rozhodnutí je na advokátce.

## Architektura a technologie

Doporučení je Python 3.12 a PySide6 (Qt), výsledek zabalený do jednoho instalátoru. Výběr OCR rozhodne až test na vzorech, proto bude motor vyměnitelný.

| Část | Návrh | Poznámka |
| --- | --- | --- |
| OCR, první kandidát | Tesseract s češtinou a úpravou fotky (narovnání, kontrast) | Lehký, běží bez grafické karty |
| OCR, srovnání | Windows OCR a PaddleOCR | Čeština ve Windows OCR je na Matějově notebooku dostupná, na jejím počítači je třeba ověřit |
| Rukopis | Zatím jen označení „nutné přepsat ručně“ | Spolehlivé místní řešení pro český rukopis zatím nenalezeno |
| Rozdělené zobrazení | Text vlevo, fotka vpravo, zoom, kliknutí propojí slovo a místo na fotce | Slova s nízkou jistotou OCR jsou podbarvená |
| Hledání údajů | Pravidla (rodné číslo, IČO, účet, telefon, e-mail) a seznam českých jmen | Funguje bez modelu a licencí |
| Hledání jmen, rozšíření | Místní NER model, například NameTag 3 | Modely jsou nekomerční, pro CNEC 2.0 [nabízí ÚFAL komerční licenci](https://ufal.mff.cuni.cz/node/3032) |
| Sjednocení jmen | Novák, Nováka i Novákovi se sloučí na jednu `OSOBA_001` | Nutné kvůli českému skloňování |

Navržené výchozí zacházení s údaji, advokátka si ho může změnit:

| Údaj | Výchozí akce |
| --- | --- |
| Jména osob a firem | Pseudonym s rolí (`OSOBA_001`, žalobce) |
| Rodné číslo, datum narození, doklady, účty, kontakty | Odstranit |
| Adresy | Pseudonym, volitelně ponechat obec |
| Spisové značky, IČO | Pseudonym |
| Data úkonů a lhůt, částky, paragrafy | Ponechat, jsou potřeba k právní práci |

Výstup z Claude Code si program umí vzít zpět a dosadit skutečná jména. Tvar jména podle pádu zkontroluje advokátka, protože automatické skloňování bude chybovat.

## Struktura složek a formát dat

Trezor a export jsou dvě oddělené složky a Claude Code se spouští jen v exportu. Každá strana je samostatný Markdown.

```text
Koncipient/
  Trezor/                     šifrované, Claude Code nemá přístup
    S-0001/
      fotky/                  originální fotky
      ocr/                    text a pozice slov
      klic.db                 kdo je kdo
  ProClaude/                  tady se spouští Claude Code
    CLAUDE.md                 pravidla práce se spisy
    .claude/settings.json     pravidla deny
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

1. **Test OCR.** Porovnání motorů na vymyšlených vzorech. Advokátka spustí srovnání na pár skutečných fotkách u sebe. Výstupem je volba motoru.
2. **Prohlížeč.** Import fotek, OCR, rozdělené zobrazení, oprava textu a šifrovaný trezor.
3. **Anonymizace a export.** Návrhy údajů, schvalování, klíč pseudonymů, složka `ProClaude` a kontrola před zápisem.
4. **Propojení s Claude Code.** `CLAUDE.md`, pravidla přístupu a dosazení skutečných jmen zpět do výstupu.
5. **Instalátor a předání.** Instalace na jejím počítači, záloha trezoru a krátký návod.

## Rizika

- Pseudonymizace není anonymizace: i bez jmen může spis prozradit, o koho jde, a podle GDPR se pseudonymizovaná data stále považují za osobní údaje. Jestli takové použití obstojí z pohledu mlčenlivosti advokáta, musí posoudit ona.
- Ztráta hesla k trezoru znamená ztrátu dat, proto bude součástí programu záloha a náhradní klíč.
- Matějův notebook má 8 GB RAM. To na lehké OCR stačí, na místní jazykový model spíš ne. Parametry počítače advokátky nejsou známé.

## Zdroje

- [Claude Code: sandboxing](https://code.claude.com/docs/en/sandboxing)
- [ÚFAL: komerční licence NameTag 3](https://ufal.mff.cuni.cz/node/3032)
