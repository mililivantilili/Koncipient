"""Z vymyšlených dokumentů udělá fotky stran a referenční údaje pro měření anonymizace.

Dokumenty v dokumenty/*.txt mají citlivé údaje vyznačené jako [[typ|hodnota]].
Typy: osoba, firma, adresa, nemovitost, znacka, ico, rc, narozeni, ucet, telefon, email, jiny.

Výstup (mimo repozitář):
    <vystup>/fotky/<dokument>.jpg       stránka jako fotka z telefonu (natočení, stín, rozmazání)
    <vystup>/reference/<dokument>.json  čistý text a seznam vyznačených údajů

Použití:
    python generuj.py --vystup D:/mereni-anonymizace [--bez-efektu]
"""

import argparse
import json
import random
import re
import textwrap
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ZNACKA = re.compile(r"\[\[(\w+)\|([^\]]+)\]\]")
PISMA = [
    "/usr/share/fonts/truetype/liberation/LiberationSerif-Regular.ttf",
    "C:/Windows/Fonts/times.ttf",
    "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
    "C:/Windows/Fonts/arial.ttf",
    "/usr/share/fonts/truetype/dejavu/DejaVuSerif.ttf",
    "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
    "C:/Windows/Fonts/calibri.ttf",
    "/usr/share/fonts/truetype/freefont/FreeSerif.ttf",
]


def rozloz(zdroj: str):
    """Vrátí čistý text a seznam údajů (typ, hodnota, poloha od–do v čistém textu)."""
    udaje, casti, pozice, posledni = [], [], 0, 0
    for m in ZNACKA.finditer(zdroj):
        pred = zdroj[posledni:m.start()]
        casti.append(pred)
        pozice += len(pred)
        hodnota = m.group(2)
        udaje.append({"typ": m.group(1), "hodnota": hodnota, "od": pozice, "do": pozice + len(hodnota)})
        casti.append(hodnota)
        pozice += len(hodnota)
        posledni = m.end()
    casti.append(zdroj[posledni:])
    return "".join(casti), udaje


def pismo(velikost, nahoda=None):
    dostupna = [p for p in PISMA if Path(p).exists()]
    if not dostupna:
        raise SystemExit("Nenašlo se písmo s češtinou (Liberation Serif, Times New Roman nebo DejaVu).")
    return ImageFont.truetype(dostupna[0] if nahoda is None else nahoda.choice(dostupna), velikost)


def rozdel_na_strany(text: str, nahoda=None):
    """Zalomí text do řádků a stran A4. Vrátí seznam stran, každá je (text strany, poloha začátku v textu, písmo, rozteč)."""
    velikost = 33 if nahoda is None else nahoda.choice([29, 31, 33, 35])
    f = pismo(velikost, nahoda)
    rozteč = int(velikost * 1.4)
    radku_na_stranu = (2339 - 2 * 200) // rozteč
    znaku = int(78 * 33 / velikost)
    strany, aktualni, zacatek, pozice = [], [], 0, 0
    for odstavec in text.split("\n"):
        radky = textwrap.wrap(odstavec, znaku, break_long_words=False) or [""]
        hledani = pozice
        for r in radky:
            if len(aktualni) >= radku_na_stranu:
                strany.append(aktualni)
                aktualni = []
            od = text.find(r, hledani) if r else hledani
            aktualni.append((r, od))
            hledani = od + len(r)
        pozice += len(odstavec) + 1
    if aktualni:
        strany.append(aktualni)
    return [(s, f, rozteč) for s in strany]


def vykresli(radky, f, rozteč) -> Image.Image:
    """Stránka A4 při 200 dpi, okraje 2,5 cm."""
    sirka, vyska, okraj = 1654, 2339, 200
    stranka = Image.new("RGB", (sirka, vyska), "white")
    kresba = ImageDraw.Draw(stranka)
    y = okraj
    for r, _ in radky:
        kresba.text((okraj, y), r, font=f, fill=(20, 20, 20))
        y += rozteč
    return stranka


