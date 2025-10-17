using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VorTech.App.Models;
using VorTech.App.Services;

namespace VorTech.App.Views
{
    public partial class SettingsView : UserControl
    {
        private AppConfig _config = default!;
        private readonly SettingsCatalogService _catalogs = new SettingsCatalogService();

        private ObservableCollection<CategoryVM> _cats = new();
        private ObservableCollection<TvaVM> _tvas = new();
        private ObservableCollection<CotiVM> _cotis = new();
        private CompanyProfile _company = new CompanyProfile();

        public SettingsView()
        {
            InitializeComponent();
            LoadGeneral();
            ShowPanel(PanelGeneral);
            LoadArticles();
        }

        // ---------- Panels ----------
        private void ShowPanel(UIElement panel)
        {
            PanelGeneral.Visibility = (panel == PanelGeneral) ? Visibility.Visible : Visibility.Collapsed;
            PanelArticles.Visibility = (panel == PanelArticles) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnShowGeneral_Click(object sender, RoutedEventArgs e) => ShowPanel(PanelGeneral);
        private void BtnShowArticles_Click(object sender, RoutedEventArgs e) => ShowPanel(PanelArticles);

        // ---------- Général (JSON) ----------
        private void LoadGeneral()
        {
            _config = ConfigService.Load();
            // micro <=> pas de TVA
            RBTaxMicro.IsChecked = string.Equals(_config.TaxMode, "Micro", StringComparison.OrdinalIgnoreCase);
            RBTaxTVA.IsChecked = string.Equals(_config.TaxMode, "TVA", StringComparison.OrdinalIgnoreCase);
            // Charger identité société (BDD)
            _company = _catalogs.GetCompanyProfile();
            BindCompanyToForm();
        }

        private void BtnSaveGeneral_Click(object sender, RoutedEventArgs e)
        {
            // 1) Sauver mode fiscal (JSON)
            _config.TaxMode = (RBTaxTVA.IsChecked == true) ? "TVA" : "Micro";
            ConfigService.Save(_config);

            // 2) Sauver identité société (BDD)
            ReadCompanyFromForm();
            _catalogs.SaveCompanyProfile(_company);

            MessageBox.Show("Paramètres généraux enregistrés.");
        }

        // ---------- Articles (catalogues) ----------
        private void LoadArticles()
        {
            // Catégories
            _cats = new ObservableCollection<CategoryVM>(
                _catalogs.GetCategories().Select(c => new CategoryVM { Id = c.Id, Name = c.Name, Actif = c.Actif })
            );
            GridCategories.ItemsSource = _cats;

            // TVA
            _tvas = new ObservableCollection<TvaVM>(
                _catalogs.GetTvaRates().Select(t => new TvaVM
                {
                    Id = t.Id,
                    Name = t.Name,
                    Rate = Convert.ToDecimal(t.Rate, CultureInfo.InvariantCulture),
                    IsDefault = t.IsDefault
                })
            );
            GridTva.ItemsSource = _tvas;

            // Cotisations
            _cotis = new ObservableCollection<CotiVM>(
                _catalogs.GetCotisationTypes().Select(c => new CotiVM
                {
                    Id = c.Id,
                    Name = c.Name,
                    Rate = Convert.ToDecimal(c.Rate, CultureInfo.InvariantCulture)
                })
            );
            GridCoti.ItemsSource = _cotis;
        }

        // --- Catégories
        private void BtnAddCategory_Click(object sender, RoutedEventArgs e)
        {
            _cats.Add(new CategoryVM { Id = 0, Name = "Nouvelle catégorie", Actif = true });
        }

        private void BtnSaveCategory_Click(object sender, RoutedEventArgs e)
        {
            GridCategories.CommitEdit(DataGridEditingUnit.Cell, true);
            GridCategories.CommitEdit(DataGridEditingUnit.Row, true);

            foreach (var c in _cats)
            {
                if (string.IsNullOrWhiteSpace(c.Name)) continue;
                if (c.Id == 0) c.Id = _catalogs.InsertCategory(c.Name.Trim(), c.Actif);
                else _catalogs.UpdateCategory(c.Id, c.Name.Trim(), c.Actif);
            }
            MessageBox.Show("Catégories enregistrées.");
            LoadArticles();
        }

        private void BtnDeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            var sel = GridCategories.SelectedItem as CategoryVM;
            if (sel == null) return;
            if (sel.Id == 0) { _cats.Remove(sel); return; }
            _catalogs.DeleteCategory(sel.Id);
            _cats.Remove(sel);
        }

