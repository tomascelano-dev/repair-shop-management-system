using FluentValidation;
using RepairShop.Application.Contracts;

namespace RepairShop.Api.Validation;

public sealed class RepairOrderCreateRequestValidator : AbstractValidator<RepairOrderCreateRequest>
{
    public RepairOrderCreateRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().WithName("El cliente");
        RuleFor(x => x.DeviceId).NotEmpty().WithName("El equipo");
        RuleFor(x => x.IssueDescription).NotEmpty().MinimumLength(5).MaximumLength(500).WithName("La falla");
        RuleFor(x => x.Notes).MaximumLength(2000).WithName("Las notas");
        RuleFor(x => x.IssueCategory).MaximumLength(60).WithName("La categoría");
        RuleFor(x => x.Priority).IsInEnum().WithName("La prioridad");
        RuleFor(x => x.WarrantyDays).InclusiveBetween(0, 3650).When(x => x.WarrantyDays is not null).WithName("La garantía");
    }
}

public sealed class RepairOrderUpdateRequestValidator : AbstractValidator<RepairOrderUpdateRequest>
{
    public RepairOrderUpdateRequestValidator()
    {
        RuleFor(x => x.IssueDescription).NotEmpty().MinimumLength(5).MaximumLength(500).WithName("La falla");
        RuleFor(x => x.Notes).MaximumLength(2000).WithName("Las notas");
        RuleFor(x => x.IssueCategory).MaximumLength(60).WithName("La categoría");
        RuleFor(x => x.WarrantyDays).InclusiveBetween(0, 3650).When(x => x.WarrantyDays is not null).WithName("La garantía");
    }
}

public sealed class PlanOrderRequestValidator : AbstractValidator<PlanOrderRequest>
{
    public PlanOrderRequestValidator() => RuleFor(x => x.Priority).IsInEnum().WithName("La prioridad");
}

public sealed class ChangeOrderStatusRequestValidator : AbstractValidator<ChangeOrderStatusRequest>
{
    public ChangeOrderStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum().WithName("El estado");
        RuleFor(x => x.Channel).IsInEnum().When(x => x.Channel is not null).WithName("El canal");
        RuleFor(x => x.Reason).MaximumLength(500).WithName("El motivo");
    }
}

public sealed class CreateRepairOrderPaymentRequestValidator : AbstractValidator<CreateRepairOrderPaymentRequest>
{
    public CreateRepairOrderPaymentRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithName("El monto");
        RuleFor(x => x.Currency).NotEmpty().Length(3).Must(c => c.All(char.IsLetter)).WithName("La moneda");
        RuleFor(x => x.Reference).MaximumLength(120).WithName("La referencia");
        RuleFor(x => x.Method).IsInEnum().WithName("El medio de pago");
    }
}

public sealed class RefundOrderPaymentRequestValidator : AbstractValidator<RefundOrderPaymentRequest>
{
    public RefundOrderPaymentRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithName("El monto");
        RuleFor(x => x.Method).IsInEnum().WithName("El medio de devolución");
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(3).MaximumLength(300).WithName("El motivo");
    }
}

public sealed class CreateRepairOrderNoteRequestValidator : AbstractValidator<CreateRepairOrderNoteRequest>
{
    public CreateRepairOrderNoteRequestValidator() => RuleFor(x => x.Body).NotEmpty().MinimumLength(2).MaximumLength(2000).WithName("La nota");
}

public sealed class SaveQuoteRequestValidator : AbstractValidator<SaveQuoteRequest>
{
    public SaveQuoteRequestValidator()
    {
        RuleFor(x => x.Currency).NotEmpty().Length(3).WithName("La moneda");
        RuleFor(x => x.Items).NotEmpty().WithMessage("El presupuesto necesita al menos un ítem.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Kind).IsInEnum().WithName("El tipo de ítem");
            item.RuleFor(i => i.Description).NotEmpty().MinimumLength(2).MaximumLength(200).WithName("La descripción");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithName("La cantidad");
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0).WithName("El precio");
        });
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0).WithName("El descuento");
        RuleFor(x => x.Notes).MaximumLength(1000).WithName("Las notas");
    }
}

public sealed class SaveSignatureRequestValidator : AbstractValidator<SaveSignatureRequest>
{
    public SaveSignatureRequestValidator()
    {
        RuleFor(x => x.Kind).IsInEnum().WithName("El tipo de firma");
        RuleFor(x => x.SignerName).NotEmpty().MinimumLength(2).MaximumLength(120).WithName("El nombre de quien firma");
        RuleFor(x => x.ImageDataUrl).NotEmpty().WithName("La firma");
    }
}
