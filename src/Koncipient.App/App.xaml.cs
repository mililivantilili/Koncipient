using System.Windows;
using System.Windows.Threading;

namespace Koncipient.App;

public partial class App : Application
{
    private void NeosetrenaChyba(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "Koncipient: chyba", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
