using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using VorTech.App.Services;

namespace VorTech.App.Views
{
    public partial class ClientDevisWindow : Window
    {
        private readonly int _clientId;
        private readonly DevisService _service = new DevisService();

        public ClientDevisWindow(int clientId)
        {
            InitializeComponent();
            _clientId = clientId;
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                var devis = _service.GetByClient(_clientId);
                var items = devis.Select(d => new
                {
                    d.Id,
                    d.Numero,
                    d.Date,
                    Etat = d.Etat,   // <- même nom que la colonne
                    Total = d.Total,  // <- même nom que la colonne
                    PdfPath = string.IsNullOrWhiteSpace(d.Numero) ? "" : GetPdfPath(d.Numero),
                    HasPdf = !string.IsNullOrWhiteSpace(d.Numero) && File.Exists(GetPdfPath(d.Numero))
                }).ToList();

                Grid.ItemsSource = items;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur chargement devis: " + ex.Message);
            }
        }

        private static string GetPdfPath(string numero)
            => Path.Combine(Paths.DataDir, "Devis", $"{numero}.pdf");

        private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Grid.SelectedItem == null) return;
            dynamic row = Grid.SelectedItem;
            int id = row.Id;

            var mw = (MainWindow)Application.Current.MainWindow;
            mw.MainContent.Content = new DevisView(id);
            Close();
        }

        private void OpenPdf_Click(object sender, RoutedEventArgs e)
        {
            if (Grid.SelectedItem == null) return;
            dynamic row = Grid.SelectedItem;
            string path = row.PdfPath;

            if (File.Exists(path))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show("PDF introuvable. Émet le devis pour générer le PDF.");
            }
        }
    }
}
