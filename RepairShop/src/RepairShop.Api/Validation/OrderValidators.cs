using FluentValidation;
using RepairShop.Application.Contracts;

namespace RepairShop.Api.Validation;

public sealed class RepairOrderCreateRequestValidator : AbstractValidator<RepairOrderCreateRequest>
{
    public RepairOrderCreateRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.DeviceId).NotEmpty();
        RuleFor(x => x.IssueDescription).NotEmpty().MinimumLength(5).MaximumLength(500);
        RuleFor(x => x.Notes).MaximumLength(2000).When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}

public sealed class RepairOrderUpdateRequestValidator : AbstractValidator<RepairOrderUpdateRequest>
{
    public RepairOrderUpdateRequestValidator()
    {
        RuleFor(x => x.IssueDescription).NotEmpty().MinimumLength(5).MaximumLength(500);
        RuleFor(x => x.Notes).MaximumLength(2000).When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}

public sealed class ChangeOrderStatusRequestValidator : AbstractValidator<ChangeOrderStatusRequest>
{
    public ChangeOrderStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class CreateRepairOrderPaymentRequestValidator : AbstractValidator<CreateRepairOrderPaymentRequest>
{
    public CreateRepairOrderPaymentRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3)
            .Must(c => c.ToUpperInvariant() == c).WithMessage("Currency must be uppercase ISO code");
        RuleFor(x => x.Reference)
            .MaximumLength(120)
            .When(x => !string.IsNullOrWhiteSpace(x.Reference));
        RuleFor(x => x.Method).IsInEnum();
    }
}

public sealed class CreateRepairOrderNoteRequestValidator : AbstractValidator<CreateRepairOrderNoteRequest>
{
    public CreateRepairOrderNoteRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MinimumLength(2).MaximumLength(2000);
    }
}
