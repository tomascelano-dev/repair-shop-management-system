using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Security;
using RepairShop.Domain.Customers;
using RepairShop.Domain.Devices;
using RepairShop.Domain.Inventory;
using RepairShop.Domain.RepairOrders;
using RepairShop.Domain.Shops;
using RepairShop.Domain.Users;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Api.V2;

public static class LocalDemoSeeder
{
    public static async Task Seed(RepairShopDbContext db, IPasswordHasher hasher)
    {
        if(await db.Workflows.AnyAsync())return;
        var shop=await db.Shops.OrderBy(x=>x.CreatedAtUtc).FirstAsync();
        var user=await db.Users.FirstAsync(x=>x.ShopId==shop.Id&&x.Role==UserRole.Admin);
        var examples=new[]{
            ("Lucía Fernández","Apple","iPhone 13","Pantalla rota después de una caída",RepairOrderStatus.AwaitingApproval,95000m,"Alta"),
            ("Marcos López","Samsung","Galaxy A54","El conector de carga hace falso contacto",RepairOrderStatus.InProgress,42000m,"Normal"),
            ("Valentina Ruiz","Apple","iPhone 11","La batería se descarga muy rápido",RepairOrderStatus.Ready,58000m,"Normal"),
            ("Joaquín Pérez","Motorola","Moto G84","El equipo no enciende después de cargar",RepairOrderStatus.Received,0m,"Alta"),
            ("Camila Torres","Lenovo","IdeaPad 3","Sobrecalentamiento y apagado al trabajar",RepairOrderStatus.Diagnosing,0m,"Normal"),
            ("Nicolás Gómez","Apple","iPhone 12","Se necesita reemplazar la cámara trasera",RepairOrderStatus.WaitingParts,78000m,"Normal"),
            ("Sofía Acosta","Samsung","Galaxy S22","El audio del auricular se escucha bajo",RepairOrderStatus.QualityCheck,38000m,"Normal"),
            ("Mateo Silva","Apple","iPhone SE","Reemplazo de batería y limpieza general",RepairOrderStatus.Delivered,45000m,"Normal")};
        var check=new[]{"Pantalla y táctil","Cámaras","Audio y micrófono","Carga","Botones","Biometría"}.ToDictionary(x=>x,_=>"ok");
        for(var i=0;i<examples.Length;i++)
        {
            var e=examples[i];var when=DateTime.UtcNow.AddDays(-i%4).AddHours(-i);
            var customer=new Customer(shop.Id,e.Item1,$"11400010{i:00}","Datos ficticios para la demostración local.",when);db.Customers.Add(customer);
            var device=new Device(shop.Id,customer.Id,e.Item2,e.Item3,null,$"DEMO-{i+1:0000}",null,when);db.Devices.Add(device);
            var order=new RepairOrder(shop.Id,customer.Id,device.Id,e.Item4,null,when);db.RepairOrders.Add(order);
            var w=new WorkshopWorkflow{Id=order.Id,ShopId=shop.Id,CustomerName=e.Item1,CustomerPhone=customer.Phone,DeviceLabel=$"{e.Item2} {e.Item3}",Identifier=device.SerialNumber,Condition="Marcas de uso en carcasa. Recepción documentada.",Accessories="Equipo sin cargador",Priority=e.Item7,IsDemo=true,IntakeChecksJson=JsonSerializer.Serialize(check),QualityChecksJson=e.Item5 is RepairOrderStatus.Ready or RepairOrderStatus.Delivered?JsonSerializer.Serialize(check):"{}",Diagnosis=e.Item6>0?"Falla verificada en banco. Se propone reemplazo del componente y pruebas de funcionamiento.":""};
            db.Workflows.Add(w);
            if(e.Item5!=RepairOrderStatus.Received)order.MoveTo(RepairOrderStatus.Diagnosing,when.AddMinutes(10));
            if(e.Item6>0)
            {
                order.MoveTo(RepairOrderStatus.AwaitingApproval,when.AddMinutes(20));
                var q=new WorkflowQuote{ShopId=shop.Id,OrderId=order.Id,Revision=1,Currency="ARS",Total=e.Item6,LinesJson=JsonSerializer.Serialize(new[]{new QuoteItem("Repuesto y servicio técnico",1,e.Item6,e.Item6*0.55m)},new JsonSerializerOptions(JsonSerializerDefaults.Web)),Terms="Incluye instalación y pruebas. La garantía cubre el trabajo realizado.",WarrantyDays=90,CreatedAtUtc=when,ExpiresAtUtc=DateTime.UtcNow.AddDays(7)};
                if(e.Item5!=RepairOrderStatus.AwaitingApproval)q.Decide(true,e.Item1,when.AddMinutes(25));
                db.WorkflowQuotes.Add(q);order.SetQuote(e.Item6,"ARS",user.Id,when);
                if(e.Item5==RepairOrderStatus.WaitingParts)order.MoveTo(RepairOrderStatus.WaitingParts,when.AddMinutes(30));
                else if(e.Item5!=RepairOrderStatus.AwaitingApproval)
                {
                    order.MoveTo(RepairOrderStatus.InProgress,when.AddMinutes(30));
                    if(e.Item5 is RepairOrderStatus.QualityCheck or RepairOrderStatus.Ready or RepairOrderStatus.Delivered)order.MoveTo(RepairOrderStatus.QualityCheck,when.AddMinutes(40));
                    if(e.Item5 is RepairOrderStatus.Ready or RepairOrderStatus.Delivered)order.MoveTo(RepairOrderStatus.Ready,when.AddMinutes(50));
                    if(e.Item5==RepairOrderStatus.Delivered){order.MoveTo(RepairOrderStatus.Delivered,when.AddHours(2));w.DeliveredTo=e.Item1;w.HandedOverAtUtc=when.AddHours(2);}
                }
                if(e.Item5 is RepairOrderStatus.Ready or RepairOrderStatus.Delivered or RepairOrderStatus.InProgress)
                    db.RepairOrderPayments.Add(new RepairOrderPayment(shop.Id,order.Id,e.Item5==RepairOrderStatus.InProgress?10000:e.Item6,"ARS",PaymentMethod.Transfer,"Cobro de ejemplo",user.Id,when));
            }
            db.RepairOrderNotes.Add(new RepairOrderNote(shop.Id,order.Id,"Orden de ejemplo. Podés usarla para recorrer el flujo.",user.Id,when));
        }
        foreach(var x in new[]{("BAT-IP11","Batería iPhone 11",6,18000m),("DIS-IP13","Pantalla iPhone 13 OLED",3,55000m),("USB-A54","Placa de carga Samsung A54",8,12000m),("CAM-IP12","Cámara trasera iPhone 12",0,46000m),("ADH-UNI","Adhesivo para display",20,1500m)})
            db.InventoryItems.Add(new InventoryItem(shop.Id,x.Item1,x.Item2,x.Item3,x.Item4,"ARS",true,DateTime.UtcNow));
        var other=new Shop("Taller de prueba B",null,null,"Buenos Aires","AR",DateTime.UtcNow);db.Shops.Add(other);
        db.Users.Add(new AppUser(other.Id,"beta@local","Taller B",UserRole.Admin,hasher.Hash("DemoBeta12345"),DateTime.UtcNow));
        await db.SaveChangesAsync();
    }
}
