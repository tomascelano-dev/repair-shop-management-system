using Microsoft.EntityFrameworkCore;
using RepairShop.Domain.Customers;
using RepairShop.Domain.Devices;
using RepairShop.Domain.Premium;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Api.V2;

public static class PremiumSeeder
{
    public static async Task Seed(RepairShopDbContext db) {
        foreach (var shop in await db.Shops.OrderBy(x => x.CreatedAtUtc).ToListAsync()) {
            var branch = await db.PremiumBranches.Where(x => x.ShopId == shop.Id).OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync();
            if (branch is null) { branch = new PremiumBranch { ShopId = shop.Id, Name = "Casa central", Address = "Sucursal principal" }; db.PremiumBranches.Add(branch); await db.SaveChangesAsync(); }
            foreach (var item in await db.InventoryItems.Where(x => x.ShopId == shop.Id).ToListAsync()) {
                if (await db.StockLots.AnyAsync(x => x.ShopId == shop.Id && x.ItemId == item.Id)) continue;
                var lot = new StockLot { ShopId = shop.Id, ItemId = item.Id, BranchId = branch.Id, LotCode = "APERTURA", Supplier = "Stock inicial", Quantity = item.QuantityOnHand, UnitCost = item.UnitCost ?? 0, Currency = item.UnitCostCurrency ?? "ARS" };
                db.StockLots.Add(lot); db.StockMinimums.Add(new StockMinimum { ShopId = shop.Id, ItemId = item.Id, BranchId = branch.Id, Minimum = 2 });
                db.StockMovements.Add(new StockMovement { ShopId = shop.Id, LotId = lot.Id, Kind = "Opening", Quantity = lot.Quantity, Reason = "Migración del inventario existente" });
            }
            foreach (var order in await db.Workflows.Where(x => x.ShopId == shop.Id).ToListAsync())
                if (!await db.OrderBusinesses.AnyAsync(x => x.ShopId == shop.Id && x.OrderId == order.Id)) db.OrderBusinesses.Add(new OrderBusiness { ShopId = shop.Id, OrderId = order.Id, BranchId = branch.Id });
            await db.SaveChangesAsync();
        }
        var primary = await db.Users.Where(x => x.Email == "admin@local").Select(x => x.ShopId).SingleOrDefaultAsync();
        if (primary == Guid.Empty || await db.SupplierPrices.AnyAsync(x => x.ShopId == primary)) return;
        var main = await db.PremiumBranches.Where(x => x.ShopId == primary).OrderBy(x => x.CreatedAtUtc).FirstAsync();
        db.PremiumBranches.Add(new PremiumBranch { ShopId = primary, Name = "Sucursal Norte · demo", Address = "Local de ejemplo" });
        foreach (var r in new[] {
            new PriceRow("Repuestos Sur · demo", "DIS-IP13", "Pantalla iPhone 13 OLED", "iPhone 13 · pantalla", "OLED compatible", 52000, "ARS"),
            new PriceRow("Tecno Partes · demo", "OLED-13", "Display iPhone 13 OLED", "iPhone 13 · pantalla", "OLED compatible", 49000, "ARS"),
            new PriceRow("Repuestos Sur · demo", "BAT-IP11", "Batería iPhone 11", "iPhone 11 · batería", "Premium", 17500, "ARS"),
            new PriceRow("Tecno Partes · demo", "BAT11-P", "Batería iPhone 11 premium", "iPhone 11 · batería", "Premium", 19000, "ARS"),
            new PriceRow("Repuestos Sur · demo", "USB-A54", "Placa de carga A54", "Galaxy A54 · carga", "Compatible", 11000, "ARS") })
            db.SupplierPrices.Add(new SupplierPrice { ShopId = primary, Supplier = r.Supplier, Sku = r.Sku, Description = r.Description, Compatibility = r.Compatibility, Quality = r.Quality, UnitCost = r.UnitCost, Currency = r.Currency, Source = "Demostración ficticia" });
        var refurb = new RefurbDevice { ShopId = primary, BranchId = main.Id, Model = "Apple iPhone 12 128 GB · demo", Identifier = "DEMO-REVENTA-001", Seller = "Vendedor de ejemplo", Acquisition = "TradeIn", Grade = "B", Diagnosis = "Batería al 76 %. Requiere reemplazo y pruebas.", PurchasePrice = 180000, TargetPrice = 295000, Status = "Repairing" };
        db.RefurbDevices.Add(refurb); db.RefurbExpenses.Add(new RefurbExpense { ShopId = primary, DeviceId = refurb.Id, Description = "Batería y mano de obra de ejemplo", Amount = 32000 });
        db.RefurbEvents.Add(new RefurbEvent { ShopId = primary, DeviceId = refurb.Id, Message = "Equipo ficticio recibido en canje para probar el circuito de reventa." });
        var customer = new Customer(primary, "Estudio Norte · demo", "1100000001", "Empresa ficticia", DateTime.UtcNow); db.Customers.Add(customer);
        var contract = new CompanyContract { ShopId = primary, CustomerId = customer.Id, CompanyName = customer.FullName, Contact = "Responsable de ejemplo", Phone = customer.Phone, MonthlyFee = 120000, ExtraOrderRate = 15000, IncludedOrders = 5, SlaHours = 72,
            StartsAtUtc = DateTime.UtcNow.AddMonths(-2), EndsAtUtc = DateTime.UtcNow.AddYears(1), Terms = "Demo: abono mensual por servicio. Repuestos se presupuestan por separado. Sin prorrateo." };
        db.CompanyContracts.Add(contract);
        foreach (var n in new[] { 1, 2, 3 }) {
            var device = new Device(primary, customer.Id, "Lenovo", "ThinkPad T14", null, $"DEMO-EMP-{n:000}", "Equipo de ejemplo", DateTime.UtcNow); db.Devices.Add(device);
            db.CompanyEquipments.Add(new CompanyEquipment { ShopId = primary, ContractId = contract.Id, DeviceId = device.Id, Label = "Lenovo ThinkPad T14", Identifier = device.SerialNumber! });
        }
        await db.SaveChangesAsync();
    }
}
