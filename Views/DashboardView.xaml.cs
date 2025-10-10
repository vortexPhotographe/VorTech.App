using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace VorTech.App.Views
{
    public partial class DashboardView : UserControl
    {
        public string CaMois { get; set; } = "€ 4 250";
        public string FacturesARégler { get; set; } = "3 (1 120 €)";
        public string DevisOuverts { get; set; } = "5 (2 980 €)";
        public string StockBas { get; set; } = "7 articles";

        public ObservableCollection<StockItemVm> StockBasItems { get; } =
            new ObservableCollection<StockItemVm>
            {
                new StockItemVm{ Sku="BAT-UV5R", Libelle="Batterie UV-5R", Qte=2, Mini=5 },
                new StockItemVm{ Sku="MIC-RT86", Libelle="Micro RT86", Qte=1, Mini=3 },
                new StockItemVm{ Sku="CABL-UHF", Libelle="Câble prog UHF", Qte=0, Mini=2 },
            };

        public DashboardView()
        {
            InitializeComponent();
            DataContext = this; // mock: on branchera la vraie base ensuite
        }
    }

    public class StockItemVm
    {
        public string Sku { get; set; } = "";
        public string Libelle { get; set; } = "";
        public int Qte { get; set; }
        public int Mini { get; set; }
    }
}
