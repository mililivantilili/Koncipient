# Koncipient: návrh programu pro advokátku

Stav k 2026-11-02, zatím neschváleno. Upraveno podle prvního testu OCR a Matějova přání vyvíjet ve Visual Studiu.

## Shrnutí

Doporučuji desktopový program pro Windows v C#, vyvíjený ve Visual Studiu, který fotky spisu převede na text přímo v počítači a Claude Code předá jen zkontrolovanou anonymizovanou kopii. Advokátka v něm uvidí vlevo text a vpravo fotku, po kliknutí na slovo se na fotce zvýrazní jeho místo.

- **Přepis:** místní OCR, nic se neposílá ven. Fotky se zpracují dávkově, klidně přes noc, dvěma motory (Tesseract a PaddleOCR), jejichž výsledky se sloučí. Slovo „přeložit“ je zatím chápáno jako převod fotky na text, ne překlad do cizího jazyka.
- **Kdo text čte:** přepis čte hlavně program pro anonymizaci a pak Claude. Advokátka čte fotky, proto rozhoduje hlavně přesnost jmen, adres a čísel, ne úhlednost textu.
- **Anonymizace:** program navrhne, advokátka schválí. Hledání údajů počítá s chybami OCR, takže najde i „Miček“ místo „Míček“. Bez lidské kontroly se nic neexportuje.
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

Program bude v C# na .NET s rozhraním WPF, vyvíjený ve Visual Studiu, které Matěj používá. Výsledek se zabalí do jednoho instalátoru. OCR motor je za rozhraním, aby šel vyměnit nebo přidat další.

| Část | Návrh | Poznámka |
| --- | --- | --- |
| Úprava fotky | Vyrovnání osvětlení (odečtení odhadnutého pozadí) | V testu snížila chyby Tesseractu z 16–25 % na 1–3 % znaků |
| OCR, motor 1 | Tesseract s češtinou | Rychlý (sekundy na stranu), nejméně chyb v souvislém textu, ztrácí desetinné čárky a levý okraj šikmých fotek |
| OCR, motor 2 | PaddleOCR PP-OCRv6, česky | Pomalý (desítky sekund na stranu), přesná čísla a tabulky, občas ztratí čárku nad í |
| Sloučení | Shodná slova platí, sporná rozhodne český slovník, u čísel má přednost PaddleOCR | V testu nejvíc přesně přepsaných citlivých údajů (33 z 34) |
| Dávkové zpracování | Import fotek a OCR na pozadí, třeba přes noc | Na slabém počítači advokátky je PaddleOCR pomalý, čas ale nevadí |
| OCR v C# | Tesseract přes knihovnu pro .NET, PaddleOCR přes ONNX Runtime | Je třeba ověřit, že české modely PP-OCRv6 běží v C# stejně jako v Pythonu. Záložní řešení je přibalený pomocný program v Pythonu |
| Windows OCR | Možný třetí motor | Ověřit na Matějově notebooku, v cloudu ho nejde spustit |
| Rukopis | Zatím jen označení „nutné přepsat ručně“ | Vzorky rukopisu zatím chybí, spolehlivé místní řešení pro český rukopis nenalezeno |
| Rozdělené zobrazení | Text vlevo, fotka vpravo, zoom, kliknutí propojí slovo a místo na fotce | Sporná místa ze sloučení a slova s nízkou jistotou jsou podbarvená |
| Hledání údajů | Pravidla (rodné číslo, IČO, účet, telefon, e-mail) a seznam českých jmen | Funguje bez modelu a licencí |
| Odolnost vůči chybám OCR | Porovnání bez diakritiky a s tolerancí jednoho až dvou znaků | Když je jinde ve spisu „Míček“, začerní se i „Miček“ nebo „Mícek“. Zbylé chyby OCR nikdy nebudou nulové |
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

1. **Test OCR.** První kolo je hotové, viz výsledky výše. Zbývá vyzkoušet rukopis, ověřit PaddleOCR a Tesseract v C# na Matějově notebooku a porovnat s Windows OCR. Advokátka pak spustí srovnání na pár skutečných fotkách u sebe, aby se nic neposílalo ven.
2. **Prohlížeč.** Import fotek, OCR, rozdělené zobrazení, oprava textu a šifrovaný trezor.
3. **Anonymizace a export.** Návrhy údajů, schvalování, klíč pseudonymů, složka `ProClaude` a kontrola před zápisem.
4. **Propojení s Claude Code.** `CLAUDE.md`, pravidla přístupu a dosazení skutečných jmen zpět do výstupu.
5. **Instalátor a předání.** Instalace na jejím počítači, záloha trezoru a krátký návod.

## Rizika

- Pseudonymizace není anonymizace: i bez jmen může spis prozradit, o koho jde, a podle GDPR se pseudonymizovaná data stále považují za osobní údaje. Jestli takové použití obstojí z pohledu mlčenlivosti advokáta, musí posoudit ona.
- Ztráta hesla k trezoru znamená ztrátu dat, proto bude součástí programu záloha a náhradní klíč.
- Matějův notebook má 8 GB RAM. To na lehké OCR stačí, na místní jazykový model spíš ne. Advokátka nemá silný počítač, přesné parametry nejsou známé. PaddleOCR na něm může trvat i minutu na stranu, proto dávkové zpracování přes noc.
- Převod PaddleOCR do C# nemusí dát stejnou přesnost jako v Pythonu. Ověří se jako první krok vývoje.
- Žádné OCR nepřepíše všechno správně. Chybně přečtené jméno může projít anonymizací, proto hledání údajů toleruje chyby a advokátka export schvaluje.

## Zdroje

- [Claude Code: sandboxing](https://code.claude.com/docs/en/sandboxing)
- [ÚFAL: komerční licence NameTag 3](https://ufal.mff.cuni.cz/node/3032)
- [PaddleOCR-VL a PP-OCR, článek o modelech](https://arxiv.org/html/2510.14528v1)
- [Srovnání místních OCR modelů 2026](https://vdf.ai/blog/best-local-ocr-models/)
