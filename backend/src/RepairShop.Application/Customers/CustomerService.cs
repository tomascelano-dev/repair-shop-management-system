using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Application.RepairOrders;
using RepairShop.Domain.Common;
using RepairShop.Domain.Customers;
using RepairShop.Domain.Devices;
using RepairShop.Domain.RepairOrders;
using RepairShop.Domain.Sales;

namespace RepairShop.Application.Customers;

public sealed class CustomerService
{
    private const string EntityType = "customer";

    private readonly ICustomerRepository _customers;
    private readonly IDeviceRepository _devices;
    private readonly IRepairOrderRepository _orders;
    private readonly ISaleRepository _sales;
    private readonly IRepairOrderReadModel _readModel;
    private readonly IShopRepository _shops;
    private readonly ICustomerMergeStore _merge;
    private readonly IAuditLog _audit;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public CustomerService(
        ICustomerRepository customers,
        IDeviceRepository devices,
        IRepairOrderRepository orders,
        ISaleRepository sales,
        IRepairOrderReadModel readModel,
        IShopRepository shops,
        ICustomerMergeStore merge,
        IAuditLog audit,
        IUnitOfWork uow,
        IDateTimeProvider clock)
    {
        _customers = customers;
        _devices = devices;
        _orders = orders;
        _sales = sales;
        _readModel = readModel;
        _shops = shops;
        _merge = merge;
        _audit = audit;
        _uow = uow;
        _clock = clock;
    }

    public async Task<PagedResult<CustomerResponse>> SearchAsync(Guid shopId, CustomerSearchOptions options, CancellationToken ct)
    {
        var cc = await CountryCodeAsync(shopId, ct);
        var (items, total) = await _customers.SearchAsync(shopId, options, ct);
        return new PagedResult<CustomerResponse>(items.Select(c => c.ToResponse(cc)).ToList(), total);
    }

    public async Task<CustomerResponse> GetAsync(Guid shopId, Guid id, CancellationToken ct)
        => (await RequireAsync(shopId, id, ct)).ToResponse(await CountryCodeAsync(shopId, ct));

    public async Task<CustomerResponse> CreateAsync(Guid shopId, CustomerCreateRequest req, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var c = new Customer(shopId, req.FullName, req.Phone, req.Notes, now);
        Apply(c, req.Email, req.Address, req.Tags, req.DocumentType, req.DocumentNumber, req.TaxCondition, req.NotificationsOptIn, req.MarketingOptIn, now);
        await _customers.AddAsync(c, ct);
        await _audit.AddAsync(shopId, EntityType, c.Id, "customer_created", actor, new { c.FullName }, ct);
        await _uow.SaveChangesAsync(ct);
        return c.ToResponse(await CountryCodeAsync(shopId, ct));
    }

    public async Task<CustomerResponse> UpdateAsync(Guid shopId, Guid id, CustomerUpdateRequest req, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var c = await RequireAsync(shopId, id, ct);
        c.Update(req.FullName, req.Phone, req.Notes, now);
        Apply(c, req.Email, req.Address, req.Tags, req.DocumentType, req.DocumentNumber, req.TaxCondition, req.NotificationsOptIn, req.MarketingOptIn, now);
        await _audit.AddAsync(shopId, EntityType, c.Id, "customer_updated", actor, null, ct);
        await _uow.SaveChangesAsync(ct);
        return c.ToResponse(await CountryCodeAsync(shopId, ct));
    }

    public async Task DeleteAsync(Guid shopId, Guid id, Actor actor, CancellationToken ct)
    {
        var c = await RequireAsync(shopId, id, ct);
        var usage = await _customers.GetUsageAsync(shopId, id, ct);
        if (usage.Devices + usage.Orders + usage.Sales > 0)
            throw new ConflictException($"No se puede eliminar: el cliente tiene {usage.Devices} equipo(s), {usage.Orders} orden(es) y {usage.Sales} venta(s). Podés fusionarlo con otro cliente.");
        await _customers.RemoveAsync(c, ct);
        await _audit.AddAsync(shopId, EntityType, c.Id, "customer_deleted", actor, new { c.FullName }, ct);
        await _uow.SaveChangesAsync(ct);
    }

    /// <summary>Customers sharing the phone (last 8 digits), to warn before creating a duplicate.</summary>
    public async Task<List<DuplicateCustomerResponse>> FindDuplicatesAsync(Guid shopId, string phone, Guid? excludeId, CancellationToken ct)
    {
        var key = PhoneNumber.Key(phone);
        if (key.Length < 6) return new List<DuplicateCustomerResponse>();
        var list = await _customers.FindByPhoneKeyAsync(shopId, key, excludeId, ct);
        var result = new List<DuplicateCustomerResponse>();
        foreach (var c in list)
        {
            var usage = await _customers.GetUsageAsync(shopId, c.Id, ct);
            result.Add(new DuplicateCustomerResponse(c.Id, c.FullName, c.Phone, c.Email, c.CreatedAtUtc, usage.Devices, usage.Orders));
        }
        return result;
    }

