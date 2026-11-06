# Co a jak otestovat na Windows

Návod pro Matěje. V cloudové relaci jde program jen přeložit a otestovat jádro, okno a Windows OCR se dají vyzkoušet až tady. Každý test má na konci, co poslat zpět Claudovi.

Testy může dělat i agent (Claude Code), postup pro něj je v [agent.md](agent.md).

**Používej jen svůj archiv ze stavebního úřadu nebo vymyšlené dokumenty, nikdy spisy advokátky.** Fotky do vývojové relace s Claudem neposílej, stačí texty, čísla a snímky obrazovky bez citlivých údajů.

## Přehled

| Test | Stav | Co zbývá Matějovi 👤 |
| --- | --- | --- |
| Příprava | ✅ hotovo | |
| Test 0: automatické testy | ✅ hotovo, 2026-11-06 473 z 473 | |
| Test 1: OCR a rychlost | ✅ hotovo, výsledky v [návrhu](navrh.md) | |
| Test 1b: grafická karta | ✅ hotovo, grafika asi 2× rychlejší než procesor | |
| Test 2: prohlížeč | ✅ hotovo, vše funguje, PaddleOCR na grafice znatelně rychlejší | |
| Test 3: fotky bez otočení | ✅ hotovo, otočení funguje | |
| Test 4: anonymizace a export | ✅ agent 2026-11-06 na archivu, úniky opravené, export s kontrolou funguje | |
| Test 5: vrácení jmen a Word | 🔶 agent: jména se vracejí ve správném pádě, .docx vzniká | 👤 podívat se na barvy v okně vrácení jmen a otevřít .docx ve Wordu |
| Test 6: měření anonymizace s Windows OCR | ✅ hotovo, 2026-11-04 0 úniků z 87 | |
| Test 7: dokumenty, složky a černé fotky | ✅ agent 2026-11-04, prázdná stránka už přepis nezastaví | |
| Test 8: instalátor | 🔶 agent 2026-11-04: sestavení, instalace, přepis bez sítě, odinstalace | 👤 instalace na jiném počítači bez Visual Studia, ideálně u advokátky |
| Test 9: rychlá kontrola a PDF | 🔶 agent 2026-11-06: potvrzení celé strany, N, O, PDF s textem i sken | 👤 skutečné PDF z datové schránky (třeba z archivu) |
| Test 10: hledání, PDF pro archiv, export vybraných dokumentů | 🔶 agent 2026-11-06: hledání, textová vrstva a záložky PDF, export vybraných a nových | 👤 Ctrl+F v PDF v Edge, jestli označení sedí na slova |
| Test 11: přenos přes flash disk | 🔶 agent 2026-11-06 se složkou místo disku: kontrola fotek, klíče a textu, výstupy zpět | 👤 skutečný flash disk (pozná ho program?) |
| Test 12: stránky k přefocení a potvrzení celé strany | 🔶 agent 2026-11-06 na upravených obrázcích: rozmazaná, uříznutá, prázdná | 👤 stránky vyfocené mobilem (rozmazaně, potmě, uříznuté) |
| Test 13: záznam o exportech | 🔶 agent 2026-11-06: záznam bez jmen, počty jen z exportu | 👤 otevřít uložený záznam ve Wordu |
| Test 14: úkol pro Clauda | 🔶 agent 2026-11-06: náhled, UKOL.md, nové jméno v úkolu zastaví export | 👤 jestli Claude v ProClaude úkol najde a výsledek uloží do `vystup` |
| Test 15: přehled dat | 🔶 agent 2026-11-06: řazení, skrytá data narození, nápovědy | 👤 otevřít uložený přehled ve Wordu |
| Test 16: záloha | 🔶 agent 2026-11-06: nabídka po spuštění, ověření, zákaz disku s ProClaude | 👤 záloha na skutečný externí disk |
| Test 17: upravené fotky k tisku | 🔶 agent 2026-11-06: PDF vzniká, černobílá a oříznutá | 👤 vytisknout pár stran a posoudit čitelnost, zkusit `barevně` u fotek ve fólii |

