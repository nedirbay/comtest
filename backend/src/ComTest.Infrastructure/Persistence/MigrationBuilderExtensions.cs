using Microsoft.EntityFrameworkCore.Migrations;

namespace ComTest.Infrastructure.Persistence;

public static class MigrationBuilderExtensions
{
    /// <summary>The least-privilege role the application connects with. See scripts/db/create-roles.sql.</summary>
    public const string ApplicationRole = "comtest_app";

    /// <summary>
    /// Enables row-level security on a tenant table. FORCE also applies it to the table owner,
    /// so only a superuser or a role with BYPASSRLS can read across tenants.
    /// </summary>
    public static void EnableTenantRowLevelSecurity(this MigrationBuilder migrationBuilder, string table)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        const string Predicate =
            "tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid "
            + "OR current_setting('app.platform_access', true) = 'on'";

        migrationBuilder.Sql($"""
            ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;
            ALTER TABLE {table} FORCE ROW LEVEL SECURITY;
            CREATE POLICY tenant_isolation ON {table} USING ({Predicate}) WITH CHECK ({Predicate});
            """);
    }

    /// <summary>Makes a table append-only for the application role, for records that must not change.</summary>
    public static void MakeAppendOnlyForApplication(this MigrationBuilder migrationBuilder, string table)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.Sql($"REVOKE UPDATE, DELETE, TRUNCATE ON {table} FROM {ApplicationRole};");
    }

    /// <summary>Grants the application role data access only, here and on tables created later.</summary>
    public static void GrantApplicationAccess(this MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.Sql($"""
            GRANT USAGE ON SCHEMA public TO {ApplicationRole};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {ApplicationRole};
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {ApplicationRole};
            ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {ApplicationRole};
            ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO {ApplicationRole};
            REVOKE ALL ON TABLE "__EFMigrationsHistory" FROM {ApplicationRole};
            """);
    }
}
