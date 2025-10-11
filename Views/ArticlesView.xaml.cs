using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using VorTech.App.Models;
using VorTech.App.Services;

namespace VorTech.App.Views
{
    public partial class ArticlesView : UserControl, INotifyPropertyChanged
    {
        private readonly ArticleService _svc = new ArticleService();
        private readonly SettingsCatalogService _catalog = new SettingsCatalogService();
        private readonly VariantService _variants = new VariantService();

        public ObservableCollection<Article> Articles { get; } = new ObservableCollection<Article>();
        public ObservableCollection<ArticleComponent> Components { get; } = new ObservableCollection<ArticleComponent>(); // réservé packs
        public ObservableCollection<ArticleReassort> Reassorts { get; } = new ObservableCollection<ArticleReassort>();

        public ObservableCollection<string> ArticleKinds { get; } = new ObservableCollection<string>(new[] { "Article", "Pack" });
        public ObservableCollection<CotisationType> CotisationTypes { get; } = new ObservableCollection<CotisationType>();
        public ObservableCollection<TvaRate> TvaRates { get; } = new ObservableCollection<TvaRate>();

        // Déclinaisons (axes + variantes)
        public ObservableCollection<VariantOption> AllOptions { get; } = new ObservableCollection<VariantOption>();
        public ObservableCollection<AxisSelection> AxisSelections { get; } = new ObservableCollection<AxisSelection>();
        public ObservableCollection<ArticleVariant> Variants { get; } = new ObservableCollection<ArticleVariant>();

