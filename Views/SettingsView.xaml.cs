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
        private ObservableCollection<PaymentTermVM> _pay = new();
        private readonly INumberingService _num = new NumberingService();
        private readonly BankAccountService _bank = new BankAccountService();
        private System.Collections.ObjectModel.ObservableCollection<AnnexVM> _annexes = new();
        private readonly SettingsCatalogService _catalog = new SettingsCatalogService();

        public SettingsView()
        {
            InitializeComponent();
            LoadGeneral();
            ShowPanel(PanelGeneral);
            LoadEmailSettings();
            LoadArticles();
            LoadBankAccounts();
            LoadPaymentTerms();
        }

        // ---------- Panels ----------
        private void ShowPanel(UIElement panel)
        {
            PanelGeneral.Visibility = (panel == PanelGeneral) ? Visibility.Visible : Visibility.Collapsed;
            PanelArticles.Visibility = (panel == PanelArticles) ? Visibility.Visible : Visibility.Collapsed;
            PanelDocs.Visibility = (panel == PanelDocs) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnShowGeneral_Click(object sender, RoutedEventArgs e) => ShowPanel(PanelGeneral);
        private void BtnShowArticles_Click(object sender, RoutedEventArgs e) => ShowPanel(PanelArticles);

        // ---------- Général JSON et BDD ----------
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

        private void LoadEmailSettings()
        {
            GridEmailAccounts.ItemsSource = _catalogs.GetEmailAccounts();
            GridEmailTemplates.ItemsSource = _catalogs.GetEmailTemplates();
        }

        // ==== EMAIL ACCOUNTS ====
        private EmailAccount? PromptEmailAccount(EmailAccount? src = null)
        {
            var win = new Window
            {
                Title = (src == null ? "Nouveau compte email" : "Modifier le compte email"),
                Width = 420,
                Height = 420,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };

            var grid = new Grid { Margin = new Thickness(12) };
            for (int i = 0; i < 10; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBox Txt(string? v, int r)
            {
                var tb = new TextBox { Text = v ?? "", Margin = new Thickness(4) };
                Grid.SetRow(tb, r); Grid.SetColumn(tb, 1);
                grid.Children.Add(tb);
                return tb;
            }
            void Lbl(string t, int r)
            {
                var lb = new Label { Content = t, Margin = new Thickness(4) };
                Grid.SetRow(lb, r); Grid.SetColumn(lb, 0);
                grid.Children.Add(lb);
            }

            int row = 0;
            Lbl("Nom d’affichage", row); var dn = Txt(src?.DisplayName, row++);
            Lbl("Adresse", row); var ad = Txt(src?.Address, row++);
            Lbl("SMTP Host", row); var sh = Txt(src?.SmtpHost, row++);
            Lbl("SMTP Port", row); var sp = Txt((src?.SmtpPort ?? 587).ToString(), row++);
            Lbl("SSL (true/false)", row); var ssl = Txt((src?.UseSsl ?? true).ToString(), row++);
            Lbl("Utilisateur", row); var us = Txt(src?.Username, row++);
            Lbl("Mot de passe", row); var pw = Txt(src?.Password, row++);
            Lbl("Par défaut (0/1)", row); var df = Txt(src?.IsDefault == true ? "1" : "0", row++);

            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(4) };
            var ok = new Button { Content = "OK", IsDefault = true, Margin = new Thickness(4) };
            var cancel = new Button { Content = "Annuler", IsCancel = true, Margin = new Thickness(4) };
            ok.Click += (_, __) => win.DialogResult = true;
            btns.Children.Add(ok); btns.Children.Add(cancel);
            Grid.SetRow(btns, row); Grid.SetColumnSpan(btns, 2);
            grid.Children.Add(btns);

            win.Content = grid;
            if (win.ShowDialog() != true) return null;

            if (!int.TryParse(sp.Text.Trim(), out var port)) port = 587;
            bool useSsl = !"false".Equals(ssl.Text.Trim(), StringComparison.OrdinalIgnoreCase);
            bool isDef = df.Text.Trim() == "1";

            return new EmailAccount
            {
                Id = src?.Id ?? 0,
                DisplayName = dn.Text.Trim(),
                Address = ad.Text.Trim(),
                SmtpHost = sh.Text.Trim(),
                SmtpPort = port,
                UseSsl = useSsl,
                Username = us.Text.Trim(),
                Password = pw.Text,
                IsDefault = isDef
            };
        }

        private void BtnAddEmailAccount_Click(object sender, RoutedEventArgs e)
        {
            var a = PromptEmailAccount(null);
            if (a == null) return;
            a.Id = _catalogs.InsertEmailAccount(a);
            LoadEmailSettings();
        }

        private void BtnEditEmailAccount_Click(object sender, RoutedEventArgs e)
        {
            if (GridEmailAccounts.SelectedItem is not EmailAccount sel) return;
            var upd = PromptEmailAccount(sel);
            if (upd == null) return;
            upd.Id = sel.Id;
            _catalogs.UpdateEmailAccount(upd);
            LoadEmailSettings();
        }

        private void BtnDeleteEmailAccount_Click(object sender, RoutedEventArgs e)
        {
            if (GridEmailAccounts.SelectedItem is not EmailAccount sel) return;
            if (MessageBox.Show($"Supprimer le compte « {sel.DisplayName} » ?", "Confirmer",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            _catalogs.DeleteEmailAccount(sel.Id);
            LoadEmailSettings();
        }

        // ==== EMAIL TEMPLATES ====
        private EmailTemplate? PromptEmailTemplate(EmailTemplate? src = null)
        {
            var win = new Window
            {
                Title = (src == null ? "Nouveau modèle email" : "Modifier le modèle"),
                Width = 520,
                Height = 520,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };

            var grid = new Grid { Margin = new Thickness(12) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Name
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Subject
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Body
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // IsHtml + buttons
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            void L(int r, string t)
            {
                var lb = new Label { Content = t, Margin = new Thickness(4) };
                Grid.SetRow(lb, r); Grid.SetColumn(lb, 0);
                grid.Children.Add(lb);
            }

            L(0, "Nom"); var name = new TextBox { Text = src?.Name ?? "", Margin = new Thickness(4) };
            Grid.SetRow(name, 0); Grid.SetColumn(name, 1); grid.Children.Add(name);

            L(1, "Objet"); var subj = new TextBox { Text = src?.Subject ?? "", Margin = new Thickness(4) };
            Grid.SetRow(subj, 1); Grid.SetColumn(subj, 1); grid.Children.Add(subj);

            L(2, "Corps"); var body = new TextBox { Text = src?.Body ?? "", Margin = new Thickness(4), AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetRow(body, 2); Grid.SetColumn(body, 1); grid.Children.Add(body);

            var chkHtml = new CheckBox { Content = "HTML", IsChecked = src?.IsHtml ?? true, Margin = new Thickness(4) };
            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var ok = new Button { Content = "OK", IsDefault = true, Margin = new Thickness(4) };
            var cancel = new Button { Content = "Annuler", IsCancel = true, Margin = new Thickness(4) };
            ok.Click += (_, __) => win.DialogResult = true;

            var bottom = new DockPanel { Margin = new Thickness(4) };
            DockPanel.SetDock(chkHtml, Dock.Left);
            DockPanel.SetDock(btns, Dock.Right);
            btns.Children.Add(ok); btns.Children.Add(cancel);
            bottom.Children.Add(chkHtml); bottom.Children.Add(btns);

            Grid.SetRow(bottom, 3); Grid.SetColumnSpan(bottom, 2);
            grid.Children.Add(bottom);

            win.Content = grid;
            if (win.ShowDialog() != true) return null;

            return new EmailTemplate
            {
                Id = src?.Id ?? 0,
                Name = name.Text?.Trim() ?? "",
                Subject = subj.Text?.Trim() ?? "",
                Body = body.Text ?? "",
                IsHtml = chkHtml.IsChecked == true
            };
        }

        private void BtnAddEmailTemplate_Click(object sender, RoutedEventArgs e)
        {
            var t = PromptEmailTemplate(null);
            if (t == null) return;
            t.Id = _catalogs.InsertEmailTemplate(t);
            LoadEmailSettings();
        }

        private void BtnEditEmailTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (GridEmailTemplates.SelectedItem is not EmailTemplate sel) return;
            var upd = PromptEmailTemplate(sel);
            if (upd == null) return;
            upd.Id = sel.Id;
            _catalogs.UpdateEmailTemplate(upd);
            LoadEmailSettings();
        }

        private void BtnDeleteEmailTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (GridEmailTemplates.SelectedItem is not EmailTemplate sel) return;
            if (MessageBox.Show($"Supprimer le modèle « {sel.Name} » ?", "Confirmer",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            _catalogs.DeleteEmailTemplate(sel.Id);
            LoadEmailSettings();
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

        // --- CompteBancaire ---
        private void LoadBankAccounts()
        {
            var list = _bank.GetAll();
            GridBank.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<BankAccount>(list);
        }

        private void BtnBankAdd_Click(object sender, RoutedEventArgs e)
        {
            // Ajoute une ligne en mémoire
            var oc = GridBank.ItemsSource as System.Collections.ObjectModel.ObservableCollection<BankAccount>;
            if (oc == null) return;
            oc.Add(new BankAccount { DisplayName = "Nouveau compte", Iban = "", Bic = "", Holder = "", BankName = "", IsDefault = oc.Count == 0 });
        }

        private void BtnBankSave_Click(object sender, RoutedEventArgs e)
        {
            var oc = GridBank.ItemsSource as System.Collections.ObjectModel.ObservableCollection<BankAccount>;
            if (oc == null) return;

            foreach (var b in oc)
            {
                if (b.Id == 0) b.Id = _bank.Insert(b);
                else _bank.Update(b);
            }

            // Si plusieurs IsDefault = true, garde seulement le premier
            var defaults = 0;
            foreach (var b in oc)
            {
                if (b.IsDefault)
                {
                    defaults++;
                    if (defaults > 1) { b.IsDefault = false; _bank.Update(b); }
                }
            }

            LoadBankAccounts();
            MessageBox.Show("Comptes bancaires enregistrés.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LoadPaymentTerms()
        {
            _pay = new ObservableCollection<PaymentTermVM>(
                _catalogs.GetPaymentTerms().Select(p => new PaymentTermVM
                {
                    Id = p.Id,
                    Name = p.Name,
                    Mode = p.Mode,              // "SIMPLE" | "DUAL"
                    SimpleDue = p.SimpleDue,    // "AT_ORDER" | "AT_DELIVERY" (si SIMPLE)
                    OrderPct = p.OrderPct,      // double? (si DUAL)
                    IsDefault = p.IsDefault,
                    Body = p.Body
                })
            );
            GridPay.ItemsSource = _pay;
        }

        // Annexe DEVIS
        private void LoadAnnexCatalog()
        {
            _annexes = new System.Collections.ObjectModel.ObservableCollection<AnnexVM>(
                _catalogs.GetAnnexCatalog().ConvertAll(x => new AnnexVM { Id = x.Id, Nom = x.Nom, CheminRelatif = x.CheminRelatif, Actif = x.Actif })
            );
            GridAnnexes.ItemsSource = _annexes;
        }

        private class AnnexVM
        {
            public int Id { get; set; }
            public string Nom { get; set; } = "";
            public string CheminRelatif { get; set; } = "";
            public bool Actif { get; set; }
        }

        private static void SelectByContent(ComboBox cb, string? value, string fallback = "MONTHLY")
        {
            var wanted = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            foreach (var item in cb.Items)
            {
                if (item is ComboBoxItem cbi &&
                    string.Equals(cbi.Content?.ToString(), wanted, StringComparison.OrdinalIgnoreCase))
                {
                    cb.SelectedItem = cbi;
                    return;
                }
            }
            // rien trouvé -> premier item
            if (cb.Items.Count > 0) cb.SelectedIndex = 0;
        }

        private void BtnBankDelete_Click(object sender, RoutedEventArgs e)
        {
            if (GridBank.SelectedItem is not BankAccount b) return;
            if (b.Id == 0)
            {
                var oc = GridBank.ItemsSource as System.Collections.ObjectModel.ObservableCollection<BankAccount>;
                oc?.Remove(b);
                return;
            }
            var ask = MessageBox.Show("Supprimer ce compte bancaire ?", "Confirmer",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ask != MessageBoxResult.Yes) return;

            _bank.Delete(b.Id);
            LoadBankAccounts();
        }

        // --- Btn pour les modalitée de réglement
        private void BtnPayAdd_Click(object sender, RoutedEventArgs e)
        {
            _pay.Add(new PaymentTermVM
            {
                Id = 0,
                Name = "Nouvelles modalités",
                Mode = "SIMPLE",
                SimpleDue = "AT_DELIVERY",
                OrderPct = null,
                IsDefault = (_pay.Count == 0), // 1ère ligne par défaut
                Body = ""
            });
        }

        private void BtnPaySave_Click(object sender, RoutedEventArgs e)
        {
            GridPay.CommitEdit(DataGridEditingUnit.Cell, true);
            GridPay.CommitEdit(DataGridEditingUnit.Row, true);

            foreach (var t in _pay)
            {
                // normalisation
                if (string.Equals(t.Mode, "SIMPLE", StringComparison.OrdinalIgnoreCase))
                {
                    t.OrderPct = null;
                    if (string.IsNullOrWhiteSpace(t.SimpleDue)) t.SimpleDue = "AT_DELIVERY";
                }
                else
                {
                    t.Mode = "DUAL";
                    if (t.OrderPct is null) t.OrderPct = 50.0;
                    t.SimpleDue = null;
                }

                // persistance
                if (t.Id == 0)
                    t.Id = _catalogs.InsertPaymentTerm(t.ToEntity());
                else
                    _catalogs.UpdatePaymentTerm(t.ToEntity());
            }

            // rechargement propre
            LoadPaymentTerms();
            MessageBox.Show("Modalités enregistrées.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnPayDelete_Click(object sender, RoutedEventArgs e)
        {
            if (GridPay.SelectedItem is not PaymentTermVM sel) return;
            if (sel.Id == 0)
            {
                _pay.Remove(sel);
                return;
            }
            var ask = MessageBox.Show("Supprimer ces modalités ?", "Confirmer",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ask != MessageBoxResult.Yes) return;

            _catalogs.DeletePaymentTerm(sel.Id);
            LoadPaymentTerms();
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

        // --- Email et Modeles ---


        // --- Devis et Facture ---
        private void BtnSaveFormats_Click(object sender, RoutedEventArgs e)
        {
            var devisPattern = (TxtPatternDevis.Text ?? string.Empty).Trim();
            var factPattern = (TxtPatternFact.Text ?? string.Empty).Trim();

            string GetScope(ComboBox cb)
                => (cb.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "MONTHLY";

            var devisScope = GetScope(CmbScopeDevis);   // "MONTHLY" / "YEARLY"
            var factScope = GetScope(CmbScopeFact);

            // 👉 INumberingService::SetFormat (pas AppConfig)
            _num.SetFormat("DEVI", devisPattern, devisScope);
            _num.SetFormat("FACT", factPattern, factScope);

            MessageBox.Show("Formats enregistrés.", "OK",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnSaveNextDevis_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtNextDevis.Text, out var n) && n >= 1)
            {
                var today = DateOnly.FromDateTime(DateTime.Now);
                _num.SetNextSeq("DEVI", today, n);
                MessageBox.Show("Compteur devis mis à jour.");
            }
        }

        private void BtnSaveNextFact_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtNextFact.Text, out var n) && n >= 1)
            {
                var today = DateOnly.FromDateTime(DateTime.Now);
                _num.SetNextSeq("FACT", today, n);
                MessageBox.Show("Compteur factures mis à jour.");
            }
        }
        private void BtnShowDocs_Click(object sender, RoutedEventArgs e)
        {
            // afficher le panneau Devis & Factures
            ShowPanel(PanelDocs);

            // aller sur l'onglet Paramètres et charger les valeurs
            if (TabsDocs != null) TabsDocs.SelectedItem = TabParams;
            LoadDocs();
            LoadAnnexCatalog();
        }
        private void LoadDocs()
        {
            // lire formats
            var (pDev, sDev) = _num.GetFormat("DEVI");
            var (pFac, sFac) = _num.GetFormat("FACT");

            TxtPatternDevis.Text = pDev;
            TxtPatternFact.Text = pFac;

            CmbScopeDevis.SelectedItem = CmbScopeDevis.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(i => string.Equals((string)i.Content, sDev, StringComparison.OrdinalIgnoreCase));
            CmbScopeFact.SelectedItem = CmbScopeFact.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(i => string.Equals((string)i.Content, sFac, StringComparison.OrdinalIgnoreCase));

            // lire compteurs de la période courante (scope appliqué dans NumberingService)
            var today = DateOnly.FromDateTime(DateTime.Now);
            TxtNextDevis.Text = _num.GetNextSeq("DEVI", today).ToString(CultureInfo.InvariantCulture);
            TxtNextFact.Text = _num.GetNextSeq("FACT", today).ToString(CultureInfo.InvariantCulture);
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

        private class PaymentTermVM
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Mode { get; set; } = "SIMPLE";           // "SIMPLE" | "DUAL"
            public string? SimpleDue { get; set; }                 // "AT_ORDER" | "AT_DELIVERY" (si SIMPLE)
            public double? OrderPct { get; set; }                  // 0..100 (si DUAL)
            public bool IsDefault { get; set; }
            public string Body { get; set; } = "";

            public VorTech.App.Models.PaymentTerm ToEntity() => new VorTech.App.Models.PaymentTerm
            {
                Id = this.Id,
                Name = this.Name,
                Mode = this.Mode,
                SimpleDue = this.SimpleDue,
                OrderPct = this.OrderPct,
                IsDefault = this.IsDefault,
                Body = this.Body
            };
        }

        // Bouton pour Annexes
        private void BtnAnnexAdd_Click(object sender, RoutedEventArgs e)
        {
            // 1) dialog fichier
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "PDF (*.pdf)|*.pdf",
                Multiselect = false
            };
            if (ofd.ShowDialog() != true) return;

            // 2) copie dans Assets/Annexes
            var fileName = System.IO.Path.GetFileName(ofd.FileName);
            var targetDir = System.IO.Path.Combine(Paths.AssetsDir, "Annexes");
            System.IO.Directory.CreateDirectory(targetDir);
            var targetFull = System.IO.Path.Combine(targetDir, fileName);

            // évite écrasement bête
            if (!System.IO.File.Exists(targetFull))
                System.IO.File.Copy(ofd.FileName, targetFull, false);

            var rel = System.IO.Path.Combine("Annexes", fileName).Replace("\\", "/");

            // 3) insert en BDD
            var id = _catalogs.InsertAnnex(fileName, rel, true);

            // 4) refresh UI
            LoadAnnexCatalog();
        }

        private void BtnAnnexRename_Click(object sender, RoutedEventArgs e)
        {
            if (GridAnnexes.SelectedItem is not AnnexVM a) return;
            var newName = Microsoft.VisualBasic.Interaction.InputBox("Nouveau nom :", "Renommer l'annexe", a.Nom);
            if (string.IsNullOrWhiteSpace(newName) || newName.Trim() == a.Nom) return;
            _catalogs.UpdateAnnex(a.Id, newName.Trim());
            LoadAnnexCatalog();
        }

        private void BtnAnnexDelete_Click(object sender, RoutedEventArgs e)
        {
            if (GridAnnexes.SelectedItem is not AnnexVM a) return;
            if (MessageBox.Show($"Supprimer « {a.Nom} » du catalogue ? (le fichier reste sur disque)",
                "Confirmer", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            _catalogs.DeleteAnnex(a.Id);
            LoadAnnexCatalog();
        }

    }
}
