using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Common;

namespace RepairShop.Infrastructure.Spreadsheets;

public sealed class ClosedXmlExcelExporter : IExcelExporter
{
    public byte[] Export(IReadOnlyList<SheetData> sheets)
    {
        using var wb = new XLWorkbook();
        foreach (var sheet in sheets)
        {
            var ws = wb.Worksheets.Add(SafeName(sheet.Name));
            for (var c = 0; c < sheet.Headers.Count; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = sheet.Headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
            }

            for (var r = 0; r < sheet.Rows.Count; r++)
            {
                var row = sheet.Rows[r];
                for (var c = 0; c < row.Length; c++)
                {
                    var cell = ws.Cell(r + 2, c + 1);
                    switch (row[c])
                    {
                        case null: break;
                        case decimal d: cell.Value = d; cell.Style.NumberFormat.Format = "#,##0.00"; break;
                        case double db: cell.Value = db; break;
                        case int i: cell.Value = i; break;
                        case long l: cell.Value = l; break;
                        case DateTime dt: cell.Value = dt; cell.Style.DateFormat.Format = "dd/MM/yyyy HH:mm"; break;
                        case DateOnly d0: cell.Value = d0.ToDateTime(TimeOnly.MinValue); cell.Style.DateFormat.Format = "dd/MM/yyyy"; break;
                        case bool b: cell.Value = b ? "Sí" : "No"; break;
                        default: cell.Value = row[c]!.ToString(); break;
                    }
                }
            }

            ws.SheetView.FreezeRows(1);
            if (sheet.Rows.Count > 0) ws.Range(1, 1, sheet.Rows.Count + 1, Math.Max(1, sheet.Headers.Count)).SetAutoFilter();
            ws.Columns().AdjustToContents(1, Math.Min(sheet.Rows.Count + 1, 200));
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static string SafeName(string name)
    {
        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var clean = new string(name.Where(c => !invalid.Contains(c)).ToArray());
        return clean.Length > 31 ? clean[..31] : clean.Length == 0 ? "Hoja" : clean;
    }
}

public sealed class SpreadsheetReader : ISpreadsheetReader
{
    public IReadOnlyList<IReadOnlyDictionary<string, string>> Read(Stream content, string fileName)
    {
        var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        return ext switch
        {
            ".xlsx" or ".xlsm" => ReadExcel(content),
            ".csv" or ".txt" => ReadCsv(content),
            _ => throw new DomainException("Formato no soportado: usá .xlsx o .csv.")
        };
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> ReadExcel(Stream content)
    {
        using var wb = new XLWorkbook(content);
        var ws = wb.Worksheets.First();
        var used = ws.RangeUsed();
        if (used is null) return Array.Empty<IReadOnlyDictionary<string, string>>();

        var headerRow = used.FirstRow();
        var headers = headerRow.Cells().Select((c, i) => (Index: c.Address.ColumnNumber, Name: c.GetString().Trim())).Where(h => h.Name.Length > 0).ToList();

        var rows = new List<IReadOnlyDictionary<string, string>>();
        foreach (var row in used.RowsUsed().Skip(1))
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (index, name) in headers)
            {
                var cell = row.Cell(index - used.FirstColumn().ColumnNumber() + 1);
                dict[name] = cell.DataType == XLDataType.Number
                    ? cell.GetDouble().ToString(CultureInfo.InvariantCulture)
                    : cell.GetFormattedString().Trim();
            }
            rows.Add(dict);
        }

        return rows;
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> ReadCsv(Stream content)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        var firstLine = text.Split('\n').FirstOrDefault() ?? "";
        var delimiter = firstLine.Count(c => c == ';') > firstLine.Count(c => c == ',') ? ";" : firstLine.Contains('\t') ? "\t" : ",";

        using var csv = new CsvReader(new StringReader(text), new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            BadDataFound = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim,
            DetectColumnCountChanges = false
        });

        var rows = new List<IReadOnlyDictionary<string, string>>();
        if (!csv.Read() || !csv.ReadHeader()) return rows;
        var headers = csv.HeaderRecord?.Where(h => !string.IsNullOrWhiteSpace(h)).ToArray() ?? Array.Empty<string>();
        while (csv.Read())
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in headers) dict[h.Trim()] = csv.GetField(h) ?? "";
            rows.Add(dict);
        }

        return rows;
    }
}
