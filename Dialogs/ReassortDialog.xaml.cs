using System;
using System.Globalization;
using System.Windows;

namespace VorTech.App.Dialogs
{
    public partial class ReassortDialog : Window
    {
        public string Fournisseur { get; private set; } = "";
        public int Qte { get; private set; }
        public decimal PUAchatHT { get; private set; }

        public ReassortDialog()
        {
            InitializeComponent();
        }

        private void Validate_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtQte.Text.Trim(), out var q) || q <= 0)
            {
                MessageBox.Show("Quantité invalide.", "Réassort", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!decimal.TryParse(TxtPU.Text.Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var pu) || pu < 0)
            {
                MessageBox.Show("PU Achat HT invalide.", "Réassort", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Fournisseur = TxtFournisseur.Text.Trim();
            Qte = q;
            PUAchatHT = pu;
            DialogResult = true;
            Close();
        }
    }
}
