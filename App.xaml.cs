using System.Windows;
using System.Windows.Threading;
using VorTech.App.Services;

namespace VorTech.App
{
    public partial class App : Application
    {
        private void App_Startup(object sender, StartupEventArgs e)
        {
            // Initialisation centrale
            Db.Init();
            _ = ConfigService.Load();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // Catch global des exceptions UI -> on empêche la fermeture brutale
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void App_DispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                "Erreur non gérée : " + e.Exception.Message,
                "Erreur",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            e.Handled = true; // évite que l'appli se ferme
        }
    }
}
