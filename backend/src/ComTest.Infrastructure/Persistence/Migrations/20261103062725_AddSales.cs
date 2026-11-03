using System;
using ComTest.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ComTest.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "accepts_bank_transfer",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "accepts_cash_on_delivery",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "bank_transfer_instructions",
                table: "tenants",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "refund_approval_threshold",
                table: "tenants",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "carts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guest_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    last_activity_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_carts", x => x.id);
                    table.ForeignKey(
                        name: "fk_carts_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_addresses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    details_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    details_district = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    details_line1 = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    details_line2 = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    details_phone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    details_postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    details_recipient = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    details_region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("pk_customer_addresses", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_addresses_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    customer_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    shipping_method_id = table.Column<Guid>(type: "uuid", nullable: true),
                    shipping_method_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    shipping_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    payment_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    payment_state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    customer_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    idempotency_key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    placed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    carrier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tracking_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tracking_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    shipped_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    contact_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    contact_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contact_phone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    discount_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    grand_total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    grand_total_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    ship_to_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ship_to_district = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ship_to_line1 = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ship_to_line2 = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ship_to_phone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ship_to_postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ship_to_recipient = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ship_to_region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    shipping_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    shipping_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    subtotal_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    subtotal_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tax_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orders", x => x.id);
                    table.ForeignKey(
                        name: "fk_orders_users_customer_user_id",
                        column: x => x.customer_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "shipping_zones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    regions = table.Column<string>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("pk_shipping_zones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    on_hand_delta = table.Column<int>(type: "integer", nullable: false),
                    reserved_delta = table.Column<int>(type: "integer", nullable: false),
                    on_hand_after = table.Column<int>(type: "integer", nullable: false),
                    reserved_after = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_movements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "warehouses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_warehouses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cart_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cart_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    added_unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cart_lines", x => x.id);
                    table.CheckConstraint("ck_cart_lines_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_cart_lines_carts_cart_id",
                        column: x => x.cart_id,
                        principalTable: "carts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    product_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    options = table.Column<string>(type: "jsonb", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    line_total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    line_total_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    unit_price_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    unit_price_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_items", x => x.id);
                    table.CheckConstraint("ck_order_items_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_order_items_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_order_notes_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_status_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    to_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_status_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_order_status_changes_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    refunded_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    pending_refund_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    amount_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    amount_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.CheckConstraint("ck_payments_refunds", "refunded_amount >= 0 AND pending_refund_amount >= 0 AND refunded_amount + pending_refund_amount <= amount_amount");
                    table.ForeignKey(
                        name: "fk_payments_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shipping_methods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    zone_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    names = table.Column<string>(type: "jsonb", nullable: false),
                    free_over_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    min_days = table.Column<int>(type: "integer", nullable: true),
                    max_days = table.Column<int>(type: "integer", nullable: true),
                    pickup_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    price_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    price_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
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
                    table.PrimaryKey("pk_shipping_methods", x => x.id);
                    table.ForeignKey(
                        name: "fk_shipping_methods_shipping_zones_zone_id",
                        column: x => x.zone_id,
                        principalTable: "shipping_zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    on_hand = table.Column<int>(type: "integer", nullable: false),
                    reserved = table.Column<int>(type: "integer", nullable: false),
                    low_stock_threshold = table.Column<int>(type: "integer", nullable: false),
                    low_stock_notified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_items", x => x.id);
                    table.CheckConstraint("ck_stock_items_quantities", "on_hand >= 0 AND reserved >= 0 AND reserved <= on_hand");
                    table.ForeignKey(
                        name: "fk_stock_items_product_variants_product_variant_id",
                        column: x => x.product_variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_items_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_payment_events_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refunds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    amount_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    amount_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refunds", x => x.id);
                    table.ForeignKey(
                        name: "fk_refunds_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_reservations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_reservations", x => x.id);
                    table.CheckConstraint("ck_stock_reservations_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_stock_reservations_stock_items_stock_item_id",
                        column: x => x.stock_item_id,
                        principalTable: "stock_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 7,
                column: "claim_value",
                value: "orders.manage");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 8,
                column: "claim_value",
                value: "payments.confirm");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 9,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "payments.refund.approve", new Guid("0195b2a0-0000-7000-8000-000000000003") });

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 10,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "customers.read", new Guid("0195b2a0-0000-7000-8000-000000000003") });

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 11,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "reports.read", new Guid("0195b2a0-0000-7000-8000-000000000003") });

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
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "audit.read", new Guid("0195b2a0-0000-7000-8000-000000000001") });

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 14,
                column: "claim_value",
                value: "catalog.read");

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
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "orders.read", new Guid("0195b2a0-0000-7000-8000-000000000004") });

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 18,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "orders.write", new Guid("0195b2a0-0000-7000-8000-000000000004") });

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 19,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "customers.read", new Guid("0195b2a0-0000-7000-8000-000000000004") });

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 20,
                column: "claim_value",
                value: "catalog.read");

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

            migrationBuilder.InsertData(
                table: "role_claims",
                columns: new[] { "id", "claim_type", "claim_value", "role_id" },
                values: new object[,]
                {
                    { 30, "permission", "reports.read", new Guid("0195b2a0-0000-7000-8000-000000000002") },
                    { 31, "permission", "payments.refund", new Guid("0195b2a0-0000-7000-8000-000000000002") },
                    { 32, "permission", "reports.financial", new Guid("0195b2a0-0000-7000-8000-000000000002") },
                    { 33, "permission", "settings.manage", new Guid("0195b2a0-0000-7000-8000-000000000002") },
                    { 34, "permission", "staff.manage", new Guid("0195b2a0-0000-7000-8000-000000000002") },
                    { 35, "permission", "audit.read", new Guid("0195b2a0-0000-7000-8000-000000000002") }
                });

            migrationBuilder.CreateIndex(
                name: "ix_cart_lines_cart_id_product_variant_id",
                table: "cart_lines",
                columns: new[] { "cart_id", "product_variant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_carts_last_activity_at",
                table: "carts",
                column: "last_activity_at");

            migrationBuilder.CreateIndex(
                name: "ix_carts_tenant_id_guest_token_hash",
                table: "carts",
                columns: new[] { "tenant_id", "guest_token_hash" },
                unique: true,
                filter: "guest_token_hash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_carts_tenant_id_user_id",
                table: "carts",
                columns: new[] { "tenant_id", "user_id" },
                unique: true,
                filter: "user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_carts_user_id",
                table: "carts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_addresses_tenant_id_user_id",
                table: "customer_addresses",
                columns: new[] { "tenant_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_addresses_user_id",
                table: "customer_addresses",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_items_order_id",
                table: "order_items",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_notes_order_id",
                table: "order_notes",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_status_changes_order_id",
                table: "order_status_changes",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_customer_user_id",
                table: "orders",
                column: "customer_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_status_expires_at",
                table: "orders",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_orders_tenant_id_customer_user_id_placed_at",
                table: "orders",
                columns: new[] { "tenant_id", "customer_user_id", "placed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_orders_tenant_id_idempotency_key_hash",
                table: "orders",
                columns: new[] { "tenant_id", "idempotency_key_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_tenant_id_number",
                table: "orders",
                columns: new[] { "tenant_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_tenant_id_status_placed_at",
                table: "orders",
                columns: new[] { "tenant_id", "status", "placed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_events_payment_id",
                table: "payment_events",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_order_id",
                table: "payments",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_tenant_id_order_id",
                table: "payments",
                columns: new[] { "tenant_id", "order_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refunds_payment_id",
                table: "refunds",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_shipping_methods_tenant_id",
                table: "shipping_methods",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_shipping_methods_zone_id",
                table: "shipping_methods",
                column: "zone_id");

            migrationBuilder.CreateIndex(
                name: "ix_shipping_zones_tenant_id",
                table: "shipping_zones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_items_product_variant_id",
                table: "stock_items",
                column: "product_variant_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_items_tenant_id_product_variant_id",
                table: "stock_items",
                columns: new[] { "tenant_id", "product_variant_id" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_items_warehouse_id_product_variant_id",
                table: "stock_items",
                columns: new[] { "warehouse_id", "product_variant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_reference_id",
                table: "stock_movements",
                column: "reference_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_tenant_id_product_variant_id_created_at",
                table: "stock_movements",
                columns: new[] { "tenant_id", "product_variant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservations_reference_id_status",
                table: "stock_reservations",
                columns: new[] { "reference_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservations_stock_item_id",
                table: "stock_reservations",
                column: "stock_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_warehouses_tenant_id_code",
                table: "warehouses",
                columns: new[] { "tenant_id", "code" },
                unique: true,
                filter: "NOT is_deleted");

            migrationBuilder.EnableTenantRowLevelSecurity("warehouses");
            migrationBuilder.EnableTenantRowLevelSecurity("stock_items");
            migrationBuilder.EnableTenantRowLevelSecurity("stock_movements");
            migrationBuilder.EnableTenantRowLevelSecurity("stock_reservations");
            migrationBuilder.EnableTenantRowLevelSecurity("customer_addresses");
            migrationBuilder.EnableTenantRowLevelSecurity("shipping_zones");
            migrationBuilder.EnableTenantRowLevelSecurity("shipping_methods");
            migrationBuilder.EnableTenantRowLevelSecurity("payments");
            migrationBuilder.EnableTenantRowLevelSecurity("refunds");
            migrationBuilder.EnableTenantRowLevelSecurity("payment_events");
            migrationBuilder.EnableTenantRowLevelSecurity("carts");
            migrationBuilder.EnableTenantRowLevelSecurity("cart_lines");
            migrationBuilder.EnableTenantRowLevelSecurity("orders");
            migrationBuilder.EnableTenantRowLevelSecurity("order_items");
            migrationBuilder.EnableTenantRowLevelSecurity("order_status_changes");
            migrationBuilder.EnableTenantRowLevelSecurity("order_notes");
            migrationBuilder.MakeAppendOnlyForApplication("stock_movements");
            migrationBuilder.MakeAppendOnlyForApplication("order_status_changes");
            migrationBuilder.MakeAppendOnlyForApplication("payment_events");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cart_lines");

            migrationBuilder.DropTable(
                name: "customer_addresses");

            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "order_notes");

            migrationBuilder.DropTable(
                name: "order_status_changes");

            migrationBuilder.DropTable(
                name: "payment_events");

            migrationBuilder.DropTable(
                name: "refunds");

            migrationBuilder.DropTable(
                name: "shipping_methods");

            migrationBuilder.DropTable(
                name: "stock_movements");

            migrationBuilder.DropTable(
                name: "stock_reservations");

            migrationBuilder.DropTable(
                name: "carts");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "shipping_zones");

            migrationBuilder.DropTable(
                name: "stock_items");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "warehouses");

            migrationBuilder.DeleteData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 35);

            migrationBuilder.DropColumn(
                name: "accepts_bank_transfer",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "accepts_cash_on_delivery",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "bank_transfer_instructions",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "refund_approval_threshold",
                table: "tenants");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 7,
                column: "claim_value",
                value: "customers.read");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 8,
                column: "claim_value",
                value: "reports.read");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 9,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "platform.tenants.manage", new Guid("0195b2a0-0000-7000-8000-000000000001") });

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 10,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "audit.read", new Guid("0195b2a0-0000-7000-8000-000000000001") });

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 11,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "catalog.read", new Guid("0195b2a0-0000-7000-8000-000000000004") });

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 12,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "inventory.read", new Guid("0195b2a0-0000-7000-8000-000000000004") });

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 13,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "inventory.write", new Guid("0195b2a0-0000-7000-8000-000000000004") });

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 14,
                column: "claim_value",
                value: "orders.read");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 15,
                column: "claim_value",
                value: "orders.write");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 16,
                column: "claim_value",
                value: "customers.read");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 17,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "catalog.read", new Guid("0195b2a0-0000-7000-8000-000000000002") });

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 18,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "catalog.write", new Guid("0195b2a0-0000-7000-8000-000000000002") });

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 19,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "inventory.read", new Guid("0195b2a0-0000-7000-8000-000000000002") });

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 20,
                column: "claim_value",
                value: "inventory.write");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 21,
                column: "claim_value",
                value: "orders.read");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 22,
                column: "claim_value",
                value: "orders.write");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 23,
                column: "claim_value",
                value: "customers.read");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 24,
                column: "claim_value",
                value: "reports.read");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 25,
                column: "claim_value",
                value: "payments.refund");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 26,
                column: "claim_value",
                value: "reports.financial");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 27,
                column: "claim_value",
                value: "settings.manage");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 28,
                column: "claim_value",
                value: "staff.manage");

            migrationBuilder.UpdateData(
                table: "role_claims",
                keyColumn: "id",
                keyValue: 29,
                column: "claim_value",
                value: "audit.read");
        }
    }
}
