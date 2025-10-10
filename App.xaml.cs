using System;
using System.Windows;

namespace VorTech.App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            try
            {
                Db.EnsureDatabase(out var _);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur init BDD: " + ex.Message, "VorTech.App", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
