# Koncipient: návod

Koncipient převede nafocené spisy na text přímo ve vašem počítači. Ukáže text vedle fotky, najde v něm jména, adresy a čísla a připraví kopii, ve které jsou tyto údaje nahrazené. Jen tuto kopii pak dostane Claude. Fotky, skutečná jména a klíč k nim zůstávají v počítači s Koncipientem.

## Dvě pravidla na začátek

1. **Koncipient a Claude běží na dvou různých počítačích.** Na počítač s Claudem se přenáší jen složka `ProClaude`, například na flash disku.
2. **Claudovi nikdy nedávejte fotky, složku `Spisy` ani text zkopírovaný přímo ze spisu.** Claude posílá všechno, co dostane, do cloudu.

## Instalace

Spusťte soubor `Koncipient-instalace-….exe` a potvrďte instalaci. Nepotřebuje heslo správce ani internet. Na ploše se objeví zástupce **Koncipient**.

Spisy se ukládají do složky `Dokumenty\Koncipient\Spisy`. Odinstalace ani nová verze je nesmažou.

## 1. Nafocení spisu

- Každou stránku vyfoťte zvlášť, celou a zhora, ideálně u okna nebo pod lampou. Stačí běžný mobil.
- **Mezi dokumenty vyfoťte jednu fotku se zakrytým objektivem** (prst nebo dlaň přes fotoaparát). Koncipient podle ní pozná, kde začíná další dokument (smlouva, rozsudek…).
- Fotky přeneste do počítače s Koncipientem. Můžete je roztřídit do složek, každá složka bude jeden dokument.
- **PDF (třeba z datové schránky) nefoťte.** Přidejte je rovnou, každé PDF bude jeden dokument. Když PDF obsahuje text, Koncipient ho převezme přesně a bez přepisování.

## 2. Založení spisu

1. **Nový spis** a zadejte název, například spisovou značku. Název zůstane jen u vás.
2. **Přidat fotky…** (vybrané fotky nebo PDF) nebo **Přidat složku…** (celá složka, podsložky se stanou dokumenty, PDF v nich také).
3. Otevře se okno **Oddělovače dokumentů** s fotkami, které program vzal jako zakrytý objektiv. Pokud je mezi nimi skutečná stránka (třeba hodně tmavá), zaškrtněte **Je to stránka, vrátit**. Pak klikněte na **Hotovo**.

Pořadí stran i dokumenty jdou upravit později: klikněte pravým tlačítkem na stranu v seznamu vlevo.

## 3. Přepis (klidně přes noc)

Klikněte na **Spustit přepis**. Program fotky narovná, převede na text a najde v nich údaje. Jedna stránka trvá desítky sekund, celý spis klidně hodiny. Nechte počítač zapnutý a zapojený do zásuvky, sám neusne. Internet nepotřebuje. Přepis jde **Zastavit** a později pokračovat tam, kde skončil.

Když je hotovo, uvidíte v titulku okna „přepis dokončen“ a čas. Pokud se některá strana nepřepsala, program napíše, která. Stačí přepis spustit znovu.

**Stránky k přefocení:** po přepisu program nabídne strany, které vyšly špatně (rozmazané, tmavé, uříznuté u okraje nebo s nejistým přepisem), i s náhledem. Přefoťte je, dokud máte spis v ruce, a tlačítkem **Nahradit novou fotkou…** je vyměňte. Nová fotka se přepíše při dalším spuštění přepisu. Seznam ukáže kdykoli i tlačítko **K přefocení…**.

## 4. Kontrola: hlavní práce

Automatika nikdy nenajde všechno, proto nic neodejde bez vaší kontroly.

### Rychlá kontrola (doporučeno)

Záložka **Rychlá kontrola** ukáže jen místa, na kterých záleží, s kouskem textu a výřezem fotky:

- **zelené:** údaj, který program nahradí (u každého jednou za stranu, s počtem výskytů),
- **žluté ⚠:** slovo, které zůstane, ale vypadá jako přehlédnuté jméno nebo číslo.

Stejný údaj se ukáže jen jednou, i když je na více stranách. Projděte je klávesnicí: **Enter** = v pořádku, **N** = není citlivé (soud, úřad, obec; u osob a čísel se program zeptá), **O** = je to osoba, **U** = jiný údaj, odstranit. Když má strana všechna místa vyřízená, objeví se v seznamu **celá strana k potvrzení**: dole je její text přesně tak, jak ho dostane Claude, a vpravo fotka. Přečtěte ho. Jestli v něm zůstalo skutečné jméno nebo číslo, klikněte na **Otevřít stranu** a označte ho. Když je strana v pořádku, potvrďte ji Enterem. Teprve tím je zkontrolovaná. Strany, na kterých program nic nenašel, se ukážou rovnou k potvrzení.

Program totiž nenajde všechno (třeba příjmení Malý nebo Černý uprostřed věty). Proto se každá strana nakonec potvrzuje celá.

### Kontrola po stranách

Když chcete stranu projít celou:

1. Vyberte stranu vlevo. Uprostřed je text, vpravo fotka.
2. Zapněte **Náhled pro Clauda**. Uvidíte text přesně tak, jak ho dostane Claude.
3. **Přehlédnutý údaj:** označte ho myší, klikněte pravým tlačítkem a zvolte **Anonymizovat jako osobu**, **… adresu**, **… firmu** a podobně. Program ho pak nahradí na všech stranách spisu.
4. **Zbytečně nahrazený údaj** (soud, úřad, obec, které chcete nechat): pravým tlačítkem **Není citlivé**.
5. Chybu v přepisu opravíte pravým tlačítkem přes **Opravit slovo…**.
6. Když strana sedí, klikněte na **Strana zkontrolována**.

