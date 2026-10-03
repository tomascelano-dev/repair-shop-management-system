using RepairShop.Domain.Common;

namespace RepairShop.Domain.Messaging;

public sealed class MessageTemplate : IShopScoped
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
        SetContent(title, body);
        IsActive = isActive;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void Update(string title, string body, bool isActive, DateTime nowUtc)
    {
        SetContent(title, body);
        IsActive = isActive;
        UpdatedAtUtc = nowUtc;
    }

    private void SetContent(string title, string body)
    {
        Title = (title ?? "").Trim();
        Body = (body ?? "").Trim();
        if (Title.Length < 2) throw new DomainException("El título de la plantilla es obligatorio.");
        if (Body.Length < 2) throw new DomainException("El texto de la plantilla es obligatorio.");
        if (Title.Length > 120) throw new DomainException("El título de la plantilla es demasiado largo (máx. 120).");
        if (Body.Length > 4000) throw new DomainException("El texto de la plantilla es demasiado largo (máx. 4000).");
    }

    private static string NormalizeKey(string key)
    {
        key = (key ?? "").Trim().ToLowerInvariant();
        if (key.Length < 3) throw new DomainException("La clave de la plantilla es obligatoria.");
        if (key.Length > 120 || key.Any(c => !(char.IsLetterOrDigit(c) || c is '.' or '_' or '-')))
            throw new DomainException("La clave de la plantilla solo admite letras, números, '.', '_' y '-'.");
        return key;
    }
}
