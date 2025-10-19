using System.Linq;
using System.Windows;
using System.Windows.Input;
using VorTech.App.Services;

namespace VorTech.App.Views
{
    public partial class ArticlePickerWindow : Window
    {
        private readonly ArticleService _articles = new ArticleService();

        public int SelectedArticleId { get; private set; }
        public int? SelectedVariantId { get; private set; }

        public ArticlePickerWindow()
        {
            InitializeComponent();
            RefreshList(null);
        }

        private void TxtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var q = TxtSearch.Text?.Trim();
            if (string.IsNullOrEmpty(q) || q.Length >= 2) RefreshList(q);
        }

        private void RefreshList(string? q)
        {
            var rows = _articles.GetSelectablePackRows(); // <-- une ligne par déclinaison
            if (!string.IsNullOrWhiteSpace(q))
            {
                var s = q.ToLowerInvariant();
                rows = rows.Where(r => (r.DisplayName ?? "").ToLowerInvariant().Contains(s)).ToList();
            }
            List.ItemsSource = rows;
        }

        private void List_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Ok_Click(null!, null!);

        private void Ok_Click(object? sender, RoutedEventArgs? e)
        {
            if (List.SelectedItem is ArticleService.SelectablePackRow r)
            {
                SelectedArticleId = r.ArticleId;
                SelectedVariantId = r.VariantId;
                DialogResult = true;
                Close();
            }
        }
    }
}
