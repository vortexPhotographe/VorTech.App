using System;
using System.Collections.Generic;
using System.IO;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace VorTech.App
{
    public static class InvoicePdf
    {
        public static string RenderSimpleInvoice(string outfile, string number, string clientName, string clientAddress,
                                                 List<(string designation, double qty, double pu)> lines,
                                                 bool showMention293B)
        {
            var doc = new Document();
            doc.Info.Title = $"Facture {number}";

            var sec = doc.AddSection();
            sec.PageSetup.LeftMargin = Unit.FromCentimeter(1.8);
            sec.PageSetup.RightMargin = Unit.FromCentimeter(1.8);
            sec.PageSetup.TopMargin = Unit.FromCentimeter(1.6);
            sec.PageSetup.BottomMargin = Unit.FromCentimeter(1.6);

            var s = sec.AddParagraph("FACTURE");
            s.Format.Font.Size = 15;
            s.Format.Font.Bold = true;
            s.Format.SpaceAfter = Unit.FromPoint(12);

            var top = sec.AddTable();
            top.AddColumn(Unit.FromCentimeter(9));
            top.AddColumn(Unit.FromCentimeter(8));
            var tr = top.AddRow();
            var p1 = tr.Cells[0].AddParagraph($"N° {number}\\nDate : {DateTime.Today:dd/MM/yyyy}");
            p1.Format.SpaceAfter = Unit.FromPoint(6);
            var p2 = tr.Cells[1].AddParagraph($"Destinataire :\\n{clientName}\\n{clientAddress}");
            p2.Format.Alignment = ParagraphAlignment.Right;

            var tbl = sec.AddTable();
            tbl.Borders.Width = 0.5;
            tbl.AddColumn(Unit.FromCentimeter(11));
            tbl.AddColumn(Unit.FromCentimeter(2));
            tbl.AddColumn(Unit.FromCentimeter(3));
            tbl.AddColumn(Unit.FromCentimeter(2));

            var header = tbl.AddRow();
            header.Shading.Color = Colors.LightGray;
            header.Cells[0].AddParagraph("Désignation").Format.Font.Bold = true;
            header.Cells[1].AddParagraph("Qté").Format.Font.Bold = true;
            header.Cells[2].AddParagraph("PU HT").Format.Font.Bold = true;
            header.Cells[3].AddParagraph("Montant").Format.Font.Bold = true;

            double total = 0;
            foreach (var l in lines)
            {
                var r = tbl.AddRow();
                r.Cells[0].AddParagraph(l.designation);
                r.Cells[1].AddParagraph(l.qty.ToString("0.##"));
                r.Cells[2].AddParagraph(l.pu.ToString("0.00 €"));
                double mt = l.qty * l.pu;
                total += mt;
                r.Cells[3].AddParagraph(mt.ToString("0.00 €"));
            }

            var rt = tbl.AddRow();
            rt.Cells[0].MergeRight = 2;
            rt.Cells[0].AddParagraph("Total HT");
            rt.Cells[3].AddParagraph(total.ToString("0.00 €")).Format.Font.Bold = true;

            if (showMention293B)
            {
                var m = sec.AddParagraph("TVA non applicable, article 293 B du CGI.");
                m.Format.Font.Size = 9;
                m.Format.SpaceBefore = Unit.FromPoint(6);
            }

            var foot = sec.AddParagraph("Paiement par virement / CB / espèces. Merci de votre confiance.");
            foot.Format.SpaceBefore = Unit.FromPoint(16);

			var renderer = new PdfDocumentRenderer();
            renderer.Document = doc;
            renderer.RenderDocument();
            Directory.CreateDirectory(Path.GetDirectoryName(outfile)!);
            renderer.PdfDocument.Save(outfile);

            return outfile;
        }
    }
}
