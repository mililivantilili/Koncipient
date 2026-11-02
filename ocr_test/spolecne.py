"""Společné funkce testu OCR: cesty k datům a úprava fotky.

Data (fotky, přepisy, reference) leží vždy mimo repozitář ve složce,
kterou skripty dostanou parametrem --data. Struktura složky:

    <data>/fotky/*.jpg            vstupní fotky stran
    <data>/vystup/<motor>/*.txt   přepisy podle motoru
    <data>/vystup/upravene/*.png  fotky po úpravě
    <data>/reference/*.txt        ručně přepsaný správný text (volitelné)
"""

import argparse
from pathlib import Path

import cv2
import numpy as np

PRIPONY_FOTEK = {".jpg", ".jpeg", ".png", ".tif", ".tiff"}


def argumenty(popis: str) -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description=popis)
    p.add_argument("--data", required=True, type=Path,
                   help="složka s daty testu, musí být mimo repozitář")
    return p


def zkontroluj_data(data: Path) -> Path:
    """Odmítne datovou složku uvnitř repozitáře, aby se fotky nedostaly na GitHub."""
    data = data.resolve()
    repo = Path(__file__).resolve().parent.parent
    if data == repo or repo in data.parents:
        raise SystemExit(f"Složka s daty {data} je uvnitř repozitáře. Dej ji jinam.")
    if not (data / "fotky").is_dir():
        raise SystemExit(f"Chybí složka {data / 'fotky'} s fotkami.")
    return data


def fotky(data: Path) -> list[Path]:
    return sorted(f for f in (data / "fotky").iterdir() if f.suffix.lower() in PRIPONY_FOTEK)


def vystup(data: Path, nazev: str) -> Path:
    d = data / "vystup" / nazev
    d.mkdir(parents=True, exist_ok=True)
    return d


def vyrovnat_osvetleni(seda: np.ndarray) -> np.ndarray:
    """Odstraní stín a nerovnoměrné světlo: odhadne pozadí a odečte ho.

    Na testovacích fotkách snížilo chyby Tesseractu z 16–25 % na 1–3 % znaků.
    """
    pozadi = cv2.medianBlur(cv2.dilate(seda, np.ones((15, 15), np.uint8)), 51)
    rozdil = 255 - cv2.absdiff(seda, pozadi)
    return cv2.normalize(rozdil, None, 0, 255, cv2.NORM_MINMAX)


def upravena_fotka(data: Path, fotka: Path) -> Path:
    """Vrátí cestu k upravené fotce, při prvním volání ji vytvoří."""
    cil = vystup(data, "upravene") / (fotka.stem + ".png")
    if not cil.exists():
        seda = cv2.cvtColor(cv2.imread(str(fotka)), cv2.COLOR_BGR2GRAY)
        cv2.imwrite(str(cil), vyrovnat_osvetleni(seda))
    return cil
