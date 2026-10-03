using FluentValidation.Resources;

namespace RepairShop.Api.Validation;

/// <summary>
/// FluentValidation messages in Spanish. The app runs with invariant globalization (no "es" culture
/// available), so the default (English) messages are overridden instead of switching cultures.
/// </summary>
public sealed class SpanishLanguageManager : LanguageManager
{
    public SpanishLanguageManager()
    {
        var es = new Dictionary<string, string>
        {
            ["NotEmptyValidator"] = "{PropertyName} es obligatorio.",
            ["NotNullValidator"] = "{PropertyName} es obligatorio.",
            ["EmptyValidator"] = "{PropertyName} debe estar vacío.",
            ["MinimumLengthValidator"] = "{PropertyName} debe tener al menos {MinLength} caracteres.",
            ["MaximumLengthValidator"] = "{PropertyName} no puede superar los {MaxLength} caracteres.",
            ["LengthValidator"] = "{PropertyName} debe tener entre {MinLength} y {MaxLength} caracteres.",
            ["ExactLengthValidator"] = "{PropertyName} debe tener {MaxLength} caracteres.",
            ["EmailValidator"] = "{PropertyName} no es un email válido.",
            ["GreaterThanValidator"] = "{PropertyName} debe ser mayor a {ComparisonValue}.",
            ["GreaterThanOrEqualValidator"] = "{PropertyName} debe ser mayor o igual a {ComparisonValue}.",
            ["LessThanValidator"] = "{PropertyName} debe ser menor a {ComparisonValue}.",
            ["LessThanOrEqualValidator"] = "{PropertyName} debe ser menor o igual a {ComparisonValue}.",
            ["InclusiveBetweenValidator"] = "{PropertyName} debe estar entre {From} y {To}.",
            ["ExclusiveBetweenValidator"] = "{PropertyName} debe estar entre {From} y {To} (sin incluirlos).",
            ["EqualValidator"] = "{PropertyName} debe ser igual a {ComparisonValue}.",
            ["NotEqualValidator"] = "{PropertyName} no puede ser {ComparisonValue}.",
            ["EnumValidator"] = "{PropertyName} tiene un valor inválido.",
            ["PredicateValidator"] = "{PropertyName} es inválido.",
            ["AsyncPredicateValidator"] = "{PropertyName} es inválido.",
            ["RegularExpressionValidator"] = "{PropertyName} tiene un formato inválido.",
            ["ScalePrecisionValidator"] = "{PropertyName} tiene demasiados decimales.",
            ["CreditCardValidator"] = "{PropertyName} no es un número de tarjeta válido.",
        };

        foreach (var (key, message) in es)
        {
            AddTranslation("en", key, message);
            AddTranslation("en-US", key, message);
        }
    }
}
