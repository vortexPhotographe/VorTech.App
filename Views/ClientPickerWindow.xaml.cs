using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using VorTech.App.Models;
using VorTech.App.Services;

namespace VorTech.App.Views
{
    public partial class ClientPickerWindow : Window
    {
        private readonly ClientService _clients = new ClientService();
        public Client? Selected { get; private set; }

        public ClientPickerWindow()
        {
            InitializeComponent();
            RefreshList(null);
        }

        private void TxtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var q = TxtSearch.Text?.Trim();
            if (string.IsNullOrEmpty(q) || q.Length >= 2) RefreshList(q);
        }

        private void RefreshList(string? q)
        {
            var list = _clients.GetAll(); // sans argument
            if (!string.IsNullOrWhiteSpace(q))
            {
                var s = q.Trim().ToLowerInvariant();
                list = list.Where(c =>
                    (!string.IsNullOrEmpty(c.Nom) && c.Nom!.ToLowerInvariant().Contains(s)) ||
                    (!string.IsNullOrEmpty(c.Prenom) && c.Prenom!.ToLowerInvariant().Contains(s)) ||
                    (!string.IsNullOrEmpty(c.Societe) && c.Societe!.ToLowerInvariant().Contains(s)) ||
                    (!string.IsNullOrEmpty(c.Email) && c.Email!.ToLowerInvariant().Contains(s)) ||
                    (!string.IsNullOrEmpty(c.Telephone) && c.Telephone!.ToLowerInvariant().Contains(s))
                ).ToList();
            }
            List.ItemsSource = list;
        }

        private void List_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (List.SelectedItem != null) Ok_Click(null!, null!);
        }

        private void Ok_Click(object? sender, RoutedEventArgs? e)
        {
            Selected = List.SelectedItem as Client;
            if (Selected == null) return;
            DialogResult = true;
            Close();
        }
    }
}
