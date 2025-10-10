using System.Windows;

namespace VorTech.App
{
    public partial class App : Application
    {
        private void App_Startup(object sender, StartupEventArgs e)
        {
            Db.Init(); // crée/maintiens le schéma SQLite
        }
    }
}
