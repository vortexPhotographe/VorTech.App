using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using VorTech.App.Models;
using VorTech.App.Services;

namespace VorTech.App.Views
{
    public partial class DevisView : UserControl
    {
        private readonly IDevisService _devis = new DevisService();
        private readonly INumberingService _num = new NumberingService();                                                                     // par celles-ci :
        private readonly ArticleService _articles = new ArticleService();
        private readonly ClientService _clients = new ClientService();
        private readonly BankAccountService _bank = new BankAccountService();

        private Devis? _current;
        private List<DevisLigne> _lines = new();
        private List<DevisAnnexe> _annexes = new();

        public DevisView()
        {
            InitializeComponent();
            CmbBank.ItemsSource = _bank.GetAll();
            LoadList(null);
            NewDraft(); // ouvre un brouillon vide par défaut
        }

        // --- Liste gauche ---
        private void LoadList(string? search)
        {
            var items = _devis.GetAll(search);
            DevisList.ItemsSource = items; // ListBox nommé dans ton XAML
        }

        private void OnSearchClick(object sender, RoutedEventArgs e)
        {
            LoadList(SearchBox.Text);
        }

        private void OnListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DevisList.SelectedItem is Devis d) LoadDevis(d.Id);
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            var q = SearchBox.Text?.Trim();
            if (string.IsNullOrEmpty(q) || q.Length >= 2)
                LoadList(q);
        }


        // --- Fiche ---
        private void NewDraft()
        {
            _current = new Devis
            {
                Date = DateOnly.FromDateTime(DateTime.Now),
                Etat = "Brouillon",
                RemiseGlobale = 0m,
                Total = 0m
            };
            BindCurrent();
        }

        private void LoadDevis(int id)
        {
            _current = _devis.GetById(id);
            _lines = _devis.GetLines(id);
            _annexes = _devis.GetAnnexes(id);
            BindCurrent();
        }

        private void BindCurrent()
        {
            if (_current != null)
                CmbBank.SelectedValue = _current.BankAccountId;
            DataContext = _current;
            GridLines.ItemsSource = _lines;
            ListAnnexes.ItemsSource = _annexes;
            RecalcCostsUi();

        }

        private void SelectClient_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            EnsureCurrentId(); // crée le brouillon s'il n'existe pas

            var w = new ClientPickerWindow { Owner = Window.GetWindow(this) };
            if (w.ShowDialog() == true && w.Selected != null)
            {
                _devis.SetClientSnapshot(_current!.Id, w.Selected.Id, w.Selected);
                _current = _devis.GetById(_current.Id);
                BindCurrent();
            }
        }

        private void ClientFields_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_current == null || _current.Id <= 0) return;
            _devis.UpdateClientFields(
                _current.Id,
                _current.ClientSociete,
                _current.ClientNomPrenom,
                _current.ClientAdresseL1,
                _current.ClientCodePostal,
                _current.ClientVille,
                _current.ClientEmail,
                _current.ClientTelephone
            );
        }

        // “Saisir rapide” = on efface le lien mais on laisse saisir à la main (non-lié)
        private void QuickClient_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            EnsureCurrentId();
            _devis.SetClientSnapshot(_current!.Id, null, null);
            _current = _devis.GetById(_current.Id);
            BindCurrent();
        }


        // --- Notes ---
        private void EditNoteHaut_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            var win = new NoteEditorWindow(_current.NoteHaut) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() == true)
            {
                _current.NoteHaut = win.ResultText;
                _devis.SetNotes(_current.Id, _current.NoteHaut, _current.NoteBas);
                BindCurrent();
            }
        }
        private void EditNoteBas_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            var win = new NoteEditorWindow(_current.NoteBas) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() == true)
            {
                _current.NoteBas = win.ResultText;
                _devis.SetNotes(_current.Id, _current.NoteHaut, _current.NoteBas);
                BindCurrent();
            }
        }

        // --- Lignes (boutons + édition) ---
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

                var pu = v?.PrixVenteHT ?? a.PrixVenteHT;   // PU depuis variante si choisie
                var variantId = w.SelectedVariantId;

                // image: on verra ensuite ; mets null propre, pas la chaîne "null"
                string? img = null;

                _devis.AddLine(_current!.Id, designation, 1m, pu, a.Id, variantId, img);

                _lines = _devis.GetLines(_current.Id);
                _current = _devis.GetById(_current.Id);
                BindCurrent();
                LoadList(SearchBox.Text?.Trim());
            }
        }

        private void DuplicateLine_Click(object sender, RoutedEventArgs e)
        {
            if (GridLines.SelectedItem is not DevisLigne l || _current == null) return;
            _devis.RecalcTotals(_current.Id);   // recalcul
            _lines = _devis.GetLines(_current.Id);
            _current = _devis.GetById(_current.Id);
            BindCurrent();
            LoadList(SearchBox.Text?.Trim());   // (facultatif mais recommandé)
        }

        private void DeleteLine_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            EnsureCurrentId();
            var id = _current.Id;
            var l = (DevisLigne?)GridLines.SelectedItem;
            if (l == null) return;
            _devis.DeleteLine(l.Id);
            // Recharger lignes + total
            _devis.RecalcTotals(_current.Id);   // recalcul
            _lines = _devis.GetLines(_current.Id);
            _current = _devis.GetById(_current.Id);
            BindCurrent();
            LoadList(SearchBox.Text?.Trim());   // (facultatif mais recommandé)
        }

        private void CellEditEnded(object sender, DataGridCellEditEndingEventArgs e)
        {
            // On n'agit que sur un commit réel (pas pendant l’édition/annulation)
            if (e.EditAction != DataGridEditAction.Commit) return;

            if (e.Row.Item is not DevisLigne l) return;

            // ID sûr pour toutes les opérations suivantes
            var devisId = l.DevisId;

            // Pousse la ligne éditée (les TextColumns sont TwoWay)
            _devis.UpdateLine(l.Id, l.Designation, l.Qty, l.PU);

            // Recalcule le total du devis via l'ID (pas _current)
            _devis.RecalcTotals(devisId);

            // Recharge proprement objets & UI
            _lines = _devis.GetLines(devisId);
            _current = _devis.GetById(devisId);  // peut être null si supprimé entre-temps

            if (_current == null)
                return; // le devis n'existe plus (sécurité)

            BindCurrent();
            LoadList(SearchBox.Text?.Trim());
        }

        // --- Remise globale ---
        private void RemiseLostFocus(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            EnsureCurrentId();
            var id = _current.Id;

            // lire la valeur saisie (virgule/point toléré)
            if (!decimal.TryParse(TxtRemise.Text?.Replace(',', '.'),
                                  NumberStyles.Any, CultureInfo.InvariantCulture, out var r))
                r = _current.RemiseGlobale;

            // écrire la remise + recalculer le total avec les bonnes méthodes
            _devis.SetGlobalDiscount(id, r);
            _devis.RecalcTotals(id);

            // recharger & rafraîchir l’UI
            _current = _devis.GetById(id);
            _lines = _devis.GetLines(id);
            BindCurrent();

            // rafraîchir la liste de gauche
            LoadList(SearchBox.Text?.Trim());
        }

        // --- Calcule cotisation et coup ---
        private void RecalcCostsUi()
        {
            decimal cout = 0m;
            decimal charges = 0m; // TODO: brancher plus tard sur CotisationRateId

            foreach (var l in _lines)
            {
                if (l.ArticleId is int aid)
                {
                    var a = _articles.GetById(aid);
                    if (a == null) continue;

                    decimal achat = a.PrixAchatHT;

                    // ICI : VarianteId (et pas VariantId)
                    if (l.VarianteId is int vid)
                    {
                        var v = _articles.GetVariantById(vid);
                        if (v != null) achat = v.PrixAchatHT;
                    }

                    cout += l.Qty * achat;
                }
            }

            var total = _current?.Total ?? 0m;
            var net = total - cout - charges;

            TxtCostMatiere.Text = $"Coût matières : {cout:0.##} €";
            TxtCharges.Text = $"Charges estimées : {charges:0.##} €";
            TxtMarge.Text = $"Marge nette : {net:0.##} €";
        }

        // --- Emission (numérotation) ---
        private void Emit_Click(object sender, RoutedEventArgs e)
        {
            // 1) s'assurer que l'ID est là
            EnsureCurrentId();
            var id = _current!.Id;

            // 2) émettre avec la numérotation
            var num = new NumberingService();                  // même type que dans SettingsView
            var finalPath = _devis.Emit(id, num);              // pose Numero + Etat='Envoye'

            // 3) recharger & rafraîchir UI/liste
            _lines = _devis.GetLines(id);
            _current = _devis.GetById(id);
            BindCurrent();
            LoadList(SearchBox.Text?.Trim());
        }

        // --- Utilitaires ---
        private void EnsureCurrentId()
        {
            if (_current == null) throw new InvalidOperationException("Devis non initialisé.");
            if (_current.Id > 0) return;

            // Créer en base le brouillon (avec snapshot client si tu as un Client sélectionné)
            var id = _devis.CreateDraft(_current.ClientId, null /* snapshot à brancher si client choisi */);
            _current = _devis.GetById(id);
        }

        // ---- SAVE Devis ---
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            EnsureCurrentId();
            var id = _current.Id;

            // 1) Sauver la remise (même si la TextBox a encore le focus)
            if (!decimal.TryParse(TxtRemise.Text?.Replace(',', '.'),
                                  NumberStyles.Any, CultureInfo.InvariantCulture, out var r))
                r = _current.RemiseGlobale;

            _devis.SetGlobalDiscount(id, r);  // ✅ la bonne méthode
            _devis.RecalcTotals(id);          // ✅ recalcul avec l’API existante

            // 2) Sauver les champs client
            _devis.UpdateClientFields(id,
                _current.ClientSociete, _current.ClientNomPrenom,
                _current.ClientAdresseL1, _current.ClientCodePostal, _current.ClientVille,
                _current.ClientEmail, _current.ClientTelephone);

            // 3) Recharger + UI
            _lines = _devis.GetLines(id);
            _current = _devis.GetById(id);
            BindCurrent();
            LoadList(SearchBox.Text?.Trim());
        }

        // SUPRESSION Devis
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null || _current.Id <= 0) return;
            var ask = MessageBox.Show("Supprimer ce devis ? (corbeille 30 jours)", "Confirmer",
                                      MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ask != MessageBoxResult.Yes) return;

            _devis.SoftDelete(_current.Id);

            // reload liste + ouvrir un nouveau brouillon
            LoadList(SearchBox.Text?.Trim());
            NewDraft();
        }

        private void CmbBank_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_current == null || _current.Id <= 0) return;
            var id = CmbBank.SelectedValue as int?;
            _devis.SetBankAccount(_current.Id, id);
            // recharge proprement l'objet courant
            _current = _devis.GetById(_current.Id);
            BindCurrent();
        }

        private void BtnEmit_Click(object sender, RoutedEventArgs e)
        {
            EnsureCurrentId();                  // d'abord on s'assure d'avoir un Id
            if (_current == null) return;       // ensuite on vérifie

            try
            {
                var pdfPath = _devis.Emit(_current!.Id, _num); // génère DEVI-YYYY-MM-####
                                                               // recharger la fiche et la liste (numéro + état changent)
                _current = _devis.GetById(_current.Id);
                if (_current == null) return;
                _lines = _devis.GetLines(_current.Id);
                BindCurrent();
                LoadList(SearchBox.Text?.Trim());

                MessageBox.Show($"Devis émis : {_current?.Numero}\nPDF : {pdfPath}", "OK",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                System.Diagnostics.Process.Start(new ProcessStartInfo(pdfPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Échec de l'émission : " + ex.Message, "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


    }
}
