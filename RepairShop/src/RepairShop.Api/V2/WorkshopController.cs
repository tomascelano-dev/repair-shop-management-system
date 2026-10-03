using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepairShop.Api.Common;
using RepairShop.Domain.Common;
using RepairShop.Domain.Customers;
using RepairShop.Domain.Devices;
using RepairShop.Domain.RepairOrders;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Api.V2;

public sealed class WorkflowConflict(string message) : Exception(message);
public sealed record IntakeRequest(Guid? CustomerId, Guid? DeviceId,
    [Required, StringLength(120, MinimumLength=3)] string CustomerName,
    [Required, StringLength(40, MinimumLength=6)] string Phone,
    [Required, StringLength(60, MinimumLength=2)] string Brand,
    [Required, StringLength(60, MinimumLength=2)] string Model,
    [StringLength(80)] string? Identifier,
    [Required, StringLength(500, MinimumLength=5)] string Issue,
    [StringLength(500)] string Condition, [StringLength(200)] string Accessories,
    string Priority, Dictionary<string,string>? Checks, [StringLength(160)] string? Email = null);
public sealed record EditWorkflow(int Version, [StringLength(1200)] string Diagnosis, Dictionary<string,string> QualityChecks, decimal LaborCost);
public sealed record PublishQuote(int Version, List<QuoteItem> Lines, string Currency, [StringLength(2000)] string Terms, int ValidDays, int WarrantyDays);
public sealed record ChangeStage(int Version, string Status, [StringLength(300)] string? Reason);
public sealed record PaymentRequest(int Version, decimal Amount, string Currency, PaymentMethod Method, [StringLength(120)] string? Reference);
public sealed record RefundRequest(int Version, Guid PaymentId, decimal Amount, [Required, StringLength(300,MinimumLength=3)] string Reason);
public sealed record HandoverRequest(int Version, [Required, StringLength(120,MinimumLength=3)] string Recipient, [StringLength(300)] string? DebtReason);
public sealed record PortalRequest(int Version);
public sealed record DecideRequest(Guid QuoteId, bool Accept, [Required, StringLength(120,MinimumLength=3)] string Name, string? Signature = null);
public sealed record PhotoRequest(int Version, string DataUrl);

