using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Validation;

/// <summary>
/// Rejects <see cref="Guid.Empty"/> on a required identifier. <c>[Required]</c> alone does not:
/// a non-nullable Guid is never null, so an omitted id arrives as all-zeroes and would otherwise
/// reach the store as a lookup that 404s (or 500s) deeper in the stack.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class NotEmptyGuidAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            // Nullable ids are optional by definition; pair with [Required] when one is mandatory.
            return true;
        }

        if (value is Guid guid)
        {
            return guid != Guid.Empty;
        }

        return false;
    }

    public override string FormatErrorMessage(string name)
    {
        return $"The {name} field is required.";
    }
}
