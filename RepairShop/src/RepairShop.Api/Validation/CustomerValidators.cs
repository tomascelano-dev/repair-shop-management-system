using FluentValidation;
using RepairShop.Application.Contracts;

namespace RepairShop.Api.Validation;

public sealed class CustomerCreateRequestValidator : AbstractValidator<CustomerCreateRequest>
{
    public CustomerCreateRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().MinimumLength(3).MaximumLength(120);

        RuleFor(x => x.Phone)
            .NotEmpty().MinimumLength(6).MaximumLength(32)
            .Must(p => p.Any(char.IsDigit)).WithMessage("Phone must contain digits");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}

public sealed class CustomerUpdateRequestValidator : AbstractValidator<CustomerUpdateRequest>
{
    public CustomerUpdateRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().MinimumLength(3).MaximumLength(120);

        RuleFor(x => x.Phone)
            .NotEmpty().MinimumLength(6).MaximumLength(32)
            .Must(p => p.Any(char.IsDigit)).WithMessage("Phone must contain digits");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}
