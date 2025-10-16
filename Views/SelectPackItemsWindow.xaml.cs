using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using VorTech.App.Services;


namespace VorTech.App.Views
{
    public partial class SelectPackItemsWindow : Window
    {
        private readonly ArticleService _articles;
        private ObservableCollection<ArticleService.SelectablePackRow> _rows = new();
        private List<ArticleService.SelectablePackRow> _all = new();


        public List<(int ArticleId, int? VariantId, decimal Quantity)> SelectedRows { get; private set; } = new();


        public SelectPackItemsWindow(ArticleService svc)
        {
            InitializeComponent();
            _articles = svc;
            LoadChoices();
        }


        private void LoadChoices()
        {
            _all = _articles.GetSelectablePackRows();
            _rows = new ObservableCollection<ArticleService.SelectablePackRow>(_all);
            GridChoices.ItemsSource = _rows;
        }


        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var q = (SearchBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(q))
            {
                _rows = new ObservableCollection<ArticleService.SelectablePackRow>(_all);
            }
            else
            {
                var f = _all.Where(r => (r.DisplayName ?? string.Empty)
                .IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                _rows = new ObservableCollection<ArticleService.SelectablePackRow>(f);
            }
            GridChoices.ItemsSource = _rows;
        }


        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }


        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(QtyDefaultBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var q) || q < 1)
            {
                MessageBox.Show("Quantité invalide (>=1)");
                return;
            }


            var picked = GridChoices.SelectedItems.Cast<ArticleService.SelectablePackRow>().ToList();
            if (picked.Count == 0)
            {
                MessageBox.Show("Sélectionnez au moins une ligne.");
                return;
            }


            SelectedRows = picked.Select(p => (p.ArticleId, p.VariantId, q)).ToList();
            DialogResult = true;
            Close();
        }
    }
}