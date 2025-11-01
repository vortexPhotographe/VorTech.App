using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace VorTech.App.Views
{
    public partial class AnnexPickerWindow : Window
    {
        public List<int> SelectedIds { get; } = new();

        public AnnexPickerWindow(List<(int Id, string Nom, string CheminRelatif, bool Actif)> catalog)
        {
            InitializeComponent();
            // VM minimaliste pour checkboxes
            var items = catalog
                .Where(a => a.Actif)
                .Select(a => new Item { Id = a.Id, Display = $"{a.Nom}  —  {a.CheminRelatif}" })
                .ToList();
            List.ItemsSource = items;
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            foreach (Item it in List.Items)
                if (it.IsChecked) SelectedIds.Add(it.Id);

            DialogResult = true;
            Close();
        }

        private class Item
        {
            public int Id { get; set; }
            public string Display { get; set; } = "";
            public bool IsChecked { get; set; }
        }
    }
}
