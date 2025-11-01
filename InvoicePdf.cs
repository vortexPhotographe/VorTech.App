using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Fields;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Versioning;
using VorTech.App;          // Paths
using VorTech.App.Models;
using VorTech.App.Services; // ConfigService, SettingsCatalogService

namespace VorTech.App
{
    [SupportedOSPlatform("windows")]
    public static class InvoicePdf
    {
        // Surcharge existante (compat)
        public static void RenderSimpleInvoice(
            string outputPath,
            string numero,
            string clientName,
            string clientAddr,
            IReadOnlyList<(string designation, double qty, double pu)> lines,
            bool showMention293B,
            string? clientPhone = null,
            string? clientEmail = null,
            int? devisId = null
        )
        {
            RenderSimpleInvoice(
                outputPath, numero, clientName, clientAddr, lines, showMention293B,
                discountEuro: 0m,
                bankHolder: null, bankName: null, iban: null, bic: null,
                paymentTermsText: null,
                paymentPlanJson: null,
                noteTop: null,
                noteBottom: null,
                devisDateText: null,
                clientPhone: clientPhone,
                clientEmail: clientEmail,
                devisId: devisId
            );
        }

        /// <summary>Rendu PDF conforme à la maquette 1→11.</summary>
        [SupportedOSPlatform("windows")]
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
            string? bic,
            string? paymentTermsText,
            string? paymentPlanJson,
            string? noteTop = null,
            string? noteBottom = null,
            string? devisDateText = null,
            string? clientPhone = null,
            string? clientEmail = null,
            int? devisId = null
        )
        {
            var cfg = ConfigService.Load();
            var company = new SettingsCatalogService().GetCompanyProfile();
            var culture = CultureInfo.GetCultureInfo("fr-FR");
            Color Accent = Color.FromRgb(255, 0, 0); // #FF0000

            var doc = new Document();
            doc.Info.Title = $"Devis {numero}";

            // Styles
            Style normal = doc.Styles["Normal"]!;
            normal.Font.Name = "Arial";
            normal.Font.Size = 11;

            var sTitle = doc.Styles.AddStyle("H-Title", "Normal");
            sTitle.Font.Size = 12;
            sTitle.Font.Bold = true;

            var sSmall = doc.Styles.AddStyle("Small", "Normal");
            sSmall.Font.Size = 10;

            var sSmallItalic = doc.Styles.AddStyle("SmallItalic", "Small");
            sSmallItalic.Font.Italic = true;

            // Page
            var sec = doc.AddSection();
            sec.PageSetup.LeftMargin = Unit.FromMillimeter(5);
            sec.PageSetup.RightMargin = Unit.FromMillimeter(5);
            sec.PageSetup.TopMargin = Unit.FromMillimeter(5);
            sec.PageSetup.BottomMargin = Unit.FromMillimeter(5);

            /* Page X sur Y (haut droite)
            var pageHdr = sec.Headers.Primary.AddParagraph();
            pageHdr.Format.Alignment = ParagraphAlignment.Right;
            pageHdr.AddText("Page ");
            pageHdr.AddPageField();
            pageHdr.AddText(" sur ");
            pageHdr.AddNumPagesField();
            pageHdr.Format.SpaceAfter = Unit.FromMillimeter(1.5);*/

            // ================== EN-TÊTE: grille 2 x 2 ==================
            // colonnes: gauche 90mm / droite 105mm
            var grid = sec.AddTable();
            grid.Borders.Visible = false;
            grid.AddColumn(Unit.FromMillimeter(90));
            grid.AddColumn(Unit.FromMillimeter(105));
            grid.AddRow(); // Row 0: société (gauche) + logo (droite)
            grid.AddRow(); // Row 1: client (gauche) + panneau devis (droite)

            // (1) Société (gauche, row 0)
            {
                var cell = grid.Rows[0].Cells[0];
                var p = cell.AddParagraph();
                p.Format.Font.Size = 14; // plus grand, pour tomber au niveau du logo
                void L(string? t) { if (!string.IsNullOrWhiteSpace(t)) { p.AddText(t); p.AddLineBreak(); } }
                L(company.NomCommercial ?? cfg.BusinessName ?? "");
                L(company.Adresse1);
                L(company.Adresse2);
                var cpVille = $"{company.CodePostal} {company.Ville}".Trim();
                L(!string.IsNullOrWhiteSpace(company.Pays) ? $"{cpVille} - {company.Pays}" : cpVille);
                L(company.Email);
                if (!string.IsNullOrWhiteSpace(company.SiteWeb)) p.AddText(company.SiteWeb);
            }

            // (2) Logo (droite, row 0)
            {
                var cell = grid.Rows[0].Cells[1];
                try
                {
                    var logoPath = Path.Combine(Paths.AssetsDir, "Brand", "logo.png");
                    if (File.Exists(logoPath))
                    {
                        var img = cell.AddImage(logoPath);
                        img.LockAspectRatio = true;
                        img.Width = Unit.FromMillimeter(98);
                    }
                }
                catch { /* ignore */ }
            }

            // ======= LIGNE client + panneau devis, même hauteur =======
            var hdrRow = sec.AddTable();
            hdrRow.Borders.Visible = false;
            hdrRow.AddColumn(Unit.FromMillimeter(90));  // client (gauche)
            hdrRow.AddColumn(Unit.FromMillimeter(105)); // panneau devis (droite)
            var hr = hdrRow.AddRow();

            // (4) CLIENT (gauche, même hauteur que (3)) — 14pt, 1ʳᵉ ligne en gras
            {
                var cell = hr.Cells[0];

                var t = cell.AddParagraph("Client");
                t.Style = "H-Title";
                t.Format.SpaceAfter = Unit.FromMillimeter(2);

                var p = cell.AddParagraph();
                p.Format.SpaceAfter = Unit.FromMillimeter(3);

                // lignes de base (Société/Nom, Adresse, CP Ville…)
                var full = ((clientName ?? "") + (string.IsNullOrWhiteSpace(clientAddr) ? "" : "\n" + clientAddr));

                // -> on passe en List<string> pour pouvoir .Add(...)
                var linesClient = new List<string>(
                    full.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                );

                // Tél / Email (si fournis) — nécessite que ta méthode ait les params clientPhone/clientEmail
                if (!string.IsNullOrWhiteSpace(clientPhone)) linesClient.Add(clientPhone.Trim());
                if (!string.IsNullOrWhiteSpace(clientEmail)) linesClient.Add(clientEmail.Trim());

                // rendu : 14pt, 1ʳᵉ ligne (nom/société) en gras
                for (int i = 0; i < linesClient.Count; i++)
                {
                    var run = p.AddFormattedText(linesClient[i].Trim());
                    run.Font.Size = 12;
                    run.Bold = (i == 0);
                    p.AddLineBreak();
                }

            }

            // (3) PANNEAU "DEVIS" (cellule droite) — **sans table imbriquée**,
            // on utilise 2 sous-paragraphes + une mini-table *créée via Elements* (OK avec MigraDoc).
            {
                var cell = hr.Cells[1];

                // Bandeau rouge "Devis"
                var band = cell.Elements.AddTable();
                band.Borders.Visible = false;
                band.AddColumn(Unit.FromMillimeter(105));
                var br = band.AddRow();
                br.Cells[0].Shading.Color = Accent;
                var pBand = br.Cells[0].AddParagraph("Devis");
                pBand.Format.Font.Color = Colors.White;
                pBand.Format.Font.Bold = true;
                pBand.Format.Font.Size = 11;
                pBand.Format.SpaceBefore = Unit.FromMillimeter(1.2);
                pBand.Format.SpaceAfter = Unit.FromMillimeter(1.2);

                // Tableau infos (libellé / valeur) — via Elements.AddTable()
                var info = cell.Elements.AddTable();
                info.Borders.Visible = false;
                info.AddColumn(Unit.FromMillimeter(55)); // libellé
                info.AddColumn(Unit.FromMillimeter(50)); // valeur (assez large pour tenir sur 1 ligne)
                void AddInfo(string lib, string? val, bool rightAlign = true)
                {
                    var row = info.AddRow();
                    var lp = row.Cells[0].AddParagraph(lib); lp.Format.Font.Size = 11;
                    var rp = row.Cells[1].AddParagraph(val ?? "");
                    rp.Format.Font.Size = 11;
                    rp.Format.Alignment = rightAlign ? ParagraphAlignment.Right : ParagraphAlignment.Left;
                }

                AddInfo("Numéro de devis", numero);
                AddInfo("Date du devis", !string.IsNullOrWhiteSpace(devisDateText) ? devisDateText : DateTime.Now.ToString("dd/MM/yyyy"));
                AddInfo("Devis valable 30 jours", "", rightAlign: false);

                // “Conditions de paiement” sur 1 ligne (espaces insécables)
                var payText = (paymentTermsText ?? "—").Replace(" ", "\u00A0");
                AddInfo("Conditions de paiement", payText);

                // Barre rouge “Total à payer”
                decimal totalPanel = 0m;
                foreach (var (designation, qty, pu) in lines) totalPanel += (decimal)qty * (decimal)pu;
                if (discountEuro > 0) totalPanel = Math.Max(0m, totalPanel - discountEuro);

                var totalBar = cell.Elements.AddTable();
                totalBar.Borders.Visible = false;
                totalBar.AddColumn(Unit.FromMillimeter(65)); // libellé
                totalBar.AddColumn(Unit.FromMillimeter(40)); // montant
                var trb = totalBar.AddRow();
                trb.Cells[0].Shading.Color = Accent;
                trb.Cells[1].Shading.Color = Accent;

                var lpb = trb.Cells[0].AddParagraph("Total à payer");
                lpb.Format.Font.Color = Colors.White; lpb.Format.Font.Bold = true; lpb.Format.Font.Size = 11;

                var rpb = trb.Cells[1].AddParagraph(totalPanel.ToString("N2", culture) + " €");
                rpb.Format.Font.Color = Colors.White; rpb.Format.Font.Bold = true; rpb.Format.Font.Size = 11; rpb.Format.Alignment = ParagraphAlignment.Right;
            }

            // (5) Texte libre haut (juste sous la ligne client/panneau)
            if (!string.IsNullOrWhiteSpace(noteTop))
            {
                var p = sec.AddParagraph(noteTop);
                p.Format.SpaceBefore = Unit.FromMillimeter(2);
                p.Format.SpaceAfter = Unit.FromMillimeter(4);
            }

            // -- espace entre panneau devis et tableau articles (~ 18 mm ≈ 68 px)
            {
                var sep = sec.AddParagraph();
                sep.Format.SpaceBefore = Unit.FromMillimeter(18);
                sep.AddText(" "); // paragraphe vide porteur d'espacement
            }

            // ================== (6) TABLEAU DES ARTICLES ==================
            {
                var tbl = sec.AddTable();
                tbl.Borders.Width = 0.25;
                tbl.Borders.Color = Colors.Gray;      // lignes internes gris
                tbl.Borders.Left.Width = 0;          // pas de trait à gauche
                tbl.Borders.Right.Width = 0;          // pas de trait à droite

                // Colonnes (total 200 mm)
                tbl.AddColumn(Unit.FromMillimeter(109.3)); // Description
                tbl.AddColumn(Unit.FromMillimeter(18.6)); // Quantité
                tbl.AddColumn(Unit.FromMillimeter(32.4)); // Prix
                tbl.AddColumn(Unit.FromMillimeter(39.7)); // Montant

                // EN-TÊTE ROUGE (texte blanc gras)
                var hdr = tbl.AddRow();
                hdr.HeadingFormat = true;
                hdr.Shading.Color = Accent;
                hdr.Cells[0].AddParagraph("Description");
                var pQtyHdr = hdr.Cells[1].AddParagraph("Quantité");   // Paragraph
                pQtyHdr.Format.Alignment = ParagraphAlignment.Center;
                hdr.Cells[2].AddParagraph("Prix");
                hdr.Cells[3].AddParagraph("Montant");
                for (int i = 0; i < hdr.Cells.Count; i++)
                {
                    Cell c = hdr.Cells[i]!;
                    c.Format.Font.Color = Colors.White;
                    c.Format.Font.Bold = true;
                }
                hdr.Borders.Bottom.Color = Accent;
                hdr.Borders.Bottom.Width = 1;


                // Lignes
                decimal total = 0m;
                foreach (var (designation, qty, pu) in lines)
                {
                    var row = tbl.AddRow();
                    row.Cells[0].AddParagraph(designation ?? "");
                    row.Cells[1].AddParagraph(qty.ToString("0.##", culture)).Format.Alignment = ParagraphAlignment.Center; // Quantité centrée
                    row.Cells[2].AddParagraph(((decimal)pu).ToString("N2", culture) + " €").Format.Alignment = ParagraphAlignment.Right;
                    var montant = (decimal)qty * (decimal)pu;
                    total += montant;
                    row.Cells[3].AddParagraph(montant.ToString("N2", culture) + " €").Format.Alignment = ParagraphAlignment.Right;
                }

                // Remise ?
                if (discountEuro > 0m)
                {
                    var rr = tbl.AddRow();
                    // Col 0 (Description) vide
                    rr.Cells[0].AddParagraph(" ");
                    // Col 1 (Quantité) → "REMISE"
                    var pRem = rr.Cells[1].AddParagraph("REMISE");
                    pRem.Format.Alignment = ParagraphAlignment.Center;
                    // Col 2 (Prix) vide
                    rr.Cells[2].AddParagraph(" ");
                    // Col 3 (Montant) → valeur à droite
                    rr.Cells[3].AddParagraph("- " + discountEuro.ToString("N2", culture) + " €")
                               .Format.Alignment = ParagraphAlignment.Right;
                    rr.Borders.Bottom.Color = Accent;
                    rr.Borders.Bottom.Width = 1;
                    total -= discountEuro;
                    if (total < 0) total = 0;
                }


                // Séparateur rouge avant Total (inchangé)
                var sep = tbl.AddRow();
                sep.Borders.Top.Color = Accent;
                sep.Borders.Top.Width = 1;
                sep.Cells[0].MergeRight = 3;
                sep.Cells[0].AddParagraph(" ");

                // Total HT (libellé à gauche comme avant)
                var tot = tbl.AddRow();

                // Trait de séparation (rouge) au-dessus
                tot.Borders.Top.Color = Accent;
                tot.Borders.Top.Width = 1;

                // Col 0 vide
                tot.Cells[0].AddParagraph(" ");

                // Col 1 → "Total HT"
                var pTotLbl = tot.Cells[1].AddParagraph("Total HT");
                pTotLbl.Format.Alignment = ParagraphAlignment.Center;

                // Col 2 vide
                tot.Cells[2].AddParagraph(" ");

                // Col 3 → montant à droite en gras
                var pTotVal = tot.Cells[3].AddParagraph(total.ToString("N2", culture) + " €");
                pTotVal.Format.Alignment = ParagraphAlignment.Right;
                pTotVal.Format.Font.Bold = true;
            }

            // (7–9) Mention 293B + Texte bas (gauche) ET Signature (droite) dans une même grille 2 colonnes
            {
                var side = sec.AddTable();
                side.Borders.Visible = false;
                side.AddColumn(Unit.FromMillimeter(120)); // zone texte (gauche)
                side.AddColumn(Unit.FromMillimeter(80));  // zone signature (droite)

                // Ligne haute : contenu texte à gauche + titre signature à droite
                var rTop = side.AddRow();

                // (1) + (2) dans la cellule gauche
                var left = rTop.Cells[0];
                if (showMention293B)
                {
                    var p = left.AddParagraph("TVA non applicable, article 293B du code général des impôts.");
                    p.Style = "SmallItalic";
                    p.Format.SpaceAfter = Unit.FromMillimeter(2);
                }
                if (!string.IsNullOrWhiteSpace(noteBottom))
                {
                    var p = left.AddParagraph(noteBottom);
                    p.Style = "Small";
                    p.Format.SpaceBefore = Unit.FromMillimeter(2);
                }

                // Titre du bloc signature à droite
                var right = rTop.Cells[1];
                var title = right.AddParagraph("Cachet ou signature date et mention\n'Bon pour accord'");
                title.Style = "Small";
                title.Format.Alignment = ParagraphAlignment.Center;
                title.Format.SpaceAfter = Unit.FromMillimeter(3);

                // Ligne basse : cadre de signature (hauteur fixe) à droite
                var rBox = side.AddRow();
                rBox.Height = Unit.FromMillimeter(30.9);
                rBox.HeightRule = RowHeightRule.Exactly;
                rBox.Cells[0].AddParagraph(" "); // cellule gauche vide (pas de grand blanc autonome)

                var sigCell = rBox.Cells[1];
                sigCell.Borders.Color = Colors.Black;
                sigCell.Borders.Left.Width = 1.25;
                sigCell.Borders.Top.Width = 1.25;
                sigCell.Borders.Right.Width = 1.25;
                sigCell.Borders.Bottom.Width = 1.25;
                sigCell.Shading.Color = Colors.White;
                sigCell.AddParagraph("\u00A0"); // contenu non vide
            }

            // (10) En options — tableau simple "Désignation | Prix | [ ]"
            var options = devisId.HasValue ? new DevisService().GetOptions(devisId.Value) : new List<DevisOption>();
            if (options.Count > 0)
            {
                // Titre
                var title = sec.AddParagraph("En options");
                title.Style = "H-Title";
                title.Format.SpaceBefore = Unit.FromMillimeter(6);
                title.Format.SpaceAfter = Unit.FromMillimeter(3);

                // Tableau options (3 colonnes)
                var opt = sec.AddTable();
                opt.Borders.Width = 0.25;
                opt.Borders.Color = Colors.Gray;
                opt.Borders.Left.Width = 0;
                opt.Borders.Right.Width = 0;

                opt.AddColumn(Unit.FromMillimeter(150)); // Désignation
                opt.AddColumn(Unit.FromMillimeter(30));  // Prix
                opt.AddColumn(Unit.FromMillimeter(15));  // case

                // En-tête
                var oh = opt.AddRow();
                oh.HeadingFormat = true;
                oh.Shading.Color = Accent;
                oh.Cells[0].AddParagraph("Désignation");
                oh.Cells[1].AddParagraph("Prix");
                oh.Cells[2].AddParagraph(" ");
                for (int i = 0; i < oh.Cells.Count; i++)
                {
                    var c = oh.Cells[i];
                    c.Format.Font.Color = Colors.White;
                    c.Format.Font.Bold = true;
                    if (i == 1) c.Format.Alignment = ParagraphAlignment.Right;
                    if (i == 2) c.Format.Alignment = ParagraphAlignment.Center;
                }
                oh.Borders.Bottom.Color = Accent;
                oh.Borders.Bottom.Width = 1;

                // Lignes
                foreach (var o in options)
                {
                    var r = opt.AddRow();
                    r.Cells[0].AddParagraph(o.Libelle ?? "");
                    r.Cells[1].AddParagraph(o.Prix.ToString("N2", culture) + " €").Format.Alignment = ParagraphAlignment.Right;
                    r.Cells[2].AddParagraph("[ ]").Format.Alignment = ParagraphAlignment.Center;
                }
            }

            // Helper pied : ajoute "Label : valeur" en centrant et en insérant "  -  " entre items
            void addCenteredPair(Paragraph p, string label, string? value, ref bool first)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                if (!first) p.AddText("   -   ");
                p.AddFormattedText(label + " : ", TextFormat.Bold);
                p.AddText(value.Trim());
                first = false;
            }

