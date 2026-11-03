using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Koncipient.Jadro;
using Koncipient.Jadro.Anonymizace;
using Microsoft.Win32;

namespace Koncipient.App;

/// <summary>
/// Zpětné dosazení: vlevo dokument od Clauda, vpravo stejný dokument se skutečnými
/// jmény. Barvou je vyznačeno, co program vrátil a co musí advokátka zkontrolovat
/// nebo doplnit. Pravou stranu jde upravit a uložit jako Word.
/// </summary>
public sealed class ZpetneOkno : Window
{
    private static readonly Brush Vraceno = new SolidColorBrush(Color.FromRgb(0xDC, 0xED, 0xC8));
    private static readonly Brush Zkontrolovat = new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0xB2));
    private static readonly Brush Doplnit = new SolidColorBrush(Color.FromRgb(0xFF, 0xCD, 0xD2));

    private readonly Spis _spis;
    private readonly Klic _klic;
    private readonly FlowDocumentScrollViewer _puvodni = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly RichTextBox _vysledek = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, AcceptsTab = true };
    private readonly TextBlock _stav = new() { Margin = new Thickness(8, 4, 8, 4), TextWrapping = TextWrapping.Wrap };
    private string? _soubor;

    public ZpetneOkno(Spis spis, Klic klic)
    {
        _spis = spis;
        _klic = klic;
        Title = $"Koncipient: vrácení jmen ({spis.Id})";
        Width = 1300;
        Height = 800;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13;

        var otevrit = new Button { Content = "Otevřít dokument od Clauda…", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(4) };
        var ulozit = new Button { Content = "Uložit jako Word…", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(4), IsEnabled = false };
        otevrit.Click += (_, _) => Otevri();
        ulozit.Click += (_, _) => Uloz();
        var legenda = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        legenda.Inlines.Add(new Run(" vráceno ") { Background = Vraceno });
        legenda.Inlines.Add(new Run("  "));
        legenda.Inlines.Add(new Run(" zkontrolovat ") { Background = Zkontrolovat });
        legenda.Inlines.Add(new Run("  "));
        legenda.Inlines.Add(new Run(" doplnit ručně ") { Background = Doplnit });
        legenda.Inlines.Add(new Run("   najetím myši se ukáže proč, pravou stranu jde přepisovat") { Foreground = Brushes.Gray });

        var lista = new WrapPanel();
        lista.Children.Add(otevrit);
        lista.Children.Add(ulozit);
        lista.Children.Add(legenda);

        var mrizka = new Grid();
        mrizka.ColumnDefinitions.Add(new ColumnDefinition());
        mrizka.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
        mrizka.ColumnDefinitions.Add(new ColumnDefinition());
        mrizka.Children.Add(Obal("Od Clauda (s vymyšlenými jmény)", _puvodni, 0));
        var delic = new GridSplitter { HorizontalAlignment = HorizontalAlignment.Stretch };
        Grid.SetColumn(delic, 1);
        mrizka.Children.Add(delic);
        mrizka.Children.Add(Obal("Se skutečnými jmény (upravitelné)", _vysledek, 2));

        var panel = new DockPanel();
        DockPanel.SetDock(lista, Dock.Top);
        DockPanel.SetDock(_stav, Dock.Bottom);
        panel.Children.Add(lista);
        panel.Children.Add(_stav);
        panel.Children.Add(mrizka);
        Content = panel;

        _stav.Text = "Otevři dokument, který Claude uložil do složky vystup ve spisu (přenesený zpět na tento počítač).";
        _ulozit = ulozit;
    }

    private readonly Button _ulozit;

    private static UIElement Obal(string nadpis, Control obsah, int sloupec)
    {
        var d = new DockPanel();
        var t = new TextBlock { Text = nadpis, FontWeight = FontWeights.SemiBold, Margin = new Thickness(6, 4, 6, 4) };
        DockPanel.SetDock(t, Dock.Top);
        d.Children.Add(t);
        d.Children.Add(obsah);
        Grid.SetColumn(d, sloupec);
        return d;
    }

    private void Otevri()
    {
        var vychozi = Path.Combine(Export.VychoziSlozka, _spis.Id, "vystup");
        var dialog = new OpenFileDialog
        {
            Title = "Dokument od Clauda",
            Filter = "Text a Markdown|*.md;*.txt|Všechny soubory|*.*",
            InitialDirectory = Directory.Exists(vychozi) ? vychozi : Export.VychoziSlozka,
        };
        if (dialog.ShowDialog(this) != true) return;
        _soubor = dialog.FileName;
        var text = File.ReadAllText(_soubor).Replace("\r\n", "\n");

        var puvodni = new FlowDocument { FontFamily = new FontFamily("Segoe UI"), FontSize = 14, PagePadding = new Thickness(14) };
        foreach (var r in text.Split('\n')) puvodni.Blocks.Add(new Paragraph(new Run(r)) { Margin = new Thickness(0, 0, 0, 4) });
        _puvodni.Document = puvodni;

        var useky = ZpetneDosazeni.Vrat(text, _klic);
        var vysledek = new FlowDocument { FontFamily = new FontFamily("Segoe UI"), FontSize = 14, PagePadding = new Thickness(14) };
        var odstavec = new Paragraph { Margin = new Thickness(0, 0, 0, 4) };
        foreach (var u in useky)
        {
            var casti = u.Text.Split('\n');
            for (int i = 0; i < casti.Length; i++)
            {
                if (i > 0)
                {
                    vysledek.Blocks.Add(odstavec);
                    odstavec = new Paragraph { Margin = new Thickness(0, 0, 0, 4) };
                }
                if (casti[i].Length == 0) continue;
                var beh = new Run(casti[i]);
                if (u.Druh != DruhUseku.Text)
                {
                    beh.Background = u.Druh switch { DruhUseku.Vraceno => Vraceno, DruhUseku.Zkontrolovat => Zkontrolovat, _ => Doplnit };
                    beh.ToolTip = u.Poznamka;
                }
                odstavec.Inlines.Add(beh);
            }
        }
        vysledek.Blocks.Add(odstavec);
        _vysledek.Document = vysledek;
        _ulozit.IsEnabled = true;

        int zk = useky.Count(u => u.Druh == DruhUseku.Zkontrolovat), dop = useky.Count(u => u.Druh == DruhUseku.Doplnit);
        _stav.Text = $"Vráceno {useky.Count(u => u.Druh == DruhUseku.Vraceno)} míst, ke kontrole {zk}, k doplnění {dop}. " +
                     "Projdi barevná místa a uprav text vpravo, pak ulož jako Word.";
    }

    private void Uloz()
    {
        var text = string.Join("\n", _vysledek.Document.Blocks.OfType<Paragraph>()
            .Select(p => new TextRange(p.ContentStart, p.ContentEnd).Text));
        if (text.Contains("[ODSTRANĚNO") || text.Contains("[NOVÁ") || text.Contains("[NOVÉ") || text.Contains("[DOPLNIT")
            || System.Text.RegularExpressions.Regex.IsMatch(text, @"\[[A-Z]+_\d{3}\]"))
        {
            if (MessageBox.Show(this, "V textu ještě zůstaly značky k doplnění ([ODSTRANĚNO…], [NOVÁ OSOBA…] apod.). Přesto uložit?",
                    "Koncipient", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
        }
        var slozka = Path.Combine(_spis.Slozka, "vystupy");
        Directory.CreateDirectory(slozka);
        var dialog = new SaveFileDialog
        {
            Title = "Uložit jako Word",
            Filter = "Dokument Word|*.docx",
            InitialDirectory = slozka,
            FileName = Path.GetFileNameWithoutExtension(_soubor ?? "dokument") + ".docx",
        };
        if (dialog.ShowDialog(this) != true) return;
        Word.Uloz(dialog.FileName, text);
        _stav.Text = $"Uloženo do {dialog.FileName}.";
    }
}
