using System;
using ComTest.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ComTest.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "promotions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    value = table.Column<decimal>(type: "numeric", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    min_subtotal = table.Column<decimal>(type: "numeric", nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    usage_limit = table.Column<int>(type: "integer", nullable: true),
                    usage_count = table.Column<int>(type: "integer", nullable: false),
                    per_customer_limit = table.Column<int>(type: "integer", nullable: true),
                    combines_with_others = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_promotions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "promotion_redemptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    promotion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: true),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_promotion_redemptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_promotion_redemptions_promotions_promotion_id",
                        column: x => x.promotion_id,
                        principalTable: "promotions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 11,
                column: "claim_value",
                value: "promotions.manage");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 12,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "reports.read", new Guid("0195b2a0-0000-7000-8000-000000000003") });

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 13,
                column: "claim_value",
                value: "platform.tenants.manage");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 14,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "audit.read", new Guid("0195b2a0-0000-7000-8000-000000000001") });

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 15,
                column: "claim_value",
                value: "catalog.read");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 16,
                column: "claim_value",
                value: "inventory.read");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 17,
                column: "claim_value",
                value: "inventory.write");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 18,
                column: "claim_value",
                value: "orders.read");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 19,
                column: "claim_value",
                value: "orders.write");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 20,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "customers.read", new Guid("0195b2a0-0000-7000-8000-000000000004") });

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 21,
                column: "claim_value",
                value: "catalog.read");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 22,
                column: "claim_value",
                value: "catalog.write");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 23,
                column: "claim_value",
                value: "inventory.read");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 24,
                column: "claim_value",
                value: "inventory.write");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 25,
                column: "claim_value",
                value: "orders.read");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 26,
                column: "claim_value",
                value: "orders.write");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 27,
                column: "claim_value",
                value: "orders.manage");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 28,
                column: "claim_value",
                value: "payments.confirm");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 29,
                column: "claim_value",
                value: "payments.refund.approve");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 30,
                column: "claim_value",
                value: "customers.read");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 31,
                column: "claim_value",
                value: "promotions.manage");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 32,
                column: "claim_value",
                value: "reports.read");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 33,
                column: "claim_value",
                value: "payments.refund");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 34,
                column: "claim_value",
                value: "reports.financial");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 35,
                column: "claim_value",
                value: "settings.manage");

            migrationBuilder.InsertData(
                table: "role_claims",
                columns: new[] { "id", "claim_type", "claim_value", "role_id" },
                values: new object[,]
                {
                    { 36, "permission", "staff.manage", new Guid("0195b2a0-0000-7000-8000-000000000002") },
                    { 37, "permission", "audit.read", new Guid("0195b2a0-0000-7000-8000-000000000002") }
                });

            migrationBuilder.CreateIndex(
                name: "ix_promotion_redemptions_promotion_id",
                table: "promotion_redemptions",
                column: "promotion_id");

            migrationBuilder.CreateIndex(
                name: "ix_promotion_redemptions_tenant_id_order_id",
                table: "promotion_redemptions",
                columns: new[] { "tenant_id", "order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_promotion_redemptions_tenant_id_promotion_id_customer_key_s",
                table: "promotion_redemptions",
                columns: new[] { "tenant_id", "promotion_id", "customer_key", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_promotions_tenant_id_code",
                table: "promotions",
                columns: new[] { "tenant_id", "code" },
                unique: true,
                filter: "code IS NOT NULL AND NOT is_deleted");

            migrationBuilder.EnableTenantRowLevelSecurity("promotions");
            migrationBuilder.EnableTenantRowLevelSecurity("promotion_redemptions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "promotion_redemptions");

            migrationBuilder.DropTable(
                name: "promotions");

            migrationBuilder.DeleteData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 37);

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 11,
                column: "claim_value",
                value: "reports.read");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 12,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "platform.tenants.manage", new Guid("0195b2a0-0000-7000-8000-000000000001") });

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 13,
                column: "claim_value",
                value: "audit.read");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 14,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "catalog.read", new Guid("0195b2a0-0000-7000-8000-000000000004") });

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 15,
                column: "claim_value",
                value: "inventory.read");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 16,
                column: "claim_value",
                value: "inventory.write");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 17,
                column: "claim_value",
                value: "orders.read");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 18,
                column: "claim_value",
                value: "orders.write");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 19,
                column: "claim_value",
                value: "customers.read");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 20,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "catalog.read", new Guid("0195b2a0-0000-7000-8000-000000000002") });

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 21,
                column: "claim_value",
                value: "catalog.write");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 22,
                column: "claim_value",
                value: "inventory.read");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 23,
                column: "claim_value",
                value: "inventory.write");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 24,
                column: "claim_value",
                value: "orders.read");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 25,
                column: "claim_value",
                value: "orders.write");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 26,
                column: "claim_value",
                value: "orders.manage");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 27,
                column: "claim_value",
                value: "payments.confirm");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 28,
                column: "claim_value",
                value: "payments.refund.approve");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 29,
                column: "claim_value",
                value: "customers.read");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 30,
                column: "claim_value",
                value: "reports.read");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 31,
                column: "claim_value",
                value: "payments.refund");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 32,
                column: "claim_value",
                value: "reports.financial");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 33,
                column: "claim_value",
                value: "settings.manage");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 34,
                column: "claim_value",
                value: "staff.manage");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 35,
                column: "claim_value",
                value: "audit.read");
        }
    }
}
