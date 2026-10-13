using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NotifyHub.Infrastructure.Persistence.Configurations;

internal static class ConfigurationExtensions
{
    /// <summary>Builds a CHECK constraint restricting a text column to the names of <typeparamref name="TEnum"/>.</summary>
    public static void HasEnumCheck<TEnum>(this TableBuilder table, string tableName, string column)
        where TEnum : struct, Enum
    {
        var values = string.Join(", ", Enum.GetNames<TEnum>().Select(n => $"'{n}'"));
        table.HasCheckConstraint($"ck_{tableName}_{column}", $"{column} IN ({values})");
    }

    /// <summary>
    /// Declares (tenant_id, id) as an alternate key so that dependents can reference it with a composite
    /// foreign key. This makes cross-tenant references impossible at the database level.
    /// </summary>
    public static void HasTenantScopedKey<TEntity>(this EntityTypeBuilder<TEntity> builder, string tableName)
        where TEntity : class
    {
        builder.HasAlternateKey("TenantId", "Id").HasName($"ak_{tableName}_tenant_id_id");
    }

    public static PropertyBuilder<T> IsJsonb<T>(this PropertyBuilder<T> property) => property.HasColumnType("jsonb");
}