Sloupec 👤 jsou kroky, které agent udělat nemůže (skutečný hardware, Word, tisk, Claude na druhém počítači). Ostatní testy dělá agent, výsledky jsou v [vysledky-testu-windows.md](vysledky-testu-windows.md).

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
4. Klikni na **Spustit všechny testy** (dvojitá zelená šipka, Ctrl+R, A). Má jich být 473, všechny zelené.

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
   - [ ] Nabídka **Spis → Oddělovače dokumentů…**, zaškrtni **Je to stránka, vrátit**: fotka se objeví mezi stranami na svém místě a oba dokumenty se spojí.
   - [ ] Po **Spustit přepis** dostaly oba dokumenty název navržený z prvního textu (třeba „Kupní smlouva“). Když ne, napiš, co na první stránce bylo.
4. Pravé tlačítko na straně v seznamu:
   - [ ] **Posunout stranu nahoru/dolů** mění pořadí, na hranici přejde strana do sousedního dokumentu.
   - [ ] **Přesunout stranu do dokumentu…** i do nového dokumentu.
   - [ ] **Začít od této strany nový dokument** a **Spojit dokument s předchozím**.
   - [ ] **Přejmenovat dokument…** a **Navrhnout název dokumentu z textu**.
5. Exportuj (strany označ jako zkontrolované). [ ] V `ProClaude\S-…\dokumenty` je jeden soubor na dokument se stranami jako oddíly a jméno osoby v názvu dokumentu nebo složky je nahrazené.

**Poslat:** co nefungovalo, hlavně jestli se černá fotka poznala (případně při jakém světle) a jaké názvy program navrhl.

## Test 8: instalátor (20 minut, poprvé déle kvůli stahování)

Instalátor je jeden soubor `.exe`, který advokátka spustí dvojklikem. Obsahuje program i modely OCR, takže nainstalovaný Koncipient nepotřebuje síť.

1. Jednou nainstaluj Inno Setup: v PowerShellu `winget install JRSoftware.InnoSetup`.
2. V kořeni repozitáře spusť v PowerShellu:
   ```
   powershell -ExecutionPolicy Bypass -File instalace\sestav.ps1
   ```
   [ ] Skript skončí řádkem `Hotovo: …\vystup\Koncipient-instalace-0.1.0.exe` a velikostí (čekám kolem 150 MB).
3. Spusť instalátor. [ ] Nechce práva správce, je česky a vytvoří zástupce na ploše.
4. **Odpoj počítač od sítě** (Wi-Fi vypnout nebo vytáhnout kabel) a spusť Koncipient ze zástupce.
   - [ ] Založ spis, přidej pár fotek ze vzorů a spusť přepis. Nesmí se ptát na stažení modelů a přepis má doběhnout.
   - [ ] Najdi údaje a exportuj. Funguje i vrácení jmen do Wordu.
5. Odinstaluj ho přes Nastavení → Aplikace. [ ] Složka `Dokumenty\Koncipient` se spisy zůstala.
6. Kdo chce, může zkusit instalaci na jiném počítači bez Visual Studia (třeba na notebooku): ověří se tím, že program nepotřebuje nic doinstalovat.

**Poslat:** velikost instalátoru, jestli přepis běžel bez sítě a na čem se instalace zasekla.

## Test 9: rychlá kontrola a import PDF (15 minut)

1. Nový spis, **Přidat fotky…** a vyber `vzory\pdf\s-textem.pdf` a `vzory\pdf\sken.pdf`.
   - [ ] Vzniknou dva dokumenty pojmenované podle souborů, fotka vpravo je vykreslená stránka.
   - [ ] Strana z `s-textem.pdf` má text hned po importu (bez přepisu) a slova se při kliknutí označí na správném místě fotky.
2. Zkus i skutečné PDF z datové schránky (třeba z Matějova archivu, ne od advokátky). [ ] Text sedí, nic nechybí.
3. **Spustit přepis.** [ ] Přepíše se jen naskenovaná strana, strana s textem ne.
4. Záložka **Rychlá kontrola**:
   - [ ] Seznam ukazuje nahrazené údaje (údaj → náhrada, počet) a žlutá podezřelá slova, dole je kousek textu a výřez fotky.
   - [ ] Enter posune na další místo. Po vyřízení všech míst strany se objeví „celá strana k potvrzení“: dole celý text pro Clauda, vpravo fotka. Teprve Enter na ní stranu označí jako zkontrolovanou.
   - [ ] **N** u nahrazeného údaje ho ponechá na všech stranách (v záložce Osoby a údaje má akci Ponechat).
   - [ ] **O** u podezřelého slova z něj udělá osobu s vymyšleným jménem.
   - [ ] Strany bez nálezů jsou v seznamu rovnou jako „celá strana k potvrzení“.
