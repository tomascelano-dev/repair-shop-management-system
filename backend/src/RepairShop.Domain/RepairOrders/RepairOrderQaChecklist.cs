using RepairShop.Domain.Common;

namespace RepairShop.Domain.RepairOrders;

/// <summary>
/// Exit quality-control checklist completed by the technician before the order can be marked as Ready.
/// Each check is nullable: null = not applicable for this device.
/// </summary>
public sealed class RepairOrderQaChecklist : IShopScoped
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShopId { get; private set; }
    public Guid RepairOrderId { get; private set; }

    public bool? PowersOn { get; private set; }
    public bool? ScreenOk { get; private set; }
    public bool? TouchOk { get; private set; }
    public bool? CamerasOk { get; private set; }
    public bool? AudioOk { get; private set; }
    public bool? MicrophoneOk { get; private set; }
    public bool? ButtonsOk { get; private set; }
    public bool? ChargingOk { get; private set; }
    public bool? ConnectivityOk { get; private set; }
    public bool? BiometricsOk { get; private set; }

    public int? BatteryHealthPercent { get; private set; }
    public string? Notes { get; private set; }

    // The technician explicitly approves the QA (all applicable checks must be OK).
    public bool Passed { get; private set; }

    public Guid CheckedByUserId { get; private set; }
    public DateTime CheckedAtUtc { get; private set; }

    private RepairOrderQaChecklist() { } // EF

    public RepairOrderQaChecklist(Guid shopId, Guid repairOrderId, Guid userId, DateTime nowUtc)
    {
        if (repairOrderId == Guid.Empty) throw new DomainException("El control de calidad debe pertenecer a una orden.");
        if (userId == Guid.Empty) throw new DomainException("El control de calidad debe tener un usuario.");
        ShopId = shopId;
        RepairOrderId = repairOrderId;
        CheckedByUserId = userId;
        CheckedAtUtc = nowUtc;
    }

    public void Update(
        bool? powersOn,
        bool? screenOk,
        bool? touchOk,
        bool? camerasOk,
        bool? audioOk,
        bool? microphoneOk,
        bool? buttonsOk,
        bool? chargingOk,
        bool? connectivityOk,
        bool? biometricsOk,
        int? batteryHealthPercent,
        string? notes,
        bool approve,
        Guid userId,
        DateTime nowUtc)
    {
        if (userId == Guid.Empty) throw new DomainException("El control de calidad debe tener un usuario.");
        if (batteryHealthPercent is < 0 or > 100) throw new DomainException("La salud de batería debe estar entre 0 y 100.");

        PowersOn = powersOn;
        ScreenOk = screenOk;
        TouchOk = touchOk;
        CamerasOk = camerasOk;
        AudioOk = audioOk;
        MicrophoneOk = microphoneOk;
        ButtonsOk = buttonsOk;
        ChargingOk = chargingOk;
        ConnectivityOk = connectivityOk;
        BiometricsOk = biometricsOk;
        BatteryHealthPercent = batteryHealthPercent;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()[..Math.Min(notes.Trim().Length, 1000)];

        var checks = new[] { powersOn, screenOk, touchOk, camerasOk, audioOk, microphoneOk, buttonsOk, chargingOk, connectivityOk, biometricsOk };
        if (approve)
        {
            if (checks.All(c => c is null)) throw new DomainException("Completá al menos un control antes de aprobar.");
            if (checks.Any(c => c == false)) throw new DomainException("No se puede aprobar: hay controles con falla.");
        }

        Passed = approve;
        CheckedByUserId = userId;
        CheckedAtUtc = nowUtc;
    }

    /// <summary>A new repair round invalidates the previous approval.</summary>
    public void Invalidate(DateTime nowUtc)
    {
        Passed = false;
        CheckedAtUtc = nowUtc;
    }
}
