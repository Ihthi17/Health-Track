using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Security;
using System.Text;

namespace HealthTrack.Services
{
    /// <summary>
    /// Turns a report (already produced as CSV text) into the other export formats
    /// WITHOUT any extra NuGet packages:
    ///   - Excel: a real .xlsx file (Office Open XML written with System.IO.Compression)
    ///   - PDF:   a printable HTML page that opens the browser's print dialog,
    ///            where the user chooses "Save as PDF"
    /// Reusing the CSV means every report type supports every format automatically.
    /// </summary>
    public static class ReportFileWriter
    {
        // ===================== CSV -> rows =====================

        /// <summary>
        /// Splits CSV text into rows and cells. Handles quoted values containing
        /// commas, quotes ("") and line breaks.
        /// </summary>
        public static List<List<string>> ParseCsv(byte[] csvBytes)
        {
            var text = Encoding.UTF8.GetString(csvBytes).TrimStart('﻿');   // remove BOM if present
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];

                if (inQuotes)
                {
                    if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else if (ch == '"') inQuotes = false;
                    else cell.Append(ch);
                }
                else if (ch == '"') inQuotes = true;
                else if (ch == ',') { row.Add(cell.ToString()); cell.Clear(); }
                else if (ch == '\r') { /* ignore - handled by \n */ }
                else if (ch == '\n') { row.Add(cell.ToString()); cell.Clear(); rows.Add(row); row = new List<string>(); }
                else cell.Append(ch);
            }

