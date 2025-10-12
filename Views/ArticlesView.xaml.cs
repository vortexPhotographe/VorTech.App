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
        private readonly ObservableCollection<Article> _items = new();

        public ArticlesView()
        {
            InitializeComponent();
            List.ItemsSource = _items;
            Reload();
        }

        private void Reload()
        {
            _items.Clear();
            foreach (var a in ArticleService.GetAll()) _items.Add(a);
            if (_items.Count > 0) List.SelectedIndex = 0; else ClearForm();
        }

        private void ClearForm()
        {
            TbRef.Text   = "";
            TbLib.Text   = "";
            TbPA.Text    = "0";
            TbPV.Text    = "0";
            TbStock.Text = "0";
        }

        private Article? Current()
            => List.SelectedItem as Article;

        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var a = Current();
            if (a == null) { ClearForm(); return; }
            TbRef.Text   = a.Reference;
            TbLib.Text   = a.Libelle;
            TbPA.Text    = a.PrixAchatHT.ToString(CultureInfo.InvariantCulture);
            TbPV.Text    = a.PrixVenteHT.ToString(CultureInfo.InvariantCulture);
            TbStock.Text = a.StockActuel.ToString(CultureInfo.InvariantCulture);
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            List.SelectedItem = null;
            ClearForm();
            TbRef.Focus();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var a = Current() ?? new Article();
            a.Reference    = TbRef.Text?.Trim() ?? "";
            a.Libelle      = TbLib.Text?.Trim() ?? "";
            _ = double.TryParse(TbPA.Text,  NumberStyles.Any, CultureInfo.InvariantCulture, out var pa);
            _ = double.TryParse(TbPV.Text,  NumberStyles.Any, CultureInfo.InvariantCulture, out var pv);
            _ = int.TryParse(TbStock.Text,  NumberStyles.Any, CultureInfo.InvariantCulture, out var stock);
            a.PrixAchatHT  = pa;
            a.PrixVenteHT  = pv;
            a.StockActuel  = stock;

            ArticleService.Save(a);
            Reload();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var a = Current();
            if (a == null) return;
            if (MessageBox.Show($"Supprimer « {a.Libelle} » ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            ArticleService.Delete(a.Id);
            Reload();
        }
    }
}
