using FluentValidation;
using RepairShop.Application.Contracts;

namespace RepairShop.Api.Validation;

public sealed class CustomerCreateRequestValidator : AbstractValidator<CustomerCreateRequest>
{
    public CustomerCreateRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MinimumLength(3).MaximumLength(120).WithName("El nombre");
        RuleFor(x => x.Phone).NotEmpty().MinimumLength(6).MaximumLength(32).WithName("El teléfono")
            .Must(p => p.Count(char.IsDigit) >= 6).WithMessage("El teléfono debe tener al menos 6 dígitos.");
        RuleFor(x => x.Email).MaximumLength(180).EmailAddress().WithName("El email").When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Notes).MaximumLength(1000).WithName("Las notas");
        RuleFor(x => x.Address).MaximumLength(250).WithName("La dirección");
        RuleFor(x => x.Tags).MaximumLength(300).WithName("Las etiquetas");
        RuleFor(x => x.DocumentType).IsInEnum().WithName("El tipo de documento");
        RuleFor(x => x.TaxCondition).IsInEnum().WithName("La condición fiscal");
    }
}

public sealed class CustomerUpdateRequestValidator : AbstractValidator<CustomerUpdateRequest>
{
    public CustomerUpdateRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MinimumLength(3).MaximumLength(120).WithName("El nombre");
        RuleFor(x => x.Phone).NotEmpty().MinimumLength(6).MaximumLength(32).WithName("El teléfono")
            .Must(p => p.Count(char.IsDigit) >= 6).WithMessage("El teléfono debe tener al menos 6 dígitos.");
        RuleFor(x => x.Email).MaximumLength(180).EmailAddress().WithName("El email").When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Notes).MaximumLength(1000).WithName("Las notas");
        RuleFor(x => x.Address).MaximumLength(250).WithName("La dirección");
        RuleFor(x => x.Tags).MaximumLength(300).WithName("Las etiquetas");
        RuleFor(x => x.DocumentType).IsInEnum().WithName("El tipo de documento");
        RuleFor(x => x.TaxCondition).IsInEnum().WithName("La condición fiscal");
    }
}

public sealed class DeviceCreateRequestValidator : AbstractValidator<DeviceCreateRequest>
{
    public DeviceCreateRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().WithName("El cliente");
        RuleFor(x => x.Brand).NotEmpty().MinimumLength(2).MaximumLength(60).WithName("La marca");
        RuleFor(x => x.Model).NotEmpty().MinimumLength(2).MaximumLength(80).WithName("El modelo");
        RuleFor(x => x.Label).MaximumLength(80).WithName("La etiqueta");
        RuleFor(x => x.SerialNumber).MaximumLength(80).WithName("El número de serie");
        RuleFor(x => x.Notes).MaximumLength(1000).WithName("Las notas");
    }
}

public sealed class DeviceUpdateRequestValidator : AbstractValidator<DeviceUpdateRequest>
{
    public DeviceUpdateRequestValidator()
    {
        RuleFor(x => x.Brand).NotEmpty().MinimumLength(2).MaximumLength(60).WithName("La marca");
        RuleFor(x => x.Model).NotEmpty().MinimumLength(2).MaximumLength(80).WithName("El modelo");
        RuleFor(x => x.Label).MaximumLength(80).WithName("La etiqueta");
        RuleFor(x => x.SerialNumber).MaximumLength(80).WithName("El número de serie");
        RuleFor(x => x.Notes).MaximumLength(1000).WithName("Las notas");
    }
}
