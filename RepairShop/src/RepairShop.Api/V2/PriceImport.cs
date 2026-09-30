using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Security;
using RepairShop.Domain.Common;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace RepairShop.Api.V2;

public sealed record PriceFile(string FileName, string Base64, string Supplier, string Currency);

public sealed partial class PremiumController
{
    [HttpPost("prices/preview"), Authorize(Policy = Policies.AdminOnly), RequestSizeLimit(8_000_000)]
    public IActionResult Preview(PriceFile body) {
        var supplier = Text(body.Supplier, "Proveedor", 2, 120); var currency = Currency(body.Currency);
        byte[] bytes;
        try { bytes = Convert.FromBase64String(body.Base64 ?? ""); } catch { throw new DomainException("El archivo no tiene un formato válido."); }
        if (bytes.Length is < 1 or > 5_000_000) throw new DomainException("El archivo debe pesar hasta 5 MB.");
        var ext = Path.GetExtension(body.FileName).ToLowerInvariant();
        var rows = new List<PriceRow>(); var warnings = new List<string>();
        try {
            if (ext == ".pdf") {
                using var pdf = PdfDocument.Open(bytes);
                if (pdf.NumberOfPages > 50) throw new DomainException("Importá un PDF de hasta 50 páginas.");
                var lines = pdf.GetPages().SelectMany(p => ContentOrderTextExtractor.GetText(p).Split('\n')).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
                if (lines.Count == 0) throw new DomainException("Este PDF no tiene texto legible. Usá un Excel/CSV o copiá los precios manualmente; los escaneos requieren OCR.");
                foreach (var line in lines) {
                    if (rows.Count >= 500) throw new DomainException("El archivo supera las 500 filas.");
                    var match = Regex.Match(line, @"^(?<name>.+?)\s+(?<currency>ARS|USD|\$)?\s*(?<price>\d[\d.,]*)\s*$", RegexOptions.IgnoreCase);
                    if (!match.Success || !TryPrice(match.Groups["price"].Value, out var cost)) continue;
                    var name = match.Groups["name"].Value.Trim();
                    var skuMatch = Regex.Match(name, @"^(?<sku>[A-Za-z0-9]+[-_][A-Za-z0-9_-]+)\s+(?<description>.+)$");
                    var rowCurrency = match.Groups["currency"].Value.ToUpperInvariant() switch { "USD" => "USD", "ARS" => "ARS", _ => currency };
                    rows.Add(new PriceRow(supplier, skuMatch.Success ? skuMatch.Groups["sku"].Value : $"PDF-{rows.Count + 1:000}", skuMatch.Success ? skuMatch.Groups["description"].Value : name, "", "", cost, rowCurrency));
                }
                warnings.Add("PDF: revisá cada fila, código, moneda y equivalencia. La lectura toma líneas terminadas en un precio; puede omitir encabezados o tablas complejas.");
                warnings.Add($"Se extrajeron {rows.Count} precios candidatos de {lines.Count} líneas de texto. No se guardó ningún precio todavía.");
            } else {
                var cells = new List<string[]>(); var numericCosts = new Dictionary<int, decimal>();
                if (ext == ".xlsx") {
                    using (var zipStream = new MemoryStream(bytes)) using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Read))
                        if (zip.Entries.Sum(x => x.Length) > 30_000_000) throw new DomainException("El Excel es demasiado grande una vez descomprimido.");
                    using var stream = new MemoryStream(bytes); using var book = new XLWorkbook(stream);
                    var sheet = book.Worksheets.First(); var range = sheet.RangeUsed();
                    if (range is null) throw new DomainException("La primera hoja está vacía.");
                    if (range.RowCount() > 501 || range.ColumnCount() > 30) throw new DomainException("La primera hoja debe tener hasta 500 productos y 30 columnas.");
                    foreach (var row in range.Rows()) cells.Add(row.Cells().Select(c => c.GetFormattedString(CultureInfo.InvariantCulture)).ToArray());
                    var header = cells[0].Select(NormalizeHeader).ToArray(); var costIndex = Array.FindIndex(header, x => x is "costo" or "precio" or "unitcost" or "cost");
                    if (costIndex >= 0) for (var i = 1; i < cells.Count; i++) {
                        var cell = range.Cell(i + 1, costIndex + 1);
                        if (cell.DataType == XLDataType.Number) numericCosts[i] = cell.GetValue<decimal>();
                    }
                } else if (ext == ".csv" || ext == ".txt") {
                    var text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
                    var first = text.Split('\n')[0]; var separator = first.Contains(';') ? ';' : first.Contains('\t') ? '\t' : ',';
                    cells = ParseDelimited(text, separator);
                    if (cells.Count > 501) throw new DomainException("Importá hasta 500 productos por archivo.");
                } else throw new DomainException("Usá un archivo .xlsx, .csv o .pdf con texto.");
                if (cells.Count < 2) throw new DomainException("El archivo necesita encabezados y al menos un producto.");
                var headers = cells[0].Select(NormalizeHeader).ToArray();
                int Col(params string[] names) => Array.FindIndex(headers, x => names.Contains(x));
                var sku = Col("sku", "codigo", "code"); var nameCol = Col("descripcion", "producto", "nombre", "description"); var price = Col("costo", "precio", "unitcost", "cost");
                var compat = Col("compatibilidad", "modelo", "compatibility"); var quality = Col("calidad", "quality"); var curr = Col("moneda", "currency"); var supp = Col("proveedor", "supplier");
                if (sku < 0 || nameCol < 0 || price < 0) throw new DomainException("Faltan columnas. Usá: codigo;descripcion;compatibilidad;calidad;costo;moneda. Podés descargar el ejemplo.");
                for (var i = 1; i < cells.Count; i++) {
                    var row = cells[i]; string Value(int index) => index >= 0 && index < row.Length ? row[index].Trim() : "";
                    if (row.All(string.IsNullOrWhiteSpace)) continue;
                    decimal cost;
                    if (!numericCosts.TryGetValue(i, out cost) && !TryPrice(Value(price), out cost)) throw new DomainException($"Fila {i + 1}: el costo no es válido. No se importó ninguna fila.");
                    rows.Add(ValidatePrice(new PriceRow(Value(supp) is { Length: > 0 } s ? s : supplier, Value(sku), Value(nameCol), Value(compat), Value(quality), cost, Value(curr) is { Length: > 0 } c ? c : currency)));
                }
            }
        } catch (DomainException) { throw; }
          catch { throw new DomainException("No se pudo leer el archivo. Revisá que sea un Excel/PDF válido, sin contraseña, o un CSV UTF-8."); }
        if (rows.Count == 0) throw new DomainException("No se encontraron precios. Usá la plantilla CSV o cargá las ofertas manualmente.");
        return Ok(new { data = new { rows, warnings, source = Path.GetFileName(body.FileName) } });
    }
    private static string NormalizeHeader(string value) => string.Concat(value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));
    private static bool TryPrice(string value, out decimal result) {
        value = value.Replace("$", "").Replace("ARS", "", StringComparison.OrdinalIgnoreCase).Replace("USD", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (value.Contains(',') && value.Contains('.')) value = value.LastIndexOf(',') > value.LastIndexOf('.') ? value.Replace(".", "").Replace(',', '.') : value.Replace(",", "");
        else if (value.Contains(',')) value = value.Replace(',', '.');
        else if (Regex.IsMatch(value, @"^\d{1,3}(\.\d{3})+$")) value = value.Replace(".", "");
        return decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out result) && result >= 0 && result <= 1000000000m;
    }
    private static List<string[]> ParseDelimited(string text, char separator) {
        var result = new List<string[]>(); var row = new List<string>(); var field = new StringBuilder(); var quoted = false;
        for (var i = 0; i < text.Length; i++) {
            var c = text[i];
            if (c == '"') { if (quoted && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; } else quoted = !quoted; }
            else if (!quoted && c == separator) { row.Add(field.ToString()); field.Clear(); }
            else if (!quoted && c == '\n') { row.Add(field.ToString().TrimEnd('\r')); result.Add(row.ToArray()); row.Clear(); field.Clear(); }
            else field.Append(c);
        }
        if (quoted) throw new DomainException("El CSV tiene comillas sin cerrar.");
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString().TrimEnd('\r')); result.Add(row.ToArray()); }
        return result;
    }
}