def jako_fotka(stranka: Image.Image, nahoda: random.Random) -> Image.Image:
    """Napodobí fotku z telefonu: pozadí stolu, natočení, perspektiva, stín, rozmazání, JPEG."""
    w, h = stranka.size
    pozadi = Image.new("RGB", (int(w * 1.25), int(h * 1.18)), (150 + nahoda.randint(-20, 20), 120, 90))
    stranka = stranka.rotate(nahoda.uniform(-4, 4), expand=True, fillcolor=(150, 120, 90), resample=Image.BICUBIC)
    pozadi.paste(stranka, ((pozadi.width - stranka.width) // 2, (pozadi.height - stranka.height) // 2))
    # perspektiva: horní okraj trochu užší
    d = nahoda.randint(20, 80)
    pw, ph = pozadi.size
    koeficienty = _perspektiva([(d, 0), (pw - d, 0), (pw, ph), (0, ph)], [(0, 0), (pw, 0), (pw, ph), (0, ph)])
    pozadi = pozadi.transform(pozadi.size, Image.PERSPECTIVE, koeficienty, Image.BICUBIC, fillcolor=(150, 120, 90))
    # stín přes část stránky
    stin = Image.new("L", pozadi.size, 0)
    ImageDraw.Draw(stin).ellipse([nahoda.randint(-pw // 2, pw // 2), nahoda.randint(-ph // 2, ph // 2),
                                  pw + nahoda.randint(0, pw // 2), ph // 2 + nahoda.randint(0, ph)], fill=70)
    stin = stin.filter(ImageFilter.GaussianBlur(150))
    pozadi = Image.composite(Image.new("RGB", pozadi.size, (40, 40, 40)), pozadi, stin)
    pozadi = pozadi.filter(ImageFilter.GaussianBlur(nahoda.uniform(0.6, 1.4)))
    return pozadi.resize((pw * 3 // 4, ph * 3 // 4), Image.LANCZOS)


def _perspektiva(cil, zdroj):
    import numpy as np
    a = []
    for (x, y), (u, v) in zip(cil, zdroj):
        a.append([x, y, 1, 0, 0, 0, -u * x, -u * y])
        a.append([0, 0, 0, x, y, 1, -v * x, -v * y])
    b = np.array([c for p in zdroj for c in p], dtype=float)
    return list(np.linalg.solve(np.array(a, dtype=float), b))


def main():
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("--vystup", required=True, type=Path)
    p.add_argument("--dokumenty", type=Path, default=Path(__file__).parent / "dokumenty",
                   help="složka s dokumenty *.txt (výchozí vzory/dokumenty)")
    p.add_argument("--bez-efektu", action="store_true", help="čistý obrázek bez napodobení fotky")
    p.add_argument("--ruzne-pismo", action="store_true", help="náhodné písmo a velikost pro každý dokument")
    a = p.parse_args()
    (a.vystup / "fotky").mkdir(parents=True, exist_ok=True)
    (a.vystup / "reference").mkdir(parents=True, exist_ok=True)
    for i, soubor in enumerate(sorted(a.dokumenty.glob("*.txt"))):
        text, udaje = rozloz(soubor.read_text(encoding="utf-8"))
        nahoda = random.Random(i)
        strany = rozdel_na_strany(text, nahoda if a.ruzne_pismo else None)
        for c, (radky, f, roztec) in enumerate(strany, 1):
            nazev = soubor.stem if len(strany) == 1 else f"{soubor.stem}_s{c:02d}"
            obrazek = vykresli(radky, f, roztec)
            if not a.bez_efektu:
                obrazek = jako_fotka(obrazek, random.Random(i * 100 + c))
            obrazek.save(a.vystup / "fotky" / f"{nazev}.jpg", quality=82)
            # text strany a údaje, které na ní začínají, s polohami vůči textu strany
            casti, mapa = [], []
            for r, od in radky:
                mapa.append((od, sum(len(x) + 1 for x in casti)))
                casti.append(r)
            text_strany = "\n".join(casti)
            def prevod(poz):
                for od, kam in reversed(mapa):
                    if poz >= od:
                        return kam + (poz - od)
                return 0
            od_strany, do_strany = radky[0][1], radky[-1][1] + len(radky[-1][0])
            udaje_strany = []
            for u in udaje:
                if od_strany <= u["od"] < do_strany:
                    zac = prevod(u["od"])
                    hodnota_na_strane = text_strany[zac:zac + len(u["hodnota"])]
                    # údaj zalomený na další řádek: v textu strany je místo mezery konec řádku
                    if hodnota_na_strane.replace("\n", " ") != u["hodnota"]:
                        continue
                    udaje_strany.append({"typ": u["typ"], "hodnota": hodnota_na_strane, "od": zac, "do": zac + len(u["hodnota"])})
            (a.vystup / "reference" / f"{nazev}.json").write_text(
                json.dumps({"text": text_strany, "udaje": udaje_strany}, ensure_ascii=False, indent=1), encoding="utf-8")
        print(f"{soubor.stem}: {len(udaje)} údajů, {len(strany)} stran")


if __name__ == "__main__":
    main()
