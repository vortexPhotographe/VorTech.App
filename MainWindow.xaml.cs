using System.Windows;
using VorTech.App.Views;

namespace VorTech.App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Dashboard au démarrage
            MainContent.Content = new DashboardView();
        }

        // NAV
        private void NavDashboard_Click(object sender, RoutedEventArgs e) => MainContent.Content = new DashboardView();
        private void NavClients_Click(object sender, RoutedEventArgs e)   => MainContent.Content = new ClientsView();
        private void NavArticles_Click(object sender, RoutedEventArgs e) => MainContent.Content = new VorTech.App.Views.ArticlesView();
        private void NavDevis_Click(object sender, RoutedEventArgs e)     => MainContent.Content = new DevisView();
        private void NavInvoices_Click(object sender, RoutedEventArgs e)  => MainContent.Content = new InvoicesView();
        private void NavSettings_Click(object sender, RoutedEventArgs e)  => MainContent.Content = new SettingsView();
    }
}
