"""Přepíše fotky zvoleným OCR motorem.

Příklad:
    python ocr.py --data D:/ocr-data --motor tesseract
    python ocr.py --data D:/ocr-data --motor paddle

Motory:
    tesseract-surovy  Tesseract na původní fotce
    tesseract         Tesseract na fotce s vyrovnaným osvětlením
    paddle            PaddleOCR (PP-OCRv6, čeština), pomalý, nejlepší na čísla
    doctr             docTR, neumí českou diakritiku, jen pro srovnání
"""

import os
import time

from spolecne import argumenty, fotky, upravena_fotka, vystup, zkontroluj_data


def tesseract(cesta):
    import pytesseract
    return pytesseract.image_to_string(str(cesta), lang="ces", config="--psm 3")


def paddle_motor():
    os.environ.setdefault("PADDLE_PDX_MODEL_SOURCE", "huggingface")
    from paddleocr import PaddleOCR
    # enable_mkldnn=False obchází chybu oneDNN v paddlepaddle 3.3 na procesoru
    ocr = PaddleOCR(lang="cs", use_doc_orientation_classify=False,
                    use_doc_unwarping=False, use_textline_orientation=False,
                    enable_mkldnn=False)
    return lambda cesta: "\n".join(ocr.predict(str(cesta))[0]["rec_texts"])


def doctr_motor():
    from doctr.io import DocumentFile
    from doctr.models import ocr_predictor
    model = ocr_predictor(det_arch="db_resnet50", reco_arch="parseq", pretrained=True)

    def prepis(cesta):
        dok = model(DocumentFile.from_images(str(cesta)))
        return "\n".join(" ".join(w.value for w in radek.words)
                         for strana in dok.pages for blok in strana.blocks
                         for radek in blok.lines)
    return prepis


def main():
    p = argumenty(__doc__.splitlines()[0])
    p.add_argument("--motor", required=True,
                   choices=["tesseract-surovy", "tesseract", "paddle", "doctr"])
    a = p.parse_args()
    data = zkontroluj_data(a.data)

    if a.motor == "tesseract-surovy":
        prepis, upravit = tesseract, False
    elif a.motor == "tesseract":
        prepis, upravit = tesseract, True
    elif a.motor == "paddle":
        prepis, upravit = paddle_motor(), True
    else:
        prepis, upravit = doctr_motor(), True

    cil = vystup(data, a.motor)
    for f in fotky(data):
        start = time.time()
        vstup = upravena_fotka(data, f) if upravit else f
        text = prepis(vstup)
        (cil / (f.stem + ".txt")).write_text(text, encoding="utf-8")
        print(f"{f.name}: {len(text.split())} slov, {time.time() - start:.1f} s", flush=True)


if __name__ == "__main__":
    main()
