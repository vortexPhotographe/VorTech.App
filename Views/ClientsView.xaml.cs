using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VorTech.App.Models;
using VorTech.App.Services;
using VorTech.App.Views;

namespace VorTech.App.Views
{
    public partial class ClientsView : UserControl, INotifyPropertyChanged
    {
        private readonly ClientService _svc = new ClientService();
        private readonly DevisService _devisService = new DevisService();
        private readonly ClientService _clientService = new ClientService();

        public ObservableCollection<Client> Clients { get; } = new ObservableCollection<Client>();

        private Client? _selectedClient;
        public Client? SelectedClient
        {
            get => _selectedClient;
            set
            {
                if (_selectedClient == value) return;
                _selectedClient = value;
                OnPropertyChanged();
                LoadEmailLogsForSelectedClient();   // <- rafraîchit l’historique à chaque changement
            }
        }

        public ClientsView()
        {
            InitializeComponent();
            DataContext = this;
            Reload();
            LoadEmailLogsForSelectedClient();
        }

        private void Reload()
        {
            Clients.Clear();
            foreach (var c in _svc.GetAll())
                Clients.Add(c);

            SelectedClient = Clients.Count > 0 ? Clients[0] : null;
            LoadEmailLogsForSelectedClient();
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            SelectedClient = new Client();
            LoadEmailLogsForSelectedClient();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedClient == null) return;

            _svc.Save(SelectedClient);

            var keepId = SelectedClient.Id;
            Reload();
            SelectedClient = Clients.FirstOrDefault(x => x.Id == keepId) ?? Clients.FirstOrDefault();
            LoadEmailLogsForSelectedClient();
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
            LoadEmailLogsForSelectedClient();
            Reload();
        }

        // Raccourcis (placeholders)
        private void Shortcut_NewFacture_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Nouvelle Facture (TODO liaison)", "Raccourcis");
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

        private void Shortcut_NewDevis_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedClient == null)
            {
                MessageBox.Show("Sélectionne d'abord un client.");
                return;
            }

            // 1) Créer un brouillon pré-rempli avec snapshot client
            var devisId = _devisService.CreateDraft(SelectedClient.Id, SelectedClient);

            // 2) Ouvrir DevisView et charger CE devis
            var mw = (MainWindow)Application.Current.MainWindow;
            var view = new DevisView();        // une seule instance visible
            mw.MainContent.Content = view;     // on l'affiche
            view.OpenDevis(devisId);          // et on charge le devis dedans
        }

        private void Shortcut_DevisClient_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedClient == null)
            {
                MessageBox.Show("Sélectionne d'abord un client.");
                return;
            }

            var dlg = new ClientDevisWindow(SelectedClient.Id);
            dlg.Owner = Application.Current.MainWindow;
            dlg.ShowDialog();
        }

        // ======== EMAILS envoyés ========
        private void LoadEmailLogsForSelectedClient()
        {
            try
            {
                if (SelectedClient == null || SelectedClient.Id <= 0)
                {
                    GridEmails.ItemsSource = null;
                    return;
                }

                // Cas 1 : par adresse destinataire
                var byTo = EmailService.GetLogsByToAddress(SelectedClient.Email ?? string.Empty);

                // Cas 2 : par devis du client (contexte "DEVIS:xxx")
                var devisIds = _devisService.GetByClient(SelectedClient.Id).Select(d => d.Id).ToList();
                var byContext = EmailService.GetLogsByContexts(
                    devisIds.Select(id => $"DEVIS:{id}").ToList()
                );

                // Fusion + tri (évite doublons)
                var all = byTo.Concat(byContext)
                      .GroupBy(x => x.Id)
                      .Select(g => g.First())
                      .OrderByDescending(x => x.SentAt)
                      .ToList();

                var display = all.Select(x => new
                {
                    x.SentAt,
                    x.Subject,
                    x.ToAddress,
                    x.Status,
                    Attachments = string.Join("; ",
                        (x.Attachments ?? string.Empty)
                            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(p => System.IO.Path.GetFileName(p.Trim()))
                        )
                }).ToList();
                GridEmails.ItemsSource = display;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur lors du chargement des emails: " + ex.Message);
            }
        }

        private void GridEmails_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Option simple : si la ligne a un PDF en pièce jointe, ouvrir le dossier Devis
            // (Tu pourras affiner plus tard vers .eml si tu les stockes)
            if (GridEmails.SelectedItem is EmailLog log)
            {
                try
                {
                    // S'il y a "DEVIS:ID" dans le contexte, on ouvre le dossier où sont les PDF
                    if (!string.IsNullOrEmpty(log.Context) && log.Context.StartsWith("DEVIS:"))
                    {
                        var folder = System.IO.Path.Combine(Paths.DataDir, "Devis");
                        if (Directory.Exists(folder))
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = folder, UseShellExecute = true });
                    }
                }
                catch { /* silencieux */ }
            }
        }
    }
}