            if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
            return rows;
        }

        // ===================== rows -> Excel (.xlsx) =====================

        /// <summary>
        /// Builds a minimal but valid .xlsx workbook with one sheet.
        /// The first row and the first row of every block (after an empty row) are bold;
        /// numeric values are stored as numbers so Excel can sum/sort them.
        /// </summary>
        public static byte[] ToXlsx(List<List<string>> rows, string sheetName = "Report")
        {
            using var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                AddEntry(zip, "[Content_Types].xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                    "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                    "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                    "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                    "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                    "</Types>");

                AddEntry(zip, "_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                    "</Relationships>");

                AddEntry(zip, "xl/workbook.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
                    "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                    $"<sheets><sheet name=\"{Xml(SafeSheetName(sheetName))}\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
                    "</workbook>");

                AddEntry(zip, "xl/_rels/workbook.xml.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                    "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                    "</Relationships>");

                // Style 0 = normal, style 1 = bold
                AddEntry(zip, "xl/styles.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                    "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
                    "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
                    "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>" +
                    "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
                    "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                    "<cellXfs count=\"2\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
                    "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/></cellXfs>" +
                    "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
                    "</styleSheet>");

                // The sheet itself
                var sheet = new StringBuilder();
                sheet.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                sheet.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
                sheet.Append("<cols><col min=\"1\" max=\"1\" width=\"32\" customWidth=\"1\"/><col min=\"2\" max=\"12\" width=\"18\" customWidth=\"1\"/></cols>");
                sheet.Append("<sheetData>");

                bool previousEmpty = true;   // so the very first row is bold
                for (int r = 0; r < rows.Count; r++)
                {
                    var cells = rows[r];
                    bool isEmpty = cells.All(string.IsNullOrWhiteSpace);
                    int style = (!isEmpty && previousEmpty) ? 1 : 0;   // bold = first row of a block

                    sheet.Append($"<row r=\"{r + 1}\">");
                    for (int c = 0; c < cells.Count; c++)
                    {
                        var value = cells[c];
                        if (string.IsNullOrEmpty(value)) continue;

                        string reference = ColumnName(c) + (r + 1);
                        string styleAttr = style == 1 ? " s=\"1\"" : "";

                        // Store numbers as numbers, but keep values like phone numbers ("0764...") as text
                        bool isNumber = decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                                        && (value == "0" || !value.StartsWith("0") || value.StartsWith("0."));
                        if (isNumber)
                        {
                            sheet.Append($"<c r=\"{reference}\"{styleAttr}><v>{number.ToString(CultureInfo.InvariantCulture)}</v></c>");
                            continue;
                        }

                        sheet.Append($"<c r=\"{reference}\" t=\"inlineStr\"{styleAttr}><is><t xml:space=\"preserve\">{Xml(value)}</t></is></c>");
                    }
                    sheet.Append("</row>");
                    previousEmpty = isEmpty;
                }

                sheet.Append("</sheetData></worksheet>");
                AddEntry(zip, "xl/worksheets/sheet1.xml", sheet.ToString());
            }

            return stream.ToArray();
        }

        private static void AddEntry(ZipArchive zip, string path, string content)
        {
            var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }

        /// <summary>0 -> A, 1 -> B, ... 25 -> Z, 26 -> AA</summary>
        private static string ColumnName(int index)
        {
            var name = "";
            index++;
            while (index > 0)
            {
                int mod = (index - 1) % 26;
                name = (char)('A' + mod) + name;
                index = (index - 1) / 26;
            }
            return name;
        }

        /// <summary>Escapes text for XML and removes characters XML does not allow.</summary>
        private static string Xml(string value)
        {
            var clean = new string(value.Where(ch => ch == '\t' || ch == '\n' || ch == '\r' || ch >= ' ').ToArray());
            return SecurityElement.Escape(clean) ?? "";
        }

        /// <summary>Excel sheet names: max 31 chars, no : \ / ? * [ ]</summary>
        private static string SafeSheetName(string name)
        {
            var cleaned = new string(name.Where(ch => !":\\/?*[]".Contains(ch)).ToArray()).Trim();
            if (cleaned.Length == 0) cleaned = "Report";
            return cleaned.Length > 31 ? cleaned[..31] : cleaned;
        }

        // ===================== rows -> printable page ("Save as PDF") =====================

        /// <summary>
        /// Builds a clean, printable HTML page of the report and opens the print
        /// dialog automatically, so the user can choose "Save as PDF".
        /// Blocks separated by empty rows become separate tables; a block with a
        /// single cell is shown as a heading.
        /// </summary>
        public static string ToPrintableHtml(List<List<string>> rows, string title)
        {
            var html = new StringBuilder();
            html.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
            html.Append($"<title>{WebUtility.HtmlEncode(title)}</title>");
            html.Append("<style>");
            html.Append("body{font-family:'Segoe UI',Arial,sans-serif;color:#2D3E47;margin:32px;}");
            html.Append("h1{color:#123B4A;font-size:22px;margin:0 0 4px;}");
            html.Append(".meta{color:#6B7F89;font-size:12px;margin-bottom:20px;}");
            html.Append("h3{color:#123B4A;font-size:15px;margin:22px 0 6px;}");
            html.Append("table{border-collapse:collapse;width:100%;margin-bottom:10px;font-size:12px;}");
            html.Append("th{background:#123B4A;color:#fff;text-align:left;padding:6px 8px;}");
            // Keep the coloured headings when printing (browsers drop backgrounds by default)
            html.Append("th,tr:nth-child(even) td{-webkit-print-color-adjust:exact;print-color-adjust:exact;}");
            html.Append("td{border-bottom:1px solid #E0EBF0;padding:5px 8px;}");
            html.Append("tr:nth-child(even) td{background:#F3F7F8;}");
            html.Append(".noprint{margin-bottom:16px;} @media print{.noprint{display:none;} body{margin:12mm;}}");
            html.Append("button{background:#16A6A1;color:#fff;border:0;padding:8px 16px;border-radius:6px;cursor:pointer;}");
            html.Append("</style></head><body>");

            html.Append("<div class=\"noprint\"><button onclick=\"window.print()\">Print / Save as PDF</button></div>");
            html.Append($"<h1>{WebUtility.HtmlEncode(title)}</h1>");
            html.Append($"<div class=\"meta\">HealthTrack · generated {DateTime.Now:dd MMM yyyy, hh:mm tt}</div>");

            // Split rows into blocks separated by empty rows
            var blocks = new List<List<List<string>>>();
            var current = new List<List<string>>();
            foreach (var row in rows)
            {
                if (row.All(string.IsNullOrWhiteSpace))
                {
                    if (current.Count > 0) { blocks.Add(current); current = new(); }
                }
                else current.Add(row);
            }
            if (current.Count > 0) blocks.Add(current);

            bool firstBlock = true;
            foreach (var block in blocks)
            {
                int start = 0;

                // A first block whose first line repeats the title is skipped (already shown as h1)
                if (firstBlock && block[0].Count(c => !string.IsNullOrWhiteSpace(c)) == 1)
                {
                    start = 1;
                }
                firstBlock = false;

                // A single-cell line at the start of a block is a section heading
                if (start < block.Count && block[start].Count(c => !string.IsNullOrWhiteSpace(c)) == 1 && block.Count - start > 1)
                {
                    html.Append($"<h3>{WebUtility.HtmlEncode(block[start].First(c => !string.IsNullOrWhiteSpace(c)))}</h3>");
                    start++;
                }
                if (start >= block.Count) continue;

                int columns = block.Skip(start).Max(r => r.Count);
                bool firstRowIsHeader = block.Count - start > 1 && block[start].Count == columns && columns > 2;

                html.Append("<table>");
                for (int r = start; r < block.Count; r++)
                {
                    string tag = (firstRowIsHeader && r == start) ? "th" : "td";
                    html.Append("<tr>");
                    for (int c = 0; c < columns; c++)
                    {
                        var value = c < block[r].Count ? block[r][c] : "";
                        html.Append($"<{tag}>{WebUtility.HtmlEncode(value)}</{tag}>");
                    }
                    html.Append("</tr>");
                }
                html.Append("</table>");
            }

            html.Append("<script>window.addEventListener('load',function(){setTimeout(function(){window.print();},400);});</script>");
            html.Append("</body></html>");
            return html.ToString();
        }
    }
}