        private Article? _selected;
        public Article? Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                OnPropertyChanged();
                LoadReassorts();
                OnPropertyChanged(nameof(SuggestedPriceSummary));
                LoadVariants();
            }
        }

        public ArticlesView()
        {
            InitializeComponent();
            DataContext = this;
            ReloadCatalogs();
            ReloadOptions();
            Reload();
        }

        // ===== Chargements ===================================================

        private void ReloadCatalogs()
        {
            CotisationTypes.Clear();
            foreach (var c in _catalog.GetCotisationTypes()) CotisationTypes.Add(c);

            TvaRates.Clear();
            foreach (var r in _catalog.GetTvaRates()) TvaRates.Add(r);
        }

        private void ReloadOptions()
        {
            AllOptions.Clear();
            foreach (var o in _variants.GetOptions()) AllOptions.Add(o);
        }

        private void Reload()
        {
            Articles.Clear();
            foreach (var a in _svc.GetAll()) Articles.Add(a);
            Selected = Articles.FirstOrDefault();
        }

        private void LoadReassorts()
        {
            Reassorts.Clear();
            if (Selected == null || Selected.Id <= 0) return;
            foreach (var r in _svc.GetReassorts(Selected.Id)) Reassorts.Add(r);
        }

        // ===== Actions fiche =================================================

        private void New_Click(object sender, RoutedEventArgs e)
        {
            var defTva = TvaRates.FirstOrDefault(t => t.IsDefault) ?? TvaRates.FirstOrDefault(r => r.Rate == 0) ?? TvaRates.FirstOrDefault();
            Selected = new Article
            {
                Actif = true,
                Type = "Article",
                TvaRateId = defTva?.Id,
                TVA = defTva?.Rate ?? 0,
                BarcodeType = "CODE128",
                PoidsUnitaireGr = 0
            };
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;

            if (Selected.TvaRateId.HasValue)
            {
                var rate = TvaRates.FirstOrDefault(x => x.Id == Selected.TvaRateId.Value)?.Rate ?? 0;
                Selected.TVA = rate;
            }

            Selected.DerniereMAJ = System.DateTime.UtcNow.ToString("s");
            _svc.Save(Selected);

            var id = Selected.Id;
            Reload();
            Selected = Articles.FirstOrDefault(x => x.Id == id);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null || Selected.Id <= 0) return;
            var res = MessageBox.Show("Supprimer cet article ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes) return;
            _svc.Delete(Selected.Id);
            Reload();
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            ReloadCatalogs();
            Reload();
        }

        private void ReloadReassorts_Click(object sender, RoutedEventArgs e) => LoadReassorts();

        private void Reassort_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null || Selected.Id <= 0) { MessageBox.Show("Aucun article sélectionné."); return; }

            var qteText = Microsoft.VisualBasic.Interaction.InputBox("Quantité reçue :", "Réassort", "1");
            if (!double.TryParse(qteText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var qte) || qte <= 0)
            {
                MessageBox.Show("Quantité invalide."); return;
            }
            var puText = Microsoft.VisualBasic.Interaction.InputBox("PU Achat HT :", "Réassort", "0");
            if (!double.TryParse(puText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var pu) || pu < 0)
            {
                MessageBox.Show("Prix d'achat invalide."); return;
            }
            var fournisseur = Microsoft.VisualBasic.Interaction.InputBox("Fournisseur (optionnel) :", "Réassort", "");
            var notes = Microsoft.VisualBasic.Interaction.InputBox("Notes (optionnel) :", "Réassort", "");

            _svc.AddReassortPmp(Selected.Id, qte, pu, string.IsNullOrWhiteSpace(fournisseur) ? null : fournisseur, string.IsNullOrWhiteSpace(notes) ? null : notes);
            LoadReassorts();

            // refresh fiche
            var id = Selected.Id; Reload(); Selected = Articles.FirstOrDefault(x => x.Id == id);
        }

        private void GenerateArticleBarcode_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            if (string.IsNullOrWhiteSpace(Selected.Barcode))
            {
                Selected.Barcode = VariantService.GenerateBarcode(isVariant: false);
                OnPropertyChanged(nameof(Selected));
            }
        }

        // Prix conseillé (affichage)
        public string SuggestedPriceSummary
        {
            get
            {
                if (Selected == null) return "";
                double c = Selected.PrixAchatHT;
                double r = 0;

                if (Selected.CotisationTypeId.HasValue)
                {
                    var ct = CotisationTypes.FirstOrDefault(x => x.Id == Selected.CotisationTypeId.Value);
                    if (ct != null) r = ct.Rate / 100.0;
                }

                if (1.0 - r <= 0.000001) return "—";
                double pht = 2.0 * c / (1.0 - r);

                double tv = 0;
                if (Selected.TvaRateId.HasValue)
                {
                    var tr = TvaRates.FirstOrDefault(x => x.Id == Selected.TvaRateId.Value);
                    if (tr != null) tv = tr.Rate / 100.0;
                }
                double pttc = pht * (1.0 + tv);

                return $"{pht:0.00} € HT   |   {pttc:0.00} € TTC";
            }
        }

        private void Recalc_OnChanged(object sender, RoutedEventArgs e)
        {
            OnPropertyChanged(nameof(SuggestedPriceSummary));
        }

        // ===== Déclinaisons ==================================================

        private void LoadVariants()
        {
            Variants.Clear();
            AxisSelections.Clear();
            if (Selected == null || Selected.Id <= 0) return;

            foreach (var v in _variants.GetVariants(Selected.Id))
                Variants.Add(v);
        }

        public class ValueChoice : INotifyPropertyChanged
        {
            public VariantOptionValue Value { get; }
            private bool _isSelected;
            public bool IsSelected { get => _isSelected; set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); } }
            public ValueChoice(VariantOptionValue v) { Value = v; }
            public event PropertyChangedEventHandler? PropertyChanged;
        }

        public class AxisSelection : INotifyPropertyChanged
        {
            private readonly VariantService _svc;
            private VariantOption? _selectedOption;
            public VariantOption? SelectedOption
            {
                get => _selectedOption;
                set
                {
                    _selectedOption = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedOption)));
                    LoadValues();
                }
            }
            public ObservableCollection<ValueChoice> AvailableChoices { get; } = new ObservableCollection<ValueChoice>();

            public AxisSelection(VariantService svc) { _svc = svc; }

            private void LoadValues()
            {
                AvailableChoices.Clear();
                if (SelectedOption == null) return;
                foreach (var v in _svc.GetOptionValues(SelectedOption.Id))
                    AvailableChoices.Add(new ValueChoice(v));
            }

            public Dictionary<int, List<int>> ToSelectionMapOrEmpty()
            {
                if (SelectedOption == null) return new Dictionary<int, List<int>>();
                var vals = AvailableChoices.Where(c => c.IsSelected).Select(c => c.Value.Id).ToList();
                if (vals.Count == 0) return new Dictionary<int, List<int>>();
                return new Dictionary<int, List<int>> { { SelectedOption.Id, vals } };
            }

            public event PropertyChangedEventHandler? PropertyChanged;
        }

        private void AddAxis_Click(object sender, RoutedEventArgs e)
        {
            AxisSelections.Add(new AxisSelection(_variants));
        }

        private void RemoveAxis_Click(object sender, RoutedEventArgs e)
        {
            if (AxisSelections.Count > 0)
                AxisSelections.RemoveAt(AxisSelections.Count - 1);
        }

        private void GenerateVariants_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null || Selected.Id <= 0)
            {
                MessageBox.Show("Sélectionnez un article d'abord."); return;
            }

            // Construire la map OptionId -> [ValueId...] à partir des axes sélectionnés
            var map = new Dictionary<int, List<int>>();
            foreach (var a in AxisSelections)
            {
                var m = a.ToSelectionMapOrEmpty();
                foreach (var kv in m)
                {
                    if (!map.TryGetValue(kv.Key, out var list)) { list = new List<int>(); map[kv.Key] = list; }
                    foreach (var v in kv.Value) if (!list.Contains(v)) list.Add(v);
                }
            }

            if (map.Count == 0)
            {
                MessageBox.Show("Aucune valeur sélectionnée."); return;
            }

            int created;
            try
            {
                created = _variants.GenerateMissingVariants(
                    Selected.Id,
                    map,
                    maxCombinaisons: 300,
                    skuFactory: sel =>
                    {
                        // SKU lisible: {CodeArticle}-{O<id>V<id>-...}
                        var baseCode = Selected.Code?.Trim();
                        if (string.IsNullOrWhiteSpace(baseCode)) baseCode = $"ART{Selected.Id}";
                        return baseCode + "-" + string.Join("-", sel.Select(kv => $"O{kv.Key}V{kv.Value}"));
                    });
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message, "Génération variantes"); return;
            }

            if (created > 0) LoadVariants();
            MessageBox.Show(created == 0 ? "Aucune variante créée (déjà existantes)." : $"Créées : {created}", "Génération variantes");
        }

        private void SaveVariants_Click(object sender, RoutedEventArgs e)
        {
            foreach (var v in Variants)
                _variants.UpdateVariant(v);
            MessageBox.Show("Variantes enregistrées.");
        }

        private void GenerateMissingBarcodes_Click(object sender, RoutedEventArgs e)
        {
            int count = 0;
            foreach (var v in Variants.Where(x => string.IsNullOrWhiteSpace(x.Barcode)))
            {
                v.Barcode = VariantService.GenerateBarcode(isVariant: true);
                _variants.UpdateVariant(v);
                count++;
            }
            if (count > 0) LoadVariants();
            MessageBox.Show(count == 0 ? "Aucun barcode manquant." : $"Barcodes générés : {count}");
        }

        // ===== INotifyPropertyChanged =======================================
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
