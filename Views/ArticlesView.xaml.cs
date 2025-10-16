using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
}
using VorTech.App.Models;
_articles.RecomputePackAggregates(_current.Id);
_pack = new ObservableCollection<PackItem>(_articles.GetPackItems(_current.Id));
GridPack.ItemsSource = _pack;
UpdateComputedStates();
}


private void BtnRemovePackItem_Click(object sender, RoutedEventArgs e)
{
    if (GridPack.SelectedItem is not PackItem it) return;
    _articles.DeletePackItem(it.Id);
    _articles.RecomputePackAggregates(_current!.Id);
    _pack.Remove(it);
    UpdateComputedStates();
}


private void BtnUpdatePackQty_Click(object sender, RoutedEventArgs e)
{
    if (GridPack.SelectedItem is not PackItem it) return;
    if (!decimal.TryParse(QtyBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var q) || q < 1)
    {
        MessageBox.Show("Quantité invalide (>=1)");
        return;
    }
    _articles.UpdatePackItem(it.Id, q);
    _articles.RecomputePackAggregates(_current!.Id);
    it.Quantite = q;
    GridPack.Items.Refresh();
    UpdateComputedStates();
}


// --------------- Prix conseillé ---------------
private void RefreshPrixConseille()
{
    if (_current == null) return;
    bool isTva = TvaBox.IsEnabled; // piloté par TaxMode
    decimal pa = ParseDec(PrixAchatBox.Text, 2);


    decimal taux = 0m;
    if (isTva)
    {
        var r = _catalogs.GetRateById(_current.TvaRateId);
        taux = r;
        // En TVA, conseillé = 2*PA (taux non appliqué dans la formule HT)
        var pv = 2m * pa;
        PrixConseilleBox.Text = pv.ToString("0.00", CultureInfo.InvariantCulture);
    }
    else
    {
        var r = _catalogs.GetRateById(_current.CotisationRateId);
        taux = r / 100m;
        if (1 - taux <= 0m) { PrixConseilleBox.Text = "--"; return; }
        var pv = (2m * pa) / (1m - taux);
        PrixConseilleBox.Text = Math.Round(pv, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);
    }
}


// --------------- Helpers ---------------
private static decimal ParseDec(string? s, int decimals)
{
    if (!decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v)) v = 0m;
    if (decimals == 0) return Math.Floor(v);
    return Math.Round(v, 2, MidpointRounding.AwayFromZero);
}


// --------------- UI events ---------------
private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ReloadList(SearchBox.Text);
private void GridArticles_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (GridArticles.SelectedItem is Article a)
    {
        _current = a;
        BindToForm();
    }
}
private void PrixAchatBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshPrixConseille();
private void CotisationBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshPrixConseille();
private void TvaBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshPrixConseille();
}
}