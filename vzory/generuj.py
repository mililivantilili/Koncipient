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
    "/usr/share/fonts/truetype/dejavu/DejaVuSerif.ttf",
    "C:/Windows/Fonts/arial.ttf",
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


def pismo(velikost):
    for p in PISMA:
        if Path(p).exists():
            return ImageFont.truetype(p, velikost)
    raise SystemExit("Nenašlo se písmo s češtinou (Liberation Serif, Times New Roman nebo DejaVu).")


def vykresli(text: str) -> Image.Image:
    """Stránka A4 při 200 dpi, okraje 2,5 cm, písmo 12 bodů."""
    sirka, vyska, okraj = 1654, 2339, 200
    stranka = Image.new("RGB", (sirka, vyska), "white")
    kresba = ImageDraw.Draw(stranka)
    f = pismo(33)
    y = okraj
    znaku = 78
    for odstavec in text.split("\n"):
        radky = textwrap.wrap(odstavec, znaku) or [""]
        for r in radky:
            kresba.text((okraj, y), r, font=f, fill=(20, 20, 20))
            y += 46
        if y > vyska - okraj:
            break
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
    p.add_argument("--bez-efektu", action="store_true", help="čistý obrázek bez napodobení fotky")
    a = p.parse_args()
    (a.vystup / "fotky").mkdir(parents=True, exist_ok=True)
    (a.vystup / "reference").mkdir(parents=True, exist_ok=True)
    for i, soubor in enumerate(sorted((Path(__file__).parent / "dokumenty").glob("*.txt"))):
        text, udaje = rozloz(soubor.read_text(encoding="utf-8"))
        obrazek = vykresli(text)
        if not a.bez_efektu:
            obrazek = jako_fotka(obrazek, random.Random(i))
        obrazek.save(a.vystup / "fotky" / f"{soubor.stem}.jpg", quality=82)
        (a.vystup / "reference" / f"{soubor.stem}.json").write_text(
            json.dumps({"text": text, "udaje": udaje}, ensure_ascii=False, indent=1), encoding="utf-8")
        print(f"{soubor.stem}: {len(udaje)} údajů")


if __name__ == "__main__":
    main()