Záložka **Osoby a údaje** ukazuje všechno nalezené na jednom místě: skutečné jméno, čím se nahradí a kolikrát se v textu vyskytuje. Tady můžete:

- změnit vymyšlené jméno (**Jiné vymyšlené jméno**),
- nechat údaj beze změny (**Tohle neanonymizovat**),
- převzít stejná vymyšlená jména z jiného spisu téhož klienta (**Převzít z jiného spisu…**).

Osoby se nahrazují vymyšlenými jmény (např. Bohdan Jasaník). Claude s nimi pak píše přirozenou češtinu.

## 5. Export pro Clauda

1. **Exportovat pro Clauda…** Do pole **Co má Claude udělat** můžete rovnou napsat úkol, třeba „Navrhni odvolání proti rozsudku ve 2. dokumentu“. Jména v něm program nahradí stejně jako ve spisu a pod polem ukáže, co Claude dostane. Na počítači s Claudem pak stačí napsat „udělej úkol ve spisu S-0001“. Když má spis víc dokumentů, vyberte, které Claude dostane. Po doplnění spisu stačí tlačítko **Jen nové**. Kontrolují se jen strany vybraných dokumentů. Program ještě jednou projde text a zastaví export, pokud v něm zůstal údaj, který zná (jméno, číslo nebo adresa z tabulky Osoby a údaje). Údaj, který nikdy nepoznal, ale zastavit nemůže, proto je důležitá vaše kontrola v kroku 4.
2. Vznikne složka `Dokumenty\Koncipient\ProClaude\S-…`. Obsahuje jen anonymizovaný text (jeden soubor na dokument) a pokyny pro Clauda. Fotky ani skutečná jména v ní nejsou.
3. Program nabídne **zkopírovat ji na flash disk** (kdykoli později tlačítko **Na flash disk**). Zkopíruje jen složku `ProClaude` a předtím ověří, že v ní nejsou fotky ani klíč. Na počítači s Claudem ji zkopírujte na stejné místo jako minule a pracujte s ní jako obvykle. Na strany se Claude odkazuje čísly ze seznamu vlevo.

## 6. Návrat: skutečná jména do výsledku

1. Požádejte Clauda, ať výsledek (podání, shrnutí…) uloží jako soubor `.md` do složky `vystup` ve spisu. Složku `ProClaude` pak zkopírujte zpět na flash disk.
2. Na počítači s Koncipientem zasuňte flash disk a klikněte na **Načíst od Clauda z flash disku**. Program najde nové výstupy a rovnou otevře okno pro vrácení jmen. (Jinak jde soubor otevřít ručně: **Vrátit jména do výstupu od Clauda…**, pak **Otevřít dokument od Clauda…**.)
3. Program dosadí skutečná jména ve správném pádě. V okně barevně označí, co vrátil (zeleně), co stojí za kontrolu (oranžově) a co musíte doplnit sama (červeně). Text vpravo jde rovnou opravit.
4. **Uložit jako Word…** a dokument dál upravujte ve Wordu jako obvykle.

## Záznam o exportech (GDPR)

Program si u každého spisu sám zapisuje, kdy a co odešlo Claudovi nebo na flash disk: které dokumenty, kolik stran, jestli byly zkontrolované a kolik údajů se nahradilo. Záznam neobsahuje žádné osobní údaje. Tlačítko **Záznam o exportech…** ho ukáže a uloží jako Word, třeba jako doklad pro klienta nebo pro kontrolu ochrany osobních údajů.

## Hledání ve spisu

**Ctrl+F** otevře záložku **Hledání**. Napište slovo nebo frázi a stiskněte Enter. Nezáleží na diakritice a začátek slova najde i ostatní tvary („smlouv“ najde smlouvu i smlouvě). Enter nebo dvojklik na nález otevře stranu a slovo v ní označí.

## PDF pro vlastní archiv

**PDF pro archiv…** uloží fotky spisu (nebo vybraných dokumentů) do jednoho PDF, ve kterém jde hledat a kopírovat text. Dokumenty jsou v něm záložky. PDF obsahuje skutečné údaje, proto ho ukládejte jen u sebe a nikdy ho nedávejte Claudovi.

## Doplnění spisu

Nové písemnosti přidáte do stejného spisu tlačítkem **Přidat fotky…** nebo **Přidat složku…**. Spusťte přepis, zkontrolujte nové strany v rychlé kontrole a při exportu zvolte **Jen nové**. Vymyšlená jména zůstávají stejná jako v předchozím exportu.

## Co nedělat

- Nekopírujte do Clauda text ze složky `Spisy` ani z okna Koncipientu bez zapnutého náhledu pro Clauda.
- Nedávejte na počítač s Claudem fotky ani složku `Spisy`.
- Neposílejte složku `Spisy` e-mailem ani ji nedávejte do cloudu (OneDrive, Google Disk).

## Záloha

Složku `Dokumenty\Koncipient\Spisy` občas zkopírujte na externí disk a ten uložte stejně bezpečně jako papírový spis. Jsou v ní fotky i skutečná jména.

## Když něco nefunguje

Napište Matějovi, co jste dělala a co program napsal. Pošlete text hlášky nebo číslo spisu a strany, **nikdy fotku ani text ze spisu**.
