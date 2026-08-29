using System.ComponentModel;
using System.Reflection;

namespace Cinema.Business.Extensions;

public static class EntityExtensions
{
    /// <summary>Maps matching public properties from entity to a new DTO instance.</summary>
    public static TDTO ToDTO<TEntity, TDTO>(this TEntity entity) where TDTO : new()
    {
        var dto = new TDTO();
        var entityProps = typeof(TEntity).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var dtoProps = typeof(TDTO).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var ep in entityProps)
        {
            if (!dtoProps.TryGetValue(ep.Name, out var dp))
            {
                continue;
            }
            if (!dp.PropertyType.IsAssignableFrom(ep.PropertyType))
            {
                continue;
            }
            dp.SetValue(dto, ep.GetValue(entity));
        }
        return dto;
    }

    /// <summary>Patches matching non-null public properties from DTO onto an existing entity.</summary>
    public static void PatchEntity<TEntity, TDTO>(this TEntity entity, TDTO dto)
    {
        var dtoProps = typeof(TDTO).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead);
        var entityProps = typeof(TEntity).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var dp in dtoProps)
        {
            if (!entityProps.TryGetValue(dp.Name, out var ep))
            {
                continue;
            }
            var value = dp.GetValue(dto);
            if (value == null)
            {
                continue;
            }
            if (!ep.PropertyType.IsAssignableFrom(dp.PropertyType))
            {
                continue;
            }
            ep.SetValue(entity, value);
        }
    }

    /// <summary>Creates a new entity instance and copies matching properties from the DTO.</summary>
    public static TEntity ToNewEntity<TDTO, TEntity>(this TDTO dto) where TEntity : new()
    {
        var entity = new TEntity();
        entity.PatchEntity<TEntity, TDTO>(dto);
        return entity;
    }

    /// <summary>Returns the [Description] attribute value of an enum member, or its name.</summary>
    public static string ToDescriptionString<TEnum>(this TEnum value) where TEnum : Enum
    {
        var fi = value.GetType().GetField(value.ToString());
        var attr = fi?.GetCustomAttribute<DescriptionAttribute>();
        return attr?.Description ?? value.ToString();
    }

    /// <summary>Finds the enum member whose [Description] matches the given string.</summary>
    public static T GetValueFromDescription<T>(string description) where T : Enum
    {
        foreach (var field in typeof(T).GetFields())
        {
            var attr = field.GetCustomAttribute<DescriptionAttribute>();
            if (attr?.Description == description || field.Name == description)
            {
                return (T)field.GetValue(null)!;
            }
        }
        throw new ArgumentException($"'{description}' is not a valid description for {typeof(T)}.");
    }

    /// <summary>Creates an empty (default-constructed) instance of the entity.</summary>
    public static TEntity CreateEmpty<TEntity>() where TEntity : new()
    {
        return new();
    }

    /// <summary>Sets all nullable reference and value-type properties to null/default.</summary>
    public static TEntity EmptyNullables<TEntity>(this TEntity entity)
    {
        foreach (var prop in typeof(TEntity).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite))
        {
            var type = prop.PropertyType;
            bool isNullable = !type.IsValueType ||
                              (Nullable.GetUnderlyingType(type) != null);
            if (isNullable)
            {
                prop.SetValue(entity, null);
            }
        }
        return entity;
    }
}
