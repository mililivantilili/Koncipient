using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Koncipient.Jadro;

/// <summary>
/// Uloží text jako dokument Word (.docx). Rozumí základnímu Markdownu, který píše
/// Claude: nadpisy #, odrážky -, číslované seznamy a tučné **písmo**.
/// </summary>
public static class Word
{
    private static readonly Regex Tucne = new(@"\*\*(.+?)\*\*");

    public static void Uloz(string cesta, string text)
    {
        using var dokument = WordprocessingDocument.Create(cesta, WordprocessingDocumentType.Document);
        var hlavni = dokument.AddMainDocumentPart();
        var telo = new Body();
        hlavni.Document = new Document(telo);

        foreach (var puvodni in text.Replace("\r\n", "\n").Split('\n'))
        {
            var radek = puvodni.TrimEnd();
            int uroven = 0;
            while (uroven < radek.Length && radek[uroven] == '#') uroven++;
            if (uroven > 0 && uroven < radek.Length && radek[uroven] == ' ') radek = radek[(uroven + 1)..];
            else uroven = 0;

            bool odrazka = radek.StartsWith("- ") || radek.StartsWith("* ");
            if (odrazka) radek = "•\t" + radek[2..];

            var odstavec = new Paragraph(new ParagraphProperties(
                new SpacingBetweenLines { After = uroven > 0 ? "120" : "80", Before = uroven > 0 ? "240" : "0" },
                odrazka ? new Indentation { Left = "360", Hanging = "360" } : new Indentation()));

            int pozice = 0;
            foreach (Match m in Tucne.Matches(radek))
            {
                if (m.Index > pozice) odstavec.Append(Beh(radek[pozice..m.Index], tucne: uroven > 0, uroven));
                odstavec.Append(Beh(m.Groups[1].Value, tucne: true, uroven));
                pozice = m.Index + m.Length;
            }
            if (pozice < radek.Length) odstavec.Append(Beh(radek[pozice..], tucne: uroven > 0, uroven));
            telo.Append(odstavec);
        }
        telo.Append(new SectionProperties(new PageMargin { Top = 1418, Bottom = 1418, Left = 1418, Right = 1418 }));
    }

    private static Run Beh(string text, bool tucne, int uroven)
    {
        var velikost = uroven switch { 1 => "32", 2 => "28", 3 => "26", _ => "24" }; // v půlbodech
        var vlastnosti = new RunProperties(
            new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", ComplexScript = "Times New Roman" },
            new FontSize { Val = velikost });
        if (tucne) vlastnosti.Append(new Bold());
        return new Run(vlastnosti, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }
}