            // (11) Pied de page — centré, gris foncé, séparateurs "  -  "
            {
                var dark = Colors.DimGray;

                // L1 — centrée, gras : nom + adresse complète
                var foot1 = sec.Footers.Primary.AddParagraph();
                foot1.Format.Alignment = ParagraphAlignment.Center;
                foot1.Format.Font.Size = 10;
                foot1.Format.Font.Color = dark;
                foot1.Format.Font.Bold = true;
                var a1 = (company.NomCommercial ?? cfg.BusinessName ?? "").Trim();
                var a2 = $"{company.Adresse1} {company.Adresse2}".Trim();
                var a3 = $"{company.CodePostal} {company.Ville} {company.Pays}".Trim();
                foot1.AddText($"{a1} {a2} {a3}".Replace("  ", " ").Trim());

                // L2
                var p2 = sec.Footers.Primary.AddParagraph();
                p2.Format.Alignment = ParagraphAlignment.Center;
                p2.Format.Font.Size = 10;
                p2.Format.Font.Color = Colors.DimGray;
                bool f2 = true;
                addCenteredPair(p2, "N° SIRET", company.Siret, ref f2);
                addCenteredPair(p2, "Tél", company.Telephone, ref f2);
                addCenteredPair(p2, "Email", company.Email, ref f2);

                // L3
                var p3 = sec.Footers.Primary.AddParagraph();
                p3.Format.Alignment = ParagraphAlignment.Center;
                p3.Format.Font.Size = 10;
                p3.Format.Font.Color = Colors.DimGray;
                bool f3 = true;
                addCenteredPair(p3, "Titulaire du compte", bankHolder, ref f3);
                addCenteredPair(p3, "Banque", bankName, ref f3);

                // L4
                var p4 = sec.Footers.Primary.AddParagraph();
                p4.Format.Alignment = ParagraphAlignment.Center;
                p4.Format.Font.Size = 10;
                p4.Format.Font.Color = Colors.DimGray;
                bool f4 = true;
                addCenteredPair(p4, "IBAN", iban, ref f4);
                addCenteredPair(p4, "BIC", bic, ref f4);
            }

