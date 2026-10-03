using System.Globalization;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Domain.Common;
using RepairShop.Domain.Customers;
using RepairShop.Domain.Inventory;

namespace RepairShop.Application.Imports;

/// <summary>
/// Bulk import of customers and inventory from Excel/CSV (for shops coming from spreadsheets).
/// Always validate with dryRun=true first: nothing is written and every error is reported per row.
/// </summary>
public sealed class ImportService
{
    private const int MaxRows = 5000;

    private static readonly Dictionary<string, string[]> CustomerColumns = new()
    {
        ["name"] = new[] { "nombre", "name", "cliente", "fullname", "nombre y apellido", "apellido y nombre", "razon social" },
        ["phone"] = new[] { "telefono", "phone", "celular", "whatsapp", "tel", "movil" },
        ["email"] = new[] { "email", "mail", "correo", "e-mail" },
        ["notes"] = new[] { "notas", "notes", "observaciones", "comentarios" },
        ["document"] = new[] { "dni", "cuit", "cuil", "documento", "document" },
        ["address"] = new[] { "direccion", "address", "domicilio" },
        ["tags"] = new[] { "etiquetas", "tags" }
    };

    private static readonly Dictionary<string, string[]> InventoryColumns = new()
    {
        ["sku"] = new[] { "sku", "codigo", "code", "cod", "articulo" },
        ["name"] = new[] { "nombre", "name", "descripcion", "description", "producto" },
        ["quantity"] = new[] { "cantidad", "stock", "quantity", "qty", "existencia" },
        ["cost"] = new[] { "costo", "cost", "precio costo", "precio de costo" },
        ["price"] = new[] { "precio", "price", "precio venta", "precio de venta", "pvp" },
        ["currency"] = new[] { "moneda", "currency" },
        ["min"] = new[] { "minimo", "stock minimo", "min", "min stock", "punto de pedido" },
        ["barcode"] = new[] { "codigo de barras", "barcode", "ean", "upc" },
        ["category"] = new[] { "categoria", "category", "rubro" },
        ["sellable"] = new[] { "vendible", "sellable", "venta mostrador" },
        ["location"] = new[] { "ubicacion", "location", "deposito" }
    };

    private readonly ISpreadsheetReader _reader;
    private readonly IExcelExporter _excel;
    private readonly ICustomerRepository _customers;
    private readonly IInventoryItemRepository _items;
    private readonly IInventoryAdjustmentRepository _adjustments;
    private readonly IShopRepository _shops;
    private readonly IAuditLog _audit;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public ImportService(
        ISpreadsheetReader reader,
        IExcelExporter excel,
        ICustomerRepository customers,
        IInventoryItemRepository items,
        IInventoryAdjustmentRepository adjustments,
        IShopRepository shops,
        IAuditLog audit,
        IUnitOfWork uow,
        IDateTimeProvider clock)
    {
        _reader = reader;
        _excel = excel;
        _customers = customers;
        _items = items;
        _adjustments = adjustments;
        _shops = shops;
        _audit = audit;
        _uow = uow;
        _clock = clock;
    }

    public async Task<ImportResult> ImportCustomersAsync(Guid shopId, Stream content, string fileName, bool dryRun, Actor actor, CancellationToken ct)
    {
        var rows = Read(content, fileName);
        var map = MapColumns(rows, CustomerColumns);
        Require(map, "name", "phone");

        var now = _clock.UtcNow;
        var errors = new List<ImportError>();
        var seenKeys = new HashSet<string>();
        int created = 0, skipped = 0;

        for (var i = 0; i < rows.Count; i++)
        {
            var rowNumber = i + 2; // header is row 1
            var r = rows[i];
            var name = Get(r, map, "name");
            var phone = Get(r, map, "phone");
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(phone)) { skipped++; continue; }

            try
            {
                var key = PhoneNumber.Key(phone);
                if (key.Length >= 6 && (seenKeys.Contains(key) || (await _customers.FindByPhoneKeyAsync(shopId, key, null, ct)).Count > 0))
                {
                    skipped++;
                    errors.Add(new ImportError(rowNumber, $"Teléfono duplicado ({phone}): se omite."));
                    continue;
                }

                var c = new Customer(shopId, name ?? "", phone ?? "", Get(r, map, "notes"), now);
                c.UpdateContact(Get(r, map, "email"), Get(r, map, "address"), Get(r, map, "tags"), now);
                var doc = PhoneNumber.Digits(Get(r, map, "document"));
                if (doc.Length == 11) c.UpdateFiscal(CustomerDocumentType.Cuit, doc, CustomerTaxCondition.ConsumidorFinal, now);
                else if (doc.Length is >= 7 and <= 8) c.UpdateFiscal(CustomerDocumentType.Dni, doc, CustomerTaxCondition.ConsumidorFinal, now);

                seenKeys.Add(key);
                if (!dryRun) await _customers.AddAsync(c, ct);
                created++;
            }
            catch (DomainException ex)
            {
                errors.Add(new ImportError(rowNumber, ex.Message));
            }
        }

