"""Převede docs/navod-pro-advokatku.md do Wordu (docs/navod-pro-advokatku.docx).

Spusť po každé změně návodu:  python tools/navod_do_wordu.py
Potřebuje pandoc a python-docx (pip install python-docx).
"""
import subprocess
import tempfile
from pathlib import Path

from docx import Document
from docx.oxml.ns import qn
from docx.shared import Cm, Pt, RGBColor

KOREN = Path(__file__).resolve().parent.parent


def styl(styly, nazev, velikost=None, tucne=None, barva=None, pismo="Calibri", mezera_pred=None):
    s = styly[nazev]
    s.font.name = pismo
    rpr = s.element.get_or_add_rPr()
    rf = rpr.find(qn("w:rFonts"))
    if rf is None:
        rf = rpr.makeelement(qn("w:rFonts"), {})
        rpr.append(rf)
    for a in ("w:ascii", "w:hAnsi", "w:cs", "w:eastAsia"):
        rf.set(qn(a), pismo)
    for a in ("w:asciiTheme", "w:hAnsiTheme", "w:cstheme", "w:eastAsiaTheme"):
        rf.attrib.pop(qn(a), None)
    if velikost:
        s.font.size = Pt(velikost)
    if tucne is not None:
        s.font.bold = tucne
    if barva:
        s.font.color.rgb = RGBColor.from_string(barva)
    if mezera_pred is not None:
        s.paragraph_format.space_before = Pt(mezera_pred)


def main():
    with tempfile.TemporaryDirectory() as tmp:
        vzor = Path(tmp) / "vzor.docx"
        vzor.write_bytes(subprocess.run(["pandoc", "--print-default-data-file", "reference.docx"],
                                        check=True, capture_output=True).stdout)
        d = Document(vzor)
        styly = {s.name: s for s in d.styles}
        for n in ("Normal", "Body Text", "First Paragraph", "Compact"):
            styl(styly, n, 11)
        styl(styly, "Heading 1", 20, True, "1F3864", mezera_pred=0)
        styl(styly, "Heading 2", 14, True, "2E5597", mezera_pred=14)
        styl(styly, "Verbatim Char", 10, pismo="Consolas")
        for sekce in d.sections:
            sekce.left_margin = sekce.right_margin = Cm(2.2)
            sekce.top_margin = sekce.bottom_margin = Cm(2)
        d.save(vzor)
        subprocess.run(["pandoc", str(KOREN / "docs/navod-pro-advokatku.md"), "-o",
                        str(KOREN / "docs/navod-pro-advokatku.docx"), f"--reference-doc={vzor}",
                        "-M", "lang=cs-CZ"], check=True)
    print("docs/navod-pro-advokatku.docx")


if __name__ == "__main__":
    main()
