using RepairShop.Domain.Common;

namespace RepairShop.Domain.RepairOrders;

public sealed class RepairOrderReceptionChecklist : IShopScoped
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ShopId { get; private set; }
    public Guid RepairOrderId { get; private set; }

    public bool ScreenOk { get; private set; }
    public bool CamerasOk { get; private set; }
    public bool SpeakersOk { get; private set; }
    public bool MicrophoneOk { get; private set; }
    public bool ButtonsOk { get; private set; }
    public bool FaceIdOk { get; private set; }
    public bool FingerprintOk { get; private set; }
    public CloudLockStatus CloudLock { get; private set; }

    public int? BatteryPercent { get; private set; }
    public string? CosmeticNotes { get; private set; }

    public Guid UpdatedByUserId { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private RepairOrderReceptionChecklist() { } // EF

    public RepairOrderReceptionChecklist(
        Guid shopId,
        Guid repairOrderId,
        Guid updatedByUserId,
        DateTime nowUtc)
    {
        ShopId = shopId;
        RepairOrderId = repairOrderId;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = nowUtc;

        if (RepairOrderId == Guid.Empty) throw new DomainException("El checklist debe pertenecer a una orden.");
        if (UpdatedByUserId == Guid.Empty) throw new DomainException("El checklist debe tener un usuario.");
    }

    public void Update(
        bool screenOk,
        bool camerasOk,
        bool speakersOk,
        bool microphoneOk,
        bool buttonsOk,
        bool faceIdOk,
        bool fingerprintOk,
        CloudLockStatus cloudLock,
        int? batteryPercent,
        string? cosmeticNotes,
        Guid updatedByUserId,
        DateTime nowUtc)
    {
        if (updatedByUserId == Guid.Empty) throw new DomainException("El checklist debe tener un usuario.");
        if (batteryPercent is < 0 or > 100) throw new DomainException("El porcentaje de batería debe estar entre 0 y 100.");

        ScreenOk = screenOk;
        CamerasOk = camerasOk;
        SpeakersOk = speakersOk;
        MicrophoneOk = microphoneOk;
        ButtonsOk = buttonsOk;
        FaceIdOk = faceIdOk;
        FingerprintOk = fingerprintOk;
        CloudLock = cloudLock;
        BatteryPercent = batteryPercent;
        CosmeticNotes = cosmeticNotes?.Trim();

        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = nowUtc;
    }
}
