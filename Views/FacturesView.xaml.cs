using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using VorTech.App.Models;
using VorTech.App.Services;

namespace VorTech.App.Views
{
    public partial class FacturesView : UserControl
    {
        private readonly FactureService _factures = new FactureService();
        private readonly NumberingService _num = new NumberingService();
        private readonly ArticleService _articles = new ArticleService();
        private readonly BankAccountService _bank = new BankAccountService();
        private readonly SettingsCatalogService _catalog = new SettingsCatalogService();

        private Facture? _current;
        private List<FactureLigne> _lines = new();

        public FacturesView()
        {
            InitializeComponent();

            // combobox data
            CmbBank.ItemsSource = _bank.GetAll();
            CmbPayment.ItemsSource = _catalog.GetPaymentTerms(); // DisplayMemberPath=Name, SelectedValuePath=Id

            LoadList(null);
            NewDraft();
        }

        // ===== List / Search =====
        private void LoadList(string? search)
        {
            // réutilise le même pattern que Devis : simple listing depuis service
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT Id, Numero, Etat, ClientSociete, ClientNomPrenom
FROM Factures
WHERE DeletedAt IS NULL
  AND (
        @q IS NULL
        OR Numero LIKE '%'||@q||'%'
        OR ClientSociete LIKE '%'||@q||'%'
        OR ClientNomPrenom LIKE '%'||@q||'%'
      )
ORDER BY Id DESC;";
            Db.AddParam(cmd, "@q", string.IsNullOrWhiteSpace(search) ? (object?)DBNull.Value : search);
            var list = new List<Facture>();
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new Facture
                {
                    Id = Convert.ToInt32(rd["Id"]),
                    Numero = rd["Numero"]?.ToString(),
                    Etat = rd["Etat"]?.ToString() ?? "Brouillon",
                    ClientSociete = rd["ClientSociete"]?.ToString(),
                    ClientNomPrenom = rd["ClientNomPrenom"]?.ToString()
                });
            }
            FactureList.ItemsSource = list;
        }

        private void OnSearchClick(object sender, RoutedEventArgs e) => LoadList(SearchBox.Text);
        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            var q = SearchBox.Text?.Trim();
            if (string.IsNullOrEmpty(q) || q.Length >= 2) LoadList(q);
        }

        private void OnListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FactureList.SelectedItem is Facture f) LoadFacture(f.Id);
        }

        // ===== Current =====
        private void NewDraft()
        {
            _current = new Facture
            {
                Date = DateOnly.FromDateTime(DateTime.Now),
                Etat = "Brouillon",
                RemiseGlobale = 0m,
                Total = 0m
            };
            BindCurrent();
        }

        private void LoadFacture(int id)
        {
            _current = _factures.GetById(id);
            _lines = _factures.GetLines(id);
            BindCurrent();
        }

        private void BindCurrent()
        {
            DataContext = _current;
            GridLines.ItemsSource = _lines;

            try { CmbBank.SelectedValue = _current?.BankAccountId; } catch { }
            try { CmbPayment.SelectedValue = _current?.PaymentTermsId; } catch { }
        }

        // ===== Destinataire =====
        private void SelectClient_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            EnsureCurrentId();

            var w = new ClientPickerWindow { Owner = Window.GetWindow(this) };
            if (w.ShowDialog() == true && w.Selected != null)
            {
                _factures.SetClientSnapshot(_current!.Id, w.Selected.Id, w.Selected);
                _current = _factures.GetById(_current.Id);
                BindCurrent();
                LoadList(SearchBox.Text?.Trim());
            }
        }

        private void ClientFields_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_current == null || _current.Id <= 0) return;

            _factures.UpdateClientFields(
                _current.Id,
                _current.ClientSociete,
                _current.ClientNomPrenom,
                _current.ClientAdresseL1,
                _current.ClientCodePostal,
                _current.ClientVille,
                _current.ClientEmail,
                _current.ClientTelephone
            );

            _current = _factures.GetById(_current.Id);
            BindCurrent();
            LoadList(SearchBox.Text?.Trim());
        }

        // ===== Lignes =====
        private void AddLine_Click(object sender, RoutedEventArgs e)
        {
            EnsureCurrentId();

            var w = new ArticlePickerWindow { Owner = Window.GetWindow(this) };
            if (w.ShowDialog() == true)
            {
                var a = _articles.GetById(w.SelectedArticleId);
                if (a == null) return;

                ArticleVariant? v = null;
                if (w.SelectedVariantId.HasValue)
                    v = _articles.GetVariantById(w.SelectedVariantId.Value);

                var designation = v == null
                    ? (a.Libelle ?? $"Article #{a.Id}")
                    : $"{a.Libelle} — {v.Nom}";

                var pu = v?.PrixVenteHT ?? a.PrixVenteHT;
                int? variantId = w.SelectedVariantId;

                _factures.AddLine(_current!.Id, designation, 1m, pu, a.Id, variantId, null, null);

                _lines = _factures.GetLines(_current.Id);
                _current = _factures.GetById(_current.Id);
                BindCurrent();
                LoadList(SearchBox.Text?.Trim());
            }
        }

        private void DuplicateLine_Click(object sender, RoutedEventArgs e)
        {
            if (GridLines.SelectedItem is not FactureLigne l || _current == null) return;

            _factures.AddLine(_current.Id, l.Designation ?? "", l.Qty, l.PU, l.ArticleId, l.VarianteId, l.CotisationRateId, l.DevisLigneId);
            _lines = _factures.GetLines(_current.Id);
            _current = _factures.GetById(_current.Id);
            BindCurrent();
            LoadList(SearchBox.Text?.Trim());
        }

        private void DeleteLine_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null || GridLines.SelectedItem is not FactureLigne l) return;

            _factures.DeleteLine(l.Id);
            _lines = _factures.GetLines(_current.Id);
            _current = _factures.GetById(_current.Id);
            BindCurrent();
            LoadList(SearchBox.Text?.Trim());
        }

        private void CellEditEnded(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            if (e.Row.Item is not FactureLigne l) return;

            _factures.UpdateLine(l.Id, l.Designation ?? "", l.Qty, l.PU);

            _lines = _factures.GetLines(l.FactureId);
            _current = _factures.GetById(l.FactureId);
            if (_current == null) return;

            BindCurrent();
            LoadList(SearchBox.Text?.Trim());
        }

        // ===== Remise & totaux =====
        private void RemiseLostFocus(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            EnsureCurrentId();

            var id = _current.Id;
            if (!decimal.TryParse(TxtRemise.Text?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var r))
                r = _current.RemiseGlobale;

            _factures.SetGlobalDiscount(id, r);
            _factures.RecalcTotals(id);

            _current = _factures.GetById(id);
            _lines = _factures.GetLines(id);
            BindCurrent();
            LoadList(SearchBox.Text?.Trim());
        }

        // ===== Enregistrer / Nouveau / Supprimer =====
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            EnsureCurrentId();
            var id = _current.Id;

            if (!decimal.TryParse(TxtRemise.Text?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var r))
                r = _current.RemiseGlobale;

            _factures.SetGlobalDiscount(id, r);
            _factures.RecalcTotals(id);

            // Snapshot client déjà poussé par bindings LostFocus, on renforce au cas où:
            _factures.UpdateClientFields(id,
                _current.ClientSociete, _current.ClientNomPrenom, _current.ClientAdresseL1,
                _current.ClientCodePostal, _current.ClientVille, _current.ClientEmail, _current.ClientTelephone);

            _current = _factures.GetById(id);
            _lines = _factures.GetLines(id);
            BindCurrent();
            LoadList(SearchBox.Text?.Trim());
        }

        private void New_Click(object sender, RoutedEventArgs e) => NewDraft();

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null || _current.Id <= 0) return;
            var ask = MessageBox.Show("Supprimer cette facture ? (corbeille 30 jours)", "Confirmer",
                                      MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ask != MessageBoxResult.Yes) return;

            _factures.SoftDelete(_current.Id);
            LoadList(SearchBox.Text?.Trim());
            NewDraft();
        }

        // ===== Combo changements =====
        private void CmbBank_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_current == null || _current.Id <= 0) return;
            var id = CmbBank.SelectedValue as int?;
            _factures.SetBankAccount(_current.Id, id);

            _current = _factures.GetById(_current.Id);
            BindCurrent();
        }

        private void CmbPayment_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_current == null) return;
            EnsureCurrentId();

            var val = CmbPayment.SelectedValue as int?;
            _factures.SetPaymentTerms(_current.Id, val);

            _current = _factures.GetById(_current.Id);
            BindCurrent();
        }

        // ===== Émission numéro + PDF =====
        private void CommitAllEdits()
        {
            GridLines.CommitEdit(DataGridEditingUnit.Cell, true);
            GridLines.CommitEdit(DataGridEditingUnit.Row, true);
        }

        [SupportedOSPlatform("windows")]
        private void BtnEmit_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info($"UI Factures: BtnEmit_Click (id={_current?.Id})");
            try
            {
                CommitAllEdits();
                Save_Click(this, new RoutedEventArgs());

                EnsureCurrentId();
                if (_current == null) return;

                Logger.Info("UI Factures: appel _factures.EmitNumero(...)");
                var numero = _factures.EmitNumero(_current.Id, _num);
                Logger.Info($"UI Factures: EmitNumero OK -> {numero}");

                // regen PDF après émission (si tu as déjà l’implémentation)
                BtnRegenPdf_Click(this, new RoutedEventArgs());
            }
            catch (Exception ex)
            {
                Logger.Error("UI Factures: EmitNumero KO", ex);
                MessageBox.Show("Échec de l'émission : " + ex.Message, "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [SupportedOSPlatform("windows")]
        private void BtnRegenPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null || string.IsNullOrWhiteSpace(_current.Numero))
                return;

            try
            {
                var pdfPath = _factures.BuildInvoicePdf(_current.Id);
                if (File.Exists(pdfPath))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(pdfPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Échec de la régénération du PDF : " + ex.Message, "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===== Util =====
        private void EnsureCurrentId()
        {
            if (_current == null) throw new InvalidOperationException("Facture non initialisée.");
            if (_current.Id > 0) return;

            var id = _factures.CreateDraft(_current.ClientId, null);
            _current = _factures.GetById(id);
        }

        [SupportedOSPlatform("windows")]
        private void BtnSendMail_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null || _current.Id <= 0) return;

            try
            {
                // 1) Regénère le PDF
                var pdfPath = _factures.BuildInvoicePdf(_current.Id);

                // 2) Ouvre le PDF pour envoi manuel (et/ou tu ajoutes ton EmailService plus tard)
                if (System.IO.File.Exists(pdfPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(pdfPath)
                    {
                        UseShellExecute = true
                    });
                }

                // 3) Marquer comme envoyé (statut + date)
                _factures.MarkSent(_current.Id);

                // Refresh
                _current = _factures.GetById(_current.Id);
                BindCurrent();
                LoadList(SearchBox.Text?.Trim());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Échec de l'envoi : " + ex.Message, "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
