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

        private void TvaBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshPrixConseille();
        }

        public ArticlesView()
        {
            InitializeComponent();
            // -> petit trace simple
            MessageBox.Show("ArticlesView CTOR", "TRACE", MessageBoxButton.OK, MessageBoxImage.Information);

            // évite tout “null ref” sur les Combo
            CategorieBox.ItemsSource = _catalogs.GetCategories().ToList();
            TvaBox.ItemsSource        = _catalogs.GetTvaRates().ToList();
            CotisationBox.ItemsSource = _catalogs.GetCotisationRates().ToList();

            TvaBox.IsEnabled = false;      // Micro => désactivée par défaut
            CotisationBox.IsEnabled = true;

            Reload();
            New();
            HideStatus();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("ArticlesView LOADED", "TRACE", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void HideStatus()
        {
            if (StatusPanelOk != null)  StatusPanelOk.Visibility  = Visibility.Collapsed;
            if (StatusPanelErr != null) StatusPanelErr.Visibility = Visibility.Collapsed;
        }
        private void ShowOk(string msg)
        {
            if (StatusOkText != null)   StatusOkText.Text = msg;
            if (StatusPanelErr != null) StatusPanelErr.Visibility = Visibility.Collapsed;
            if (StatusPanelOk != null)  StatusPanelOk.Visibility  = Visibility.Visible;
        }
        private void ShowErr(string msg)
        {
            if (StatusErrText != null)  StatusErrText.Text = msg;
            if (StatusPanelOk != null)  StatusPanelOk.Visibility  = Visibility.Collapsed;
            if (StatusPanelErr != null) StatusPanelErr.Visibility = Visibility.Visible;
        }

        // ---------- RELOAD / BIND ----------
        private void Reload()
        {
            var all = _articles.GetAll().ToList();
            MessageBox.Show($"Reload(): {all.Count} article(s) lus depuis la DB", "TRACE");
            _items = new ObservableCollection<Article>(all);
            GridArticles.ItemsSource = _items;
        }

        private void BindCurrentToForm()
        {
            var a = _current ?? new Article { DerniereMaj = DateOnly.FromDateTime(DateTime.Now) };

            CodeBox.Text = a.Code;
            LibelleBox.Text = a.Libelle;
            TypeArticleRadio.IsChecked = a.Type == ArticleType.Article;
            TypePackRadio.IsChecked = a.Type == ArticleType.Pack;

            CategorieBox.SelectedValue  = a.CategorieId;
            TvaBox.SelectedValue        = a.TvaRateId;
            CotisationBox.SelectedValue = a.CotisationRateId;

            PrixAchatBox.Text = a.PrixAchatHT.ToString("0.##", CultureInfo.InvariantCulture);
            PrixVenteBox.Text = a.PrixVenteHT.ToString("0.##", CultureInfo.InvariantCulture);
            StockBox.Text     = a.StockActuel.ToString("0.##", CultureInfo.InvariantCulture);
            SeuilBox.Text     = a.SeuilAlerte.ToString("0.##", CultureInfo.InvariantCulture);
            PoidsBox.Text     = a.PoidsG.ToString("0.##", CultureInfo.InvariantCulture);
            ActifBox.IsChecked = a.Actif;
            DerniereMajLabel.Text = a.DerniereMaj.ToString("yyyy-MM-dd");
            BarcodeBox.Text = a.CodeBarres ?? "";

            _variants  = new ObservableCollection<ArticleVariant>(_articles.GetVariants(a.Id));
            _packItems = new ObservableCollection<PackItem>(_articles.GetPackItems(a.Id));
            GridVariants.ItemsSource = _variants;
            GridPack.ItemsSource     = _packItems;
        }

        private static decimal ParseDec(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0m;
            s = s.Trim().Replace(',', '.');
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;
        }

        private void ReadFormToCurrent()
        {
            _current ??= new Article();

            _current.Code    = CodeBox.Text.Trim();
            _current.Libelle = LibelleBox.Text.Trim();
            _current.Type    = (TypeArticleRadio.IsChecked == true) ? ArticleType.Article : ArticleType.Pack;

            _current.CategorieId      = CategorieBox.SelectedValue as int?;
            _current.TvaRateId        = TvaBox.SelectedValue as int?;
            _current.CotisationRateId = CotisationBox.SelectedValue as int?;

            _current.PrixAchatHT = ParseDec(PrixAchatBox.Text);
            _current.PrixVenteHT = ParseDec(PrixVenteBox.Text);
            _current.StockActuel = ParseDec(StockBox.Text);
            _current.SeuilAlerte = ParseDec(SeuilBox.Text);
            _current.PoidsG      = ParseDec(PoidsBox.Text);
            _current.Actif       = ActifBox.IsChecked == true;
            _current.CodeBarres  = string.IsNullOrWhiteSpace(BarcodeBox.Text) ? null : BarcodeBox.Text.Trim();
            _current.DerniereMaj = DateOnly.FromDateTime(DateTime.Now);
        }

        // ---------- COMMANDES ----------
        private void New()
        {
            _current = new Article { DerniereMaj = DateOnly.FromDateTime(DateTime.Now) };
            BindCurrentToForm();
        }

        private void Save()
        {
            ReadFormToCurrent();
            if (_current == null) return;

            MessageBox.Show($"Save() AVANT\nId={_current.Id}\nCode={_current.Code}", "TRACE");

            if (_current.Id == 0)
            {
                _current = _articles.Insert(_current);
                MessageBox.Show($"Insert -> Id={_current.Id}", "TRACE");
            }
            else
            {
                _articles.Update(_current);
                MessageBox.Show($"Update -> Id={_current.Id}", "TRACE");
            }

            var keepId = _current.Id;
            Reload();
            var found = _items.FirstOrDefault(x => x.Id == keepId);
            if (found != null)
            {
                GridArticles.SelectedItem = found;
                GridArticles.ScrollIntoView(found);
            }
            BindCurrentToForm();
            ShowOk("Enregistré ✓");
        }

        // ---------- EVENTS ----------
        private void GridArticles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _current = GridArticles.SelectedItem as Article;
            BindCurrentToForm();
        }

        private void BtnNew_Click(object sender, RoutedEventArgs e)     => New();
        private void BtnSave_Click(object sender, RoutedEventArgs e)    => Save();
        private void BtnReload_Click(object sender, RoutedEventArgs e)  => Reload();
        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null || _current.Id == 0) return;
            if (MessageBox.Show("Supprimer cet article ?", "Confirmation",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _articles.Delete(_current.Id);
                Reload();
                New();
                ShowOk("Supprimé.");
            }
        }

        private void BtnGenBarcode_Click(object sender, RoutedEventArgs e)
        {
            BarcodeBox.Text = BarcodeUtil.GenerateEAN13(seed: null, prefix: "200");
        }
		
		// Aligner le type si on clique sur un des RadioButtons
		private void TypeRadio_Checked(object sender, RoutedEventArgs e)
		{
			if (_current == null) return;
			_current.Type = (TypeArticleRadio?.IsChecked == true)
				? ArticleType.Article
				: ArticleType.Pack;
			// Si tu veux recalculer le prix conseillé en fonction du type, décommente :
			// RefreshPrixConseille();
		}

		// Recalcule le prix conseillé quand la cotisation change
		private void CotisationBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ReadFormToCurrent(); // on relit la sélection courante
			RefreshPrixConseille();
		}

		// Ajout d’images (lié au bouton "Ajouter image")
		private void BtnAddImage_Click(object sender, RoutedEventArgs e)
		{
			if (_current == null || string.IsNullOrWhiteSpace(_current.Code))
			{
				ShowErr("Enregistre d'abord l'article (Code requis).");
				return;
			}

			var dlg = new Microsoft.Win32.OpenFileDialog
			{
				Filter = "Images|*.jpg;*.jpeg;*.png",
				Multiselect = true
			};

			if (dlg.ShowDialog() != true) return;

			var targetDir = System.IO.Path.Combine(Paths.AssetsDir, "Images", "Articles", _current.Code);
			Directory.CreateDirectory(targetDir);

			var existing = Directory.GetFiles(targetDir)
									.Count(f => Path.GetFileName(f).StartsWith("img", StringComparison.OrdinalIgnoreCase));
			int slot = existing + 1;

			foreach (var src in dlg.FileNames)
			{
				if (slot > 4) break;
				var fileName = $"img{slot}{System.IO.Path.GetExtension(src).ToLower()}";
				var dest = System.IO.Path.Combine(targetDir, fileName);
				File.Copy(src, dest, true);

				// chemin relatif stocké en DB
				var rel = System.IO.Path.Combine("Assets", "Images", "Articles", _current.Code, fileName)
						  .Replace('\\', '/');

				// ⚠️ nécessite que ArticleService expose UpsertImage(int articleId, int slot, string relPath)
				_articles.UpsertImage(_current.Id, slot, rel);
				slot++;
			}

			ShowOk("Image(s) ajoutée(s).");
		}

		// Ajout d’une déclinaison (bouton)
		private void BtnAddVariant_Click(object sender, RoutedEventArgs e)
		{
			if (_current == null || _current.Id == 0)
			{
				ShowErr("Enregistre d'abord l'article.");
				return;
			}

			var v = new ArticleVariant
			{
				ArticleId   = _current.Id,
				Nom         = "Nouvelle déclinaison",
				PrixVenteHT = _current.PrixVenteHT,
				CodeBarres  = BarcodeUtil.GenerateEAN13(seed: null, prefix: "200")
			};

			v = _articles.InsertVariant(v);
			_variants.Add(v);
			ShowOk("Déclinaison ajoutée.");
		}

		// Suppression d’une déclinaison (bouton)
		private void BtnRemoveVariant_Click(object sender, RoutedEventArgs e)
		{
			if (GridVariants?.SelectedItem is not ArticleVariant v) return;
			_articles.DeleteVariant(v.Id);
			_variants.Remove(v);
			ShowOk("Déclinaison supprimée.");
		}
		
		// Calcule et affiche le "prix conseillé HT" selon TVA/cotisation
		private void RefreshPrixConseille()
		{
			if (_current == null)
			{
				PrixConseilleLabel.Text = "";
				return;
			}

			// Récupère la sélection actuelle des combos
			_current.TvaRateId        = TvaBox.SelectedValue as int?;
			_current.CotisationRateId = CotisationBox.SelectedValue as int?;

			// En micro-entreprise: TVA désactivée => on suit l'état du contrôle
			bool isTvaEnabled = TvaBox.IsEnabled;

			var tauxTva = _catalogs.GetRateById(_current.TvaRateId);
			var tauxCot = _catalogs.GetRateById(_current.CotisationRateId);

			var p = _current.GetPrixConseilleHT(isTvaEnabled, tauxTva, tauxCot);
			PrixConseilleLabel.Text = p == 0m ? "-" : p.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
		}
    }
}
