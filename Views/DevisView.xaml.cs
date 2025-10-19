using System;
using System.Collections.Generic;
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
        private readonly INumberingService _num = new NumberingService();
        // pour recherche client si besoin :contentReference[oaicite:6]{index=6}                                                                        // par celles-ci :
        private readonly ArticleService _articles = new ArticleService();
        private readonly ClientService _clients = new ClientService();


        private Devis? _current;
        private List<DevisLigne> _lines = new();
        private List<DevisAnnexe> _annexes = new();

        public DevisView()
        {
            InitializeComponent();
            LoadList(null);
            NewDraft(); // ouvre un brouillon vide par d�faut
        }

        // --- Liste gauche ---
        private void LoadList(string? search)
        {
            var items = _devis.GetAll(search);
            DevisList.ItemsSource = items; // ListBox nomm� dans ton XAML
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
            DataContext = _current;
            GridLines.ItemsSource = _lines;
            ListAnnexes.ItemsSource = _annexes;
            RecalcCostsUi();

        }

        private void SelectClient_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            EnsureCurrentId(); // cr�e le brouillon s'il n'existe pas

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

        // �Saisir rapide� = on efface le lien mais on laisse saisir � la main (non-li�)
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

        // --- Lignes (boutons + �dition) ---
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
                    : $"{a.Libelle} � {v.Nom}";

                var pu = v?.PrixVenteHT ?? a.PrixVenteHT;   // PU depuis variante si choisie
                var variantId = w.SelectedVariantId;

                // image: on verra ensuite ; mets null propre, pas la cha�ne "null"
                string? img = null;

                _devis.AddLine(_current!.Id, designation, 1m, pu, a.Id, variantId, img);

                _lines = _devis.GetLines(_current.Id);
                _current = _devis.GetById(_current.Id);
                BindCurrent();
            }
        }

        private void DuplicateLine_Click(object sender, RoutedEventArgs e)
        {
            if (GridLines.SelectedItem is not DevisLigne l || _current == null) return;
            _devis.AddLine(_current.Id, l.Designation, l.Qty, l.PU, l.ArticleId, l.VarianteId, l.ImagePath);
            _lines = _devis.GetLines(_current.Id);
            _current = _devis.GetById(_current.Id);
            BindCurrent();
        }

        private void DeleteLine_Click(object sender, RoutedEventArgs e)
        {
            if (GridLines.SelectedItem is not DevisLigne l) return;
            _devis.DeleteLine(l.Id);
            _lines.RemoveAll(x => x.Id == l.Id);
            _current = _devis.GetById(_current!.Id);
            BindCurrent();
        }

        private void CellEditEnded(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Row.Item is not DevisLigne l) return;
            // r�cup�re les nouvelles valeurs depuis l'objet li� (TwoWay par d�faut sur TextColumn)
            _devis.UpdateLine(l.Id, l.Designation, l.Qty, l.PU);
            _current = _devis.GetById(l.DevisId);
            // refresh lignes
            _lines = _devis.GetLines(l.DevisId);
            BindCurrent();
        }

        // --- Remise globale ---
        private void RemiseLostFocus(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            _devis.SetGlobalDiscount(_current.Id, _current.RemiseGlobale);
            _current = _devis.GetById(_current.Id);
            BindCurrent();
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

            TxtCostMatiere.Text = $"Co�t mati�res : {cout:0.##} �";
            TxtCharges.Text = $"Charges estim�es : {charges:0.##} �";
            TxtMarge.Text = $"Marge nette : {net:0.##} �";
        }

        // --- Emission (num�rotation) ---
        private void Emit_Click(object sender, RoutedEventArgs e)
        {
            EnsureCurrentId();
            var pdf = _devis.Emit(_current!.Id, _num);
            _current = _devis.GetById(_current.Id);
            BindCurrent();
            MessageBox.Show($"Devis �mis : {_current!.Numero}\nPDF: {pdf}", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // --- Utilitaires ---
        private void EnsureCurrentId()
        {
            if (_current == null) throw new InvalidOperationException("Devis non initialis�.");
            if (_current.Id > 0) return;

            // Cr�er en base le brouillon (avec snapshot client si tu as un Client s�lectionn�)
            var id = _devis.CreateDraft(_current.ClientId, null /* snapshot � brancher si client choisi */);
            _current = _devis.GetById(id);
        }

        // ---- SAVE Devis ---
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            EnsureCurrentId(); // cr�e le brouillon si n�cessaire

            // push des champs saisis � la main
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

            // recharger l�objet et rafra�chir l�UI
            _current = _devis.GetById(_current.Id);
            BindCurrent();
            MessageBox.Show("Devis enregistr�.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
        }

    }
}
