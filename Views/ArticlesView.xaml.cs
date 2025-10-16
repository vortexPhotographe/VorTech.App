using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using System.Collections.ObjectModel;
using System.Globalization;
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
        private ObservableCollection<PackItem> _pack = new();

        private Article? _current;
        private string _taxMode = "Micro"; // lu depuis ConfigService

        public ArticlesView()
        {
            InitializeComponent();
            LoadConfigAndBinds();
            ReloadList();
            NewArticle();
        }

        // -------------- Images --------------
        private void LoadArticleImages()
        {
            if (_current == null || _current.Id <= 0)
            {
                ImgSlot1.Source = ImgSlot2.Source = ImgSlot3.Source = ImgSlot4.Source = null;
                return;
            }

            var imgs = _articles.GetImagePaths(_current.Id); // List<(int Slot, string RelPath, string FullPath)>
            var map = imgs.ToDictionary(i => i.Slot, i => i.FullPath);

            SetImg(ImgSlot1, map.TryGetValue(1, out var p1) ? p1 : null);
            SetImg(ImgSlot2, map.TryGetValue(2, out var p2) ? p2 : null);
            SetImg(ImgSlot3, map.TryGetValue(3, out var p3) ? p3 : null);
            SetImg(ImgSlot4, map.TryGetValue(4, out var p4) ? p4 : null);
        }

        private void SetImg(System.Windows.Controls.Image img, string? absPath)
        {
            img.Source = null;
            if (string.IsNullOrWhiteSpace(absPath)) return;
            if (!File.Exists(absPath)) return;

            try
            {
                // signature correcte: (path, FileMode, FileAccess, FileShare)
                using var fs = new FileStream(absPath, FileMode.Open, FileAccess.Read, FileShare.Read);

                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = fs;
                bi.EndInit();
                bi.Freeze(); // évite les locks WPF
                img.Source = bi;
            }
            catch
            {
                // image invalide: on laisse le slot vide
            }
        }

        private static string MakeRelToAssetsLocal(string absPath)
        {
            var rel = Path.GetRelativePath(Paths.AssetsDir, absPath);
            return rel.Replace('\\', '/');
        }

        private int FirstEmptySlot()
        {
            if (ImgSlot1.Source == null) return 1;
            if (ImgSlot2.Source == null) return 2;
            if (ImgSlot3.Source == null) return 3;
            if (ImgSlot4.Source == null) return 4;
            return -1;
        }

        private void BtnImportImage_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null || _current.Id <= 0)
            {
                MessageBox.Show("Enregistrez d'abord la fiche article avant d'importer des images.");
                return;
            }

            var ofd = new OpenFileDialog
            {
                Filter = "Images|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.gif|Tous fichiers|*.*",
                Multiselect = false
            };
            if (ofd.ShowDialog() != true) return;

            int slot = FirstEmptySlot();
            if (slot == -1)
            {
                if (MessageBox.Show("Les 4 slots sont remplis. Remplacer le slot 1 ?", "Images",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;
                slot = 1;
            }

            // Dossier: Assets/Images/Articles/{articleId}
            var destDir = Path.Combine(Paths.AssetsDir, "Images", "Articles", _current.Id.ToString());
            Directory.CreateDirectory(destDir);

            var destName = Path.GetFileName(ofd.FileName);
            var destPath = Path.Combine(destDir, destName);

            int i = 1;
            while (File.Exists(destPath))
            {
                var name = Path.GetFileNameWithoutExtension(destName);
                var ext = Path.GetExtension(destName);
                destPath = Path.Combine(destDir, $"{name}_{i}{ext}");
                i++;
            }
            File.Copy(ofd.FileName, destPath, overwrite: false);

            var rel = MakeRelToAssetsLocal(destPath);
            _articles.UpsertImage(_current.Id, slot, rel);

            LoadArticleImages();
        }

        private void Img_OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null || _current.Id <= 0) return;

            if (sender is System.Windows.Controls.MenuItem mi
                && int.TryParse(mi.Tag?.ToString(), out var slot))
            {
                var list = _articles.GetImagePaths(_current.Id);
                var it = list.FirstOrDefault(x => x.Slot == slot);
                var folder = Path.Combine(Paths.AssetsDir, "Images", "Articles", _current.Id.ToString());
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                try
                {
                    if (!string.IsNullOrWhiteSpace(it.FullPath) && File.Exists(it.FullPath))
                    {
                        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{it.FullPath}\"") { UseShellExecute = true });
                        return;
                    }
                    Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
                }
                catch { /* ignore */ }
            }
        }

        private void Img_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null || _current.Id <= 0) return;

            if (sender is System.Windows.Controls.MenuItem mi
                && int.TryParse(mi.Tag?.ToString(), out var slot))
            {
                if (MessageBox.Show($"Supprimer l'image du slot {slot} ?", "Confirmation",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;

                var list = _articles.GetImagePaths(_current.Id);
                var it = list.FirstOrDefault(x => x.Slot == slot);
                if (!string.IsNullOrWhiteSpace(it.FullPath) && File.Exists(it.FullPath))
                {
                    try { File.Delete(it.FullPath); } catch { /* ignore */ }
                }

                _articles.DeleteImage(_current.Id, slot);
                LoadArticleImages();
            }
        }



        // --------------- Init ---------------
        private void LoadConfigAndBinds()
        {
            var cfg = ConfigService.Get();
            _taxMode = string.IsNullOrWhiteSpace(cfg.TaxMode) ? "Micro" : cfg.TaxMode;

            bool isMicro = string.Equals(_taxMode, "Micro", StringComparison.OrdinalIgnoreCase);
            TvaBox.IsEnabled = !isMicro;
            CotisationBox.IsEnabled = isMicro;

            CotisationBox.ItemsSource = _catalogs.GetCotisationTypes();
            TvaBox.ItemsSource = _catalogs.GetTvaRates(); // peut être vide si non implémenté
            CategorieBox.ItemsSource = _catalogs.GetCategories();
        }

        private void ReloadList(string? search = null)
        {
            _items = new ObservableCollection<Article>(_articles.GetAll(search));
            GridArticles.ItemsSource = _items;
        }

        private void NewArticle()
        {
            _current = new Article { Actif = true, Type = ArticleType.Article, DerniereMaj = DateOnly.FromDateTime(DateTime.Now) };
            BindToForm();
        }

        private void BindToForm()
        {
            if (_current == null) return;
            var t = _current.Type;
            TypeArticleRadio.IsChecked = (t == ArticleType.Article);
            TypePackRadio.IsChecked = (t == ArticleType.Pack);
            var a = _current!;
            SkuBox.Text = _current.Code;
            LibelleBox.Text = _current.Libelle;
            CategorieBox.SelectedValue = a.CategorieId;
            TvaBox.SelectedValue = a.TvaRateId;
            CotisationBox.SelectedValue = a.CotisationRateId;
            PrixAchatBox.Text = a.PrixAchatHT.ToString("0.00", CultureInfo.InvariantCulture);
            PrixVenteBox.Text = a.PrixVenteHT.ToString("0.00", CultureInfo.InvariantCulture);
            PoidsBox.Text = a.PoidsG.ToString("0", CultureInfo.InvariantCulture);
            StockBox.Text = a.StockActuel.ToString("0", CultureInfo.InvariantCulture);
            SeuilBox.Text = a.SeuilAlerte.ToString("0", CultureInfo.InvariantCulture);
            ActifBox.IsChecked = a.Actif;
            DerniereMajText.Text = a.DerniereMaj.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            CodeBarresBox.Text = a.CodeBarres ?? string.Empty;

            // Variantes & Pack
            _variants = new ObservableCollection<ArticleVariant>(_articles.GetVariants(a.Id));
            GridVariants.ItemsSource = _variants;
            _pack = new ObservableCollection<PackItem>(_articles.GetPackItems(a.Id));
            GridPack.ItemsSource = _pack;

            UpdateComputedStates();
            RefreshPrixConseille();
            LoadArticleImages();
        }

        private void UpdateComputedStates()
        {
            bool hasVariants = _variants.Count > 0;
            var t = _current?.Type ?? ArticleType.Article;
            bool isPack = (t == ArticleType.Pack);

            // Champs grisés
            bool greyPA_PV_Stock_Seuil = hasVariants;
            PrixAchatBox.IsEnabled = !greyPA_PV_Stock_Seuil && !isPack;
            PrixVenteBox.IsEnabled = !greyPA_PV_Stock_Seuil;
            StockBox.IsEnabled = !greyPA_PV_Stock_Seuil && !isPack;
            SeuilBox.IsEnabled = !greyPA_PV_Stock_Seuil;

            PoidsBox.IsEnabled = !isPack; // pack = calculé

            // Stock article = somme variantes (lecture seule visuelle)
            if (hasVariants)
            {
                var sum = _variants.Sum(v => v.StockActuel);
                StockBox.Text = sum.ToString("0", CultureInfo.InvariantCulture);
            }

            // Pack agrégats : déjà calculés côté service → on ne permet pas la saisie
            if (isPack)
            {
                PrixAchatBox.Text = _current!.PrixAchatHT.ToString("0.00", CultureInfo.InvariantCulture);
                StockBox.Text = _current!.StockActuel.ToString("0", CultureInfo.InvariantCulture);
                PoidsBox.Text = _current!.PoidsG.ToString("0", CultureInfo.InvariantCulture);
            }
        }

        // --------------- Actions barre ----------------
        private void BtnNew_Click(object sender, RoutedEventArgs e) => NewArticle();

        private void BtnReload_Click(object sender, RoutedEventArgs e)
        {
            ReloadList(SearchBox.Text);
            if (_current != null)
            {
                var again = _articles.GetAll().FirstOrDefault(x => x.Id == _current.Id);
                _current = again ?? _current;
                BindToForm();
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            // lire form → _current
            _current.Code = SkuBox.Text?.Trim() ?? "";
            _current.Libelle = LibelleBox.Text?.Trim() ?? "";
            _current.Type = (TypePackRadio.IsChecked == true) ? ArticleType.Pack : ArticleType.Article;
            _current.CategorieId = CategorieBox.SelectedValue as int?;
            _current.TvaRateId = TvaBox.SelectedValue as int?;
            _current.CotisationRateId = CotisationBox.SelectedValue as int?;
            _current.PrixAchatHT = ParseDec(PrixAchatBox.Text, 2);
            _current.PrixVenteHT = ParseDec(PrixVenteBox.Text, 2);
            _current.PoidsG = ParseDec(PoidsBox.Text, 0);
            _current.StockActuel = ParseDec(StockBox.Text, 0);
            _current.SeuilAlerte = ParseDec(SeuilBox.Text, 0);
            _current.Actif = ActifBox.IsChecked == true;
            _current.CodeBarres = string.IsNullOrWhiteSpace(CodeBarresBox.Text) ? null : CodeBarresBox.Text.Trim();

            if (_current.Id == 0)
            {
                var id = _articles.Insert(_current);
                _current.Id = id;
            }
            else
            {
                _articles.Update(_current);
            }

            // packs: recompute si pack
            if ((_current?.Type ?? ArticleType.Article) == ArticleType.Pack)
                _articles.RecomputePackAggregates(_current!.Id);

            ReloadList(SearchBox.Text);
            LoadArticleImages();
            BindToForm();
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null || _current.Id == 0) return;
            if (!_articles.Delete(_current.Id))
            {
                MessageBox.Show("Impossible de supprimer : l’article est utilisé par un pack.", "Suppression", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ReloadList(SearchBox.Text);
            NewArticle();
        }

        // --------------- Variantes ---------------
        private void BtnAddVariant_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null || _current.Id == 0) { MessageBox.Show("Enregistrer l’article d’abord."); return; }
            var v = new ArticleVariant
            {
                ArticleId = _current.Id,
                Nom = "",
                PrixAchatHT = 0,
                PrixVenteHT = 0,
                StockActuel = 0,
                SeuilAlerte = 0,
                CodeBarres = "" // obligatoire à l’édition
            };
            try
            {
                var id = _articles.InsertVariant(v);
                v.Id = id;
                _variants.Add(v);
                UpdateComputedStates();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Variante", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnSaveVariant_Click(object sender, RoutedEventArgs e)
        {
            if (GridVariants.SelectedItem is not ArticleVariant v) return;
            try
            {
                _articles.UpdateVariant(v);
                UpdateComputedStates();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Variante", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnDeleteVariant_Click(object sender, RoutedEventArgs e)
        {
            if (GridVariants.SelectedItem is not ArticleVariant v) return;
            _articles.DeleteVariant(v.Id);
            _variants.Remove(v);
            UpdateComputedStates();
        }

        // --------------- Pack composition ---------------
        private void BtnAddToPack_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null || _current.Id == 0) { MessageBox.Show("Enregistrer l’article pack d’abord."); return; }
            var dlg = new SelectPackItemsWindow(_articles);
            var ok = dlg.ShowDialog();
            if (ok != true) return;

            foreach (var (articleId, variantId, q) in dlg.SelectedRows)
            {
                _articles.UpsertPackItem(_current.Id, articleId, variantId, q);
            }
            _articles.RecomputePackAggregates(_current.Id);
            _pack = new ObservableCollection<PackItem>(_articles.GetPackItems(_current.Id));
            GridPack.ItemsSource = _pack;
            UpdateComputedStates();
        }

        private void BtnRemovePackItem_Click(object sender, RoutedEventArgs e)
        {
            if (GridPack.SelectedItem is not PackItem it) return;
            _articles.DeletePackItem(it.Id);
            _articles.RecomputePackAggregates(_current!.Id);
            _pack.Remove(it);
            UpdateComputedStates();
        }

        private void BtnUpdatePackQty_Click(object sender, RoutedEventArgs e)
        {
            if (GridPack.SelectedItem is not PackItem it) return;
            if (!decimal.TryParse(QtyBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var q) || q < 1)
            {
                MessageBox.Show("Quantité invalide (>=1)");
                return;
            }
            _articles.UpdatePackItem(it.Id, q);
            _articles.RecomputePackAggregates(_current!.Id);
            it.Quantite = (double)q;
            GridPack.Items.Refresh();
            UpdateComputedStates();
        }

        // --------------- Prix conseillé ---------------
        private void RefreshPrixConseille()
        {
            if (_current == null) return;
            bool isTva = TvaBox.IsEnabled; // piloté par TaxMode
            decimal pa = ParseDec(PrixAchatBox.Text, 2);

            decimal taux = 0m;
            if (isTva)
            {
                var r = _catalogs.GetRateById(_current.TvaRateId);
                taux = r;
                // En TVA, conseillé = 2*PA (taux non appliqué dans la formule HT)
                var pv = 2m * pa;
                PrixConseilleBox.Text = pv.ToString("0.00", CultureInfo.InvariantCulture);
            }
            else
            {
                var r = _catalogs.GetRateById(_current.CotisationRateId);
                taux = r / 100m;
                if (1 - taux <= 0m) { PrixConseilleBox.Text = "--"; return; }
                var pv = (2m * pa) / (1m - taux);
                PrixConseilleBox.Text = Math.Round(pv, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);
            }
        }

        // --------------- BareCode ---------------
        private void BtnGenBarcode_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null)
            {
                MessageBox.Show("Sélectionnez ou créez un article avant de générer un code-barres.");
                return;
            }

            // 1) Génère un EAN-13 valide (prefixe 200 = codes internes)
            //    Seed: on utilise le Code article si dispo pour stabiliser un peu
            string seed = string.IsNullOrWhiteSpace(_current.Code) ? string.Empty : _current.Code;
            string candidate = BarcodeUtil.GenerateEAN13(seed, prefix: "200");

            // 2) Collision check en BDD (Articles + Variantes)
            //    On tente quelques itérations si collision (très rare)
            const int maxTries = 5;
            int tries = 0;
            while (_articles.BarcodeExists(candidate, excludeArticleId: _current.Id) && tries < maxTries)
            {
                candidate = BarcodeUtil.GenerateEAN13(Guid.NewGuid().ToString("N"), prefix: "200");
                tries++;
            }

            if (_articles.BarcodeExists(candidate, excludeArticleId: _current.Id))
            {
                MessageBox.Show("Impossible de générer un code-barres unique après plusieurs tentatives. Réessayez.");
                return;
            }

            // 3) Affecte le champ (l'enregistrement se fera sur 'Enregistrer')
            CodeBarresBox.Text = candidate;
            CodeBarresBox.Focus();
            CodeBarresBox.CaretIndex = CodeBarresBox.Text.Length;
        }

        private void BtnScanBarcode_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null)
            {
                MessageBox.Show("Sélectionnez ou créez un article avant de scanner un code-barres.");
                return;
            }

            // 1) Récupère texte du presse-papiers
            string text = "";
            try
            {
                if (Clipboard.ContainsText())
                    text = Clipboard.GetText()?.Trim() ?? "";
            }
            catch { /* no-op */ }

            if (string.IsNullOrWhiteSpace(text))
            {
                // Fallback: on place le focus pour que la douchette tape dans le champ
                CodeBarresBox.Focus();
                MessageBox.Show("Presse-papiers vide. Scannez le code-barres dans le champ puis cliquez à nouveau, ou copiez-le (Ctrl+C) avant de cliquer.");
                return;
            }

            // 2) Garde seulement les chiffres (certaines douchettes ajoutent CR/LF)
            var onlyDigits = new string(text.Where(char.IsDigit).ToArray());

            // 3) Valide EAN-13
            if (onlyDigits.Length != 13 || !onlyDigits.All(char.IsDigit) || !BarcodeUtil.IsValidEAN13(onlyDigits))
            {
                MessageBox.Show($"Le contenu scanné n'est pas un EAN-13 valide : '{text}'.");
                return;
            }

            // 4) Collision check en BDD
            if (_articles.BarcodeExists(onlyDigits, excludeArticleId: _current.Id))
            {
                MessageBox.Show($"Ce code-barres ({onlyDigits}) est déjà utilisé.");
                return;
            }

            // 5) OK
            CodeBarresBox.Text = onlyDigits;
            CodeBarresBox.Focus();
            CodeBarresBox.CaretIndex = CodeBarresBox.Text.Length;
        }



        // --------------- Helpers ---------------
        private static decimal ParseDec(string? s, int decimals)
        {
            if (!decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v)) v = 0m;
            if (decimals == 0) return Math.Floor(v);
            return Math.Round(v, 2, MidpointRounding.AwayFromZero);
        }

        private void TypeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            _current.Type = (TypePackRadio.IsChecked == true) ? ArticleType.Pack : ArticleType.Article;
            UpdateComputedStates();
        }

        // --------------- UI events ---------------
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ReloadList(SearchBox.Text);
        private void GridArticles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridArticles.SelectedItem is Article a)
            {
                _current = a;
                BindToForm();
                LoadArticleImages();
            }
        }
        private void PrixAchatBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshPrixConseille();
        private void CotisationBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshPrixConseille();
        private void TvaBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshPrixConseille();
    }
}
