using RepairShop.Domain.Common;

namespace RepairShop.Domain.Messaging;

public sealed class MessageTemplate
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShopId { get; private set; }

    public string Key { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string Body { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private MessageTemplate() { } // EF

    public MessageTemplate(Guid shopId, string key, string title, string body, bool isActive, DateTime nowUtc)
    {
        ShopId = shopId;
        Key = NormalizeKey(key);
        Title = (title ?? "").Trim();
        Body = (body ?? "").Trim();
        IsActive = isActive;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;

        if (Title.Length < 2) throw new DomainException("Template title is required.");
        if (Body.Length < 2) throw new DomainException("Template body is required.");
    }

    public void Update(string title, string body, bool isActive, DateTime nowUtc)
    {
        Title = (title ?? "").Trim();
        Body = (body ?? "").Trim();
        IsActive = isActive;
        UpdatedAtUtc = nowUtc;

        if (Title.Length < 2) throw new DomainException("Template title is required.");
        if (Body.Length < 2) throw new DomainException("Template body is required.");
    }

    private static string NormalizeKey(string key)
    {
        key = (key ?? "").Trim().ToLowerInvariant();
        if (key.Length < 3) throw new DomainException("Template key is required.");
        return key;
    }
}
