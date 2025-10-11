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

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
