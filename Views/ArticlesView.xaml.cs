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

        public ObservableCollection<Article> Articles { get; } = new ObservableCollection<Article>();
        public ObservableCollection<ArticleComponent> Components { get; } = new ObservableCollection<ArticleComponent>();

        private Article? _selected;
        public Article? Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                OnPropertyChanged();
                LoadComponents();
            }
        }

        public ArticlesView()
        {
            InitializeComponent();
            DataContext = this;
            Reload();
        }

        private void Reload()
        {
            Articles.Clear();
            foreach (var a in _svc.GetAll()) Articles.Add(a);
            Selected = Articles.FirstOrDefault();
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            Selected = new Article { Actif = true, Type = "Produit" };
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;

            // MAJ auto de DerniereMAJ ISO
            Selected.DerniereMAJ = System.DateTime.UtcNow.ToString("s");
            _svc.Save(Selected);

            // recharger la liste et reselectionner
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
            Reload();
        }

        private void LoadComponents()
        {
            Components.Clear();
            if (Selected == null || !Selected.EstPack || Selected.Id <= 0) return;

            foreach (var c in _svc.GetComponents(Selected.Id))
                Components.Add(c);
        }

        private void AddComponent_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null)
            {
                MessageBox.Show("Aucun article sélectionné.");
                return;
            }
            if (!Selected.EstPack)
            {
                MessageBox.Show("L’article courant n’est pas de Type = Pack.");
                return;
            }
            if (Selected.Id <= 0)
            {
                MessageBox.Show("Enregistrez d’abord le pack (il doit avoir un Id).");
                return;
            }

            // boite de saisie simple du code + quantité
            var code = Microsoft.VisualBasic.Interaction.InputBox("Code de l’article composant :", "Ajouter composant", "");
            if (string.IsNullOrWhiteSpace(code)) return;
            var qteText = Microsoft.VisualBasic.Interaction.InputBox("Quantité :", "Ajouter composant", "1");
            if (!double.TryParse(qteText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var qte) || qte <= 0)
            {
                MessageBox.Show("Quantité invalide.");
                return;
            }

            var compId = _svc.ResolveArticleIdByCode(code);
            if (compId == null)
            {
                MessageBox.Show("Article introuvable pour ce code.");
                return;
            }

            var newList = Components.ToList();
            newList.Add(new ArticleComponent { PackArticleId = Selected.Id, ComponentArticleId = compId.Value, Qte = qte });

            _svc.SetComponents(Selected.Id, newList);
            LoadComponents();
        }

        private void RemoveComponent_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null || !Selected.EstPack || Selected.Id <= 0) return;
            if (GridComponents.SelectedItem is not ArticleComponent row) return;

            var newList = Components.Where(c => c.Id != row.Id).ToList();
            _svc.SetComponents(Selected.Id, newList);
            LoadComponents();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
