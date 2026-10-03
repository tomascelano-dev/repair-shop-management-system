using FluentValidation;
using RepairShop.Application.Contracts;

namespace RepairShop.Api.Validation;

public sealed class CreateInventoryItemRequestValidator : AbstractValidator<CreateInventoryItemRequest>
{
    public CreateInventoryItemRequestValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MinimumLength(2).MaximumLength(64).WithName("El SKU");
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(200).WithName("El nombre");
        RuleFor(x => x.InitialQuantity).GreaterThanOrEqualTo(0).WithName("El stock inicial");
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).When(x => x.UnitCost is not null).WithName("El costo");
        RuleFor(x => x.SalePrice).GreaterThanOrEqualTo(0).When(x => x.SalePrice is not null).WithName("El precio de venta");
        RuleFor(x => x.MinStock).GreaterThanOrEqualTo(0).WithName("El stock mínimo");
        RuleFor(x => x.Barcode).MaximumLength(64).WithName("El código de barras");
    }
}

public sealed class UpdateInventoryItemRequestValidator : AbstractValidator<UpdateInventoryItemRequest>
{
    public UpdateInventoryItemRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(200).WithName("El nombre");
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).When(x => x.UnitCost is not null).WithName("El costo");
        RuleFor(x => x.SalePrice).GreaterThanOrEqualTo(0).When(x => x.SalePrice is not null).WithName("El precio de venta");
        RuleFor(x => x.MinStock).GreaterThanOrEqualTo(0).WithName("El stock mínimo");
        RuleFor(x => x.Barcode).MaximumLength(64).WithName("El código de barras");
    }
}

public sealed class CreateInventoryAdjustmentRequestValidator : AbstractValidator<CreateInventoryAdjustmentRequest>
{
    public CreateInventoryAdjustmentRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum().WithName("El tipo de movimiento");
        RuleFor(x => x.DeltaQuantity).NotEqual(0).WithName("La cantidad");
        RuleFor(x => x.Reason).MaximumLength(200).WithName("El motivo");
    }
}

public sealed class UsePartOnOrderRequestValidator : AbstractValidator<UsePartOnOrderRequest>
{
    public UsePartOnOrderRequestValidator()
    {
        RuleFor(x => x.InventoryItemId).NotEmpty().WithName("El repuesto");
        RuleFor(x => x.QuantityUsed).GreaterThan(0).WithName("La cantidad");
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).When(x => x.UnitPrice is not null).WithName("El precio");
    }
}

public sealed class CreateSaleRequestValidator : AbstractValidator<CreateSaleRequest>
{
    public CreateSaleRequestValidator()
    {
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Agregá al menos un producto a la venta.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Quantity).GreaterThan(0).WithName("La cantidad");
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0).When(l => l.UnitPrice is not null).WithName("El precio");
            line.RuleFor(l => l.DiscountAmount).GreaterThanOrEqualTo(0).WithName("El descuento");
            line.RuleFor(l => l.DiscountPercent).InclusiveBetween(0, 100).When(l => l.DiscountPercent is not null).WithName("El descuento (%)");
            line.RuleFor(l => l.Description).NotEmpty().When(l => l.InventoryItemId is null)
                .WithMessage("Los ítems sin producto de inventario necesitan una descripción.");
        });
        RuleFor(x => x.Payments).NotEmpty().WithMessage("Indicá al menos un medio de pago.");
        RuleForEach(x => x.Payments).ChildRules(p =>
        {
            p.RuleFor(x => x.Method).IsInEnum().WithName("El medio de pago");
            p.RuleFor(x => x.Amount).GreaterThan(0).WithName("El monto");
        });
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0).WithName("El descuento");
        RuleFor(x => x.DiscountPercent).InclusiveBetween(0, 100).When(x => x.DiscountPercent is not null).WithName("El descuento (%)");
        RuleFor(x => x.Notes).MaximumLength(500).WithName("Las notas");
    }
}

public sealed class CreateCashMovementRequestValidator : AbstractValidator<CreateCashMovementRequest>
{
    public CreateCashMovementRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum().WithName("El tipo de movimiento");
        RuleFor(x => x.Amount).GreaterThan(0).WithName("El monto");
        RuleFor(x => x.Description).NotEmpty().MinimumLength(2).MaximumLength(200).WithName("La descripción");
    }
}

public sealed class SavePurchaseOrderRequestValidator : AbstractValidator<SavePurchaseOrderRequest>
{
    public SavePurchaseOrderRequestValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty().WithName("El proveedor");
        RuleFor(x => x.Currency).NotEmpty().Length(3).WithName("La moneda");
        RuleFor(x => x.Lines).NotEmpty().WithMessage("La orden de compra necesita al menos un ítem.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.InventoryItemId).NotEmpty().WithName("El ítem");
            l.RuleFor(x => x.Quantity).GreaterThan(0).WithName("La cantidad");
            l.RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).WithName("El costo");
        });
    }
}
