using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
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

        // ------------------- NEW: images en mémoire pour le rendu -------------------
        private record ImgRow(int Slot, string RelPath, string FullPath);
        private readonly ObservableCollection<ImgRow> _images = new();

        private void TvaBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ReadFormToCurrent();
            RefreshPrixConseille();
        }

        public ArticlesView()
        {
            InitializeComponent();

            // évite tout “null ref” sur les Combo
            CategorieBox.ItemsSource = _catalogs.GetCategories().ToList();
            TvaBox.ItemsSource = _catalogs.GetTvaRates().ToList();
            CotisationBox.ItemsSource = _catalogs.GetCotisationRates().ToList();

            TvaBox.IsEnabled = false;      // Micro => désactivée par défaut
            CotisationBox.IsEnabled = true;

            Reload();
            New();
            HideStatus();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) { }

        private void HideStatus()
        {
            if (StatusPanelOk != null) StatusPanelOk.Visibility = Visibility.Collapsed;
            if (StatusPanelErr != null) StatusPanelErr.Visibility = Visibility.Collapsed;
        }
        private void ShowOk(string msg)
        {
            if (StatusOkText != null) StatusOkText.Text = msg;
            if (StatusPanelErr != null) StatusPanelErr.Visibility = Visibility.Collapsed;
            if (StatusPanelOk != null) StatusPanelOk.Visibility = Visibility.Visible;
        }
        private void ShowErr(string msg)
        {
            if (StatusErrText != null) StatusErrText.Text = msg;
            if (StatusPanelOk != null) StatusPanelOk.Visibility = Visibility.Collapsed;
            if (StatusPanelErr != null) StatusPanelErr.Visibility = Visibility.Visible;
        }

        // ---------- RELOAD / BIND ----------
        private void Reload()
        {
            var all = _articles.GetAll().ToList();
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
            _packItems = new ObservableCollection<PackItem>(_articles.GetPackItems(a.Id));
            GridVariants.ItemsSource = _variants;
            GridPack.ItemsSource = _packItems;

            RefreshImages(); // <-- NEW : à chaque bind, on recharge les vignettes
                             // Variantes : griser stock/seuil article et afficher la somme des stocks variantes
            if (_variants != null && _variants.Count > 0)
            {
                var sum = _variants.Sum(v => v.StockActuel);
                StockBox.IsEnabled = false;
                SeuilBox.IsEnabled = false;
                StockBox.Text = sum.ToString("0.##", CultureInfo.InvariantCulture);
            }
            else
            {
                StockBox.IsEnabled = true;
                SeuilBox.IsEnabled = true;
            }
            RefreshPrixConseille();
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

            _current.Code = CodeBox.Text.Trim();
            _current.Libelle = LibelleBox.Text.Trim();
            _current.Type = (TypeArticleRadio.IsChecked == true) ? ArticleType.Article : ArticleType.Pack;

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

            if (_current.Id == 0)
            {
                _current = _articles.Insert(_current);
            }
            else
            {
                _articles.Update(_current);
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

        private void BtnNew_Click(object sender, RoutedEventArgs e) => New();
        private void BtnSave_Click(object sender, RoutedEventArgs e) => Save();
        private void BtnReload_Click(object sender, RoutedEventArgs e) => Reload();
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
        }

        // Recalcule le prix conseillé quand la cotisation change
        private void CotisationBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ReadFormToCurrent();
            RefreshPrixConseille();
        }

        // ------------------- VARIANTES (handlers manquants ajoutés) -------------------
        private void BtnAddVariant_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null || _current.Id == 0)
            {
                ShowErr("Enregistre d'abord l'article avant d'ajouter une variante.");
                return;
            }

            // Nom par défaut = "Variante N" (tu pourras brancher une saisie plus tard)
            var name = $"Variante {_variants.Count + 1}";
            var v = new ArticleVariant
            {
                ArticleId = _current.Id,
                Nom = name,
                PrixVenteHT = _current.PrixVenteHT,   // base sur prix article par défaut
                CodeBarres = null
            };

            v = _articles.InsertVariant(v);
            _variants.Add(v);
            GridVariants.ItemsSource = _variants;
            GridVariants.SelectedItem = v;
            GridVariants.ScrollIntoView(v);
            ShowOk("Variante ajoutée.");
        }

        private void BtnRemoveVariant_Click(object sender, RoutedEventArgs e)
        {
            var v = GridVariants?.SelectedItem as ArticleVariant;
            if (v == null)
            {
                ShowErr("Sélectionne une variante à supprimer.");
                return;
            }

            if (MessageBox.Show($"Supprimer la variante \"{v.Nom}\" ?", "Confirmation",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            _articles.DeleteVariant(v.Id);
            _variants.Remove(v);
            ShowOk("Variante supprimée.");
        }

        // ------------------- IMAGES -------------------

        // 1) Import (lié à ton bouton existant)
        private void BtnAddImage_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null || _current.Id == 0 || string.IsNullOrWhiteSpace(_current.Code))
            {
                ShowErr("Enregistre d'abord l'article (Code requis).");
                return;
            }

            var dlg = new OpenFileDialog
            {
                Filter = "Images|*.jpg;*.jpeg;*.png;*.webp;*.bmp",
                Multiselect = true
            };
            if (dlg.ShowDialog() != true) return;

            // slots DB existants → on comble les trous (1..4)
            var existing = _articles.GetImages(_current.Id); // (Slot, RelPath)
            var used = existing.Select(i => i.Slot).ToHashSet();
            int NextFreeSlot()
            {
                for (int s = 1; s <= 4; s++) if (!used.Contains(s)) return s;
                return -1;
            }

            var targetDir = Path.Combine(Paths.AssetsDir, "Images", "Articles", _current.Code);
            Directory.CreateDirectory(targetDir);

            foreach (var src in dlg.FileNames)
            {
                int slot = NextFreeSlot();
                if (slot == -1) break;

                var ext = Path.GetExtension(src).ToLowerInvariant();
                if (string.IsNullOrEmpty(ext)) ext = ".jpg";

                var fileName = $"img{slot}{ext}";
                var dest = Path.Combine(targetDir, fileName);
                File.Copy(src, dest, true);

                var rel = Path.Combine("Assets", "Images", "Articles", _current.Code, fileName).Replace('\\', '/');
                _articles.UpsertImage(_current.Id, slot, rel);
                used.Add(slot);
            }

            RefreshImages();
            ShowOk("Image(s) ajoutée(s).");
        }

        // 2) Supprimer une image (depuis menu contextuel)
        private void DeleteImage(int slot)
        {
            if (_current == null || _current.Id == 0) return;
            if (MessageBox.Show("Supprimer cette image ?", "Confirmation",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            // Supprimer le fichier si présent
            var img = _images.FirstOrDefault(x => x.Slot == slot);
            if (img != null && File.Exists(img.FullPath))
            {
                try { File.Delete(img.FullPath); } catch { /* ignore */ }
            }

            _articles.DeleteImage(_current.Id, slot);
            RefreshImages();
            ShowOk("Image supprimée.");
        }

        // 3) Ouvrir le dossier image (utilitaire)
        private void OpenFolder()
        {
            if (_current == null || string.IsNullOrWhiteSpace(_current.Code)) return;
            var dir = Path.Combine(Paths.AssetsDir, "Images", "Articles", _current.Code);
            Directory.CreateDirectory(dir);
            try { Process.Start("explorer.exe", dir); } catch { }
        }

        // 4) Aperçu plein écran
        private void OpenPreview(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath)) return;
            var w = new Window
            {
                Title = "Aperçu",
                Width = 900,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current?.MainWindow,
                Background = System.Windows.Media.Brushes.Black,
                Content = new ScrollViewer
                {
                    Content = new Image
                    {
                        Source = new BitmapImage(new Uri(fullPath)),
                        Stretch = System.Windows.Media.Stretch.Uniform
                    }
                }
            };
            w.ShowDialog();
        }

        // 5) Chargement & rendu des vignettes
        private void RefreshImages()
        {
            _images.Clear();
            if (_current == null || _current.Id == 0) { RenderImages(); return; }

            var list = _articles.GetImages(_current.Id); // slot + relPath
            foreach (var it in list.OrderBy(i => i.Slot))
            {
                var full = Path.Combine(AppContext.BaseDirectory, it.RelPath.Replace('/', Path.DirectorySeparatorChar));
                _images.Add(new ImgRow(it.Slot, it.RelPath, full));
            }
            RenderImages();
        }

        // Sauvegarde “au fil de l’eau”
        private void GridVariants_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            Dispatcher.InvokeAsync(() =>
            {
                if (GridVariants?.SelectedItem is ArticleVariant v)
                {
                    _articles.UpdateVariant(v);
                    // si le stock/seuil a changé : mettre à jour la somme affichée
                    var sum = _variants.Sum(x => x.StockActuel);
                    StockBox.Text = sum.ToString("0.##", CultureInfo.InvariantCulture);
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void GridVariants_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            if (e.Row?.Item is ArticleVariant v)
            {
                _articles.UpdateVariant(v);
                var sum = _variants.Sum(x => x.StockActuel);
                StockBox.Text = sum.ToString("0.##", CultureInfo.InvariantCulture);
            }
        }

        private void GridVariants_CurrentCellChanged(object sender, EventArgs e)
        {
            if (GridVariants?.CurrentItem is ArticleVariant v)
            {
                _articles.UpdateVariant(v);
                var sum = _variants.Sum(x => x.StockActuel);
                StockBox.Text = sum.ToString("0.##", CultureInfo.InvariantCulture);
            }
        }

        // Génération EAN-13 (menu contextuel)
        private void Variant_GenBarcode_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            var artCb = BarcodeBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(artCb) || artCb.Length < 12)
            {
                ShowErr("Génère (ou saisis) d'abord le code-barres de l'article.");
                return;
            }

            // Récupère la variante de la ligne
            var fe = sender as FrameworkElement;
            var v = fe?.DataContext as ArticleVariant;
            if (v == null) return;

            // base = 12 premiers chiffres du code article
            var base12 = new string(artCb.Where(char.IsDigit).Take(12).ToArray());
            if (base12.Length < 12)
            {
                ShowErr("Le code article doit contenir au moins 12 chiffres.");
                return;
            }

            // index de variante 1..99 selon l'ordre actuel dans la grille
            int index = Math.Max(1, _variants.IndexOf(v) + 1); // 1-based
            string suffix2 = (index % 100).ToString("00", CultureInfo.InvariantCulture);

            // remplace les 2 derniers digits du base12 par suffix2
            var core12 = base12.Substring(0, 10) + suffix2;

            // calcule la clé EAN-13
            int sum = 0;
            for (int i = 0; i < core12.Length; i++)
            {
                int digit = core12[i] - '0';
                sum += (i % 2 == 0) ? digit : digit * 3; // index 0 = position 1
            }
            int check = (10 - (sum % 10)) % 10;

            v.CodeBarres = core12 + check.ToString(CultureInfo.InvariantCulture);
            _articles.UpdateVariant(v);

            ShowOk("Code-barres variante généré.");
        }

        private void Variant_Codebar_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // Laisse le menu s'ouvrir même si CodeBarres est vide (utile pour "Générer").
        }

        private void RenderImages()
        {
            if (ThumbsList == null) return;
            ThumbsList.ItemsSource = null;
            ThumbsList.ItemsSource = _images;
        }

        // ------------------- PRIX CONSEILLÉ -------------------
        private void RefreshPrixConseille()
        {
            if (_current == null)
            {
                PrixConseilleLabel.Text = "";
                return;
            }

            _current.TvaRateId = TvaBox.SelectedValue as int?;
            _current.CotisationRateId = CotisationBox.SelectedValue as int?;

            bool isTvaEnabled = TvaBox.IsEnabled;

            var tauxTva = _catalogs.GetRateById(_current.TvaRateId);
            var tauxCot = _catalogs.GetRateById(_current.CotisationRateId);

            var p = _current.GetPrixConseilleHT(isTvaEnabled, tauxTva, tauxCot);
            PrixConseilleLabel.Text = p == 0m ? "-" : p.ToString("0.00", CultureInfo.InvariantCulture);
        }

        // ------------------- EVENTS pour la liste de vignettes -------------------
        private void Thumb_Open(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is string path) OpenPreview(path);
        }

        private void Thumb_Delete(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is int slot) DeleteImage(slot);
        }

        private void Thumb_OpenFolder(object sender, RoutedEventArgs e)
        {
            OpenFolder();
        }

        private void PrixAchatBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ReadFormToCurrent();
            RefreshPrixConseille();
        }
    }
}
