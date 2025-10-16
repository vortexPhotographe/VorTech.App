using DocumentFormat.OpenXml.VariantTypes;
using System.Globalization;
using VorTech.App;
using VorTech.App.Models;

ArticleItemId = Convert.ToInt32(rd["ArticleItemId"]),
VariantId = rd["VariantId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["VariantId"]),
Quantite = Convert.ToDecimal(rd["Quantite"], CultureInfo.InvariantCulture)
};
}


private void UpdatePackComputed(int packArticleId, decimal achat, decimal poids, decimal stock)
{
    using var cn = Db.Open();
    using var cmd = cn.CreateCommand();
    cmd.CommandText = @"UPDATE Articles SET PrixAchatHT=@Achat, PoidsG=@Poids, StockActuel=@Stock WHERE Id=@Id";
    Db.AddParam(cmd, "@Achat", achat);
    Db.AddParam(cmd, "@Poids", poids);
    Db.AddParam(cmd, "@Stock", stock);
    Db.AddParam(cmd, "@Id", packArticleId);
    cmd.ExecuteNonQuery();
}


private static void EnsureQuantiteValid(decimal qte)
{
    if (qte < 1) throw new InvalidOperationException("Quantite must be >= 1");
}


private static void RequireVariantBarcode(ArticleVariant v)
{
    if (string.IsNullOrWhiteSpace(v.CodeBarres))
        throw new InvalidOperationException("Code-barres obligatoire pour une variante");
}


private static void EnsureArticleIsNotPack(int articleId)
{
    using var cn = Db.Open();
    using var cmd = cn.CreateCommand();
    cmd.CommandText = "SELECT Type FROM Articles WHERE Id=@Id";
    Db.AddParam(cmd, "@Id", articleId);
    var t = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    if (t == 1) throw new InvalidOperationException("A pack cannot contain another pack");
}


private static void EnsureVariantBelongs(int articleId, int variantId)
{
    using var cn = Db.Open();
    using var cmd = cn.CreateCommand();
    cmd.CommandText = "SELECT 1 FROM ArticleVariants WHERE Id=@V AND ArticleId=@A";
    Db.AddParam(cmd, "@V", variantId);
    Db.AddParam(cmd, "@A", articleId);
    var ok = cmd.ExecuteScalar();
    if (ok == null) throw new InvalidOperationException("VariantId does not belong to ArticleItemId");
}


private bool HasVariants(int articleId)
{
    using var cn = Db.Open();
    using var cmd = cn.CreateCommand();
    cmd.CommandText = "SELECT 1 FROM ArticleVariants WHERE ArticleId=@A LIMIT 1";
    Db.AddParam(cmd, "@A", articleId);
    return cmd.ExecuteScalar() != null;
}


// DTO pour SelectPackItemsWindow
public class SelectablePackRow
{
    public int ArticleId { get; set; }
    public int? VariantId { get; set; }
    public string DisplayName { get; set; } = "";
}
}
}