using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VorTech.App.Models;
using VorTech.App.Services;

namespace VorTech.App.Views
{
    public partial class ArticlesView : UserControl
    {
        private readonly ArticleService _articles = new();
        private readonly SettingsCatalogService _catalogs = new();

        private ObservableCollection<Article> _items = new();
        private ObservableCollection<ArticleVariant> _variants = new();
        private ObservableCollection<PackItem> _packItems = new();

        private Article? _current;

        public ArticlesView()
        {
            InitializeComponent();
            Reload();
            New();
            InitCatalogs();
            ApplyTvaToggle(); // par défaut: TVA désactivée, cotisation active
        }

        private void InitCatalogs()
        {
            CategorieBox.ItemsSource = _catalogs.GetCategories().ToList();
            TvaBox.ItemsSource = _catalogs.GetTvaRates().ToList();
            CotisationBox.ItemsSource = _catalogs.GetCotisationRates().ToList();
        }

        // Micro par défaut: TVA désactivée → combo TVA disabled, Cotisation enabled
        private void ApplyTvaToggle()
        {
            TvaBox.IsEnabled = false;
            CotisationBox.IsEnabled = true;
        }

        // ----------------- Helpers -----------------
        private void BindCurrentToForm()
        {
            var a = _current ?? new Article();

            CodeBox.Text = a.Code;
            LibelleBox.Text = a.Libelle;
            TypeArticleRadio.IsChecked = a.Type == ArticleType.Article;
            TypePackRadio.IsChecked = a.Type == ArticleType.Pack;

            CategorieBox.SelectedValue = a.CategorieId;
            TvaBox.SelectedValue = a.TvaRateId;
            CotisationBox.SelectedValue = a.CotisationRateId;

            PrixAchatBox.Text = a.PrixAchatHT.ToString("0.##", CultureInfo.InvariantCulture);
            PrixVenteBox.Text = a.PrixVenteHT.ToString("0.##", CultureInfo.InvariantCulture);
            StockBox.Text = a.StockActuel.ToString("0.##", CultureInfo.InvariantCulture);
            SeuilBox.Text = a.SeuilAlerte.ToString("0.##", CultureInfo.InvariantCulture);
            PoidsBox.Text = a.PoidsG.ToString("0.##", CultureInfo.InvariantCulture);
            ActifBox.IsChecked = a.Actif;
            DerniereMajLabel.Text = a.DerniereMaj.ToString("yyyy-MM-dd");
            BarcodeBox.Text = a.CodeBarres ?? "";

            _variants = new ObservableCollection<ArticleVariant>(_articles.GetVariants(a.Id));
            GridVariants.ItemsSource = _variants;

            _packItems = new ObservableCollection<PackItem>(_articles.GetPackItems(a.Id));
            GridPack.ItemsSource = _packItems;

            RefreshPrixConseille();
        }

        private void ReadFormToCurrent()
        {
            if (_current == null) _current = new Article();

            _current.Code = CodeBox.Text.Trim();
            _current.Libelle = LibelleBox.Text.Trim();
            _current.Type = TypeArticleRadio.IsChecked == true ? ArticleType.Article : ArticleType.Pack;

            _current.CategorieId = CategorieBox.SelectedValue as int?;
            _current.TvaRateId = TvaBox.SelectedValue as int?;
            _current.CotisationRateId = CotisationBox.SelectedValue as int?;

            _current.PrixAchatHT = ParseDec(PrixAchatBox.Text);
            _current.PrixVenteHT = ParseDec(PrixVenteBox.Text);
            _current.StockActuel = ParseDec(StockBox.Text);
            _current.SeuilAlerte = ParseDec(SeuilBox.Text);
            _current.PoidsG = ParseDec(PoidsBox.Text);
            _current.Actif = ActifBox.IsChecked == true;
            _current.CodeBarres = string.IsNullOrWhiteSpace(BarcodeBox.Text) ? null : BarcodeBox.Text.Trim();
            _current.DerniereMaj = DateOnly.FromDateTime(DateTime.Now);
        }

        private static decimal ParseDec(string s)
        {
            if (decimal.TryParse(s.Replace(',', '.'), NumberStyles.Any,
                CultureInfo.InvariantCulture, out var v)) return v;
            return 0m;
        }

