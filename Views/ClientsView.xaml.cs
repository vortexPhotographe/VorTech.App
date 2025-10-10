using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VorTech.App.Models;
using VorTech.App.Services;

namespace VorTech.App.Views
{
    public partial class ClientsView : UserControl
    {
        private readonly ClientService _svc = new ClientService();
        public ObservableCollection<Client> Items { get; } = new();

        public ClientsView()
        {
            InitializeComponent();
            Reload();
            GridClients.ItemsSource = Items;
        }

        private void Reload()
        {
            Items.Clear();
            foreach (var c in _svc.GetAll())
                Items.Add(c);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Ajoute les nouvelles lignes (Id = 0) et sauvegarde les modifiées
            foreach (var c in Items.ToList())
            {
                if (string.IsNullOrWhiteSpace(c.Name)) continue; // Nom obligatoire
                _svc.Save(c);
            }
            Reload();
            MessageBox.Show("Enregistré.", "Clients", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (GridClients.SelectedItem is Client c && c.Id > 0)
            {
                if (MessageBox.Show($"Supprimer « {c.Name} » ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _svc.Delete(c.Id);
                    Items.Remove(c);
                }
            }
        }

        private void Reload_Click(object sender, RoutedEventArgs e) => Reload();
    }
}