5. Na spisu z archivu odhadni, kolik času ušetří rychlá kontrola proti procházení po stranách, a jestli je podezřelých slov rozumně (ne stovky).

**Poslat:** jak dlouho trvala rychlá kontrola, kolik bylo podezřelých slov a kolik z nich byly opravdu přehlédnuté údaje.

## Test 10: hledání, PDF pro archiv, export vybraných dokumentů (10 minut)

Na spisu s několika dokumenty a hotovým přepisem:

1. **Ctrl+F**, napiš začátek slova, které ve spisu je (třeba „smlouv“), Enter.
   - [ ] Nálezy ukazují stranu a okolí, Enter nebo dvojklik otevře stranu a slovo je označené.
   - [ ] Najde to i bez diakritiky a frázi přes konec řádku.
2. **Tisk a archiv → PDF pro archiv (s textem)…**, nech vybrané všechny dokumenty a ulož.
   - [ ] PDF otevřené v prohlížeči (Edge) vypadá jako fotky, Ctrl+F v něm najde slovo a označí ho zhruba na správném místě.
   - [ ] Vlevo jsou záložky s názvy dokumentů.
3. **Exportovat pro Clauda…**
   - [ ] Okno nabídne dokumenty se stavem „nový“ nebo „exportováno“.
   - [ ] Vyber jeden dokument: v `ProClaude\S-…\dokumenty` je jen on, se stejným číslem jako v pořadí spisu, a `manifest.json` má `"cely_spis": false`.
   - [ ] Přidej do spisu další fotky, přepiš je a v exportu klikni **Jen nové**: vybere se jen nový dokument a v `ProClaude\\S-…\\dokumenty` zůstanou i ty dřívější.

**Poslat:** co nesedělo, hlavně jestli označení v PDF sedí na slova.

## Test 11: přenos přes flash disk (10 minut)

1. Exportuj spis. [ ] Hláška po exportu nabídne zkopírování na flash disk. Bez zasunutého disku napíše, ať ho zasuneš.
2. Zasuň flash disk a dej **Ano**. [ ] Na disku je `ProClaude` se stejným obsahem jako v `Dokumenty\Koncipient\ProClaude`, bez souborů `.docx`.
3. Zkus podvrh: do `Dokumenty\Koncipient\ProClaude\S-…` zkopíruj libovolnou fotku a dej **Claude → Zkopírovat export na flash disk**. [ ] Program odmítne a vypíše tu fotku. Fotku pak smaž.
4. Na disk do `ProClaude\S-…\vystup` ulož textový soubor `navrh.md` s vymyšleným jménem z exportu (třeba „Bohdan Jasaník podal žalobu.“) a klikni na **Načíst od Clauda z flash disku**.
   - [ ] Otevře se okno vrácení jmen s tímto souborem a jméno je vrácené.
   - [ ] Podruhé program napíše, že nové výstupy nejsou.

**Poslat:** co nesedělo a jestli program poznal flash disk (některé disky se hlásí jako pevné, pak je nevidí).

## Test 12: stránky k přefocení a potvrzení celé strany (10 minut)

1. Nafoť mobilem 4 stránky: jednu normálně, jednu rozmazaně (pohyb), jednu potmě a jednu s uříznutým okrajem textu. Přidej je do nového spisu a spusť přepis.
   - [ ] Po přepisu se program zeptá na stránky k přefocení. Normální stránka v seznamu není, ostatní ano se srozumitelným důvodem.
   - [ ] **Nahradit novou fotkou…** vymění fotku, náhled se změní. Po dalším přepisu strana zmizí ze seznamu **Spis → Stránky k přefocení…**.
   - [ ] Prázdná stránka (papír na stole) v seznamu není.
2. Rychlá kontrola na tomto spisu:
   - [ ] Po vyřízení míst strany se objeví „celá strana k potvrzení“ s celým textem pro Clauda a fotkou vpravo. Strana je zkontrolovaná až po Enteru na ní.