    /// <summary>Moves devices, orders, sales and feedback of <paramref name="sourceId"/> to <paramref name="targetId"/> and deletes the source.</summary>
    public async Task<CustomerResponse> MergeAsync(Guid shopId, Guid targetId, Guid sourceId, Actor actor, CancellationToken ct)
    {
        if (targetId == sourceId) throw new DomainException("Elegí dos clientes distintos.");
        var target = await RequireAsync(shopId, targetId, ct);
        var source = await RequireAsync(shopId, sourceId, ct);

        await _uow.InTransactionAsync(async c =>
        {
            var moved = await _merge.ReassignAsync(shopId, source.Id, target.Id, c);
            if (string.IsNullOrWhiteSpace(target.Email) && !string.IsNullOrWhiteSpace(source.Email))
                target.UpdateContact(source.Email, target.Address ?? source.Address, MergeTags(target.Tags, source.Tags), _clock.UtcNow);
            await _customers.RemoveAsync(source, c);
            await _audit.AddAsync(shopId, EntityType, target.Id, "customer_merged", actor, new { sourceId = source.Id, source = source.FullName, moved }, c);
            await _uow.SaveChangesAsync(c);
            return true;
        }, ct);

        return target.ToResponse(await CountryCodeAsync(shopId, ct));
    }

    public async Task<CustomerSummaryResponse> GetSummaryAsync(Guid shopId, Guid id, CancellationToken ct)
    {
        var shop = await _shops.GetByIdAsync(shopId, ct) ?? throw new NotFoundException("Sucursal no encontrada.");
        var customer = await RequireAsync(shopId, id, ct);
        var devices = await _devices.ListByCustomerAsync(shopId, id, 0, 200, ct);
        var (orders, _) = await _orders.SearchAsync(shopId, new RepairOrderSearchOptions(CustomerId: id, Take: 200), ct);
        var (sales, _) = await _sales.SearchAsync(shopId, new SaleSearchOptions(CustomerId: id, Take: 200), ct);
        var info = orders.Count == 0 ? new Dictionary<Guid, OrderReadInfo>() : await _readModel.GetInfoAsync(shopId, orders.Select(o => o.Id).ToList(), ct);
        var feedback = await _merge.AverageFeedbackAsync(shopId, id, ct);

        var orderItems = orders.Select(o =>
        {
            var money = OrderMapping.Financials(o, info[o.Id], shop.DefaultCurrency);
            return new CustomerOrderItem(o.Id, o.Code, o.Status.ToString(), info[o.Id].DeviceDisplay, o.IssueDescription,
                money.Total, money.BalanceDue, money.Currency, o.CreatedAtUtc, o.DeliveredAtUtc);
        }).ToList();

        var spent = orderItems.Where(o => o.Status != nameof(RepairOrderStatus.Cancelled))
            .Select(o => (o.Currency, Amount: o.Total - Math.Max(0, o.Balance)))
            .Concat(sales.Where(s => s.Status != SaleStatus.Voided).Select(s => (s.Currency, Amount: s.Total - s.RefundedAmount)))
            .GroupBy(x => x.Currency).Select(g => new CurrencyAmount(g.Key, Money.Round(g.Sum(x => x.Amount)))).ToList();
        var balance = orderItems.Where(o => o.Balance > 0 && o.Status != nameof(RepairOrderStatus.Cancelled))
            .GroupBy(o => o.Currency).Select(g => new CurrencyAmount(g.Key, Money.Round(g.Sum(x => x.Balance)))).ToList();

        var lastVisit = orders.Select(o => (DateTime?)o.CreatedAtUtc).Concat(sales.Select(s => (DateTime?)s.CreatedAtUtc)).DefaultIfEmpty(null).Max();

        return new CustomerSummaryResponse(
            customer.ToResponse(shop.PhoneCountryCode),
            devices.Select(d => d.ToResponse(customer.FullName)).ToList(),
            orderItems,
            sales.Select(s => new CustomerSaleItem(s.Id, s.Code, s.Status.ToString(), s.Total, s.Currency, s.CreatedAtUtc)).ToList(),
            spent,
            balance,
            orders.Count(o => !o.IsFinal),
            lastVisit,
            feedback);
    }

    // ---------------------------------------------------------------------------------------------

    private static void Apply(Customer c, string? email, string? address, string? tags, CustomerDocumentType docType, string? docNumber,
        CustomerTaxCondition taxCondition, bool notificationsOptIn, bool marketingOptIn, DateTime now)
    {
        c.UpdateContact(email, address, tags, now);
        c.UpdateFiscal(docType, docNumber, taxCondition, now);
        c.SetConsents(notificationsOptIn, marketingOptIn, now);
    }

    private static string? MergeTags(string? a, string? b)
        => string.Join(",", new[] { a, b }.Where(x => !string.IsNullOrWhiteSpace(x)));

    private async Task<Customer> RequireAsync(Guid shopId, Guid id, CancellationToken ct)
        => await _customers.GetByIdAsync(shopId, id, ct) ?? throw new NotFoundException("Cliente no encontrado.");

    private async Task<string> CountryCodeAsync(Guid shopId, CancellationToken ct)
        => (await _shops.GetByIdAsync(shopId, ct))?.PhoneCountryCode ?? "54";
}

/// <summary>Bulk operations used when merging duplicated customers.</summary>
public interface ICustomerMergeStore
{
    Task<int> ReassignAsync(Guid shopId, Guid fromCustomerId, Guid toCustomerId, CancellationToken ct);
    Task<double?> AverageFeedbackAsync(Guid shopId, Guid customerId, CancellationToken ct);
}