        // --- TVA
        private void BtnAddTva_Click(object sender, RoutedEventArgs e)
        {
            _tvas.Add(new TvaVM { Id = 0, Name = "TVA", Rate = 0, IsDefault = false });
        }

        private void BtnSaveTva_Click(object sender, RoutedEventArgs e)
        {
            GridTva.CommitEdit(DataGridEditingUnit.Cell, true);
            GridTva.CommitEdit(DataGridEditingUnit.Row, true);

            foreach (var t in _tvas)
            {
                if (string.IsNullOrWhiteSpace(t.Name)) continue;
                if (t.Id == 0) t.Id = _catalogs.InsertTvaRate(t.Name.Trim(), t.Rate, t.IsDefault);
                else _catalogs.UpdateTvaRate(t.Id, t.Name.Trim(), t.Rate, t.IsDefault);
            }
            MessageBox.Show("TVA enregistrées.");
            LoadArticles();
        }

        private void BtnDeleteTva_Click(object sender, RoutedEventArgs e)
        {
            var sel = GridTva.SelectedItem as TvaVM;
            if (sel == null) return;
            if (sel.Id == 0) { _tvas.Remove(sel); return; }
            _catalogs.DeleteTvaRate(sel.Id);
            _tvas.Remove(sel);
        }

        // --- Cotisations
        private void BtnAddCoti_Click(object sender, RoutedEventArgs e)
        {
            _cotis.Add(new CotiVM { Id = 0, Name = "Cotisation", Rate = 0 });
        }

        private void BtnSaveCoti_Click(object sender, RoutedEventArgs e)
        {
            GridCoti.CommitEdit(DataGridEditingUnit.Cell, true);
            GridCoti.CommitEdit(DataGridEditingUnit.Row, true);

            foreach (var c in _cotis)
            {
                if (string.IsNullOrWhiteSpace(c.Name)) continue;
                if (c.Id == 0) c.Id = _catalogs.InsertCotisationType(c.Name.Trim(), c.Rate);
                else _catalogs.UpdateCotisationType(c.Id, c.Name.Trim(), c.Rate);
            }
            MessageBox.Show("Cotisations enregistrées.");
            LoadArticles();
        }

        private void BtnDeleteCoti_Click(object sender, RoutedEventArgs e)
        {
            var sel = GridCoti.SelectedItem as CotiVM;
            if (sel == null) return;
            if (sel.Id == 0) { _cotis.Remove(sel); return; }
            _catalogs.DeleteCotisationType(sel.Id);
            _cotis.Remove(sel);
        }

        // --- CompanyProfile
        private void BindCompanyToForm()
        {
            BoxNomCommercial.Text = _company.NomCommercial ?? "";
            BoxSiret.Text = _company.Siret ?? "";
            BoxAdresse1.Text = _company.Adresse1 ?? "";
            BoxAdresse2.Text = _company.Adresse2 ?? "";
            BoxCodePostal.Text = _company.CodePostal ?? "";
            BoxVille.Text = _company.Ville ?? "";
            BoxPays.Text = _company.Pays ?? "";
            BoxEmail.Text = _company.Email ?? "";
            BoxTelephone.Text = _company.Telephone ?? "";
            BoxSiteWeb.Text = _company.SiteWeb ?? "";
        }

        private void ReadCompanyFromForm()
        {
            _company.NomCommercial = BoxNomCommercial.Text?.Trim() ?? "";
            _company.Siret = BoxSiret.Text?.Trim() ?? "";
            _company.Adresse1 = BoxAdresse1.Text?.Trim() ?? "";
            _company.Adresse2 = BoxAdresse2.Text?.Trim() ?? "";
            _company.CodePostal = BoxCodePostal.Text?.Trim() ?? "";
            _company.Ville = BoxVille.Text?.Trim() ?? "";
            _company.Pays = BoxPays.Text?.Trim() ?? "";
            _company.Email = BoxEmail.Text?.Trim() ?? "";
            _company.Telephone = BoxTelephone.Text?.Trim() ?? "";
            _company.SiteWeb = BoxSiteWeb.Text?.Trim() ?? "";
        }

        // --- VM internes
        private class CategoryVM
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public bool Actif { get; set; }
        }

        private class TvaVM
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public decimal Rate { get; set; }  // %
            public bool IsDefault { get; set; }
        }

        private class CotiVM
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public decimal Rate { get; set; }  // %
        }
    }
}
