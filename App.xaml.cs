using System;
using System.Windows;
using System.Windows.Threading;

namespace VorTech.App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Affiche toute exception non gérée (XAML/Binding/etc.)
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            base.OnStartup(e);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                e.Exception.ToString(),
                "Erreur non gérée (Dispatcher)",
                MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true; // évite la fermeture brutale; tu peux mettre false si tu veux laisser crasher
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            MessageBox.Show(
                ex?.ToString() ?? "(exception inconnue)",
                "Erreur non gérée (AppDomain)",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
