using System.Windows;
using VorTech.App.Services;
using System.IO;

namespace VorTech.App
{
    public partial class App : Application
    {
        private void App_Startup(object sender, StartupEventArgs e)
        {
            Db.Init(); // crée/maintiens le schéma SQLite
			protected override void OnStartup(StartupEventArgs e)
			{
				base.OnStartup(e);

				// S'assure que /Config existe (clé USB / exe portable)
				Directory.CreateDirectory(Paths.ConfigDir);

				// Charge (ou crée) la config au démarrage
				_ = ConfigService.Load();
			}
        }
    }
}