            // === CONCAT DES ANNEXES et RENDU ===
            // Rendu vers un fichier temporaire
            var outDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outDir))
                Directory.CreateDirectory(outDir);

            var renderer = new PdfDocumentRenderer();
            renderer.Document = doc;
            renderer.RenderDocument();

            // on sauve d'abord un "core" temporaire
            var corePath = Path.Combine(Paths.TempDir, $"devis-core-{Guid.NewGuid():N}.pdf");
            renderer.PdfDocument.Save(corePath);

            // Concat annexes → outputPath
            var annexes = devisId.HasValue ? new DevisService().GetAnnexes(devisId.Value) : new List<DevisAnnexe>();
            if (annexes.Count > 0)
            {
                ConcatPdfFiles(
                    files: BuildAnnexFileList(corePath, annexes),
                    outputPath: outputPath
                );
                try { File.Delete(corePath); } catch { /* ignore */ }
            }
            else
            {
                // pas d'annexe : on copie le core directement comme fichier final
                File.Copy(corePath, outputPath, overwrite: true);
                try { File.Delete(corePath); } catch { /* ignore */ }
            }
        }
        private static List<string> BuildAnnexFileList(string corePath, List<DevisAnnexe> annexes)
        {
            var files = new List<string> { corePath };
            foreach (var a in annexes)
            {
                if (string.IsNullOrWhiteSpace(a.Chemin)) continue;
                var full = Path.Combine(Paths.AssetsDir, a.Chemin.Replace("/", Path.DirectorySeparatorChar.ToString()));
                if (File.Exists(full)) files.Add(full);
            }
            return files;
        }

        private static void ConcatPdfFiles(List<string> files, string outputPath)
        {
            using var outDoc = new PdfSharp.Pdf.PdfDocument();
            foreach (var f in files)
            {
                using var src = PdfSharp.Pdf.IO.PdfReader.Open(f, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
                for (int i = 0; i < src.PageCount; i++)
                    outDoc.AddPage(src.Pages[i]);
            }
            outDoc.Save(outputPath);
        }
    }
}
