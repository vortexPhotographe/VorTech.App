using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VorTech.App.Models;
using VorTech.App.Services;
using System.Runtime.Versioning;

namespace VorTech.App.Views
{
    public partial class FacturesView : UserControl
    {
        private readonly FactureService _factures = new FactureService();
        private readonly INumberingService _num = new NumberingService();
        private readonly ArticleService _articles = new ArticleService();
        private readonly ClientService _clients = new ClientService();
        private readonly BankAccountService _bank = new BankAccountService();
        private readonly SettingsCatalogService _catalog = new SettingsCatalogService();

        private Facture? _current;
        private List<FactureLigne> _lines = new();

        public FacturesView()
        {
            InitializeComponent();

            // datasources
            CmbBank.ItemsSource = _bank.GetAll();
            CmbPayment.ItemsSource = _catalog.GetPaymentTerms(); // Display Name, SelectedValue=Id

            LoadList(null);
            NewDraft();
        }

        // Liste
        private void LoadList(string? search)
        {
            var items = new List<Facture>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            if (string.IsNullOrWhiteSpace(search))
                cmd.CommandText = "SELECT Id, Numero, Etat, Date, RemiseGlobale, Total FROM Factures WHERE DeletedAt IS NULL ORDER BY Date DESC, Id DESC LIMIT 500;";
            else
            {
                cmd.CommandText = "SELECT Id, Numero, Etat, Date, RemiseGlobale, Total FROM Factures WHERE DeletedAt IS NULL AND (Numero LIKE @q) ORDER BY Date DESC, Id DESC LIMIT 500;";
                Db.AddParam(cmd, "@q", "%" + search + "%");
            }
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                items.Add(new Facture
                {
                    Id = Convert.ToInt32(rd["Id"]),
                    Numero = rd["Numero"]?.ToString(),
                    Etat = rd["Etat"]?.ToString() ?? "Brouillon",
                    Date = DateOnly.Parse(rd["Date"]?.ToString() ?? DateTime.Today.ToString("yyyy-MM-dd")),
                    RemiseGlobale = Convert.ToDecimal(rd["RemiseGlobale"] ?? 0m, CultureInfo.InvariantCulture),
                    Total = Convert.ToDecimal(rd["Total"] ?? 0m, CultureInfo.InvariantCulture)
                });
            }
            FactureList.ItemsSource = items;
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

        // Fiche
        private void NewDraft()
        {
            var id = _factures.CreateDraft(null);
            LoadFacture(id);
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
            DataContext = new { SelectedFacture = _current };
            try { CmbBank.SelectedValue = _current?.GetType().GetProperty("BankAccountId")?.GetValue(_current) ?? null; } catch { }
            try { CmbPayment.SelectedValue = _current?.PaymentTermsId; } catch { }
            GridLines.ItemsSource = _lines;

            TxtRemise.Text = (_current?.RemiseGlobale ?? 0m).ToString("0.##", CultureInfo.InvariantCulture);
            TxtTotal.Text = $"Total : {(_current?.Total ?? 0m):0.##} €";
        }

        // Client
        private void SelectClient_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;

            var w = new ClientPickerWindow { Owner = Window.GetWindow(this) };
            if (w.ShowDialog() == true && w.Selected != null)
            {
                _factures.SetClientSnapshot(_current.Id, w.Selected.Id);
                _current = _factures.GetById(_current.Id);
                BindCurrent();
                LoadList(SearchBox.Text?.Trim());
            }
        }

        private void QuickClient_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;

            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE Factures SET ClientId=NULL WHERE Id=@id;";
            Db.AddParam(cmd, "@id", _current.Id);
            cmd.ExecuteNonQuery();

            _current = _factures.GetById(_current.Id);
            BindCurrent();
            LoadList(SearchBox.Text?.Trim());
        }

        private void ClientFields_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;

            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE Factures SET
  ClientSociete=@cs, ClientNomPrenom=@cnp, ClientAdresseL1=@cadr,
  ClientCodePostal=@cp, ClientVille=@cv, ClientEmail=@ce, ClientTelephone=@ctel