        private void RefreshPrixConseille()
        {
            if (_current == null) { PrixConseilleLabel.Text = ""; return; }

            // On détermine "isTvaEnabled" par l'état des combos (par défaut: TVA off)
            bool isTvaEnabled = TvaBox.IsEnabled;

            var tauxTva = _catalogs.GetRateById(_current.TvaRateId);
            var tauxCot = _catalogs.GetRateById(_current.CotisationRateId);

            var p = _current.GetPrixConseilleHT(isTvaEnabled, tauxTva, tauxCot);
            PrixConseilleLabel.Text = p == 0m ? "-" : p.ToString("0.00");
        }

        // ----------------- Actions -----------------
        private void Reload()
        {
            _items = new ObservableCollection<Article>(_articles.GetAll());
            GridArticles.ItemsSource = _items;
        }

        private void New()
        {
            _current = new Article { DerniereMaj = DateOnly.FromDateTime(DateTime.Now) };
            BindCurrentToForm();
        }

        private void Save()
        {
            ReadFormToCurrent();
            if (_current == null) return;

            if (_current.Id == 0)
            {
                _articles.Insert(_current);
                _items.Insert(0, _current);
            }
            else
            {
                _articles.Update(_current);
                var idx = _items.IndexOf(_items.First(x => x.Id == _current.Id));
                if (idx >= 0) _items[idx] = _current;
                GridArticles.Items.Refresh();
            }
            BindCurrentToForm();
        }

        // ----------------- Events -----------------
        private void GridArticles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _current = GridArticles.SelectedItem as Article;
            BindCurrentToForm();
        }

        private void BtnNew_Click(object sender, RoutedEventArgs e) => New();

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            Save();
            MessageBox.Show("Enregistré.");
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null || _current.Id == 0) return;
            if (MessageBox.Show("Supprimer cet article ?", "Confirmation",
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _articles.Delete(_current.Id);
                _items.Remove(_current);
                New();
            }
        }

        private void BtnReload_Click(object sender, RoutedEventArgs e) => Reload();

        private void TypeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            _current.Type = TypeArticleRadio.IsChecked == true ? ArticleType.Article : ArticleType.Pack;
        }

        private void BtnGenBarcode_Click(object sender, RoutedEventArgs e)
        {
            // EAN interne par défaut (préfixe 200)
            var candidate = BarcodeUtil.GenerateEAN13(seed: null, prefix: "200");
            BarcodeBox.Text = candidate;
        }

        private void BtnAddImage_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;

            var dlg = new OpenFileDialog
            {
                Filter = "Images|*.jpg;*.jpeg;*.png",
                Multiselect = true
            };
            if (dlg.ShowDialog() != true) return;

            var targetDir = Path.Combine(Paths.AssetsDir, "Images", "Articles", _current.Code);
            Directory.CreateDirectory(targetDir);

            var existing = Directory.GetFiles(targetDir)
                                    .Count(f => Path.GetFileName(f).StartsWith("img", StringComparison.OrdinalIgnoreCase));
            int slot = existing + 1;

            foreach (var src in dlg.FileNames)
            {
                if (slot > 4) break;
                var fileName = $"img{slot}{Path.GetExtension(src).ToLower()}";
                var dest = Path.Combine(targetDir, fileName);
                File.Copy(src, dest, true);

                // Chemin RELATIF : Assets/Images/Articles/{code}/imgX.ext
                var rel = Path.Combine("Assets", "Images", "Articles", _current.Code, fileName)
                          .Replace('\\', '/');

                _articles.UpsertImage(_current.Id, slot, rel);
                slot++;
            }

            MessageBox.Show("Image(s) ajoutée(s).");
        }

        private void BtnAddVariant_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null || _current.Id == 0)
            {
                MessageBox.Show("Enregistre d'abord l'article.");
                return;
            }

            var v = new ArticleVariant
            {
                ArticleId = _current.Id,
                Nom = "Nouvelle déclinaison",
                PrixVenteHT = _current.PrixVenteHT,
                CodeBarres = BarcodeUtil.GenerateEAN13(seed: null, prefix: "200")
            };

            v = _articles.InsertVariant(v);
            _variants.Add(v);
        }

        private void BtnRemoveVariant_Click(object sender, RoutedEventArgs e)
        {
            if (GridVariants.SelectedItem is not ArticleVariant v) return;
            _articles.DeleteVariant(v.Id);
            _variants.Remove(v);
        }
    }
}