        if (!dryRun && created > 0)
        {
            await _audit.AddAsync(shopId, "import", Guid.NewGuid(), "customers_imported", actor, new { created, skipped, errors = errors.Count, fileName }, ct);
            await _uow.SaveChangesAsync(ct);
        }

        return new ImportResult("customers", dryRun, rows.Count, created, 0, skipped, errors.Take(500).ToList(), rows.Take(20).ToList(), map.Keys.ToList());
    }

    public async Task<ImportResult> ImportInventoryAsync(Guid shopId, Stream content, string fileName, bool dryRun, bool updateExisting, Actor actor, CancellationToken ct)
    {
        var rows = Read(content, fileName);
        var map = MapColumns(rows, InventoryColumns);
        Require(map, "sku", "name");

        var shop = await _shops.GetByIdAsync(shopId, ct) ?? throw new NotFoundException("Sucursal no encontrada.");
        var now = _clock.UtcNow;
        var errors = new List<ImportError>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int created = 0, updated = 0, skipped = 0;

        for (var i = 0; i < rows.Count; i++)
        {
            var rowNumber = i + 2;
            var r = rows[i];
            var sku = Get(r, map, "sku");
            var name = Get(r, map, "name");
            if (string.IsNullOrWhiteSpace(sku) && string.IsNullOrWhiteSpace(name)) { skipped++; continue; }

            try
            {
                if (string.IsNullOrWhiteSpace(sku)) throw new DomainException("Falta el SKU.");
                if (!seen.Add(sku)) throw new DomainException($"SKU repetido en el archivo ({sku}).");

                var qty = ParseInt(Get(r, map, "quantity")) ?? 0;
                var cost = ParseDecimal(Get(r, map, "cost"));
                var price = ParseDecimal(Get(r, map, "price"));
                var currency = Get(r, map, "currency") ?? shop.DefaultCurrency;
                var min = ParseInt(Get(r, map, "min")) ?? 0;
                var sellable = ParseBool(Get(r, map, "sellable")) ?? price is not null;
                if (qty < 0) throw new DomainException("La cantidad no puede ser negativa.");

                var existing = await _items.GetBySkuAsync(shopId, sku, ct);
                if (existing is not null)
                {
                    if (!updateExisting) { skipped++; continue; }
                    existing.Update(name ?? existing.Name, true, now);
                    existing.UpdateCatalog(Get(r, map, "category") ?? existing.Category, Get(r, map, "barcode") ?? existing.Barcode, min, existing.TrackStock,
                        sellable, price ?? existing.SalePrice, price is null ? existing.SalePriceCurrency : currency, existing.WarrantyDays, Get(r, map, "location") ?? existing.Location, now);
                    if (cost is not null) existing.UpdateCost(cost, currency, now);
                    updated++;
                    continue;
                }

                var item = new InventoryItem(shopId, sku, name ?? sku, qty, cost, cost is null ? null : currency, true, now);
                item.UpdateCatalog(Get(r, map, "category"), Get(r, map, "barcode"), min, true, sellable, price, price is null ? null : currency, null, Get(r, map, "location"), now);
                if (!dryRun)
                {
                    await _items.AddAsync(item, ct);
                    if (qty > 0) await _adjustments.AddAsync(new InventoryAdjustment(shopId, item.Id, InventoryAdjustmentType.Correction, qty, "import", null, actor.UserId, now), ct);
                }
                created++;
            }
            catch (DomainException ex)
            {
                errors.Add(new ImportError(rowNumber, ex.Message));
            }
        }

        if (!dryRun && created + updated > 0)
        {
            await _audit.AddAsync(shopId, "import", Guid.NewGuid(), "inventory_imported", actor, new { created, updated, skipped, errors = errors.Count, fileName }, ct);
            await _uow.SaveChangesAsync(ct);
        }

        return new ImportResult("inventory", dryRun, rows.Count, created, updated, skipped, errors.Take(500).ToList(), rows.Take(20).ToList(), map.Keys.ToList());
    }

    public async Task<byte[]> ExportCustomersAsync(Guid shopId, CancellationToken ct)
    {
        var rows = new List<object?[]>();
        for (var skip = 0; ; skip += 200)
        {
            var (items, total) = await _customers.SearchAsync(shopId, new CustomerSearchOptions(Skip: skip, Take: 200, SortBy: "name", SortDir: "asc"), ct);
            rows.AddRange(items.Select(c => new object?[] { c.FullName, c.Phone, c.Email, c.DocumentNumber, c.Address, c.Tags, c.Notes, c.NotificationsOptIn ? "Sí" : "No", c.CreatedAtUtc }));
            if (skip + 200 >= total || items.Count == 0) break;
        }

        return _excel.Export(new[] { new SheetData("Clientes", new[] { "Nombre", "Teléfono", "Email", "Documento", "Dirección", "Etiquetas", "Notas", "Acepta mensajes", "Alta" }, rows) });
    }

    public async Task<byte[]> ExportInventoryAsync(Guid shopId, CancellationToken ct)
    {
        var rows = new List<object?[]>();
        for (var skip = 0; ; skip += 200)
        {
            var (items, total) = await _items.SearchAsync(shopId, new InventorySearchOptions(IncludeInactive: true, Skip: skip, Take: 200, SortBy: "sku"), ct);
            rows.AddRange(items.Select(i => new object?[] { i.Sku, i.Name, i.Category, i.Barcode, i.QuantityOnHand, i.MinStock, i.UnitCost, i.UnitCostCurrency, i.SalePrice, i.SalePriceCurrency, i.IsSellable ? "Sí" : "No", i.Location, i.IsActive ? "Sí" : "No" }));
            if (skip + 200 >= total || items.Count == 0) break;
        }

        return _excel.Export(new[] { new SheetData("Inventario", new[] { "SKU", "Nombre", "Categoría", "Código de barras", "Stock", "Stock mínimo", "Costo", "Moneda costo", "Precio", "Moneda precio", "Vendible", "Ubicación", "Activo" }, rows) });
    }

    // ---------------------------------------------------------------------------------------------

    private IReadOnlyList<IReadOnlyDictionary<string, string>> Read(Stream content, string fileName)
    {
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows;
        try { rows = _reader.Read(content, fileName); }
        catch (DomainException) { throw; }
        catch (Exception) { throw new DomainException("No se pudo leer el archivo. Usá un .xlsx o .csv con encabezados en la primera fila."); }

        if (rows.Count == 0) throw new DomainException("El archivo no tiene filas.");
        if (rows.Count > MaxRows) throw new DomainException($"El archivo supera el máximo de {MaxRows} filas.");
        return rows;
    }

    private static Dictionary<string, string> MapColumns(IReadOnlyList<IReadOnlyDictionary<string, string>> rows, Dictionary<string, string[]> synonyms)
    {
        var headers = rows.SelectMany(r => r.Keys).Distinct().ToList();
        var map = new Dictionary<string, string>();
        foreach (var (field, names) in synonyms)
        {
            var header = headers.FirstOrDefault(h => names.Contains(Normalize(h)));
            if (header is not null) map[field] = header;
        }
        return map;
    }

    private static void Require(Dictionary<string, string> map, params string[] fields)
    {
        var missing = fields.Where(f => !map.ContainsKey(f)).ToList();
        if (missing.Count > 0) throw new DomainException($"Faltan columnas obligatorias: {string.Join(", ", missing)}.");
    }

    private static string? Get(IReadOnlyDictionary<string, string> row, Dictionary<string, string> map, string field)
        => map.TryGetValue(field, out var header) && row.TryGetValue(header, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

    private static string Normalize(string header)
    {
        var s = header.Trim().ToLowerInvariant().Replace('_', ' ');
        var map = new Dictionary<char, char> { ['á'] = 'a', ['é'] = 'e', ['í'] = 'i', ['ó'] = 'o', ['ú'] = 'u', ['ñ'] = 'n' };
        return new string(s.Select(c => map.TryGetValue(c, out var r) ? r : c).ToArray());
    }

    private static int? ParseInt(string? v) => decimal.TryParse(NormalizeNumber(v), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? (int)d : null;

    private static decimal? ParseDecimal(string? v) => decimal.TryParse(NormalizeNumber(v), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;

    private static bool? ParseBool(string? v) => v?.Trim().ToLowerInvariant() switch
    {
        "si" or "sí" or "s" or "yes" or "y" or "true" or "1" or "x" => true,
        "no" or "n" or "false" or "0" => false,
        _ => null
    };

    /// <summary>Accepts "1.234,56", "1,234.56", "1234,56" and "$ 1.234".</summary>
    public static string? NormalizeNumber(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var s = new string(v.Where(c => char.IsDigit(c) || c is ',' or '.' or '-').ToArray());
        var commas = s.Count(c => c == ',');
        var dots = s.Count(c => c == '.');
        var lastComma = s.LastIndexOf(',');
        var lastDot = s.LastIndexOf('.');

        if (commas > 0 && dots > 0)
            return lastComma > lastDot ? s.Replace(".", "").Replace(',', '.') : s.Replace(",", "");  // 1.234,56 | 1,234.56
        if (dots > 1) return s.Replace(".", "");                                                     // 1.234.567
        if (commas > 1) return s.Replace(",", "");                                                   // 1,234,567
        if (dots == 1 && s.Length - lastDot - 1 == 3) return s.Replace(".", "");                     // 1.234 (AR thousands)
        if (commas == 1) return s.Replace(',', '.');                                                 // 12,50 (AR decimals)
        return s;
    }
}