**Poslat:** které stránky program nahlásil a které ne, a jestli nějaká dobrá stránka byla v seznamu zbytečně.

## Test 13: záznam o exportech (5 minut)

1. Na spisu, který už byl exportovaný, exportuj ještě jednou jen jeden dokument a zkopíruj na flash disk.
2. **Claude → Záznam o exportech…**
   - [ ] Jsou tam oba exporty (celý spis a jeden dokument) a kopie na flash disk, s časem a jménem uživatele Windows.
   - [ ] U exportu jsou počty nahrazených údajů podle typu a jestli byly strany zkontrolované.
   - [ ] V záznamu není žádné skutečné jméno, adresa ani název dokumentu.
3. **Uložit jako Word…** [ ] Dokument jde otevřít ve Wordu.

## Test 14: úkol pro Clauda (5 minut)

1. **Exportovat pro Clauda…** a do pole úkolu napiš větu se skutečným (vymyšleným) jménem ze spisu ve skloňovaném tvaru, třeba „Shrň, co tvrdí Petra Pokusného“.
   - [ ] Pod polem se ukazuje náhled s vymyšleným jménem ve správném pádě.
   - [ ] V `ProClaude\S-…` je `UKOL.md` bez skutečného jména a `manifest.json` má `"ukol": "UKOL.md"`.
2. Na počítači s Claudem (nebo v Claude Code ve složce ProClaude) napiš „udělej úkol ve spisu S-…“. [ ] Claude najde UKOL.md a výsledek uloží do `vystup`.
3. Exportuj znovu bez úkolu. [ ] `UKOL.md` zmizel.

## Test 15: přehled dat (5 minut)

Na přepsaném spisu z archivu otevři záložku **Data**.
- [ ] Data jsou seřazená od nejstaršího a mají okolí a číslo strany. Najde i zápis slovy („7. května 2024“).
- [ ] Nápověda v hranatých závorkách sedí aspoň u většiny (doručení, ze dne, jednání…).
- [ ] Data narození jsou skrytá, po zrušení zaškrtnutí se objeví.
- [ ] Enter otevře stranu a označí datum. **Uložit jako Word…** vytvoří dokument.

**Poslat:** kolik dat bylo a jestli některé chybělo (třeba kvůli chybě OCR) nebo bylo nesmyslné.

## Test 16: záloha (5 minut)

1. Spusť Koncipient. [ ] Když záloha ještě neproběhla, program ji po spuštění nabídne.
2. **Spis → Zálohovat spisy…** a vyber externí disk, **který se nepoužívá k přenosu pro Clauda**. [ ] Na disk s přenosem (se složkou ProClaude) program zálohu odmítne. [ ] Na disku vznikne `Koncipient-zaloha\RRRR-MM-DD` se všemi spisy a hláška potvrdí ověření.
3. Zavři a spusť znovu. [ ] Připomenutí už nepřijde (vrátí se po týdnu).

## Test 17: upravené fotky k tisku (10 minut)

1. Nafoť mobilem 3 stránky: list položený šikmo na tmavém stole, list ve stínu (třeba u okna) a list se zvýrazňovačem. Přidej je do spisu.
2. **Tisk a archiv → Upravené fotky k tisku…**, nech obě volby zaškrtnuté a ulož PDF.
   - [ ] Stůl zmizel a list je narovnaný (ne šikmý lichoběžník).
   - [ ] Papír je bílý i ve stínu, text je černý a čitelný, zvýrazňovač nevadí.
   - [ ] PDF jde vytisknout na A4 (Ano v hlášce PDF otevře).
3. Zkus fotku, na které je vedle listu další papír. [ ] Program ji neořízne špatně (raději nechá celou).

**Poslat:** PDF nebo popis, co nesedělo (oříznutí, slabý text, černé skvrny).

## Kde jsou data

| Co | Kde |
| --- | --- |
| Spisy (fotky, přepisy, klíč pseudonymů) | `Dokumenty\Koncipient\Spisy` |
| Export pro Clauda | `Dokumenty\Koncipient\ProClaude` |
| Modely OCR | `%LOCALAPPDATA%\Koncipient\modely` |

Po testu můžeš obě složky v `Dokumenty\Koncipient` smazat.
