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

## 2. Založení spisu

1. **Nový spis** a zadejte název, například spisovou značku. Název zůstane jen u vás.
2. **Přidat fotky…** (vybrané fotky) nebo **Přidat složku…** (celá složka, podsložky se stanou dokumenty).
3. Otevře se okno **Oddělovače dokumentů** s fotkami, které program vzal jako zakrytý objektiv. Pokud je mezi nimi skutečná stránka (třeba hodně tmavá), zaškrtněte **Je to stránka, vrátit**. Pak klikněte na **Hotovo**.

Pořadí stran i dokumenty jdou upravit později: klikněte pravým tlačítkem na stranu v seznamu vlevo.

## 3. Přepis (klidně přes noc)

Klikněte na **Spustit přepis**. Program fotky narovná, převede na text a najde v nich údaje. Jedna stránka trvá desítky sekund, celý spis klidně hodiny. Nechte počítač zapnutý v síti, sám neusne. Přepis jde **Zastavit** a později pokračovat tam, kde skončil.

Když je hotovo, uvidíte v titulku okna „přepis dokončen“ a čas. Pokud se některá strana nepřepsala, program napíše, která. Stačí přepis spustit znovu.

## 4. Kontrola: hlavní práce

Automatika nikdy nenajde všechno, proto nic neodejde bez vaší kontroly.

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

1. **Exportovat pro Clauda…** Program ještě jednou zkontroluje celý text. Pokud v něm zůstalo skutečné jméno nebo číslo, export zastaví a ukáže, co zbývá. Doplňte to podle kroku 4.
2. Vznikne složka `Dokumenty\Koncipient\ProClaude\S-…`. Obsahuje jen anonymizovaný text (jeden soubor na dokument) a pokyny pro Clauda. Fotky ani skutečná jména v ní nejsou.
3. Tuto složku přeneste na počítač s Claudem a pracujte s ní jako obvykle. Na strany se Claude odkazuje čísly ze seznamu vlevo.

## 6. Návrat: skutečná jména do výsledku

1. Požádejte Clauda, ať výsledek (podání, shrnutí…) uloží jako soubor `.md` do složky spisu.
2. Soubor přeneste zpět na počítač s Koncipientem.
3. **Vrátit jména do výstupu od Clauda…**, pak **Otevřít dokument od Clauda…**. Program dosadí skutečná jména ve správném pádě. V okně barevně označí, co vrátil (zeleně), co stojí za kontrolu (oranžově) a co musíte doplnit sama (červeně). Text vpravo jde rovnou opravit.
4. **Uložit jako Word…** a dokument dál upravujte ve Wordu jako obvykle.

## Co nedělat

- Nekopírujte do Clauda text ze složky `Spisy` ani z okna Koncipientu bez zapnutého náhledu pro Clauda.
- Nedávejte na počítač s Claudem fotky ani složku `Spisy`.
- Neposílejte složku `Spisy` e-mailem ani ji nedávejte do cloudu (OneDrive, Google Disk).

## Záloha

Složku `Dokumenty\Koncipient\Spisy` občas zkopírujte na externí disk a ten uložte stejně bezpečně jako papírový spis. Jsou v ní fotky i skutečná jména.

## Když něco nefunguje

Napište Matějovi, co jste dělala a co program napsal. Pošlete text hlášky nebo číslo spisu a strany, **nikdy fotku ani text ze spisu**.