[ApiController, Authorize, Route("api/v2")]
public sealed class WorkshopController(RepairShopDbContext db, IWebHostEnvironment env, RepairShop.Api.Saas.SaasEvents events) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] Tests = ["Pantalla y táctil", "Cámaras", "Audio y micrófono", "Carga", "Botones", "Biometría"];
    private Guid ShopId => CurrentUser.GetShopId(User) is var id && id != Guid.Empty ? id : throw new UnauthorizedAccessException();
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static object ReadJson(string value) => JsonSerializer.Deserialize<JsonElement>(value, Json);
    private static Dictionary<string,string> ValidateChecks(Dictionary<string,string>? checks)
    {
        checks ??= new();
        if (checks.Any(x => !Tests.Contains(x.Key) || !new[]{"ok","fail","untested","na"}.Contains(x.Value)))
            throw new DomainException("Hay resultados de checklist inválidos.");
        return checks;
    }
    private async Task<(WorkshopWorkflow W, RepairOrder O)> Load(Guid id, Guid shop, bool locked = false)
    {
        var w = locked
            ? await db.Workflows.FromSqlInterpolated($"SELECT * FROM workflows WHERE \"Id\" = {id} AND \"ShopId\" = {shop} FOR UPDATE").SingleOrDefaultAsync()
            : await db.Workflows.SingleOrDefaultAsync(x => x.Id == id && x.ShopId == shop);
        if (w is null) throw new RepairShop.Application.Common.NotFoundException("Orden no encontrada.");
        var o = await db.RepairOrders.SingleAsync(x => x.Id == id && x.ShopId == shop);
        return (w,o);
    }
    private static void CheckVersion(WorkshopWorkflow w, int version)
    { if (w.Version != version) throw new WorkflowConflict("La orden cambió. Actualizá la pantalla e intentá nuevamente."); }
    private static void RequireOpen(RepairOrder o)
    { if (o.Status is RepairOrderStatus.Delivered or RepairOrderStatus.Cancelled) throw new DomainException("Esta orden está cerrada."); }
    private async Task<WorkflowQuote?> LatestQuote(Guid id, Guid shop) => await db.WorkflowQuotes.Where(x => x.OrderId == id && x.ShopId == shop).OrderByDescending(x => x.Revision).FirstOrDefaultAsync();
    private async Task<decimal> Paid(Guid id, Guid shop) => await db.RepairOrderPayments.Where(x=>x.RepairOrderId==id && x.ShopId==shop).SumAsync(x=>x.Amount) - await db.WorkflowRefunds.Where(x=>x.OrderId==id && x.ShopId==shop).SumAsync(x=>x.Amount);
    private void Note(Guid id, Guid shop, string body) => db.RepairOrderNotes.Add(new RepairOrderNote(shop,id,body,CurrentUser.GetUserId(User),DateTime.UtcNow));
    private void Move(RepairOrder o, RepairOrderStatus target)
    {
        var previous=o.Status; o.MoveTo(target,DateTime.UtcNow);
        db.RepairOrderStatusHistory.Add(new RepairOrderStatusHistory(o.ShopId,o.Id,previous,target,CurrentUser.GetUserId(User),DateTime.UtcNow));
        pendingStatus.Add((o.ShopId,o.Id,previous,target));
    }
    private readonly List<(Guid Shop,Guid Order,RepairOrderStatus From,RepairOrderStatus To)> pendingStatus=[];
    private async Task<IActionResult> Mutate(Guid shop, string operation, object body, Func<Task<object>> action)
    {
        var key = Request.Headers["Idempotency-Key"].ToString();
        if (key.Length is < 8 or > 100) throw new DomainException("Falta una clave válida de idempotencia.");
        var fingerprint=Hash(JsonSerializer.Serialize(body,Json));
        await using var tx=await db.Database.BeginTransactionAsync();
        var lockName=$"{shop}:{operation}:{key}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockName}, 0))");
        var previous=await db.WorkflowRequests.SingleOrDefaultAsync(x=>x.ShopId==shop && x.Operation==operation && x.Key==key);
        if(previous is not null)
        {
            if(previous.Hash!=fingerprint) throw new WorkflowConflict("La clave del pedido ya fue usada con otros datos.");
            return Ok(new { data=ReadJson(previous.ResponseJson) });
        }
        var result=await action();
        foreach(var change in pendingStatus)await events.StatusChanged(change.Shop,change.Order,change.From,change.To);
        pendingStatus.Clear();
        db.WorkflowRequests.Add(new WorkflowRequest{ShopId=shop,Operation=operation,Key=key,Hash=fingerprint,ResponseJson=JsonSerializer.Serialize(result,Json)});
        await db.SaveChangesAsync(); await tx.CommitAsync();
        return Ok(new { data=result });
    }

    [HttpGet("orders")]
    public async Task<IActionResult> List()
    {
        var shop=ShopId;
        var orders=await (from o in db.RepairOrders join w in db.Workflows on o.Id equals w.Id
            where o.ShopId==shop && w.ShopId==shop orderby o.CreatedAtUtc descending
            select new { o.Id, w.Number, w.CustomerName, w.CustomerPhone, w.DeviceLabel, w.Identifier,
                o.IssueDescription, Status=o.Status.ToString(), w.Priority,w.Version,w.IsDemo,
                o.CreatedAtUtc,o.UpdatedAtUtc,w.HandedOverAtUtc }).Take(1000).ToListAsync();
        var quotes=await db.WorkflowQuotes.Where(x=>x.ShopId==shop).Select(x=>new{x.Id,x.OrderId,x.Revision,x.Total,x.Currency,x.Status,x.ExpiresAtUtc}).ToListAsync();
        var receipts=await db.RepairOrderPayments.Where(x=>x.ShopId==shop).Select(x=>new{x.RepairOrderId,x.Amount,x.Currency,x.CreatedAtUtc}).ToListAsync();
        var refunds=await db.WorkflowRefunds.Where(x=>x.ShopId==shop).Select(x=>new{x.OrderId,x.Amount,x.CreatedAtUtc}).ToListAsync();
        return Ok(new{data=new{orders,quotes,receipts,refunds}});
    }

    [HttpGet("orders/{id:guid}")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var shop=ShopId; var (w,o)=await Load(id,shop);
        return Ok(new{data=new { order=o,workflow=w,
            intakeChecks=ReadJson(w.IntakeChecksJson),qualityChecks=ReadJson(w.QualityChecksJson),
            quotes=(await db.WorkflowQuotes.Where(x=>x.OrderId==id&&x.ShopId==shop).OrderByDescending(x=>x.Revision).ToListAsync()).Select(q=>new{q.Id,q.Revision,q.Currency,q.Total,q.Terms,q.WarrantyDays,q.Status,q.CreatedAtUtc,q.ExpiresAtUtc,q.DecidedAtUtc,q.DecisionBy,Lines=ReadJson(q.LinesJson)}),
            payments=await db.RepairOrderPayments.Where(x=>x.RepairOrderId==id&&x.ShopId==shop).OrderBy(x=>x.CreatedAtUtc).ToListAsync(),
            refunds=await db.WorkflowRefunds.Where(x=>x.OrderId==id&&x.ShopId==shop).ToListAsync(),
            notes=await db.RepairOrderNotes.Where(x=>x.RepairOrderId==id&&x.ShopId==shop).OrderByDescending(x=>x.CreatedAtUtc).ToListAsync(),
            history=await db.RepairOrderStatusHistory.Where(x=>x.RepairOrderId==id&&x.ShopId==shop).OrderBy(x=>x.ChangedAtUtc).ToListAsync(),
            photos=await db.WorkflowPhotos.Where(x=>x.OrderId==id&&x.ShopId==shop).Select(x=>new{x.Id,x.CreatedAtUtc}).ToListAsync()
        }});
    }

    [HttpPost("intake")]
    public Task<IActionResult> Intake(IntakeRequest b) => Mutate(ShopId,"intake",b,async()=> {
        var created=await WorkshopIntake.Create(db,ShopId,CurrentUser.GetUserId(User),b);
        await events.OrderReceived(ShopId,created.Id,b.Email??"");
        return new {id=created.Id,number=created.Number};
    });

    [HttpPut("orders/{id:guid}/diagnosis")]
    public Task<IActionResult> Edit(Guid id, EditWorkflow b)=>Mutate(ShopId,$"diagnosis:{id}",b,async()=> {
        var(w,o)=await Load(id,ShopId,true);CheckVersion(w,b.Version);RequireOpen(o);
        if(b.LaborCost<0 || b.LaborCost>100000000)throw new DomainException("Costo de mano de obra inválido.");
        w.Diagnosis=b.Diagnosis??"";w.LaborCost=b.LaborCost;w.QualityChecksJson=JsonSerializer.Serialize(ValidateChecks(b.QualityChecks),Json);w.Version++;
        Note(id,ShopId,"Diagnóstico / control de calidad actualizado.");return new{w.Version};
    });

    [HttpPost("orders/{id:guid}/quotes")]
    public Task<IActionResult> Quote(Guid id, PublishQuote b)=>Mutate(ShopId,$"quote:{id}",b,async()=> {
        var(w,o)=await Load(id,ShopId,true);CheckVersion(w,b.Version);RequireOpen(o);
        if(o.Status is not (RepairOrderStatus.Diagnosing or RepairOrderStatus.AwaitingApproval or RepairOrderStatus.InProgress or RepairOrderStatus.WaitingParts))throw new DomainException("Iniciá el diagnóstico antes de presupuestar.");
        var total=WorkflowQuote.CalculateTotal(b.Lines??[]);
        if(b.Currency is not ("ARS" or "USD") || b.ValidDays is <1 or >90 || b.WarrantyDays is <0 or >730)throw new DomainException("Revisá moneda, vigencia y garantía.");
        var old=await LatestQuote(id,ShopId);
        if (await db.StockReservations.AnyAsync(x => x.ShopId == ShopId && x.OrderId == id && x.Status != "Released" && x.Currency != b.Currency)
            || await db.OrderExpenses.AnyAsync(x => x.ShopId == ShopId && x.OrderId == id && x.Currency != b.Currency))
            throw new DomainException("La moneda debe coincidir con los repuestos y costos registrados en la orden.");
        if(old is not null && old.Currency!=b.Currency && await db.RepairOrderPayments.AnyAsync(x=>x.RepairOrderId==id))throw new DomainException("No se puede cambiar la moneda con cobros registrados.");
        if(old?.Status=="Sent")old.Status="Superseded";
        var lines=(b.Lines??[]).Select(l=>l with{UnitPrice=decimal.Round(l.UnitPrice,2,MidpointRounding.AwayFromZero),UnitCost=decimal.Round(l.UnitCost,2,MidpointRounding.AwayFromZero)}).ToList();
        var q=new WorkflowQuote{ShopId=ShopId,OrderId=id,Revision=(old?.Revision??0)+1,Currency=b.Currency,LinesJson=JsonSerializer.Serialize(lines,Json),Total=total,Terms=b.Terms??"",WarrantyDays=b.WarrantyDays,CreatedAtUtc=DateTime.UtcNow,ExpiresAtUtc=DateTime.UtcNow.AddDays(b.ValidDays)};
        db.WorkflowQuotes.Add(q);if(total>0)o.SetQuote(total,b.Currency,CurrentUser.GetUserId(User),DateTime.UtcNow);
        if(o.Status!=RepairOrderStatus.AwaitingApproval)Move(o,RepairOrderStatus.AwaitingApproval);
        w.Version++;Note(id,ShopId,$"Presupuesto v{q.Revision} publicado ({b.Currency} {total:N2}).");return new{q.Id,q.Revision};
    });

    [HttpPost("orders/{id:guid}/stage")]
    public Task<IActionResult> Stage(Guid id,ChangeStage b)=>Mutate(ShopId,$"stage:{id}",b,async()=> {
        var(w,o)=await Load(id,ShopId,true);CheckVersion(w,b.Version);RequireOpen(o);
        if(!Enum.TryParse<RepairOrderStatus>(b.Status,out var target)||!Enum.IsDefined(target)||target==RepairOrderStatus.Delivered)throw new DomainException("Estado inválido; usá el formulario de entrega para finalizar.");
        if(target is RepairOrderStatus.InProgress or RepairOrderStatus.WaitingParts or RepairOrderStatus.QualityCheck or RepairOrderStatus.Ready)
        {var q=await LatestQuote(id,ShopId);if(q?.Status!="Accepted")throw new DomainException("Se necesita la aprobación del presupuesto vigente.");}
        if(target==RepairOrderStatus.Ready)
        {var checks=JsonSerializer.Deserialize<Dictionary<string,string>>(w.QualityChecksJson)!;if(o.Status!=RepairOrderStatus.QualityCheck||Tests.Any(k=>!checks.TryGetValue(k,out var v)||v is not ("ok" or "na")))throw new DomainException("Completá el control de calidad antes de marcar listo.");}
        if(target==RepairOrderStatus.Cancelled && string.IsNullOrWhiteSpace(b.Reason))throw new DomainException("Indicá el motivo de cancelación.");
        if (target == RepairOrderStatus.Cancelled && await db.StockReservations.AnyAsync(x => x.ShopId == ShopId && x.OrderId == id && x.Status == "Reserved"))
            throw new DomainException("Liberá las reservas de repuestos antes de cancelar la orden.");
        Move(o,target);w.Version++;Note(id,ShopId,$"Estado: {target}. {b.Reason}");return new{w.Version};
    });

    [HttpPost("orders/{id:guid}/payments")]
    public Task<IActionResult> Pay(Guid id,PaymentRequest b)=>Mutate(ShopId,$"payment:{id}",b,async()=> {
        var(w,o)=await Load(id,ShopId,true);CheckVersion(w,b.Version);RequireOpen(o);var q=await LatestQuote(id,ShopId);
        if(q?.Status!="Accepted" || q.Currency!=b.Currency)throw new DomainException("El cobro requiere un presupuesto aprobado en la misma moneda.");
        var amount=decimal.Round(b.Amount,2,MidpointRounding.AwayFromZero);
        if(!Enum.IsDefined(b.Method)||amount<=0||amount>q.Total-await Paid(id,ShopId))throw new DomainException("El importe debe ser mayor a cero y no superar el saldo.");
        var payment=new RepairOrderPayment(ShopId,id,amount,b.Currency,b.Method,b.Reference,CurrentUser.GetUserId(User),DateTime.UtcNow);db.RepairOrderPayments.Add(payment);w.Version++;
        Note(id,ShopId,$"Cobro registrado: {b.Currency} {amount:N2}.");
        await events.PaymentRegistered(ShopId,id,payment.Id,amount,b.Currency,b.Method,CurrentUser.GetUserId(User));return new{payment.Id,w.Version};
    });

    [HttpPost("orders/{id:guid}/refunds"),Authorize(Policy="AdminOnly")]
    public Task<IActionResult> Refund(Guid id,RefundRequest b)=>Mutate(ShopId,$"refund:{id}",b,async()=> {
        var(w,o)=await Load(id,ShopId,true);CheckVersion(w,b.Version);
        var p=await db.RepairOrderPayments.SingleOrDefaultAsync(x=>x.Id==b.PaymentId&&x.RepairOrderId==id&&x.ShopId==ShopId);
        var amount=decimal.Round(b.Amount,2,MidpointRounding.AwayFromZero);
        if(p is null||amount<=0||amount>p.Amount-await db.WorkflowRefunds.Where(x=>x.PaymentId==p.Id).SumAsync(x=>x.Amount))throw new DomainException("La devolución supera el cobro disponible.");
        db.WorkflowRefunds.Add(new WorkflowRefund{ShopId=ShopId,OrderId=id,PaymentId=p.Id,Amount=amount,Reason=b.Reason,CreatedAtUtc=DateTime.UtcNow});w.Version++;
        await events.RefundRegistered(ShopId,id,p.Id,amount,CurrentUser.GetUserId(User));Note(id,ShopId,$"Devolución: {amount:N2}. {b.Reason}");return new{w.Version};
    });

    [HttpPost("orders/{id:guid}/handover")]
    public Task<IActionResult> Handover(Guid id,HandoverRequest b)=>Mutate(ShopId,$"handover:{id}",b,async()=> {
        var(w,o)=await Load(id,ShopId,true);CheckVersion(w,b.Version);
        if(w.HandedOverAtUtc.HasValue || o.Status is not (RepairOrderStatus.Ready or RepairOrderStatus.Cancelled))throw new DomainException("El equipo debe estar listo o cancelado y pendiente de retiro.");
        if (await db.StockReservations.AnyAsync(x => x.ShopId == ShopId && x.OrderId == id && x.Status == "Reserved"))
            throw new DomainException("Consumí o liberá las reservas de repuestos antes de entregar el equipo.");
        var q=await LatestQuote(id,ShopId);var paid=await Paid(id,ShopId);
        var due=q?.Status=="Accepted"?q.Total-paid:0;
        if(due>0 && (!User.IsInRole("Admin")||string.IsNullOrWhiteSpace(b.DebtReason)))throw new DomainException("Hay saldo pendiente. Un administrador debe justificar la entrega con deuda.");
        if(o.Status==RepairOrderStatus.Ready)Move(o,RepairOrderStatus.Delivered);
        w.DeliveredTo=b.Recipient.Trim();w.HandedOverAtUtc=DateTime.UtcNow;w.Version++;
        if(o.Status==RepairOrderStatus.Delivered)await events.Delivered(ShopId,id);Note(id,ShopId,$"Equipo retirado por {b.Recipient}. {b.DebtReason}");return new{w.Version};
    });

    [HttpPost("orders/{id:guid}/portal")]
    public async Task<IActionResult> PortalLink(Guid id,PortalRequest b)
    {
        await using var tx=await db.Database.BeginTransactionAsync();var(w,_)=await Load(id,ShopId,true);CheckVersion(w,b.Version);
        var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));w.PortalTokenHash=Hash(token);w.PortalExpiresAtUtc=DateTime.UtcNow.AddDays(30);w.Version++;
        Note(id,ShopId,"Enlace del portal creado/renovado; los anteriores quedan revocados.");await db.SaveChangesAsync();await tx.CommitAsync();return Ok(new{data=new{token,w.Version}});
    }
    [HttpDelete("orders/{id:guid}/portal")]
    public async Task<IActionResult> RevokePortal(Guid id)
    {
        await using var tx=await db.Database.BeginTransactionAsync();var(w,_)=await Load(id,ShopId,true);w.PortalTokenHash=null;w.PortalExpiresAtUtc=null;w.Version++;Note(id,ShopId,"Enlace de portal revocado.");await db.SaveChangesAsync();await tx.CommitAsync();return Ok(new{data=new{w.Version}});
    }
    private async Task<WorkshopWorkflow> Grant()
    {
        var token=Request.Headers["X-Portal-Token"].ToString();
        if(token.Length!=64)throw new RepairShop.Application.Common.NotFoundException("Enlace no disponible.");
        var hash=Hash(token);return await db.Workflows.SingleOrDefaultAsync(x=>x.PortalTokenHash==hash && x.PortalExpiresAtUtc>DateTime.UtcNow)??throw new RepairShop.Application.Common.NotFoundException("El enlace venció o fue revocado.");
    }
    [AllowAnonymous,HttpGet("portal")]
    public async Task<IActionResult> PublicPortal()
    {
        var w=await Grant();var o=await db.RepairOrders.SingleAsync(x=>x.Id==w.Id&&x.ShopId==w.ShopId);var q=await LatestQuote(w.Id,w.ShopId);
        var lines=q is null?[]:JsonSerializer.Deserialize<List<QuoteItem>>(q.LinesJson,Json)!;
        return Ok(new{data=new{w.Number,w.CustomerName,w.DeviceLabel,Status=o.Status.ToString(),w.HandedOverAtUtc,ShopName=await db.Shops.Where(x=>x.Id==w.ShopId).Select(x=>x.Name).SingleAsync(),
            Branding=await db.ShopProfiles.Where(x=>x.ShopId==w.ShopId).Select(x=>new{x.LogoDataUrl,x.PrimaryColor,x.RequireSignature,x.Phone,x.Address}).SingleOrDefaultAsync(),Paid=await Paid(w.Id,w.ShopId),
            Quote=q is null?null:new{q.Id,q.Revision,q.Currency,q.Total,q.Terms,q.WarrantyDays,q.Status,q.ExpiresAtUtc,q.DecidedAtUtc,q.DecisionBy,Lines=lines.Select(l=>new{l.Description,l.Quantity,l.UnitPrice})}}});
    }
    [AllowAnonymous,HttpPost("portal/decision")]
    public async Task<IActionResult> Decide(DecideRequest b)
    {
        var grant=await Grant();var hash=grant.PortalTokenHash;db.Entry(grant).State=EntityState.Detached;
        return await Mutate(grant.ShopId,$"decision:{grant.Id}",b,async()=> {
            var(w,o)=await Load(grant.Id,grant.ShopId,true);
            if(w.PortalTokenHash!=hash||w.PortalExpiresAtUtc<=DateTime.UtcNow)throw new WorkflowConflict("El enlace fue renovado. Pedí el enlace actual.");
            RequireOpen(o);var q=await LatestQuote(w.Id,w.ShopId);if(q?.Id!=b.QuoteId)throw new WorkflowConflict("Hay una nueva versión del presupuesto. Actualizá la página.");
            var profile=await RepairShop.Api.Saas.ShopProvisioning.EnsureProfile(db,w.ShopId);
            if(b.Accept&&profile.RequireSignature&&string.IsNullOrEmpty(b.Signature))throw new DomainException("Firmá en el recuadro para aprobar el presupuesto.");
            q.Decide(b.Accept,b.Name,DateTime.UtcNow);w.Version++;
            await events.QuoteDecided(w.ShopId,w.Id,q.Id,b.Accept,b.Name.Trim(),b.Signature??"",HttpContext.Connection.RemoteIpAddress?.ToString()??"",Request.Headers.UserAgent.ToString());
            return new{q.Status};
        });
    }

    [HttpPost("orders/{id:guid}/photos"),RequestSizeLimit(4_500_000)]
    public Task<IActionResult> Upload(Guid id,PhotoRequest b)=>Mutate(ShopId,$"photo:{id}",b,async()=> {
        var(w,o)=await Load(id,ShopId,true);CheckVersion(w,b.Version);RequireOpen(o);
        if(await db.WorkflowPhotos.CountAsync(x=>x.OrderId==id&&x.ShopId==ShopId)>=8)throw new DomainException("Máximo 8 fotos por orden.");
        var parts=b.DataUrl.Split(',',2);if(parts.Length!=2)throw new DomainException("Imagen inválida.");
        byte[] bytes;try{bytes=Convert.FromBase64String(parts[1]);}catch{throw new DomainException("Imagen inválida.");}
        if(bytes.Length is <12 or >3_000_000)throw new DomainException("La imagen debe pesar menos de 3 MB.");
        var png=bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10});var jpg=bytes[0]==255&&bytes[1]==216&&bytes[2]==255;
        if(!png&&!jpg)throw new DomainException("Elegí una foto JPG o PNG.");
        var photo=new WorkflowPhoto{ShopId=ShopId,OrderId=id,MimeType=png?"image/png":"image/jpeg",CreatedAtUtc=DateTime.UtcNow};photo.FileName=$"{photo.Id:N}.{(png?"png":"jpg")}";
        var dir=Path.Combine(env.ContentRootPath,"data","photos");Directory.CreateDirectory(dir);await System.IO.File.WriteAllBytesAsync(Path.Combine(dir,photo.FileName),bytes);
        db.WorkflowPhotos.Add(photo);w.Version++;return new{photo.Id,w.Version};
    });
    [HttpGet("orders/{id:guid}/photos/{photoId:guid}")]
    public async Task<IActionResult> Photo(Guid id,Guid photoId)
    {
        var p=await db.WorkflowPhotos.SingleOrDefaultAsync(x=>x.Id==photoId&&x.OrderId==id&&x.ShopId==ShopId);if(p is null)return NotFound();
        return PhysicalFile(Path.Combine(env.ContentRootPath,"data","photos",p.FileName),p.MimeType);
    }
}

