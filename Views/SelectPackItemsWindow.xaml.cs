using MigraDoc.DocumentObjectModel.Tables;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VorTech.App.Services;


namespace VorTech.App.Views
{
    public partial class SelectPackItemsWindow : Window
    {
        private readonly ArticleService _articles;
        private List<SelectRowVm> _rows = new(); // <-- on garde la liste pour lire les cases cochées

        // classe locale pour binder le "IsSelected" (on enrichit le DTO service)
        private class SelectRowVm
        {
            public int ArticleId { get; set; }
            public int? VariantId { get; set; }
            public string DisplayName { get; set; } = "";
            public bool IsSelected { get; set; } = false;
        }

        public List<(int ArticleId, int? VariantId)> SelectedRows { get; private set; } = new();

        public SelectPackItemsWindow(ArticleService articles)
        {
            InitializeComponent();
            _articles = articles;

            // On alimente la grille quand le visuel est prêt
            Loaded += SelectPackItemsWindow_Loaded;
        }

        private void SelectPackItemsWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            // Articles simples + déclinaisons -> VM avec case à cocher
            _rows = _articles.GetSelectablePackRows()
                             .Select(r => new SelectRowVm
                             {
                                 ArticleId = r.ArticleId,
                                 VariantId = r.VariantId,
                                 DisplayName = r.DisplayName,
                                 IsSelected = false
                             })
                             .ToList();

            GridSelect.ItemsSource = _rows;
            GridSelect.UpdateLayout();
            GridSelect.Items.Refresh();
        }

        private void BtnValidate_Click(object sender, RoutedEventArgs e)
        {
            GridSelect.CommitEdit();

            var selected = _rows
                .Where(r => r.IsSelected)
                .Select(r => (r.ArticleId, r.VariantId))
                .ToList();

            if (selected.Count == 0) { DialogResult = false; Close(); return; }

            SelectedRows = selected;
            DialogResult = true;
            Close();
        }

        private void GridSelect_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject d = (DependencyObject)e.OriginalSource;
            while (d != null && d is not CheckBox && d is not DataGridRow)
                d = VisualTreeHelper.GetParent(d);

            if (d is DataGridRow)
                e.Handled = true;   // on bloque la sélection de ligne
                                    // si d est CheckBox => on laisse faire (toggle IsSelected)
        }
    }
}