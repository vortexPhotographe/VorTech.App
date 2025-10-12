// Views/SettingsView.xaml.cs
using System.Windows;
using System.Windows.Controls;
using VorTech.App.Models;
using VorTech.App.Services;

namespace VorTech.App.Views
{
    public partial class SettingsView : UserControl
    {
        private AppConfig _config;

        public SettingsView()
        {
            InitializeComponent();
            _config = ConfigService.Load();   // ← ok, méthode fournie ci-dessus
            DataContext = _config;
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            // Si tu as un DataGrid nommé GridPay en édition : sécurise les commits
            if (FindName("GridPay") is DataGrid dg)
            {
                if (dg.CommitEdit(DataGridEditingUnit.Cell, true))
                    dg.CommitEdit(DataGridEditingUnit.Row, true);
            }

            ConfigService.Save(_config);
            MessageBox.Show("Réglages enregistrés.", "VorTech.App",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