public static class WorkshopIntake
{
    public static async Task<(Guid Id, long Number)> Create(RepairShopDbContext db, Guid shop, Guid actor, IntakeRequest b)
    {
        var now=DateTime.UtcNow;
        var customer=b.CustomerId.HasValue ? await db.Customers.SingleOrDefaultAsync(x=>x.Id==b.CustomerId && x.ShopId==shop) : null;
        if(b.CustomerId.HasValue && customer is null) throw new DomainException("Cliente no disponible en este taller.");
        if(customer is null){customer=new Customer(shop,b.CustomerName,b.Phone,null,now);db.Customers.Add(customer);}
        var device=b.DeviceId.HasValue ? await db.Devices.SingleOrDefaultAsync(x=>x.Id==b.DeviceId && x.ShopId==shop && x.CustomerId==customer.Id) : null;
        if(b.DeviceId.HasValue && device is null) throw new DomainException("El equipo no pertenece al cliente seleccionado.");
        if(device is null){device=new Device(shop,customer.Id,b.Brand,b.Model,null,b.Identifier,null,now);db.Devices.Add(device);}
        var order=new RepairOrder(shop,customer.Id,device.Id,b.Issue,null,now);db.RepairOrders.Add(order);
        var w=new WorkshopWorkflow {Id=order.Id,ShopId=shop,CustomerName=customer.FullName,CustomerPhone=customer.Phone,
            DeviceLabel=$"{device.Brand} {device.Model}",Identifier=device.SerialNumber,Condition=b.Condition??"",Accessories=b.Accessories??"",
            Priority=b.Priority=="Alta"?"Alta":"Normal",IntakeChecksJson=JsonSerializer.Serialize(ValidateChecks(b.Checks),new JsonSerializerOptions(JsonSerializerDefaults.Web))};
        db.Workflows.Add(w);
        db.RepairOrderNotes.Add(new RepairOrderNote(shop,order.Id,"Equipo recibido y condición de ingreso registrada.",actor,now));
        var branch = await db.PremiumBranches.Where(x => x.ShopId == shop).OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync();
        if (branch is not null) db.OrderBusinesses.Add(new RepairShop.Domain.Premium.OrderBusiness { ShopId = shop, OrderId = order.Id, BranchId = branch.Id });
        await db.SaveChangesAsync();
        return (order.Id,w.Number);
    }

    private static readonly string[] Tests = ["Pantalla y táctil", "Cámaras", "Audio y micrófono", "Carga", "Botones", "Biometría"];
    private static Dictionary<string,string> ValidateChecks(Dictionary<string,string>? checks)
    {
        checks ??= new();
        if (checks.Any(x => !Tests.Contains(x.Key) || !new[]{"ok","fail","untested","na"}.Contains(x.Value)))
            throw new DomainException("Hay resultados de checklist inválidos.");
        return checks;
    }
}
