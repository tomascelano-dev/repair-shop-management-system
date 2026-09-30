using FluentValidation;
using RepairShop.Application.Contracts;

namespace RepairShop.Api.Validation;

public sealed class CreateInventoryItemRequestValidator : AbstractValidator<CreateInventoryItemRequest>
{
    public CreateInventoryItemRequestValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MinimumLength(2).MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(200);
        RuleFor(x => x.InitialQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).When(x => x.UnitCost is not null);
        RuleFor(x => x.UnitCostCurrency)
            .Length(3)
            .Must(c => c!.ToUpperInvariant() == c).WithMessage("UnitCostCurrency must be uppercase ISO code")
            .When(x => !string.IsNullOrWhiteSpace(x.UnitCostCurrency));
    }
}

public sealed class UpdateInventoryItemRequestValidator : AbstractValidator<UpdateInventoryItemRequest>
{
    public UpdateInventoryItemRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(200);
    }
}

public sealed class CreateInventoryAdjustmentRequestValidator : AbstractValidator<CreateInventoryAdjustmentRequest>
{
    public CreateInventoryAdjustmentRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.DeltaQuantity).NotEqual(0);
        RuleFor(x => x.Reason).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.Reason));
    }
}

public sealed class UsePartOnOrderRequestValidator : AbstractValidator<UsePartOnOrderRequest>
{
    public UsePartOnOrderRequestValidator()
    {
        RuleFor(x => x.InventoryItemId).NotEmpty();
        RuleFor(x => x.QuantityUsed).GreaterThan(0);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).When(x => x.UnitPrice is not null);
        RuleFor(x => x.UnitPriceCurrency)
            .Length(3)
            .Must(c => c!.ToUpperInvariant() == c).WithMessage("UnitPriceCurrency must be uppercase ISO code")
            .When(x => !string.IsNullOrWhiteSpace(x.UnitPriceCurrency));
    }
}