WHERE Id=@id;";
            Db.AddParam(cmd, "@cs", TxtSociete.Text);
            Db.AddParam(cmd, "@cnp", TxtNomPrenom.Text);
            Db.AddParam(cmd, "@cadr", TxtAdresse.Text);
            Db.AddParam(cmd, "@cp", TxtCp.Text);
            Db.AddParam(cmd, "@cv", TxtVille.Text);
            Db.AddParam(cmd, "@ce", TxtEmail.Text);
            Db.AddParam(cmd, "@ctel", TxtTel.Text);
            Db.AddParam(cmd, "@id", _current!.Id);
            cmd.ExecuteNonQuery();

            _current = _factures.GetById(_current.Id);
            BindCurrent();
            LoadList(SearchBox.Text?.Trim());
        }

        // Modalités / banque
        private void CmbPayment_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_current == null) return;
            int? paymentId = null;
            var val = CmbPayment.SelectedValue as int?;
            if (val.HasValue) paymentId = val.Value;
            _factures.SetPaymentTerms(_current.Id, paymentId);
            _current = _factures.GetById(_current.Id);
            BindCurrent();
        }

        private void CmbBank_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_current == null) return;
            var bid = CmbBank.SelectedValue as int?;
            _factures.SetBankAccount(_current.Id, bid);
            _current = _factures.GetById(_current.Id);
            BindCurrent();
        }

        // Lignes
        private void AddLine_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            var w = new ArticlePickerWindow { Owner = Window.GetWindow(this) };
            if (w.ShowDialog() == true)
            {
                var a = _articles.GetById(w.SelectedArticleId);
                if (a == null) return;

                ArticleVariant? v = null;
                if (w.SelectedVariantId.HasValue)
                    v = _articles.GetVariantById(w.SelectedVariantId.Value);

                var designation = v == null ? (a.Libelle ?? $"Article #{a.Id}")
                                            : $"{a.Libelle} — {v.Nom}";

                var pu = v?.PrixVenteHT ?? a.PrixVenteHT;
                int? variantId = w.SelectedVariantId;

                _factures.AddLine(_current.Id, designation, 1m, pu, a.Id, variantId, a.CotisationRateId, null);
                _lines = _factures.GetLines(_current.Id);
                _current = _factures.GetById(_current.Id);
                BindCurrent();
                LoadList(SearchBox.Text?.Trim());
            }
        }

        private void DuplicateLine_Click(object sender, RoutedEventArgs e)
        {
            if (GridLines.SelectedItem is not FactureLigne l || _current == null) return;
            _factures.AddLine(_current.Id, l.Designation, l.Qty, l.PU, l.ArticleId, l.VarianteId, l.CotisationRateId, l.DevisLigneId);
            _lines = _factures.GetLines(_current.Id);
            _current = _factures.GetById(_current.Id);
            BindCurrent();
            LoadList(SearchBox.Text?.Trim());
        }

        private void DeleteLine_Click(object sender, RoutedEventArgs e)
        {
            if (GridLines.SelectedItem is not FactureLigne l) return;
            _factures.DeleteLine(l.Id);
            _lines = _factures.GetLines(_current!.Id);
            _current = _factures.GetById(_current.Id);
            BindCurrent();
            LoadList(SearchBox.Text?.Trim());
        }

        private void CellEditEnded(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            if (e.Row.Item is not FactureLigne l) return;

            _factures.UpdateLine(l.Id, l.Designation, l.Qty, l.PU);
            _lines = _factures.GetLines(l.FactureId);
            _current = _factures.GetById(l.FactureId);
            BindCurrent();
            LoadList(SearchBox.Text?.Trim());
        }

        // Totaux
        private void RemiseLostFocus(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            if (!decimal.TryParse(TxtRemise.Text?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var r))
                r = _current.RemiseGlobale;

            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE Factures SET RemiseGlobale=@r WHERE Id=@id;";
            Db.AddParam(cmd, "@r", r);
            Db.AddParam(cmd, "@id", _current.Id);
            cmd.ExecuteNonQuery();

            _factures.RecalcTotals(_current.Id);
            _current = _factures.GetById(_current.Id);
            BindCurrent();
            LoadList(SearchBox.Text?.Trim());
        }

        // Actions barre
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Déjà fait “au fil de l’eau” (perte de focus, édition grid).
            // Ici on force un recalcul + refresh.
            if (_current == null) return;
            _factures.RecalcTotals(_current.Id);
            _current = _factures.GetById(_current.Id);
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

            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE Factures SET DeletedAt=DATETIME('now') WHERE Id=@id;";
            Db.AddParam(cmd, "@id", _current.Id);
            cmd.ExecuteNonQuery();

            LoadList(SearchBox.Text?.Trim());
            NewDraft();
        }

        private void BtnEmit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_current == null) return;

                var numero = _factures.EmitNumero(_current.Id, _num); // décrément stock inclus
                _current = _factures.GetById(_current.Id);
                BindCurrent();
                LoadList(SearchBox.Text?.Trim());

                MessageBox.Show($"Numéro attribué : {numero}", "Facture numérotée",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Échec de l'émission : " + ex.Message, "Erreur",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [SupportedOSPlatform("windows")]
        private async void BtnSendMail_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_current == null || string.IsNullOrWhiteSpace(_current.Numero))
                {
                    MessageBox.Show("La facture n’est pas numérotée.", "Envoi e-mail", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                await _factures.SendAndLogAsync(_current.Id);
                _current = _factures.GetById(_current.Id);
                BindCurrent();
                LoadList(SearchBox.Text?.Trim());
                MessageBox.Show("E-mail envoyé.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Échec de l’envoi e-mail : " + ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [SupportedOSPlatform("windows")]
        private void BtnRegenPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_current == null || string.IsNullOrWhiteSpace(_current.Numero))
                {
                    MessageBox.Show("La facture n’est pas numérotée.", "PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var path = _factures.RegeneratePdf(_current.Id);
                if (File.Exists(path))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Échec PDF : " + ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
