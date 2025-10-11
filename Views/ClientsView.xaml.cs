using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using VorTech.App.Models;
using VorTech.App.Services;

namespace VorTech.App.Views
{
    public partial class ClientsView : UserControl, INotifyPropertyChanged
    {
        private readonly ClientService _svc = new ClientService();

        public ObservableCollection<Client> Clients { get; } = new ObservableCollection<Client>();

        private Client? _selectedClient;
        public Client? SelectedClient
        {
            get => _selectedClient;
            set { _selectedClient = value; OnPropertyChanged(); }
        }

        public ClientsView()
        {
            InitializeComponent();
            DataContext = this;
            Reload();
        }

        private void Reload()
        {
            Clients.Clear();
            foreach (var c in _svc.GetAll())
                Clients.Add(c);

            SelectedClient = Clients.Count > 0 ? Clients[0] : null;
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            SelectedClient = new Client();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedClient == null) return;

            _svc.Save(SelectedClient);

            var keepId = SelectedClient.Id;
            Reload();
            SelectedClient = Clients.FirstOrDefault(x => x.Id == keepId) ?? Clients.FirstOrDefault();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedClient == null || SelectedClient.Id <= 0) return;

            var res = MessageBox.Show("Supprimer ce client ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes) return;

            _svc.Delete(SelectedClient.Id);
            Reload();
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            Reload();
        }

        // Raccourcis (placeholders)
        private void Shortcut_NewDevis_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Nouveau devis (TODO liaison)", "Raccourcis");
        }

        private void Shortcut_NewFacture_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Nouvelle facture (TODO liaison)", "Raccourcis");
        }

        private void Shortcut_DevisClient_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedClient == null) { MessageBox.Show("Aucun client selectionne."); return; }
            MessageBox.Show($"Voir devis du client Id={SelectedClient.Id} (TODO).", "Raccourcis");
        }

        private void Shortcut_FacturesClient_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedClient == null) { MessageBox.Show("Aucun client selectionne."); return; }
            MessageBox.Show($"Voir factures du client Id={SelectedClient.Id} (TODO).", "Raccourcis");
        }

        private void Shortcut_ParcRadios_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Parc radios (TODO liaison)", "Raccourcis");
        }

        private void Shortcut_CartesSim_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Cartes SIM (TODO liaison)", "Raccourcis");
        }

        private void Shortcut_FreqUhf_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Frequences UHF (TODO liaison)", "Raccourcis");
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
