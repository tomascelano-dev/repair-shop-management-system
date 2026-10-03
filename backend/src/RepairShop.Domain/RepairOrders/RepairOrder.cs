using System.Security.Cryptography;
using RepairShop.Domain.Common;

namespace RepairShop.Domain.RepairOrders;

public sealed class RepairOrder : IShopScoped
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    // Multi-sucursal
    public Guid ShopId { get; private set; }

    // Correlative number per shop (#000123). Assigned on creation by the application layer.
    public int OrderNumber { get; private set; }

    public Guid CustomerId { get; private set; }
    public Guid DeviceId { get; private set; }

    public string IssueDescription { get; private set; } = null!;
    public string? IssueCategory { get; private set; }
    public string? Notes { get; private set; }

    public RepairOrderStatus Status { get; private set; } = RepairOrderStatus.Received;
    public RepairOrderPriority Priority { get; private set; } = RepairOrderPriority.Normal;

    public Guid? AssignedTechnicianId { get; private set; }
    public DateTime? PromisedAtUtc { get; private set; }

    // Capability token for the public tracking page / QR (not guessable).
    public string PublicToken { get; private set; } = NewToken();

    // Agreed price (set when a quote is approved, or manually by staff).
    public decimal? QuoteAmount { get; private set; }
    public string? QuoteCurrency { get; private set; }
    public Guid? QuoteUpdatedByUserId { get; private set; }
    public DateTime? QuoteUpdatedAtUtc { get; private set; }

    // Warranty
    public int? WarrantyDays { get; private set; }
    public DateTime? WarrantyExpiresAtUtc { get; private set; }
    public bool IsWarrantyClaim { get; private set; }
    public Guid? WarrantyOfOrderId { get; private set; }

    // Lifecycle
    public DateTime LastStatusChangeAtUtc { get; private set; }
    public DateTime? ReadyAtUtc { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }

    // Device unlock code/pattern (encrypted by the application layer, purged on delivery/cancellation).
    public UnlockMethod UnlockMethod { get; private set; } = UnlockMethod.None;
    public string? UnlockSecretProtected { get; private set; }
    public DateTime? UnlockSecretPurgedAtUtc { get; private set; }

    // Signatures (stored files)
    public Guid? ReceptionSignatureFileId { get; private set; }
    public string? ReceptionSignedByName { get; private set; }
    public DateTime? ReceptionSignedAtUtc { get; private set; }
    public Guid? DeliverySignatureFileId { get; private set; }
    public string? DeliverySignedByName { get; private set; }
    public DateTime? DeliverySignedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private RepairOrder() { } // EF

    // Back-compat: constructor anterior (sin ShopId)
    public RepairOrder(Guid customerId, Guid deviceId, string issueDescription, string? notes, DateTime nowUtc)
        : this(Guid.Empty, customerId, deviceId, issueDescription, notes, nowUtc)
    {
    }

    public RepairOrder(Guid shopId, Guid customerId, Guid deviceId, string issueDescription, string? notes, DateTime nowUtc)
    {
        ShopId = shopId;
        CustomerId = customerId;
        DeviceId = deviceId;
        SetDescription(issueDescription, notes);

        if (CustomerId == Guid.Empty) throw new DomainException("La orden debe tener un cliente.");
        if (DeviceId == Guid.Empty) throw new DomainException("La orden debe tener un equipo.");

        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        LastStatusChangeAtUtc = nowUtc;
    }

    public string Code => FormatCode(OrderNumber);

    public static string FormatCode(int orderNumber) => orderNumber > 0 ? $"#{orderNumber:D6}" : "#------";

    public bool IsFinal => Status is RepairOrderStatus.Delivered or RepairOrderStatus.Cancelled;

    public bool IsOverdue(DateTime nowUtc) => !IsFinal && Status != RepairOrderStatus.Ready && PromisedAtUtc is not null && PromisedAtUtc < nowUtc;

    public bool IsUnderWarranty(DateTime nowUtc) => Status == RepairOrderStatus.Delivered && WarrantyExpiresAtUtc is not null && WarrantyExpiresAtUtc >= nowUtc;

    public void AssignNumber(int orderNumber)
    {
        if (OrderNumber != 0) throw new DomainException("La orden ya tiene número asignado.");
        if (orderNumber <= 0) throw new DomainException("El número de orden debe ser positivo.");
        OrderNumber = orderNumber;
    }

    public void SetQuote(decimal amount, string currency, Guid updatedByUserId, DateTime nowUtc)
    {
        if (amount < 0) throw new DomainException("El precio acordado no puede ser negativo.");
        if (amount == 0 && !IsWarrantyClaim) throw new DomainException("El precio acordado debe ser mayor a 0.");
        currency = Money.NormalizeCurrency(currency);
        if (updatedByUserId == Guid.Empty) throw new DomainException("Falta el usuario que actualiza el presupuesto.");
        EnsureNotFinal();

        QuoteAmount = Money.Round(amount);
        QuoteCurrency = currency;
        QuoteUpdatedByUserId = updatedByUserId;
        QuoteUpdatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void ClearQuote(Guid updatedByUserId, DateTime nowUtc)
    {
        if (updatedByUserId == Guid.Empty) throw new DomainException("Falta el usuario que actualiza el presupuesto.");
        EnsureNotFinal();
        QuoteAmount = null;
        QuoteCurrency = null;
        QuoteUpdatedByUserId = updatedByUserId;
        QuoteUpdatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void Update(string issueDescription, string? notes, DateTime nowUtc)
    {
        SetDescription(issueDescription, notes);
        UpdatedAtUtc = nowUtc;
    }

    public void SetCategory(string? category, DateTime nowUtc)
    {
        category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        if (category is { Length: > 60 }) throw new DomainException("La categoría es demasiado larga (máx. 60).");
        IssueCategory = category;
        UpdatedAtUtc = nowUtc;
    }

    public void Plan(Guid? technicianId, RepairOrderPriority priority, DateTime? promisedAtUtc, DateTime nowUtc)
    {
        if (!Enum.IsDefined(priority)) throw new DomainException("Prioridad inválida.");
        if (promisedAtUtc is not null && promisedAtUtc < CreatedAtUtc.AddMinutes(-1))
            throw new DomainException("La fecha prometida no puede ser anterior al ingreso.");

        AssignedTechnicianId = technicianId == Guid.Empty ? null : technicianId;
        Priority = priority;
        PromisedAtUtc = promisedAtUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void SetWarrantyDays(int? days, DateTime nowUtc)
    {
        if (days is < 0 or > 3650) throw new DomainException("La garantía debe estar entre 0 y 3650 días.");
        WarrantyDays = days;
        if (Status == RepairOrderStatus.Delivered && DeliveredAtUtc is not null)
            WarrantyExpiresAtUtc = days is null ? WarrantyExpiresAtUtc : DeliveredAtUtc.Value.AddDays(days.Value);
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Marks this (new) order as a warranty re-entry of a delivered order: no charge by default.
    /// </summary>
    public void MarkAsWarrantyClaim(Guid originalOrderId, string currency, DateTime nowUtc)
    {
        if (originalOrderId == Guid.Empty || originalOrderId == Id) throw new DomainException("Orden original inválida.");
        if (Status != RepairOrderStatus.Received) throw new DomainException("Solo una orden recién ingresada puede marcarse como garantía.");
        IsWarrantyClaim = true;
        WarrantyOfOrderId = originalOrderId;
        QuoteAmount = 0m;
        QuoteCurrency = Money.NormalizeCurrency(currency);
        QuoteUpdatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void SetUnlockSecret(UnlockMethod method, string? protectedSecret, DateTime nowUtc)
    {
        EnsureNotFinal();
        if (!Enum.IsDefined(method)) throw new DomainException("Método de desbloqueo inválido.");
        if (method == UnlockMethod.None)
        {
            UnlockMethod = UnlockMethod.None;
            UnlockSecretProtected = null;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(protectedSecret)) throw new DomainException("Falta el código de desbloqueo.");
            UnlockMethod = method;
            UnlockSecretProtected = protectedSecret;
            UnlockSecretPurgedAtUtc = null;
        }

        UpdatedAtUtc = nowUtc;
    }

    public void PurgeUnlockSecret(DateTime nowUtc)
    {
        if (UnlockSecretProtected is null) return;
        UnlockSecretProtected = null;
        UnlockSecretPurgedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void SetReceptionSignature(Guid fileId, string signerName, DateTime nowUtc)
    {
        (ReceptionSignatureFileId, ReceptionSignedByName, ReceptionSignedAtUtc) = ValidateSignature(fileId, signerName, nowUtc);
        UpdatedAtUtc = nowUtc;
    }

    public void SetDeliverySignature(Guid fileId, string signerName, DateTime nowUtc)
    {
        (DeliverySignatureFileId, DeliverySignedByName, DeliverySignedAtUtc) = ValidateSignature(fileId, signerName, nowUtc);
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Touch the aggregate when a related financial record changes (payments, extra charges)
    /// so concurrent writers conflict on the row version instead of over-charging.
    /// </summary>
    public void TouchFinancials(DateTime nowUtc) => UpdatedAtUtc = nowUtc;

    public void RegenerateToken(DateTime nowUtc)
    {
        PublicToken = NewToken();
        UpdatedAtUtc = nowUtc;
    }

    public static IReadOnlyList<RepairOrderStatus> AllowedTransitions(RepairOrderStatus from) => from switch
    {
        RepairOrderStatus.Received => new[] { RepairOrderStatus.Diagnosing, RepairOrderStatus.Cancelled },
        RepairOrderStatus.Diagnosing => new[] { RepairOrderStatus.InProgress, RepairOrderStatus.WaitingParts, RepairOrderStatus.Cancelled },
        RepairOrderStatus.WaitingParts => new[] { RepairOrderStatus.InProgress, RepairOrderStatus.Cancelled },
        RepairOrderStatus.InProgress => new[] { RepairOrderStatus.WaitingParts, RepairOrderStatus.Testing, RepairOrderStatus.Ready, RepairOrderStatus.Cancelled },
        RepairOrderStatus.Testing => new[] { RepairOrderStatus.InProgress, RepairOrderStatus.Ready, RepairOrderStatus.Cancelled },
        RepairOrderStatus.Ready => new[] { RepairOrderStatus.Delivered, RepairOrderStatus.InProgress, RepairOrderStatus.Cancelled },
        _ => Array.Empty<RepairOrderStatus>()
    };

    public void MoveTo(RepairOrderStatus newStatus, DateTime nowUtc)
        => MoveTo(newStatus, nowUtc, new StatusTransitionContext());

    public void MoveTo(RepairOrderStatus newStatus, DateTime nowUtc, StatusTransitionContext ctx)
    {
        if (IsFinal)
            throw new DomainException("Las órdenes finalizadas (entregadas o canceladas) no pueden cambiar de estado.");

        if (!AllowedTransitions(Status).Contains(newStatus))
            throw new DomainException($"Transición de estado inválida: {Status} -> {newStatus}.");

        if (Status == RepairOrderStatus.Diagnosing
            && newStatus is RepairOrderStatus.InProgress or RepairOrderStatus.WaitingParts
            && !IsWarrantyClaim
            && !ctx.HasApprovedQuote)
        {
            throw new DomainException("Para avanzar con la reparación el presupuesto tiene que estar aprobado por el cliente.");
        }

        if (newStatus == RepairOrderStatus.Ready && !ctx.QaPassed)
            throw new DomainException("Antes de marcar la orden como lista hay que completar y aprobar el control de calidad de salida.");

        if (newStatus == RepairOrderStatus.Delivered && ctx.BalanceDue > 0 && !ctx.AllowUnpaidDelivery)
            throw new DomainException($"La orden tiene saldo pendiente ({ctx.BalanceDue:0.00}). Registrá el pago antes de entregar.");

        if (newStatus == RepairOrderStatus.Cancelled && ctx.Reason is { Length: > 300 })
            throw new DomainException("El motivo de cancelación es demasiado largo (máx. 300).");

        Status = newStatus;
        LastStatusChangeAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;

        switch (newStatus)
        {
            case RepairOrderStatus.Ready:
                ReadyAtUtc = nowUtc;
                break;
            case RepairOrderStatus.InProgress:
                // Re-work after "Ready": the pickup clock starts again.
                ReadyAtUtc = null;
                break;
            case RepairOrderStatus.Delivered:
                DeliveredAtUtc = nowUtc;
                var days = WarrantyDays ?? ctx.DefaultWarrantyDays;
                WarrantyDays = days;
                WarrantyExpiresAtUtc = days > 0 ? nowUtc.AddDays(days) : null;
                PurgeUnlockSecret(nowUtc);
                break;
            case RepairOrderStatus.Cancelled:
                CancelledAtUtc = nowUtc;
                CancellationReason = string.IsNullOrWhiteSpace(ctx.Reason) ? null : ctx.Reason.Trim();
                PurgeUnlockSecret(nowUtc);
                break;
        }
    }

    private void EnsureNotFinal()
    {
        if (IsFinal) throw new DomainException("La orden está finalizada y no se puede modificar.");
    }

    private void SetDescription(string issueDescription, string? notes)
    {
        IssueDescription = (issueDescription ?? "").Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (IssueDescription.Length < 5) throw new DomainException("La descripción del problema es obligatoria (mín. 5 caracteres).");
        if (IssueDescription.Length > 500) throw new DomainException("La descripción del problema es demasiado larga (máx. 500).");
    }

    private static (Guid?, string?, DateTime?) ValidateSignature(Guid fileId, string signerName, DateTime nowUtc)
    {
        if (fileId == Guid.Empty) throw new DomainException("Falta la firma.");
        signerName = (signerName ?? "").Trim();
        if (signerName.Length < 2) throw new DomainException("Indicá el nombre de quien firma.");
        if (signerName.Length > 120) throw new DomainException("El nombre de quien firma es demasiado largo.");
        return (fileId, signerName, nowUtc);
    }

    private static string NewToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
