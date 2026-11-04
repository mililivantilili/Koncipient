# Co a jak otestovat na Windows

Návod pro Matěje. V cloudové relaci jde program jen přeložit a otestovat jádro, okno a Windows OCR se dají vyzkoušet až tady. Každý test má na konci, co poslat zpět Claudovi.

Testy může dělat i agent (Claude Code), postup pro něj je v [agent.md](agent.md).

**Používej jen svůj archiv ze stavebního úřadu nebo vymyšlené dokumenty, nikdy spisy advokátky.** Fotky do vývojové relace s Claudem neposílej, stačí texty, čísla a snímky obrazovky bez citlivých údajů.

## Přehled

| Test | Stav |
| --- | --- |
| Příprava | ✅ hotovo |
| Test 0: automatické testy | ✅ hotovo, vše prošlo |
| Test 1: OCR a rychlost | ✅ hotovo, výsledky v [návrhu](navrh.md) |
| Test 1b: grafická karta | ✅ hotovo, grafika asi 2× rychlejší než procesor |
| Test 2: prohlížeč | ✅ hotovo, vše funguje, PaddleOCR na grafice znatelně rychlejší |
| Test 3: fotky bez otočení | ✅ hotovo, otočení funguje (zbývá jedna drobná kontrola, viz níže) |
| Test 4: anonymizace a export | 🔶 zopakován 2026-11-04, zbylé úniky opravené (viz [výsledky](vysledky-testu-windows.md#stav-oprav-2026-11-04)), zopakovat na archivu |
| Test 5: vrácení jmen a Word | 🔶 proběhl automaticky, barvy v okně a otevření ve Wordu zbývá ověřit |
| Test 6: měření anonymizace s Windows OCR | ✅ hotovo, 2026-11-04 0 úniků z 87 |
| Test 7: dokumenty, složky a černé fotky | 🔶 černé fotky z mobilu změřené, nové pravidlo podle hran, zopakovat se 3 fotkami z Pixelu |

Hotové testy (✅) přeskoč, jejich postup je sbalený.

## ✅ Příprava: hotovo 2026-11-03

<details>
<summary>Postup (pro případné zopakování)</summary>

1. Visual Studio 2026 se sadou **Vývoj desktopových aplikací .NET**, která obsahuje .NET 10 SDK. Sadu doplníš přes Visual Studio Installer, tlačítko Upravit. Ve VS 2026 je obvykle už nainstalovaná.
2. Stáhni větev `claude/friendly-franklin-k4ma6l` (ve Visual Studiu Git, Spravovat větve, nebo `git pull`).
3. Otevři `Koncipient.sln`. Při prvním sestavení si Visual Studio stáhne balíčky NuGet, chvíli to trvá.
4. Připrav si složku s 5–10 fotkami z archivu, ideálně různorodými: tištěný text, tabulka, razítko, šikmá nebo tmavá fotka a pokud máš, i ručně psaný text.

</details>

## ✅ Test 0: automatické testy: hotovo 2026-11-03

<details>
<summary>Postup (pro případné zopakování)</summary>

1. Otevři **řešení**, ne jen složku: Soubor → Otevřít → Projekt/řešení… → `Koncipient.sln`. Bez toho se nabídka Test ani testy neukážou.
2. Počkej, až se stáhnou balíčky (vlevo dole), a sestav řešení: Sestavení → Sestavit řešení (Ctrl+Shift+B).
3. Otevři Test → Průzkumník testů (Ctrl+E, T), anglicky *Test Explorer*.
4. Klikni na **Spustit všechny testy** (dvojitá zelená šipka, Ctrl+R, A). Má jich být 428, všechny zelené.

Když sestavení selže, otevři Zobrazit → Seznam chyb.

**Poslat:** kolik testů prošlo, případně název a text chyby u neúspěšného testu nebo první chybu ze Seznamu chyb.

</details>

## ✅ Test 1: OCR a rychlost: hotovo 2026-11-03

<details>
<summary>Postup (pro případné zopakování)</summary>

1. V Průzkumníku řešení klikni pravým na `Koncipient.OcrTest`, Nastavit jako spouštěný projekt, a stiskni F5.
2. Přetáhni do okna složku s fotkami a stiskni Enter. Poprvé se stáhnou modely (asi 140 MB).
3. Počkej na konec. Program vypíše čas každé fotky a vytvoří ve složce s fotkami `vysledky-ocr.zip`.
4. Pokud napíše, že Windows OCR chybí čeština, postup je v [src/README.md](../src/README.md). Test pak spusť znovu.

**Poslat:** soubor `vysledky-ocr.zip` (jen texty a časy, žádné fotky). Do zprávy napiš, kolik má notebook paměti a jaký procesor.

</details>

## ✅ Test 1b: grafická karta: hotovo 2026-11-03

<details>
<summary>Postup (pro případné zopakování)</summary>

PaddleOCR umí počítat na grafické kartě (DirectML). Notebook má dvě, Intel UHD 630 a NVIDIA GTX 1050, a test zjistí, která je rychlejší.

1. Nastav `Koncipient.OcrTest` jako spouštěný projekt (pravým tlačítkem v Průzkumníku řešení), stiskni F5, přetáhni složku s fotkami a na otázku „Co spustit?“ napiš **2**.
2. Program přepíše prvních 5 fotek PaddleOCR čtyřikrát: na procesoru, na nejvýkonnější grafice, na grafice 0 a na grafice 1. U každého vypíše, kde skutečně počítal.
3. První fotka na grafice bývá pomalejší (grafika se „zahřívá“), rozhodují další.

**Poslat:** nový `vysledky-ocr.zip`. Jestli program napíše „grafická karta nejde použít“, opiš i text chyby.

</details>

## ✅ Test 2: prohlížeč: hotovo 2026-11-03

<details>
<summary>Postup (pro případné zopakování)</summary>

1. Nastav `Koncipient.App` jako spouštěný projekt a stiskni F5.
2. **Nový spis**, pojmenuj ho třeba „test archiv“.
3. **Přidat fotky…** a vyber fotky ze složky.
4. **Spustit přepis.** Sleduj průběh dole. Zkus uprostřed **Zastavit** a pak znovu **Spustit přepis**, má pokračovat, ne začínat znovu.
5. Klikni na stranu vlevo a zkontroluj:
   - [ ] Fotka vpravo je vidět, tlačítko **Na šířku** a Ctrl + kolečko mění zvětšení.
   - [ ] Klik na slovo v textu zvýrazní správné místo na fotce a fotka se k němu posune.
   - [ ] Klik do fotky na slovo zvýrazní slovo v textu.
   - [ ] Oranžová slova (sporná) jsou opravdu místa, kde je text těžko čitelný nebo chybný.
   - [ ] Dvojklik na slovo otevře opravu, po uložení je slovo zelené a oprava zůstane i po zavření programu. Napsáním více slov se doplní další slova, smazáním textu se slovo odstraní.
   - [ ] Ctrl + kolečko zvětšuje fotku kolem místa, kde je myš.
   - [ ] Pravé tlačítko na slově: **Doplnit vynechaný text za toto slovo…**, **Smazat vybraná slova**, **Zalomit řádek před tímto slovem**, **Spojit řádek s dalším**, **Posunout řádek nahoru/dolů** dělají, co mají.
   - [ ] Ctrl + tažení myší na fotce označí oblast s textem, který OCR úplně vynechalo. Po napsání textu se objeví jako nový řádek na správném místě a kliknutím do oblasti se najde.
6. Během přepisu by počítač neměl usnout ani po delší době.

**Poslat:** co nefungovalo nebo bylo nepohodlné, ideálně se snímkem obrazovky. Zajímá nás i to, jestli je text dobře čitelný a jestli by šlo něco udělat jednodušeji.

</details>

## ✅ Test 3: fotky bez otočení: hotovo 2026-11-03

Po Matějově připomínce se přepis při ručním otočení nemaže, ale odloží. Při otočení zpět se vrátí i s opravami. Mimochodem při testu 4 zkus:

- [ ] Stranu otoč ⟳ a zase ⟲ zpět. Text se objeví hned, bez nového přepisu, i s ručními opravami.

<details>
<summary>Původní postup (pro případné zopakování)</summary>

Fotky z foťáku často nemají údaj o otočení. Program ho pozná z textu.

1. Vezmi dvě fotky z archivu a v aplikaci Malování je otoč (jednu o 90°, druhou o 180°) a ulož jako PNG. Malování údaj o otočení neukládá.
2. Založ nový spis, přidej je a spusť přepis.
3. [ ] Po přepisu jsou obě strany rovně a text dává smysl.
4. Tlačítky ⟲ a ⟳ zkus stranu otočit ručně. [ ] Přepis strany se smaže a při dalším spuštění přepisu se udělá znovu.

**Poslat:** jestli se otočení poznalo správně.

</details>

## Test 4: anonymizace a export (etapa 3, 20 minut)

Nejdřív po Git → Pull klikni ve spisu z testu 2 na **Spustit přepis**. Program stáhne český slovník (asi 4 MB) a sloučí přepisy Windows OCR a PaddleOCR do jednoho textu, nic se znovu nepřepisuje. Strany s Tvými ručními opravami zůstanou, jak jsou.

1. Ve spisu z testu 2 klikni na **Najít údaje** (po přepisu se to udělá i samo).
2. Projdi strany. Fialově jsou údaje, které se nahradí. Najetím myši se ukáže pseudonym.
   - [ ] Jména, adresy, telefony, e-maily, čísla parcel jsou fialově.
   - [ ] Fialové není nic, co citlivé není. Takové slovo označ pravým tlačítkem, **Není citlivé**.
   - [ ] Co program přehlédl (třeba ručně psaný podpis), vyber kliknutím, případně Shift + klik na další slova, a pravým tlačítkem **Anonymizovat jako osobu** apod.
   - [ ] Stejné jméno v jiném pádě nebo s chybou z OCR dostane stejný pseudonym i na ostatních stranách.
3. Přepni na záložku **Osoby a údaje**.
   - [ ] Osoby mají vymyšlená jména se správným rodem (žena dostane ženské jméno).
   - [ ] Adresa, datum narození a telefon z hlavičky jsou odsazené pod osobou, ke které patří, a role (žalobce…) je vyplněná.
   - [ ] Dvojklikem jde opravit skutečné jméno (když ho OCR přečetlo s chybou), náhradu i roli.
   - [ ] **Tohle neanonymizovat** u soudu nebo obce nechá údaj v náhledu pro Clauda beze změny.
   - [ ] Zaškrtnutí **Iniciály** vybere jméno se stejnými iniciálami.
   - [ ] Přepnutí na **kódy [OSOBA_001]** a zpět změní náhrady v náhledu.
   - [ ] Ve druhém spisu **Převzít z jiného spisu…** dá stejné osobě stejné vymyšlené jméno.
4. Zapni **Náhled pro Clauda** a projdi text, který Claude dostane.
5. **Exportovat pro Clauda…** bez zaškrtnutých stran. [ ] Export se odmítne a vypíše, které strany nejsou zkontrolované.
6. U každé strany zaškrtni **Strana zkontrolována** a exportuj znovu. [ ] Ve složce `Dokumenty\Koncipient\ProClaude` vznikne podsložka spisu s `dokumenty/*.md`, `entity.json` a `manifest.json` a v kořeni `CLAUDE.md`.
7. [ ] V exportu nejsou žádné fotky, soubor `klic.json` ani skutečná jména.

**Poslat:** co program našel špatně nebo nenašel (typ údaje a jak vypadal, klidně přepsaný vymyšlenými slovy) a obsah jednoho souboru `dokumenty/01-….md`, pokud v něm nic citlivého nezůstalo. Napiš i, jak na Tebe působí vymyšlená jména.

## Test 5: vrácení jmen a Word (etapa 4, 10 minut)

Místo skutečného Clauda stačí dokument napsat ručně.

1. Ve složce `Dokumenty\Koncipient\ProClaude\S-0001\vystup` vytvoř soubor `test.md` a napiš do něj pár vět s vymyšlenými jmény z exportu v různých pádech, např. „Doporučujeme panu Jasaníkovi podat odvolání.“ Přidej i řádek s `[NOVÁ OSOBA: znalec]`, jeden kód typu `[ADRESA_001]` a jedno jméno, které ve spisu není.
2. V programu **Vrátit jména do výstupu od Clauda…**, Otevřít dokument, vyber `test.md`.
3. Zkontroluj:
   - [ ] Vymyšlená jména jsou nahrazená skutečnými ve správném pádě (zeleně).
   - [ ] Kód adresy je nahrazený skutečnou adresou.
   - [ ] `[NOVÁ OSOBA: …]` je červeně, neznámé jméno oranžově a najetím myši se ukáže proč.
   - [ ] Text vpravo jde přepsat.
4. **Uložit jako Word…** upozorní na neodstraněné značky. Ulož a otevři ve Wordu. [ ] Nadpisy, odrážky a tučné písmo vypadají rozumně.
5. Otevři `ProClaude\PRAVIDLA-KONCIPIENT.md` a přečti si pravidla pro Clauda. [ ] Dávají smysl, nic nechybí?

**Poslat:** co se vrátilo špatně (v jakém pádě a jaké jméno, klidně vymyšlené) a připomínky k pravidlům pro Clauda.

## Test 6: měření anonymizace s Windows OCR (nepovinný, 5 minut)

V repozitáři je vymyšlená sada šesti dokumentů vyfocených jako z telefonu (`vzory/sada`). Nástroj je prožene celým programem a spočítá, kolik vyznačených údajů proklouzlo do textu pro Clauda. V cloudu to jde jen s PaddleOCR a Tesseractem, u Tebe se zapojí i Windows OCR, tedy hlavní motor.

1. Nastav `Koncipient.MereniAnonymizace` jako spouštěný projekt a stiskni F5. Nic nezadávej, nástroj sadu najde sám.
2. Po dokončení vypíše tabulku podle typů údajů a seznam úniků. Výsledky uloží do `%TEMP%\koncipient-mereni`.

**Poslat:** soubor `%TEMP%\koncipient-mereni\vysledek-ocr.txt` a tabulku z okna. Data jsou vymyšlená, takže poslat jde všechno, i anonymizované texty `anonymizovano-ocr-*.md`.

## Test 7: dokumenty, složky a černé fotky (15 minut)

Spis se teď skládá z dokumentů (smlouva, rozsudek…). Strany jsou v seznamu vlevo seskupené pod jejich názvy.

1. Připrav složku s podsložkami, třeba `Archiv/1 Smlouva` a `Archiv/2 Rozhodnutí`, v každé pár fotek.
2. Nový spis, **Přidat složku…**, vyber `Archiv`. [ ] Každá podsložka je jeden dokument pojmenovaný podle ní, fotky jsou v pořadí podle názvu (IMG_2 před IMG_10).
3. Nafoť mobilem 2–3 stránky, pak jednu fotku se zakrytým objektivem (černou), pak další 2 stránky. Přidej je přes **Přidat fotky…**.
   - [ ] Po importu se otevřelo okno **Oddělovače dokumentů** s náhledem černé fotky. Nech ji nezaškrtnutou a dej **Hotovo**: fotky se rozdělily do dvou dokumentů.
   - [ ] Zkus i tři fotky z Pixelu (skoro černá, červená, šedá rozmazaná): všechny tři má okno ukázat jako oddělovače, fotky dokumentů ani prázdný papír ne.
   - [ ] Tlačítko **Oddělovače…**, zaškrtni **Je to stránka, vrátit**: fotka se objeví mezi stranami na svém místě a oba dokumenty se spojí.
   - [ ] Po **Spustit přepis** dostaly oba dokumenty název navržený z prvního textu (třeba „Kupní smlouva“). Když ne, napiš, co na první stránce bylo.
4. Pravé tlačítko na straně v seznamu:
   - [ ] **Posunout stranu nahoru/dolů** mění pořadí, na hranici přejde strana do sousedního dokumentu.
   - [ ] **Přesunout stranu do dokumentu…** i do nového dokumentu.
   - [ ] **Začít od této strany nový dokument** a **Spojit dokument s předchozím**.
   - [ ] **Přejmenovat dokument…** a **Navrhnout název dokumentu z textu**.
5. Exportuj (strany označ jako zkontrolované). [ ] V `ProClaude\S-…\dokumenty` je jeden soubor na dokument se stranami jako oddíly a jméno osoby v názvu dokumentu nebo složky je nahrazené.

**Poslat:** co nefungovalo, hlavně jestli se černá fotka poznala (případně při jakém světle) a jaké názvy program navrhl.

## Kde jsou data

| Co | Kde |
| --- | --- |
| Spisy (fotky, přepisy, klíč pseudonymů) | `Dokumenty\Koncipient\Spisy` |
| Export pro Clauda | `Dokumenty\Koncipient\ProClaude` |
| Modely OCR | `%LOCALAPPDATA%\Koncipient\modely` |

Po testu můžeš obě složky v `Dokumenty\Koncipient` smazat.
