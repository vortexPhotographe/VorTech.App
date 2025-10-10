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
            _config = ConfigService.Load();
            DataContext = _config;
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            // Force la validation des cellules en édition (DataGrid)
            if (GridPay.CommitEdit(DataGridEditingUnit.Cell, true))
                GridPay.CommitEdit(DataGridEditingUnit.Row, true);

            ConfigService.Save(_config);
            MessageBox.Show("Réglages enregistrés.", "VorTech.App",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
