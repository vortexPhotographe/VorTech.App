using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using VorTech.App.Models;
using VorTech.App.Services;

namespace VorTech.App.Views
{
    public partial class ArticlesView : UserControl
    {
        private ObservableCollection<Article> _items = new();
        private Article? _current;

        public ArticlesView()
        {
            InitializeComponent();
            Reload();
            New();
        }

        private void Reload()
        {
            _items = new ObservableCollection<Article>(ArticleService.GetAll());
            GridArticles.ItemsSource = _items;
        }

        private void New()
        {
            _current = new Article();
            BindCurrentToForm();
        }

        private void BindCurrentToForm()
        {
            SkuBox.Text   = _current?.Sku ?? "";
            NameBox.Text  = _current?.Name ?? "";
            PriceBox.Text = (_current?.PriceHT ?? 0).ToString("0.##", CultureInfo.InvariantCulture);
            StockBox.Text = (_current?.Stock  ?? 0).ToString("0.##", CultureInfo.InvariantCulture);
        }

        private bool ReadFormToCurrent(out string error)
        {
            error = "";
            if (_current == null) _current = new Article();

            _current.Sku  = SkuBox.Text?.Trim()  ?? "";
            _current.Name = NameBox.Text?.Trim() ?? "";

            if (!double.TryParse(PriceBox.Text?.Replace(',', '.') ?? "0",
                NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
            { error = "PU HT invalide"; return false; }

            if (!double.TryParse(StockBox.Text?.Replace(',', '.') ?? "0",
                NumberStyles.Any, CultureInfo.InvariantCulture, out var stock))
            { error = "Stock invalide"; return false; }

            _current.PriceHT = price;
            _current.Stock   = stock;
            return true;
        }

        private void GridArticles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridArticles.SelectedItem is Article a)
            {
                _current = new Article
                {
                    Id      = a.Id,
                    Sku     = a.Sku,
                    Name    = a.Name,
                    PriceHT = a.PriceHT,
                    Stock   = a.Stock
                };
                BindCurrentToForm();
            }
        }

        private void New_Click(object sender, RoutedEventArgs e) => New();

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!ReadFormToCurrent(out var err))
            {
                MessageBox.Show(err, "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ArticleService.Save(_current!);
            Reload();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_current?.Id > 0)
            {
                if (MessageBox.Show("Supprimer cet article ?", "Confirmation",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    ArticleService.Delete(_current.Id);
                    Reload();
                    New();
                }
            }
        }

        private void Reload_Click(object sender, RoutedEventArgs e) => Reload();
    }
}
