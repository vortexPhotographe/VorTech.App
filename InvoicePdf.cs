using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using VorTech.App.Models;
using VorTech.App.Services;

namespace VorTech.App
{
    public static class InvoicePdf
    {
        public static void RenderSimpleInvoice(
            string outputPath,
            string numero,
            string clientName,
            string clientAddr,
            IReadOnlyList<(string designation, double qty, double pu)> lines,
            bool showMention293B)
        {
            RenderSimpleInvoice(
                outputPath, numero, clientName, clientAddr, lines, showMention293B,
                discountEuro: 0m,
                bankHolder: null, bankName: null, iban: null, bic: null
            );
        }

        public static void RenderSimpleInvoice(
            string outputPath,
            string numero,
            string clientName,
            string clientAddr,
            IReadOnlyList<(string designation, double qty, double pu)> lines,
            bool showMention293B,
            decimal discountEuro,
            string? bankHolder,
            string? bankName,
            string? iban,
            string? bic)
        {
            var cfg = ConfigService.Load();
            var culture = CultureInfo.GetCultureInfo("fr-FR");

            var doc = new Document();
            doc.Info.Title = $"Devis {numero}";

            // --- Styles de base
            var style = doc.Styles["Normal"] ?? doc.Styles.AddStyle("Normal", "Normal");
            style.Font.Name = "Segoe UI";
            style.Font.Size = 10;

            // --- Section & marges
            var sec = doc.AddSection();
            sec.PageSetup.LeftMargin = Unit.FromCentimeter(1.8);
            sec.PageSetup.RightMargin = Unit.FromCentimeter(1.8);
            sec.PageSetup.TopMargin = Unit.FromCentimeter(1.6);
            sec.PageSetup.BottomMargin = Unit.FromCentimeter(1.6);

            // --- ENTÊTE (logo + nom commercial depuis ConfigService)
            var header = sec.Headers.Primary;
            var headerTbl = header.AddTable();
            headerTbl.AddColumn(Unit.FromCentimeter(9));
            headerTbl.AddColumn(Unit.FromCentimeter(8));
            var hr = headerTbl.AddRow();

            try
            {
                var logoPath = Path.Combine(Paths.AssetsDir, "Brand", "logo.png");
                if (File.Exists(logoPath))
                {
                    var img = hr.Cells[0].AddImage(logoPath);
                    img.LockAspectRatio = true;
                    img.Width = Unit.FromCentimeter(4.0);
                }
            }
            catch { /* ignore image bloquée */ }

            var companyName = string.IsNullOrWhiteSpace(cfg.BusinessName) ? "Votre entreprise" : cfg.BusinessName;
            var company = hr.Cells[1].AddParagraph(companyName);
            company.Format.Alignment = ParagraphAlignment.Right;
            company.Format.Font.Size = 12;
            company.Format.Font.Bold = true;

            var comp2 = hr.Cells[1].AddParagraph();
            comp2.Format.Alignment = ParagraphAlignment.Right;
            var compLines = new List<string>();
            if (!string.IsNullOrWhiteSpace(cfg.Siret)) compLines.Add($"SIREN/SIRET : {cfg.Siret}");
            // On n'affiche pas ici IBAN/BIC globaux si on a un compte bancaire par devis (géré en pied)
            comp2.AddText(string.Join("   •   ", compLines));
            comp2.Format.Font.Size = 8.5;

            // --- Titre
            var title = sec.AddParagraph("DEVIS");
            title.Format.Font.Size = 15;
            title.Format.Font.Bold = true;
            title.Format.SpaceBefore = Unit.FromPoint(6);
            title.Format.SpaceAfter = Unit.FromPoint(10);

            // --- Bandeau N° + Date + Client
            var top = sec.AddTable();
            top.Borders.Width = 0;
            top.AddColumn(Unit.FromCentimeter(9));
            top.AddColumn(Unit.FromCentimeter(8));
            var tr = top.AddRow();

            var left = tr.Cells[0].AddParagraph($"N° {numero}\nDate : {DateTime.Today:dd/MM/yyyy}");
            left.Format.SpaceAfter = Unit.FromPoint(6);

            var right = tr.Cells[1].AddParagraph($"Destinataire :\n{clientName}\n{clientAddr}");
            right.Format.Alignment = ParagraphAlignment.Right;

            // --- Tableau lignes
            var tbl = sec.AddTable();
            tbl.Borders.Width = 0.5;
            tbl.Rows.LeftIndent = 0;

            tbl.AddColumn(Unit.FromCentimeter(11)); // désignation
            tbl.AddColumn(Unit.FromCentimeter(2));  // Qté
            tbl.AddColumn(Unit.FromCentimeter(3));  // PU HT
            tbl.AddColumn(Unit.FromCentimeter(2));  // Montant

            var headerRow = tbl.AddRow();
            headerRow.Shading.Color = Colors.LightGray;
            headerRow.HeadingFormat = true;
            headerRow.Format.Font.Bold = true;
            headerRow.Cells[0].AddParagraph("Désignation");
            headerRow.Cells[1].AddParagraph("Qté");
            headerRow.Cells[2].AddParagraph("PU");
            headerRow.Cells[3].AddParagraph("Montant");

            decimal sum = 0m;
            foreach (var line in lines)
            {
                var r = tbl.AddRow();
                r.Cells[0].AddParagraph(line.designation);
                r.Cells[1].AddParagraph(line.qty.ToString("0.##", culture)).Format.Alignment = ParagraphAlignment.Right;
                r.Cells[2].AddParagraph(line.pu.ToString("0.00 €", culture)).Format.Alignment = ParagraphAlignment.Right;
                var mt = Convert.ToDecimal(line.qty * line.pu, culture);
                sum += mt;
                r.Cells[3].AddParagraph(mt.ToString("0.00 €", culture)).Format.Alignment = ParagraphAlignment.Right;
            }

            // --- Ligne Remise (si > 0)
            if (discountEuro > 0m)
            {
                var rdw = tbl.AddRow();
                rdw.Cells[0].MergeRight = 2;
                rdw.Cells[0].AddParagraph("Remise");
                var pRem = rdw.Cells[3].AddParagraph(("-" + discountEuro.ToString("0.00 €", culture)));
                rdw.Cells[3].Format.Alignment = ParagraphAlignment.Right;
            }

            // --- Total NET = sum - remise (min 0)
            var totalNet = sum - discountEuro;
            if (totalNet < 0m) totalNet = 0m;

            var rt = tbl.AddRow();
            rt.Cells[0].MergeRight = 2;
            rt.Cells[0].AddParagraph("Total");
            var totalCell = rt.Cells[3].AddParagraph(totalNet.ToString("0.00 €", culture));
            totalCell.Format.Font.Bold = true;
            rt.Cells[3].Format.Alignment = ParagraphAlignment.Right;

            // --- Mention micro 293B
            if (showMention293B || cfg.IsMicro)
            {
                var secToUse = doc.Sections.Count > 0 ? doc.LastSection : doc.AddSection();
                var p293b = secToUse.AddParagraph("TVA non applicable, art. 293 B du CGI");
                p293b.Format.Font.Size = 9;
                p293b.Format.SpaceBefore = Unit.FromPoint(6);
            }

            // --- Pied de page : coordonnées bancaires du devis si fournies, sinon fallback global
            var ftr = sec.Footers.Primary;
            var footerParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(bankHolder)) footerParts.Add($"Titulaire : {bankHolder}");
            if (!string.IsNullOrWhiteSpace(bankName)) footerParts.Add($"Banque : {bankName}");
            if (!string.IsNullOrWhiteSpace(iban)) footerParts.Add($"IBAN : {iban}");
            if (!string.IsNullOrWhiteSpace(bic)) footerParts.Add($"BIC : {bic}");

            if (footerParts.Count == 0)
            {
                // fallback global (comme avant)
                if (!string.IsNullOrWhiteSpace(cfg.Siret)) footerParts.Add($"SIRET : {cfg.Siret}");
                if (!string.IsNullOrWhiteSpace(cfg.Iban)) footerParts.Add($"IBAN : {cfg.Iban}");
                if (!string.IsNullOrWhiteSpace(cfg.Bic)) footerParts.Add($"BIC : {cfg.Bic}");
            }

            if (footerParts.Count > 0)
            {
                var pf = ftr.AddParagraph(string.Join("   •   ", footerParts));
                pf.Format.Alignment = ParagraphAlignment.Center;
                pf.Format.Font.Size = 8.5;
                pf.Format.SpaceBefore = Unit.FromPoint(8);
            }

            // --- Rendu
            var renderer = new PdfDocumentRenderer();
            renderer.Document = doc;
            renderer.RenderDocument();

            var outDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outDir))
                Directory.CreateDirectory(outDir);

            renderer.PdfDocument.Save(outputPath);
        }
    }
}
