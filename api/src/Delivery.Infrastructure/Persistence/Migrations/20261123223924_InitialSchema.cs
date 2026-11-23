using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Delivery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "banks",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    deletedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_banks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bulk_jobs",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    requestedById = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    target = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    input = table.Column<string>(type: "jsonb", nullable: false),
                    scope = table.Column<string>(type: "jsonb", nullable: false),
                    actor = table.Column<string>(type: "jsonb", nullable: false),
                    total = table.Column<int>(type: "integer", nullable: false),
                    processed = table.Column<int>(type: "integer", nullable: false),
                    doneIds = table.Column<string>(type: "jsonb", nullable: false),
                    failures = table.Column<string>(type: "jsonb", nullable: false),
                    result = table.Column<string>(type: "jsonb", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    startedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    finishedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bulk_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "claim_types",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    subjectType = table.Column<string>(type: "text", nullable: false),
                    sortOrder = table.Column<int>(type: "integer", nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    deletedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_claim_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    customDomain = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    shortName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    legalName = table.Column<string>(type: "text", nullable: true),
                    taxId = table.Column<string>(type: "text", nullable: true),
                    rc = table.Column<string>(type: "text", nullable: true),
                    proLegalName = table.Column<string>(type: "text", nullable: true),
                    proTaxId = table.Column<string>(type: "text", nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    website = table.Column<string>(type: "text", nullable: true),
                    country = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    timezone = table.Column<string>(type: "text", nullable: false),
                    defaultLocale = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    locales = table.Column<List<string>>(type: "text[]", nullable: false),
                    logoUrl = table.Column<string>(type: "text", nullable: true),
                    logoBackground = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    iconUrl = table.Column<string>(type: "text", nullable: true),
                    primaryColor = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    secondaryColor = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    theme = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    trialEndsAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    plan = table.Column<string>(type: "text", nullable: true),
                    lastActivityAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "delivery_types",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    multiplier = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    isDefault = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    deletedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_sequences",
                columns: table => new
                {
                    kind = table.Column<string>(type: "text", nullable: false),
                    period = table.Column<string>(type: "text", nullable: false),
                    lastRank = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_sequences", x => new { x.kind, x.period });
                });

            migrationBuilder.CreateTable(
                name: "idempotent_requests",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    userId = table.Column<string>(type: "text", nullable: false),
                    key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    response = table.Column<string>(type: "jsonb", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotent_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "label_jobs",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    requestedById = table.Column<string>(type: "text", nullable: false),
                    format = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    parcelCount = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    storageKey = table.Column<string>(type: "text", nullable: true),
                    sizeBytes = table.Column<int>(type: "integer", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    startedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    finishedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    expiresAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_label_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "parcel_code_sequences",
                columns: table => new
                {
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    originCityId = table.Column<string>(type: "text", nullable: false),
                    destinationCityId = table.Column<string>(type: "text", nullable: false),
                    lastRank = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parcel_code_sequences", x => new { x.day, x.originCityId, x.destinationCityId });
                });

            migrationBuilder.CreateTable(
                name: "parcel_statuses",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    group = table.Column<string>(type: "text", nullable: false),
                    color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    icon = table.Column<string>(type: "text", nullable: false),
                    sortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parcel_statuses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_attributes",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_attributes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "refresh_sessions",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    userId = table.Column<string>(type: "text", nullable: false),
                    familyId = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    issuedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    accessExpiresAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    expiresAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    rotatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    replacedById = table.Column<string>(type: "text", nullable: true),
                    revokedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "server_errors",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    at = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    requestId = table.Column<string>(type: "text", nullable: true),
                    method = table.Column<string>(type: "text", nullable: true),
                    route = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "text", nullable: true),
                    message = table.Column<string>(type: "text", nullable: false),
                    stack = table.Column<string>(type: "text", nullable: true),
                    durationMs = table.Column<int>(type: "integer", nullable: true),
                    userId = table.Column<string>(type: "text", nullable: true),
                    role = table.Column<string>(type: "text", nullable: true),
                    tenantId = table.Column<string>(type: "text", nullable: true),
                    ip = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_server_errors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "settings",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: false),
                    agencyId = table.Column<string>(type: "text", nullable: true),
                    key = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "jsonb", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "zones",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    baseRate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    sameCityRate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    deliveryDays = table.Column<int>(type: "integer", nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    deletedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bank_accounts",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    bankId = table.Column<string>(type: "text", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    rib = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    iban = table.Column<string>(type: "text", nullable: true),
                    holder = table.Column<string>(type: "text", nullable: true),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    deletedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_bank_accounts_banks_bankId",
                        column: x => x.bankId,
                        principalTable: "banks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "parcel_transitions",
                columns: table => new
                {
                    fromStatusId = table.Column<int>(type: "integer", nullable: false),
                    toStatusId = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parcel_transitions", x => new { x.fromStatusId, x.toStatusId, x.kind });
                    table.ForeignKey(
                        name: "FK_parcel_transitions_parcel_statuses_fromStatusId",
                        column: x => x.fromStatusId,
                        principalTable: "parcel_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_parcel_transitions_parcel_statuses_toStatusId",
                        column: x => x.toStatusId,
                        principalTable: "parcel_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    role = table.Column<string>(type: "text", nullable: false),
                    permissionId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => new { x.role, x.permissionId });
                    table.ForeignKey(
                        name: "FK_role_permissions_permissions_permissionId",
                        column: x => x.permissionId,
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_attribute_values",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    attributeId = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_attribute_values", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_attribute_values_product_attributes_attributeId",
                        column: x => x.attributeId,
                        principalTable: "product_attributes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cities",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    zoneId = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    nameAr = table.Column<string>(type: "text", nullable: true),
                    deliveryDelay = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    deletedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cities", x => x.id);
                    table.ForeignKey(
                        name: "FK_cities_zones_zoneId",
                        column: x => x.zoneId,
                        principalTable: "zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "zone_rates",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    fromZoneId = table.Column<string>(type: "text", nullable: false),
                    toZoneId = table.Column<string>(type: "text", nullable: false),
                    rate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    deliveryDays = table.Column<int>(type: "integer", nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zone_rates", x => x.id);
                    table.ForeignKey(
                        name: "FK_zone_rates_zones_fromZoneId",
                        column: x => x.fromZoneId,
                        principalTable: "zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_zone_rates_zones_toZoneId",
                        column: x => x.toZoneId,
                        principalTable: "zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agencies",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    cityId = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    logoUrl = table.Column<string>(type: "text", nullable: true),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    timezone = table.Column<string>(type: "text", nullable: true),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    deletedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agencies", x => x.id);
                    table.ForeignKey(
                        name: "FK_agencies_cities_cityId",
                        column: x => x.cityId,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "districts",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    cityId = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    deletedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_districts", x => x.id);
                    table.ForeignKey(
                        name: "FK_districts_cities_cityId",
                        column: x => x.cityId,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pricing_rules",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    originCityId = table.Column<string>(type: "text", nullable: false),
                    destinationCityId = table.Column<string>(type: "text", nullable: false),
                    rateDistrict = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    rateAgency = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    rateRelayPoint = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    isEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    deletedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_pricing_rules_cities_destinationCityId",
                        column: x => x.destinationCityId,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pricing_rules_cities_originCityId",
                        column: x => x.originCityId,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "zone_cities",
                columns: table => new
                {
                    zoneId = table.Column<string>(type: "text", nullable: false),
                    cityId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zone_cities", x => new { x.zoneId, x.cityId });
                    table.ForeignKey(
                        name: "FK_zone_cities_cities_cityId",
                        column: x => x.cityId,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_zone_cities_zones_zoneId",
                        column: x => x.zoneId,
                        principalTable: "zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agency_cities",
                columns: table => new
                {
                    agencyId = table.Column<string>(type: "text", nullable: false),
                    cityId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agency_cities", x => new { x.agencyId, x.cityId });
                    table.ForeignKey(
                        name: "FK_agency_cities_agencies_agencyId",
                        column: x => x.agencyId,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_agency_cities_cities_cityId",
                        column: x => x.cityId,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    isPlatformAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    agencyId = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    username = table.Column<string>(type: "text", nullable: true),
                    passwordHash = table.Column<string>(type: "text", nullable: false),
                    firstName = table.Column<string>(type: "text", nullable: false),
                    lastName = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: true),
                    avatarUrl = table.Column<string>(type: "text", nullable: true),
                    jobTitle = table.Column<string>(type: "text", nullable: true),
                    nationalId = table.Column<string>(type: "text", nullable: true),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    tokensValidFrom = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    mustChangePassword = table.Column<bool>(type: "boolean", nullable: false),
                    invitedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    invitedById = table.Column<string>(type: "text", nullable: true),
                    lastLoginAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    deletedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_users_agencies_agencyId",
                        column: x => x.agencyId,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_users_users_invitedById",
                        column: x => x.invitedById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "announcements",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    intro = table.Column<string>(type: "text", nullable: true),
                    body = table.Column<string>(type: "text", nullable: false),
                    audience = table.Column<string>(type: "text", nullable: false),
                    imageKey = table.Column<string>(type: "text", nullable: true),
                    imageName = table.Column<string>(type: "text", nullable: true),
                    isPublished = table.Column<bool>(type: "boolean", nullable: false),
                    publishedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    createdById = table.Column<string>(type: "text", nullable: true),
                    authorName = table.Column<string>(type: "text", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    deletedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_announcements", x => x.id);
                    table.ForeignKey(
                        name: "FK_announcements_users_createdById",
                        column: x => x.createdById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    entity = table.Column<string>(type: "text", nullable: false),
                    entityId = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    before = table.Column<string>(type: "jsonb", nullable: true),
                    after = table.Column<string>(type: "jsonb", nullable: true),
                    diff = table.Column<List<string>>(type: "text[]", nullable: false),
                    subjectClientId = table.Column<string>(type: "text", nullable: true),
                    userId = table.Column<string>(type: "text", nullable: true),
                    userEmail = table.Column<string>(type: "text", nullable: true),
                    role = table.Column<string>(type: "text", nullable: true),
                    ip = table.Column<string>(type: "text", nullable: true),
                    userAgent = table.Column<string>(type: "text", nullable: true),
                    requestId = table.Column<string>(type: "text", nullable: true),
                    source = table.Column<string>(type: "text", nullable: true),
                    at = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_logs_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    agencyId = table.Column<string>(type: "text", nullable: true),
                    userId = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    legalName = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "text", nullable: false),
                    taxId = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    cityId = table.Column<string>(type: "text", nullable: false),
                    address = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    isPro = table.Column<bool>(type: "boolean", nullable: false),
                    billingType = table.Column<string>(type: "text", nullable: false),
                    monthlyBilling = table.Column<bool>(type: "boolean", nullable: false),
                    vatRate = table.Column<int>(type: "integer", nullable: true),
                    pickupFee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    freePickupThreshold = table.Column<int>(type: "integer", nullable: true),
                    validatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    validatedById = table.Column<string>(type: "text", nullable: true),
                    rejectedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    rejectedById = table.Column<string>(type: "text", nullable: true),
                    rejectionReason = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    deletedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.id);
                    table.ForeignKey(
                        name: "FK_clients_agencies_agencyId",
                        column: x => x.agencyId,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_clients_cities_cityId",
                        column: x => x.cityId,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_clients_users_rejectedById",
                        column: x => x.rejectedById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_clients_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_clients_users_validatedById",
                        column: x => x.validatedById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "couriers",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    userId = table.Column<string>(type: "text", nullable: true),
                    firstName = table.Column<string>(type: "text", nullable: false),
                    lastName = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: false),
                    cityId = table.Column<string>(type: "text", nullable: false),
                    agencyId = table.Column<string>(type: "text", nullable: true),
                    nationalId = table.Column<string>(type: "text", nullable: true),
                    vehicleType = table.Column<string>(type: "text", nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    bankName = table.Column<string>(type: "text", nullable: true),
                    rib = table.Column<string>(type: "text", nullable: true),
                    payModel = table.Column<string>(type: "text", nullable: false),
                    settlementMode = table.Column<string>(type: "text", nullable: false),
                    payPeriod = table.Column<string>(type: "text", nullable: false),
                    fixedSalary = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    validatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    validatedById = table.Column<string>(type: "text", nullable: true),
                    rejectedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    rejectedById = table.Column<string>(type: "text", nullable: true),
                    rejectionReason = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    deletedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_couriers", x => x.id);
                    table.ForeignKey(
                        name: "FK_couriers_agencies_agencyId",
                        column: x => x.agencyId,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_couriers_cities_cityId",
                        column: x => x.cityId,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_couriers_users_rejectedById",
                        column: x => x.rejectedById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_couriers_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_couriers_users_validatedById",
                        column: x => x.validatedById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "field_changes",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    entity = table.Column<string>(type: "text", nullable: false),
                    entityId = table.Column<string>(type: "text", nullable: false),
                    field = table.Column<string>(type: "text", nullable: false),
                    oldValue = table.Column<string>(type: "text", nullable: true),
                    newValue = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    authorId = table.Column<string>(type: "text", nullable: true),
                    authorName = table.Column<string>(type: "text", nullable: false),
                    at = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_field_changes", x => x.id);
                    table.ForeignKey(
                        name: "FK_field_changes_users_authorId",
                        column: x => x.authorId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    userId = table.Column<string>(type: "text", nullable: false),
                    topic = table.Column<string>(type: "text", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_preferences", x => new { x.userId, x.topic });
                    table.ForeignKey(
                        name: "FK_notification_preferences_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    userId = table.Column<string>(type: "text", nullable: false),
                    topic = table.Column<string>(type: "text", nullable: false),
                    @params = table.Column<string>(name: "params", type: "jsonb", nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false),
                    readAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_notifications_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_requests",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    userId = table.Column<string>(type: "text", nullable: true),
                    ip = table.Column<string>(type: "text", nullable: true),
                    userAgent = table.Column<string>(type: "text", nullable: true),
                    mailProvider = table.Column<string>(type: "text", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_password_reset_requests_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "petty_cash_days",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    agencyId = table.Column<string>(type: "text", nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    openingBalance = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    supplied = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    spent = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    closingBalance = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    pendingDrafts = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    closedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    closedById = table.Column<string>(type: "text", nullable: true),
                    closedAutomatically = table.Column<bool>(type: "boolean", nullable: false),
                    reopenedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    reopenedById = table.Column<string>(type: "text", nullable: true),
                    reopenReason = table.Column<string>(type: "text", nullable: true),
                    reopenCount = table.Column<int>(type: "integer", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_petty_cash_days", x => x.id);
                    table.ForeignKey(
                        name: "FK_petty_cash_days_agencies_agencyId",
                        column: x => x.agencyId,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_petty_cash_days_users_closedById",
                        column: x => x.closedById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_petty_cash_days_users_reopenedById",
                        column: x => x.reopenedById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "petty_cash_movements",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    agencyId = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    occurredAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    label = table.Column<string>(type: "text", nullable: true),
                    category = table.Column<string>(type: "text", nullable: true),
                    buyerId = table.Column<string>(type: "text", nullable: true),
                    buyerName = table.Column<string>(type: "text", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    proofName = table.Column<string>(type: "text", nullable: true),
                    proofMimeType = table.Column<string>(type: "text", nullable: true),
                    proofPath = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    refusalReason = table.Column<string>(type: "text", nullable: true),
                    decidedById = table.Column<string>(type: "text", nullable: true),
                    decidedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    createdById = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_petty_cash_movements", x => x.id);
                    table.ForeignKey(
                        name: "FK_petty_cash_movements_agencies_agencyId",
                        column: x => x.agencyId,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_petty_cash_movements_users_buyerId",
                        column: x => x.buyerId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_petty_cash_movements_users_createdById",
                        column: x => x.createdById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_petty_cash_movements_users_decidedById",
                        column: x => x.decidedById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "platform_audit_logs",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    actorId = table.Column<string>(type: "text", nullable: true),
                    actorName = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    targetCompanyId = table.Column<string>(type: "text", nullable: true),
                    targetCompanyName = table.Column<string>(type: "text", nullable: false),
                    details = table.Column<string>(type: "jsonb", nullable: true),
                    ip = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_platform_audit_logs_users_actorId",
                        column: x => x.actorId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "push_tokens",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    userId = table.Column<string>(type: "text", nullable: false),
                    token = table.Column<string>(type: "text", nullable: false),
                    platform = table.Column<string>(type: "text", nullable: false),
                    locale = table.Column<string>(type: "text", nullable: false),
                    appVersion = table.Column<string>(type: "text", nullable: true),
                    lastSeenAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_push_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_push_tokens_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transfer_orders",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    agencyId = table.Column<string>(type: "text", nullable: true),
                    lines = table.Column<string>(type: "jsonb", nullable: false),
                    invoiceCodes = table.Column<List<string>>(type: "text[]", nullable: false),
                    totalCollected = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    totalFees = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    totalDue = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    createdById = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transfer_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_transfer_orders_agencies_agencyId",
                        column: x => x.agencyId,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transfer_orders_users_createdById",
                        column: x => x.createdById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user_agencies",
                columns: table => new
                {
                    userId = table.Column<string>(type: "text", nullable: false),
                    agencyId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_agencies", x => new { x.userId, x.agencyId });
                    table.ForeignKey(
                        name: "FK_user_agencies_agencies_agencyId",
                        column: x => x.agencyId,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_agencies_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_permission_overrides",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    userId = table.Column<string>(type: "text", nullable: false),
                    permissionId = table.Column<string>(type: "text", nullable: false),
                    granted = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    createdById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_permission_overrides", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_permission_overrides_permissions_permissionId",
                        column: x => x.permissionId,
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_permission_overrides_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    userId = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.userId, x.role });
                    table.ForeignKey(
                        name: "FK_user_roles_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "client_discounts",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    clientId = table.Column<string>(type: "text", nullable: false),
                    zoneIds = table.Column<List<string>>(type: "text[]", nullable: false),
                    flatRate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    sameCityRate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    pickupFee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_discounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_client_discounts_clients_clientId",
                        column: x => x.clientId,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "client_documents",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    clientId = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    storageKey = table.Column<string>(type: "text", nullable: false),
                    mimeType = table.Column<string>(type: "text", nullable: false),
                    sizeBytes = table.Column<int>(type: "integer", nullable: false),
                    uploadedById = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    deletedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_client_documents_clients_clientId",
                        column: x => x.clientId,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_client_documents_users_uploadedById",
                        column: x => x.uploadedById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "client_pickup_addresses",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    clientId = table.Column<string>(type: "text", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    address = table.Column<string>(type: "text", nullable: false),
                    cityId = table.Column<string>(type: "text", nullable: false),
                    districtId = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    isDefault = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    deletedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_pickup_addresses", x => x.id);
                    table.ForeignKey(
                        name: "FK_client_pickup_addresses_cities_cityId",
                        column: x => x.cityId,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_client_pickup_addresses_clients_clientId",
                        column: x => x.clientId,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_client_pickup_addresses_districts_districtId",
                        column: x => x.districtId,
                        principalTable: "districts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cod_payouts",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    clientId = table.Column<string>(type: "text", nullable: false),
                    agencyId = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    method = table.Column<string>(type: "text", nullable: false),
                    bankAccountId = table.Column<string>(type: "text", nullable: true),
                    reference = table.Column<string>(type: "text", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    proofName = table.Column<string>(type: "text", nullable: true),
                    proofMimeType = table.Column<string>(type: "text", nullable: true),
                    proofPath = table.Column<string>(type: "text", nullable: true),
                    paidAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    paidById = table.Column<string>(type: "text", nullable: true),
                    createdById = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cod_payouts", x => x.id);
                    table.ForeignKey(
                        name: "FK_cod_payouts_agencies_agencyId",
                        column: x => x.agencyId,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cod_payouts_bank_accounts_bankAccountId",
                        column: x => x.bankAccountId,
                        principalTable: "bank_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cod_payouts_clients_clientId",
                        column: x => x.clientId,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cod_payouts_users_paidById",
                        column: x => x.paidById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "delivery_notes",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    clientId = table.Column<string>(type: "text", nullable: false),
                    agencyId = table.Column<string>(type: "text", nullable: false),
                    locationCityId = table.Column<string>(type: "text", nullable: false),
                    validatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    validatedBy = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    pickedUpAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    pickedUpBy = table.Column<string>(type: "text", nullable: true),
                    pickupFeeApplied = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    pickupThresholdApplied = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_notes", x => x.id);
                    table.ForeignKey(
                        name: "FK_delivery_notes_agencies_agencyId",
                        column: x => x.agencyId,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_delivery_notes_cities_locationCityId",
                        column: x => x.locationCityId,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_delivery_notes_clients_clientId",
                        column: x => x.clientId,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    clientId = table.Column<string>(type: "text", nullable: false),
                    agencyId = table.Column<string>(type: "text", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    totalCod = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    totalDeliveryFee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    totalFailureFee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    totalVat = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    totalAdjustment = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    totalDue = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    vatRate = table.Column<int>(type: "integer", nullable: false),
                    proVatRate = table.Column<int>(type: "integer", nullable: true),
                    billingType = table.Column<string>(type: "text", nullable: false),
                    periodStart = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    periodEnd = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    issuedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    issuedById = table.Column<string>(type: "text", nullable: true),
                    dueAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    paidAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    paidById = table.Column<string>(type: "text", nullable: true),
                    createdById = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoices_agencies_agencyId",
                        column: x => x.agencyId,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoices_clients_clientId",
                        column: x => x.clientId,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoices_users_issuedById",
                        column: x => x.issuedById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_invoices_users_paidById",
                        column: x => x.paidById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "parcel_imports",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    clientId = table.Column<string>(type: "text", nullable: false),
                    filename = table.Column<string>(type: "text", nullable: false),
                    storageKey = table.Column<string>(type: "text", nullable: true),
                    mode = table.Column<string>(type: "text", nullable: false),
                    rowCount = table.Column<int>(type: "integer", nullable: false),
                    okCount = table.Column<int>(type: "integer", nullable: false),
                    koCount = table.Column<int>(type: "integer", nullable: false),
                    errors = table.Column<string>(type: "jsonb", nullable: false),
                    createdById = table.Column<string>(type: "text", nullable: true),
                    authorName = table.Column<string>(type: "text", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parcel_imports", x => x.id);
                    table.ForeignKey(
                        name: "FK_parcel_imports_clients_clientId",
                        column: x => x.clientId,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parcel_imports_users_createdById",
                        column: x => x.createdById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    clientId = table.Column<string>(type: "text", nullable: false),
                    reference = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    lengthMm = table.Column<int>(type: "integer", nullable: true),
                    widthMm = table.Column<int>(type: "integer", nullable: true),
                    heightMm = table.Column<int>(type: "integer", nullable: true),
                    weightG = table.Column<int>(type: "integer", nullable: true),
                    weight = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    length = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    width = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    height = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    value = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    imageUrl = table.Column<string>(type: "text", nullable: true),
                    imageKey = table.Column<string>(type: "text", nullable: true),
                    internalStock = table.Column<int>(type: "integer", nullable: true),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    deletedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                    table.ForeignKey(
                        name: "FK_products_clients_clientId",
                        column: x => x.clientId,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "return_slips",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    clientId = table.Column<string>(type: "text", nullable: false),
                    agencyId = table.Column<string>(type: "text", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    deliveredAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    receivedByName = table.Column<string>(type: "text", nullable: true),
                    deliveredById = table.Column<string>(type: "text", nullable: true),
                    proofName = table.Column<string>(type: "text", nullable: true),
                    proofMimeType = table.Column<string>(type: "text", nullable: true),
                    proofPath = table.Column<string>(type: "text", nullable: true),
                    createdById = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_slips", x => x.id);
                    table.ForeignKey(
                        name: "FK_return_slips_agencies_agencyId",
                        column: x => x.agencyId,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_return_slips_clients_clientId",
                        column: x => x.clientId,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_return_slips_users_deliveredById",
                        column: x => x.deliveredById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "storage_requests",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    clientId = table.Column<string>(type: "text", nullable: false),
                    agencyId = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    requestedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    decidedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    decidedById = table.Column<string>(type: "text", nullable: true),
                    refusalReason = table.Column<string>(type: "text", nullable: true),
                    createdById = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storage_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_storage_requests_agencies_agencyId",
                        column: x => x.agencyId,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_storage_requests_clients_clientId",
                        column: x => x.clientId,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_storage_requests_users_decidedById",
                        column: x => x.decidedById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "bl_notes",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    courierId = table.Column<string>(type: "text", nullable: false),
                    clientId = table.Column<string>(type: "text", nullable: false),
                    agencyId = table.Column<string>(type: "text", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    handedOverById = table.Column<string>(type: "text", nullable: true),
                    handedOverAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    receivedById = table.Column<string>(type: "text", nullable: true),
                    receivedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    returnedById = table.Column<string>(type: "text", nullable: true),
                    returnedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    returnedToName = table.Column<string>(type: "text", nullable: true),
                    closedById = table.Column<string>(type: "text", nullable: true),
                    closedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    createdById = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bl_notes", x => x.id);
                    table.ForeignKey(
                        name: "FK_bl_notes_agencies_agencyId",
                        column: x => x.agencyId,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bl_notes_clients_clientId",
                        column: x => x.clientId,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bl_notes_couriers_courierId",
                        column: x => x.courierId,
                        principalTable: "couriers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bl_notes_users_createdById",
                        column: x => x.createdById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "cash_remittances",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    origin = table.Column<string>(type: "text", nullable: false),
                    cityId = table.Column<string>(type: "text", nullable: false),
                    courierId = table.Column<string>(type: "text", nullable: false),
                    agencyId = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    expectedCod = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    expectedCommission = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    expectedNet = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    declaredAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    varianceReason = table.Column<string>(type: "text", nullable: true),
                    totalCash = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    totalCheque = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    variance = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    settlementMode = table.Column<string>(type: "text", nullable: false),
                    payModel = table.Column<string>(type: "text", nullable: false),
                    proofName = table.Column<string>(type: "text", nullable: true),
                    proofMimeType = table.Column<string>(type: "text", nullable: true),
                    proofPath = table.Column<string>(type: "text", nullable: true),
                    confirmedById = table.Column<string>(type: "text", nullable: true),
                    confirmedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    courierClosedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    courierClosedById = table.Column<string>(type: "text", nullable: true),
                    createdById = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_remittances", x => x.id);
                    table.ForeignKey(
                        name: "FK_cash_remittances_agencies_agencyId",
                        column: x => x.agencyId,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_remittances_cities_cityId",
                        column: x => x.cityId,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_remittances_couriers_courierId",
                        column: x => x.courierId,
                        principalTable: "couriers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_remittances_users_confirmedById",
                        column: x => x.confirmedById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_cash_remittances_users_courierClosedById",
                        column: x => x.courierClosedById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "courier_cash_sessions",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    courierId = table.Column<string>(type: "text", nullable: false),
                    agencyId = table.Column<string>(type: "text", nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    totalCash = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    totalCheque = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    totalCollected = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    totalCommission = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    expectedHandover = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    declaredAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    variance = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    settlementMode = table.Column<string>(type: "text", nullable: false),
                    payModel = table.Column<string>(type: "text", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    validatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    validatedById = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_courier_cash_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_courier_cash_sessions_agencies_agencyId",
                        column: x => x.agencyId,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_courier_cash_sessions_couriers_courierId",
                        column: x => x.courierId,
                        principalTable: "couriers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_courier_cash_sessions_users_validatedById",
                        column: x => x.validatedById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "courier_cities",
                columns: table => new
                {
                    courierId = table.Column<string>(type: "text", nullable: false),
                    cityId = table.Column<string>(type: "text", nullable: false),
                    pickupFee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    deliveryFee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_courier_cities", x => new { x.courierId, x.cityId });
                    table.ForeignKey(
                        name: "FK_courier_cities_cities_cityId",
                        column: x => x.cityId,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_courier_cities_couriers_courierId",
                        column: x => x.courierId,
                        principalTable: "couriers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "courier_statements",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    courierId = table.Column<string>(type: "text", nullable: false),
                    agencyId = table.Column<string>(type: "text", nullable: false),
                    periodStart = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    periodEnd = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    periodKind = table.Column<string>(type: "text", nullable: false),
                    payModel = table.Column<string>(type: "text", nullable: false),
                    settlementMode = table.Column<string>(type: "text", nullable: false),
                    baseAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    codCollected = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    commissionsTotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    commissionsDeducted = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    variableAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    totalDue = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    validatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    validatedById = table.Column<string>(type: "text", nullable: true),
                    paidAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    paidById = table.Column<string>(type: "text", nullable: true),
                    paymentMethod = table.Column<string>(type: "text", nullable: false),
                    bankAccountId = table.Column<string>(type: "text", nullable: true),
                    reference = table.Column<string>(type: "text", nullable: true),
                    chequeNumber = table.Column<string>(type: "text", nullable: true),
                    chequeBank = table.Column<string>(type: "text", nullable: true),
                    chequeDate = table.Column<DateOnly>(type: "date", nullable: true),
                    proofName = table.Column<string>(type: "text", nullable: true),
                    proofMimeType = table.Column<string>(type: "text", nullable: true),
                    proofPath = table.Column<string>(type: "text", nullable: true),
                    createdById = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_courier_statements", x => x.id);
                    table.ForeignKey(
                        name: "FK_courier_statements_agencies_agencyId",
                        column: x => x.agencyId,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_courier_statements_bank_accounts_bankAccountId",
                        column: x => x.bankAccountId,
                        principalTable: "bank_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_courier_statements_couriers_courierId",
                        column: x => x.courierId,
                        principalTable: "couriers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_courier_statements_users_paidById",
                        column: x => x.paidById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_courier_statements_users_validatedById",
                        column: x => x.validatedById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "provider_tokens",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    courierId = table.Column<string>(type: "text", nullable: false),
                    userId = table.Column<string>(type: "text", nullable: false),
                    tokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tokenPrefix = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    previousTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    previousExpiresAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    mode = table.Column<string>(type: "text", nullable: false),
                    statusMap = table.Column<string>(type: "jsonb", nullable: true),
                    rateLimitPerMinute = table.Column<int>(type: "integer", nullable: false),
                    revokedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    rotatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    lastUsedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    createdById = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_provider_tokens_couriers_courierId",
                        column: x => x.courierId,
                        principalTable: "couriers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_provider_tokens_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "return_handovers",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    courierId = table.Column<string>(type: "text", nullable: false),
                    agencyId = table.Column<string>(type: "text", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    origin = table.Column<string>(type: "text", nullable: false),
                    courierCanEdit = table.Column<bool>(type: "boolean", nullable: false),
                    pickedUpById = table.Column<string>(type: "text", nullable: true),
                    pickedUpAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    handedOverById = table.Column<string>(type: "text", nullable: true),
                    handedOverAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    confirmedById = table.Column<string>(type: "text", nullable: true),
                    confirmedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    closedById = table.Column<string>(type: "text", nullable: true),
                    closedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    cancelledById = table.Column<string>(type: "text", nullable: true),
                    cancelledAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    cancelReason = table.Column<string>(type: "text", nullable: true),
                    createdById = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_handovers", x => x.id);
                    table.ForeignKey(
                        name: "FK_return_handovers_agencies_agencyId",
                        column: x => x.agencyId,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_return_handovers_couriers_courierId",
                        column: x => x.courierId,
                        principalTable: "couriers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_return_handovers_users_confirmedById",
                        column: x => x.confirmedById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_return_handovers_users_createdById",
                        column: x => x.createdById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "return_notes",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    clientId = table.Column<string>(type: "text", nullable: false),
                    courierId = table.Column<string>(type: "text", nullable: true),
                    validatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_notes", x => x.id);
                    table.ForeignKey(
                        name: "FK_return_notes_clients_clientId",
                        column: x => x.clientId,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_return_notes_couriers_courierId",
                        column: x => x.courierId,
                        principalTable: "couriers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "routes",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    statusAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    cityId = table.Column<string>(type: "text", nullable: false),
                    originCityId = table.Column<string>(type: "text", nullable: false),
                    courierId = table.Column<string>(type: "text", nullable: false),
                    exitDate = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routes", x => x.id);
                    table.ForeignKey(
                        name: "FK_routes_cities_cityId",
                        column: x => x.cityId,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_routes_cities_originCityId",
                        column: x => x.originCityId,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_routes_couriers_courierId",
                        column: x => x.courierId,
                        principalTable: "couriers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "client_discount_cities",
                columns: table => new
                {
                    discountId = table.Column<string>(type: "text", nullable: false),
                    cityId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_discount_cities", x => new { x.discountId, x.cityId });
                    table.ForeignKey(
                        name: "FK_client_discount_cities_cities_cityId",
                        column: x => x.cityId,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_client_discount_cities_client_discounts_discountId",
                        column: x => x.discountId,
                        principalTable: "client_discounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invoice_adjustments",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    invoiceId = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    vatAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    reference = table.Column<string>(type: "text", nullable: true),
                    deliveryNoteId = table.Column<string>(type: "text", nullable: true),
                    createdById = table.Column<string>(type: "text", nullable: true),
                    createdByName = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_adjustments", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoice_adjustments_delivery_notes_deliveryNoteId",
                        column: x => x.deliveryNoteId,
                        principalTable: "delivery_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_invoice_adjustments_invoices_invoiceId",
                        column: x => x.invoiceId,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    invoiceId = table.Column<string>(type: "text", nullable: true),
                    clientId = table.Column<string>(type: "text", nullable: false),
                    agencyId = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    method = table.Column<string>(type: "text", nullable: false),
                    bankAccountId = table.Column<string>(type: "text", nullable: true),
                    reference = table.Column<string>(type: "text", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    paidAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    proofName = table.Column<string>(type: "text", nullable: true),
                    proofMimeType = table.Column<string>(type: "text", nullable: true),
                    proofPath = table.Column<string>(type: "text", nullable: true),
                    chequeNumber = table.Column<string>(type: "text", nullable: true),
                    chequeBank = table.Column<string>(type: "text", nullable: true),
                    chequeDate = table.Column<DateOnly>(type: "date", nullable: true),
                    createdById = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                    table.ForeignKey(
                        name: "FK_payments_agencies_agencyId",
                        column: x => x.agencyId,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_bank_accounts_bankAccountId",
                        column: x => x.bankAccountId,
                        principalTable: "bank_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_clients_clientId",
                        column: x => x.clientId,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_invoices_invoiceId",
                        column: x => x.invoiceId,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "parcels",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    externalCode = table.Column<string>(type: "text", nullable: true),
                    idempotencyKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    clientId = table.Column<string>(type: "text", nullable: false),
                    courierId = table.Column<string>(type: "text", nullable: true),
                    agencyId = table.Column<string>(type: "text", nullable: false),
                    statusId = table.Column<int>(type: "integer", nullable: false),
                    statusAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    billingState = table.Column<string>(type: "text", nullable: false),
                    financialStatus = table.Column<string>(type: "text", nullable: false),
                    originCityId = table.Column<string>(type: "text", nullable: false),
                    destinationCityId = table.Column<string>(type: "text", nullable: false),
                    destinationDistrictId = table.Column<string>(type: "text", nullable: true),
                    destinationDistrictName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    deliveryMode = table.Column<string>(type: "text", nullable: false),
                    deliveryTypeId = table.Column<string>(type: "text", nullable: true),
                    currentCityId = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    recipientName = table.Column<string>(type: "text", nullable: false),
                    recipientPhone = table.Column<string>(type: "text", nullable: false),
                    pickupAddress = table.Column<string>(type: "text", nullable: false),
                    deliveryAddress = table.Column<string>(type: "text", nullable: false),
                    codAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    deliveryFee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    failureFee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    otherFees = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    feeIncluded = table.Column<bool>(type: "boolean", nullable: false),
                    alreadyPaid = table.Column<bool>(type: "boolean", nullable: false),
                    pickupWaived = table.Column<bool>(type: "boolean", nullable: false),
                    courierCommission = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    courierCommissionAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    courierCommissionSource = table.Column<string>(type: "text", nullable: true),
                    courierCommissionCourierId = table.Column<string>(type: "text", nullable: true),
                    reimbursedByCourier = table.Column<bool>(type: "boolean", nullable: false),
                    reimbursedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    reimbursedRemittanceId = table.Column<string>(type: "text", nullable: true),
                    kind = table.Column<string>(type: "text", nullable: false),
                    isReturn = table.Column<bool>(type: "boolean", nullable: false),
                    isFailed = table.Column<bool>(type: "boolean", nullable: false),
                    referenceParcelId = table.Column<string>(type: "text", nullable: true),
                    importId = table.Column<string>(type: "text", nullable: true),
                    openOnDelivery = table.Column<bool>(type: "boolean", nullable: false),
                    fragile = table.Column<bool>(type: "boolean", nullable: false),
                    blRequested = table.Column<bool>(type: "boolean", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    expectedPayment = table.Column<string>(type: "text", nullable: true),
                    scheduledAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    deletedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parcels", x => x.id);
                    table.ForeignKey(
                        name: "FK_parcels_agencies_agencyId",
                        column: x => x.agencyId,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parcels_cities_currentCityId",
                        column: x => x.currentCityId,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parcels_cities_destinationCityId",
                        column: x => x.destinationCityId,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parcels_cities_originCityId",
                        column: x => x.originCityId,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parcels_clients_clientId",
                        column: x => x.clientId,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parcels_couriers_courierCommissionCourierId",
                        column: x => x.courierCommissionCourierId,
                        principalTable: "couriers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parcels_couriers_courierId",
                        column: x => x.courierId,
                        principalTable: "couriers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parcels_delivery_types_deliveryTypeId",
                        column: x => x.deliveryTypeId,
                        principalTable: "delivery_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parcels_districts_destinationDistrictId",
                        column: x => x.destinationDistrictId,
                        principalTable: "districts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parcels_parcel_imports_importId",
                        column: x => x.importId,
                        principalTable: "parcel_imports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parcels_parcel_statuses_statusId",
                        column: x => x.statusId,
                        principalTable: "parcel_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parcels_parcels_referenceParcelId",
                        column: x => x.referenceParcelId,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "product_attribute_assignments",
                columns: table => new
                {
                    productId = table.Column<string>(type: "text", nullable: false),
                    valueId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_attribute_assignments", x => new { x.productId, x.valueId });
                    table.ForeignKey(
                        name: "FK_product_attribute_assignments_product_attribute_values_valu~",
                        column: x => x.valueId,
                        principalTable: "product_attribute_values",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_attribute_assignments_products_productId",
                        column: x => x.productId,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_items",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    productId = table.Column<string>(type: "text", nullable: false),
                    agencyId = table.Column<string>(type: "text", nullable: false),
                    clientId = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_items_agencies_agencyId",
                        column: x => x.agencyId,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_items_clients_clientId",
                        column: x => x.clientId,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_items_products_productId",
                        column: x => x.productId,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "storage_request_items",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    requestId = table.Column<string>(type: "text", nullable: false),
                    productId = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storage_request_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_storage_request_items_products_productId",
                        column: x => x.productId,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_storage_request_items_storage_requests_requestId",
                        column: x => x.requestId,
                        principalTable: "storage_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bl_note_events",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    noteId = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    authorId = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bl_note_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_bl_note_events_bl_notes_noteId",
                        column: x => x.noteId,
                        principalTable: "bl_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bl_note_events_users_authorId",
                        column: x => x.authorId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "courier_balance_entries",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    courierId = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    sessionId = table.Column<string>(type: "text", nullable: true),
                    remittanceId = table.Column<string>(type: "text", nullable: true),
                    method = table.Column<string>(type: "text", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    authorId = table.Column<string>(type: "text", nullable: true),
                    authorName = table.Column<string>(type: "text", nullable: false),
                    at = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_courier_balance_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_courier_balance_entries_cash_remittances_remittanceId",
                        column: x => x.remittanceId,
                        principalTable: "cash_remittances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_courier_balance_entries_courier_cash_sessions_sessionId",
                        column: x => x.sessionId,
                        principalTable: "courier_cash_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_courier_balance_entries_couriers_courierId",
                        column: x => x.courierId,
                        principalTable: "couriers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_courier_balance_entries_users_authorId",
                        column: x => x.authorId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "provider_calls",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    tokenId = table.Column<string>(type: "text", nullable: false),
                    eventKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    parcelCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    parcelId = table.Column<string>(type: "text", nullable: true),
                    providerStatus = table.Column<int>(type: "integer", nullable: false),
                    mappedStatus = table.Column<int>(type: "integer", nullable: true),
                    occurredAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    mode = table.Column<string>(type: "text", nullable: false),
                    outcome = table.Column<string>(type: "text", nullable: false),
                    httpStatus = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    requestId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    alert = table.Column<bool>(type: "boolean", nullable: false),
                    alertResolvedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    at = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_calls", x => x.id);
                    table.ForeignKey(
                        name: "FK_provider_calls_provider_tokens_tokenId",
                        column: x => x.tokenId,
                        principalTable: "provider_tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "return_handover_events",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    handoverId = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    authorId = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_handover_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_return_handover_events_return_handovers_handoverId",
                        column: x => x.handoverId,
                        principalTable: "return_handovers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_return_handover_events_users_authorId",
                        column: x => x.authorId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "route_notes",
                columns: table => new
                {
                    routeId = table.Column<string>(type: "text", nullable: false),
                    deliveryNoteId = table.Column<string>(type: "text", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_route_notes", x => new { x.routeId, x.deliveryNoteId });
                    table.ForeignKey(
                        name: "FK_route_notes_delivery_notes_deliveryNoteId",
                        column: x => x.deliveryNoteId,
                        principalTable: "delivery_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_route_notes_routes_routeId",
                        column: x => x.routeId,
                        principalTable: "routes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cash_remittance_parcels",
                columns: table => new
                {
                    remittanceId = table.Column<string>(type: "text", nullable: false),
                    parcelId = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    cash = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    cheque = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    commission = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    confirmedParcelId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_remittance_parcels", x => new { x.remittanceId, x.parcelId });
                    table.ForeignKey(
                        name: "FK_cash_remittance_parcels_cash_remittances_remittanceId",
                        column: x => x.remittanceId,
                        principalTable: "cash_remittances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cash_remittance_parcels_parcels_parcelId",
                        column: x => x.parcelId,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cheques",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    number = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    bank = table.Column<string>(type: "text", nullable: true),
                    chequeDate = table.Column<DateOnly>(type: "date", nullable: true),
                    receivedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    payerName = table.Column<string>(type: "text", nullable: false),
                    parcelId = table.Column<string>(type: "text", nullable: false),
                    courierId = table.Column<string>(type: "text", nullable: false),
                    sessionId = table.Column<string>(type: "text", nullable: true),
                    remittanceId = table.Column<string>(type: "text", nullable: true),
                    invoiceId = table.Column<string>(type: "text", nullable: true),
                    handedOverAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    receivedByAdminAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    transmittedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    merchantReceivedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    rejectedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    cancelledAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    depositedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    cashedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    rejectionReason = table.Column<string>(type: "text", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cheques", x => x.id);
                    table.ForeignKey(
                        name: "FK_cheques_cash_remittances_remittanceId",
                        column: x => x.remittanceId,
                        principalTable: "cash_remittances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_cheques_courier_cash_sessions_sessionId",
                        column: x => x.sessionId,
                        principalTable: "courier_cash_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_cheques_couriers_courierId",
                        column: x => x.courierId,
                        principalTable: "couriers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cheques_invoices_invoiceId",
                        column: x => x.invoiceId,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_cheques_parcels_parcelId",
                        column: x => x.parcelId,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "claims",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    clientId = table.Column<string>(type: "text", nullable: false),
                    typeId = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    parcelId = table.Column<string>(type: "text", nullable: true),
                    parcelCode = table.Column<string>(type: "text", nullable: true),
                    assigneeId = table.Column<string>(type: "text", nullable: true),
                    dueAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    resolvedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    createdById = table.Column<string>(type: "text", nullable: true),
                    authorName = table.Column<string>(type: "text", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_claims", x => x.id);
                    table.ForeignKey(
                        name: "FK_claims_claim_types_typeId",
                        column: x => x.typeId,
                        principalTable: "claim_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_claims_clients_clientId",
                        column: x => x.clientId,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_claims_parcels_parcelId",
                        column: x => x.parcelId,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_claims_users_assigneeId",
                        column: x => x.assigneeId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_claims_users_createdById",
                        column: x => x.createdById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "cod_payout_parcels",
                columns: table => new
                {
                    payoutId = table.Column<string>(type: "text", nullable: false),
                    parcelId = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    paidOutParcelId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cod_payout_parcels", x => new { x.payoutId, x.parcelId });
                    table.ForeignKey(
                        name: "FK_cod_payout_parcels_cod_payouts_payoutId",
                        column: x => x.payoutId,
                        principalTable: "cod_payouts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cod_payout_parcels_parcels_parcelId",
                        column: x => x.parcelId,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "courier_cash_session_parcels",
                columns: table => new
                {
                    sessionId = table.Column<string>(type: "text", nullable: false),
                    parcelId = table.Column<string>(type: "text", nullable: false),
                    cash = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    cheque = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    collected = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    commission = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    addedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    settledParcelId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_courier_cash_session_parcels", x => new { x.sessionId, x.parcelId });
                    table.ForeignKey(
                        name: "FK_courier_cash_session_parcels_courier_cash_sessions_sessionId",
                        column: x => x.sessionId,
                        principalTable: "courier_cash_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_courier_cash_session_parcels_parcels_parcelId",
                        column: x => x.parcelId,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "courier_statement_lines",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    statementId = table.Column<string>(type: "text", nullable: false),
                    parcelId = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    parcelCode = table.Column<string>(type: "text", nullable: false),
                    cityName = table.Column<string>(type: "text", nullable: false),
                    statusId = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    deducted = table.Column<bool>(type: "boolean", nullable: false),
                    workedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    settledParcelId = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_courier_statement_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_courier_statement_lines_courier_statements_statementId",
                        column: x => x.statementId,
                        principalTable: "courier_statements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_courier_statement_lines_parcels_parcelId",
                        column: x => x.parcelId,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_note_parcels",
                columns: table => new
                {
                    deliveryNoteId = table.Column<string>(type: "text", nullable: false),
                    parcelId = table.Column<string>(type: "text", nullable: false),
                    correctedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    correctedStatusId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_note_parcels", x => new { x.deliveryNoteId, x.parcelId });
                    table.ForeignKey(
                        name: "FK_delivery_note_parcels_delivery_notes_deliveryNoteId",
                        column: x => x.deliveryNoteId,
                        principalTable: "delivery_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_delivery_note_parcels_parcels_parcelId",
                        column: x => x.parcelId,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_note_removed_parcels",
                columns: table => new
                {
                    deliveryNoteId = table.Column<string>(type: "text", nullable: false),
                    parcelId = table.Column<string>(type: "text", nullable: false),
                    routeId = table.Column<string>(type: "text", nullable: true),
                    removedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    removedBy = table.Column<string>(type: "text", nullable: true),
                    restoredAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    restoredBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_note_removed_parcels", x => new { x.deliveryNoteId, x.parcelId });
                    table.ForeignKey(
                        name: "FK_delivery_note_removed_parcels_delivery_notes_deliveryNoteId",
                        column: x => x.deliveryNoteId,
                        principalTable: "delivery_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_delivery_note_removed_parcels_parcels_parcelId",
                        column: x => x.parcelId,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_slips",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    parcelId = table.Column<string>(type: "text", nullable: false),
                    clientId = table.Column<string>(type: "text", nullable: false),
                    courierId = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    reference = table.Column<string>(type: "text", nullable: true),
                    collectedById = table.Column<string>(type: "text", nullable: true),
                    collectedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    handedOverById = table.Column<string>(type: "text", nullable: true),
                    handedOverAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    receivedById = table.Column<string>(type: "text", nullable: true),
                    receivedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    returnedById = table.Column<string>(type: "text", nullable: true),
                    returnedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    returnedToName = table.Column<string>(type: "text", nullable: true),
                    closedById = table.Column<string>(type: "text", nullable: true),
                    closedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_slips", x => x.id);
                    table.ForeignKey(
                        name: "FK_delivery_slips_clients_clientId",
                        column: x => x.clientId,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_delivery_slips_couriers_courierId",
                        column: x => x.courierId,
                        principalTable: "couriers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_delivery_slips_parcels_parcelId",
                        column: x => x.parcelId,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exchanges",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    originalParcelId = table.Column<string>(type: "text", nullable: false),
                    newParcelId = table.Column<string>(type: "text", nullable: true),
                    returnedProduct = table.Column<string>(type: "text", nullable: false),
                    newProduct = table.Column<string>(type: "text", nullable: false),
                    oldDestination = table.Column<string>(type: "text", nullable: false),
                    newDestination = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    fees = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    courierId = table.Column<string>(type: "text", nullable: true),
                    requestedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    pickedUpAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    deliveredAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    closedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    cancelledAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    createdById = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exchanges", x => x.id);
                    table.ForeignKey(
                        name: "FK_exchanges_couriers_courierId",
                        column: x => x.courierId,
                        principalTable: "couriers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exchanges_parcels_newParcelId",
                        column: x => x.newParcelId,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exchanges_parcels_originalParcelId",
                        column: x => x.originalParcelId,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exchanges_users_createdById",
                        column: x => x.createdById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "invoice_lines",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    invoiceId = table.Column<string>(type: "text", nullable: false),
                    parcelId = table.Column<string>(type: "text", nullable: false),
                    parcelCode = table.Column<string>(type: "text", nullable: false),
                    statusId = table.Column<int>(type: "integer", nullable: false),
                    codAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    deliveryFee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    failureFee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    vatAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    chequePaid = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    netDue = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    invoicedParcelId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoice_lines_invoices_invoiceId",
                        column: x => x.invoiceId,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_invoice_lines_parcels_parcelId",
                        column: x => x.parcelId,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    externalRef = table.Column<string>(type: "text", nullable: true),
                    clientId = table.Column<string>(type: "text", nullable: false),
                    agencyId = table.Column<string>(type: "text", nullable: true),
                    recipientName = table.Column<string>(type: "text", nullable: false),
                    recipientPhone = table.Column<string>(type: "text", nullable: false),
                    destinationCityId = table.Column<string>(type: "text", nullable: false),
                    destinationDistrictId = table.Column<string>(type: "text", nullable: true),
                    destinationDistrictName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    deliveryAddress = table.Column<string>(type: "text", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    totalAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    parcelId = table.Column<string>(type: "text", nullable: true),
                    createdById = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_orders_agencies_agencyId",
                        column: x => x.agencyId,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orders_cities_destinationCityId",
                        column: x => x.destinationCityId,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orders_clients_clientId",
                        column: x => x.clientId,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orders_districts_destinationDistrictId",
                        column: x => x.destinationDistrictId,
                        principalTable: "districts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orders_parcels_parcelId",
                        column: x => x.parcelId,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "parcel_events",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    parcelId = table.Column<string>(type: "text", nullable: false),
                    statusId = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    authorId = table.Column<string>(type: "text", nullable: true),
                    authorName = table.Column<string>(type: "text", nullable: true),
                    cityId = table.Column<string>(type: "text", nullable: true),
                    attachmentName = table.Column<string>(type: "text", nullable: true),
                    attachmentPath = table.Column<string>(type: "text", nullable: true),
                    attachmentMimeType = table.Column<string>(type: "text", nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    scheduledDate = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    isDispute = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parcel_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_parcel_events_cities_cityId",
                        column: x => x.cityId,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parcel_events_parcel_statuses_statusId",
                        column: x => x.statusId,
                        principalTable: "parcel_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parcel_events_parcels_parcelId",
                        column: x => x.parcelId,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_parcel_events_users_authorId",
                        column: x => x.authorId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "parcel_fee_changes",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    parcelId = table.Column<string>(type: "text", nullable: false),
                    field = table.Column<string>(type: "text", nullable: false),
                    oldAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    newAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    authorId = table.Column<string>(type: "text", nullable: true),
                    authorName = table.Column<string>(type: "text", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parcel_fee_changes", x => x.id);
                    table.ForeignKey(
                        name: "FK_parcel_fee_changes_parcels_parcelId",
                        column: x => x.parcelId,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_parcel_fee_changes_users_authorId",
                        column: x => x.authorId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "parcel_items",
                columns: table => new
                {
                    parcelId = table.Column<string>(type: "text", nullable: false),
                    productId = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parcel_items", x => new { x.parcelId, x.productId });
                    table.ForeignKey(
                        name: "FK_parcel_items_parcels_parcelId",
                        column: x => x.parcelId,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_parcel_items_products_productId",
                        column: x => x.productId,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "parcel_notes",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    parcelId = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    authorId = table.Column<string>(type: "text", nullable: true),
                    authorName = table.Column<string>(type: "text", nullable: false),
                    attachmentName = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parcel_notes", x => x.id);
                    table.ForeignKey(
                        name: "FK_parcel_notes_parcels_parcelId",
                        column: x => x.parcelId,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_parcel_notes_users_authorId",
                        column: x => x.authorId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "return_handover_items",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    handoverId = table.Column<string>(type: "text", nullable: false),
                    parcelId = table.Column<string>(type: "text", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    confirmedParcelId = table.Column<string>(type: "text", nullable: true),
                    reservedParcelId = table.Column<string>(type: "text", nullable: true),
                    correctedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    correctedStatusId = table.Column<int>(type: "integer", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_handover_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_return_handover_items_parcels_parcelId",
                        column: x => x.parcelId,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_return_handover_items_return_handovers_handoverId",
                        column: x => x.handoverId,
                        principalTable: "return_handovers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "return_note_parcels",
                columns: table => new
                {
                    returnNoteId = table.Column<string>(type: "text", nullable: false),
                    parcelId = table.Column<string>(type: "text", nullable: false),
                    leg = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_note_parcels", x => new { x.returnNoteId, x.parcelId, x.leg });
                    table.ForeignKey(
                        name: "FK_return_note_parcels_parcels_parcelId",
                        column: x => x.parcelId,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_return_note_parcels_return_notes_returnNoteId",
                        column: x => x.returnNoteId,
                        principalTable: "return_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "return_slip_items",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    slipId = table.Column<string>(type: "text", nullable: false),
                    parcelId = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    feeApplied = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    deliveredParcelId = table.Column<string>(type: "text", nullable: true),
                    reservedParcelId = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_slip_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_return_slip_items_parcels_parcelId",
                        column: x => x.parcelId,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_return_slip_items_return_slips_slipId",
                        column: x => x.slipId,
                        principalTable: "return_slips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "route_parcels",
                columns: table => new
                {
                    routeId = table.Column<string>(type: "text", nullable: false),
                    parcelId = table.Column<string>(type: "text", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    confirmedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    confirmedBy = table.Column<string>(type: "text", nullable: true),
                    courierFee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    finalStatusId = table.Column<int>(type: "integer", nullable: true),
                    codCollected = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    codCash = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    codCheque = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    courierCommission = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    courierCommissionSource = table.Column<string>(type: "text", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    correctedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: true),
                    correctedStatusId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_route_parcels", x => new { x.routeId, x.parcelId });
                    table.ForeignKey(
                        name: "FK_route_parcels_parcels_parcelId",
                        column: x => x.parcelId,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_route_parcels_routes_routeId",
                        column: x => x.routeId,
                        principalTable: "routes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transfer_order_parcels",
                columns: table => new
                {
                    transferOrderId = table.Column<string>(type: "text", nullable: false),
                    parcelId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transfer_order_parcels", x => new { x.transferOrderId, x.parcelId });
                    table.ForeignKey(
                        name: "FK_transfer_order_parcels_parcels_parcelId",
                        column: x => x.parcelId,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transfer_order_parcels_transfer_orders_transferOrderId",
                        column: x => x.transferOrderId,
                        principalTable: "transfer_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    stockItemId = table.Column<string>(type: "text", nullable: false),
                    productId = table.Column<string>(type: "text", nullable: false),
                    agencyId = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    balance = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    storageRequestId = table.Column<string>(type: "text", nullable: true),
                    parcelId = table.Column<string>(type: "text", nullable: true),
                    authorId = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movements", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_movements_agencies_agencyId",
                        column: x => x.agencyId,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_parcels_parcelId",
                        column: x => x.parcelId,
                        principalTable: "parcels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_products_productId",
                        column: x => x.productId,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_stock_items_stockItemId",
                        column: x => x.stockItemId,
                        principalTable: "stock_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_storage_requests_storageRequestId",
                        column: x => x.storageRequestId,
                        principalTable: "storage_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_users_authorId",
                        column: x => x.authorId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "cheque_events",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    chequeId = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    authorId = table.Column<string>(type: "text", nullable: true),
                    authorName = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cheque_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_cheque_events_cheques_chequeId",
                        column: x => x.chequeId,
                        principalTable: "cheques",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cheque_events_users_authorId",
                        column: x => x.authorId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "claim_events",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    claimId = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: true),
                    body = table.Column<string>(type: "text", nullable: true),
                    isInternal = table.Column<bool>(type: "boolean", nullable: false),
                    authorId = table.Column<string>(type: "text", nullable: true),
                    authorName = table.Column<string>(type: "text", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_claim_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_claim_events_claims_claimId",
                        column: x => x.claimId,
                        principalTable: "claims",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_claim_events_users_authorId",
                        column: x => x.authorId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "bl_note_items",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    noteId = table.Column<string>(type: "text", nullable: false),
                    slipId = table.Column<string>(type: "text", nullable: false),
                    reservedSlipId = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bl_note_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_bl_note_items_bl_notes_noteId",
                        column: x => x.noteId,
                        principalTable: "bl_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bl_note_items_delivery_slips_slipId",
                        column: x => x.slipId,
                        principalTable: "delivery_slips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_slip_events",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    slipId = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    authorId = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_slip_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_delivery_slip_events_delivery_slips_slipId",
                        column: x => x.slipId,
                        principalTable: "delivery_slips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_delivery_slip_events_users_authorId",
                        column: x => x.authorId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "exchange_events",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    exchangeId = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    authorId = table.Column<string>(type: "text", nullable: true),
                    authorName = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exchange_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_exchange_events_exchanges_exchangeId",
                        column: x => x.exchangeId,
                        principalTable: "exchanges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_exchange_events_users_authorId",
                        column: x => x.authorId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "order_lines",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    orderId = table.Column<string>(type: "text", nullable: false),
                    productId = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unitPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_lines_orders_orderId",
                        column: x => x.orderId,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_order_lines_products_productId",
                        column: x => x.productId,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "claim_attachments",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    claimId = table.Column<string>(type: "text", nullable: false),
                    eventId = table.Column<string>(type: "text", nullable: true),
                    filename = table.Column<string>(type: "text", nullable: false),
                    storageKey = table.Column<string>(type: "text", nullable: false),
                    mimeType = table.Column<string>(type: "text", nullable: false),
                    sizeBytes = table.Column<int>(type: "integer", nullable: false),
                    uploadedById = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp(3) with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_claim_attachments", x => x.id);
                    table.ForeignKey(
                        name: "FK_claim_attachments_claim_events_eventId",
                        column: x => x.eventId,
                        principalTable: "claim_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_claim_attachments_claims_claimId",
                        column: x => x.claimId,
                        principalTable: "claims",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_claim_attachments_users_uploadedById",
                        column: x => x.uploadedById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agencies_cityId",
                table: "agencies",
                column: "cityId");

            migrationBuilder.CreateIndex(
                name: "IX_agency_cities_cityId",
                table: "agency_cities",
                column: "cityId");

            migrationBuilder.CreateIndex(
                name: "IX_announcements_audience",
                table: "announcements",
                column: "audience");

            migrationBuilder.CreateIndex(
                name: "IX_announcements_createdById",
                table: "announcements",
                column: "createdById");

            migrationBuilder.CreateIndex(
                name: "IX_announcements_isPublished_publishedAt",
                table: "announcements",
                columns: new[] { "isPublished", "publishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_at",
                table: "audit_logs",
                column: "at");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_entity_entityId_at",
                table: "audit_logs",
                columns: new[] { "entity", "entityId", "at" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_subjectClientId_at",
                table: "audit_logs",
                columns: new[] { "subjectClientId", "at" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_userId_at",
                table: "audit_logs",
                columns: new[] { "userId", "at" });

            migrationBuilder.CreateIndex(
                name: "IX_bank_accounts_bankId",
                table: "bank_accounts",
                column: "bankId");

            migrationBuilder.CreateIndex(
                name: "IX_bank_accounts_label",
                table: "bank_accounts",
                column: "label",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_banks_name",
                table: "banks",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bl_note_events_authorId",
                table: "bl_note_events",
                column: "authorId");

            migrationBuilder.CreateIndex(
                name: "IX_bl_note_events_noteId_createdAt",
                table: "bl_note_events",
                columns: new[] { "noteId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bl_note_items_noteId_slipId",
                table: "bl_note_items",
                columns: new[] { "noteId", "slipId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bl_note_items_reservedSlipId",
                table: "bl_note_items",
                column: "reservedSlipId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bl_note_items_slipId",
                table: "bl_note_items",
                column: "slipId");

            migrationBuilder.CreateIndex(
                name: "IX_bl_notes_agencyId",
                table: "bl_notes",
                column: "agencyId");

            migrationBuilder.CreateIndex(
                name: "IX_bl_notes_clientId_status",
                table: "bl_notes",
                columns: new[] { "clientId", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_bl_notes_code",
                table: "bl_notes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bl_notes_courierId_status",
                table: "bl_notes",
                columns: new[] { "courierId", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_bl_notes_createdById",
                table: "bl_notes",
                column: "createdById");

            migrationBuilder.CreateIndex(
                name: "IX_bl_notes_status",
                table: "bl_notes",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_bulk_jobs_status",
                table: "bulk_jobs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_bulk_jobs_target_status",
                table: "bulk_jobs",
                columns: new[] { "target", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_cash_remittance_parcels_confirmedParcelId",
                table: "cash_remittance_parcels",
                column: "confirmedParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cash_remittance_parcels_parcelId",
                table: "cash_remittance_parcels",
                column: "parcelId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_remittances_agencyId_createdAt",
                table: "cash_remittances",
                columns: new[] { "agencyId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_cash_remittances_cityId",
                table: "cash_remittances",
                column: "cityId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_remittances_code",
                table: "cash_remittances",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cash_remittances_confirmedById",
                table: "cash_remittances",
                column: "confirmedById");

            migrationBuilder.CreateIndex(
                name: "IX_cash_remittances_courierClosedById",
                table: "cash_remittances",
                column: "courierClosedById");

            migrationBuilder.CreateIndex(
                name: "IX_cash_remittances_courierId_status",
                table: "cash_remittances",
                columns: new[] { "courierId", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_cash_remittances_status",
                table: "cash_remittances",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_cheque_events_authorId",
                table: "cheque_events",
                column: "authorId");

            migrationBuilder.CreateIndex(
                name: "IX_cheque_events_chequeId_createdAt",
                table: "cheque_events",
                columns: new[] { "chequeId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_cheques_bank",
                table: "cheques",
                column: "bank");

            migrationBuilder.CreateIndex(
                name: "IX_cheques_courierId_receivedAt",
                table: "cheques",
                columns: new[] { "courierId", "receivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_cheques_invoiceId",
                table: "cheques",
                column: "invoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_cheques_parcelId",
                table: "cheques",
                column: "parcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cheques_remittanceId",
                table: "cheques",
                column: "remittanceId");

            migrationBuilder.CreateIndex(
                name: "IX_cheques_sessionId",
                table: "cheques",
                column: "sessionId");

            migrationBuilder.CreateIndex(
                name: "IX_cheques_status",
                table: "cheques",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_cities_code",
                table: "cities",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cities_zoneId",
                table: "cities",
                column: "zoneId");

            migrationBuilder.CreateIndex(
                name: "IX_claim_attachments_claimId",
                table: "claim_attachments",
                column: "claimId");

            migrationBuilder.CreateIndex(
                name: "IX_claim_attachments_eventId",
                table: "claim_attachments",
                column: "eventId");

            migrationBuilder.CreateIndex(
                name: "IX_claim_attachments_uploadedById",
                table: "claim_attachments",
                column: "uploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_claim_events_authorId",
                table: "claim_events",
                column: "authorId");

            migrationBuilder.CreateIndex(
                name: "IX_claim_events_claimId_createdAt",
                table: "claim_events",
                columns: new[] { "claimId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_claim_types_code",
                table: "claim_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_claim_types_isActive",
                table: "claim_types",
                column: "isActive");

            migrationBuilder.CreateIndex(
                name: "IX_claims_assigneeId",
                table: "claims",
                column: "assigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_claims_clientId_createdAt",
                table: "claims",
                columns: new[] { "clientId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_claims_code",
                table: "claims",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_claims_createdById",
                table: "claims",
                column: "createdById");

            migrationBuilder.CreateIndex(
                name: "IX_claims_parcelId",
                table: "claims",
                column: "parcelId");

            migrationBuilder.CreateIndex(
                name: "IX_claims_status",
                table: "claims",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_claims_typeId",
                table: "claims",
                column: "typeId");

            migrationBuilder.CreateIndex(
                name: "IX_client_discount_cities_cityId",
                table: "client_discount_cities",
                column: "cityId");

            migrationBuilder.CreateIndex(
                name: "IX_client_discounts_clientId",
                table: "client_discounts",
                column: "clientId");

            migrationBuilder.CreateIndex(
                name: "IX_client_documents_clientId",
                table: "client_documents",
                column: "clientId");

            migrationBuilder.CreateIndex(
                name: "IX_client_documents_uploadedById",
                table: "client_documents",
                column: "uploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_client_pickup_addresses_cityId",
                table: "client_pickup_addresses",
                column: "cityId");

            migrationBuilder.CreateIndex(
                name: "IX_client_pickup_addresses_clientId",
                table: "client_pickup_addresses",
                column: "clientId");

            migrationBuilder.CreateIndex(
                name: "IX_client_pickup_addresses_districtId",
                table: "client_pickup_addresses",
                column: "districtId");

            migrationBuilder.CreateIndex(
                name: "IX_clients_agencyId",
                table: "clients",
                column: "agencyId");

            migrationBuilder.CreateIndex(
                name: "IX_clients_cityId",
                table: "clients",
                column: "cityId");

            migrationBuilder.CreateIndex(
                name: "IX_clients_rejectedById",
                table: "clients",
                column: "rejectedById");

            migrationBuilder.CreateIndex(
                name: "IX_clients_userId",
                table: "clients",
                column: "userId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clients_validatedById",
                table: "clients",
                column: "validatedById");

            migrationBuilder.CreateIndex(
                name: "IX_cod_payout_parcels_paidOutParcelId",
                table: "cod_payout_parcels",
                column: "paidOutParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cod_payout_parcels_parcelId",
                table: "cod_payout_parcels",
                column: "parcelId");

            migrationBuilder.CreateIndex(
                name: "IX_cod_payouts_agencyId_createdAt",
                table: "cod_payouts",
                columns: new[] { "agencyId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_cod_payouts_bankAccountId",
                table: "cod_payouts",
                column: "bankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_cod_payouts_clientId_createdAt",
                table: "cod_payouts",
                columns: new[] { "clientId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_cod_payouts_code",
                table: "cod_payouts",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cod_payouts_paidById",
                table: "cod_payouts",
                column: "paidById");

            migrationBuilder.CreateIndex(
                name: "IX_cod_payouts_status",
                table: "cod_payouts",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_companies_customDomain",
                table: "companies",
                column: "customDomain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_companies_slug",
                table: "companies",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_courier_balance_entries_authorId",
                table: "courier_balance_entries",
                column: "authorId");

            migrationBuilder.CreateIndex(
                name: "IX_courier_balance_entries_courierId_at",
                table: "courier_balance_entries",
                columns: new[] { "courierId", "at" });

            migrationBuilder.CreateIndex(
                name: "IX_courier_balance_entries_remittanceId",
                table: "courier_balance_entries",
                column: "remittanceId");

            migrationBuilder.CreateIndex(
                name: "IX_courier_balance_entries_sessionId",
                table: "courier_balance_entries",
                column: "sessionId");

            migrationBuilder.CreateIndex(
                name: "IX_courier_cash_session_parcels_parcelId",
                table: "courier_cash_session_parcels",
                column: "parcelId");

            migrationBuilder.CreateIndex(
                name: "IX_courier_cash_session_parcels_settledParcelId",
                table: "courier_cash_session_parcels",
                column: "settledParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_courier_cash_sessions_agencyId_day",
                table: "courier_cash_sessions",
                columns: new[] { "agencyId", "day" });

            migrationBuilder.CreateIndex(
                name: "IX_courier_cash_sessions_courierId_day",
                table: "courier_cash_sessions",
                columns: new[] { "courierId", "day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_courier_cash_sessions_status",
                table: "courier_cash_sessions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_courier_cash_sessions_validatedById",
                table: "courier_cash_sessions",
                column: "validatedById");

            migrationBuilder.CreateIndex(
                name: "IX_courier_cities_cityId",
                table: "courier_cities",
                column: "cityId");

            migrationBuilder.CreateIndex(
                name: "IX_courier_statement_lines_parcelId",
                table: "courier_statement_lines",
                column: "parcelId");

            migrationBuilder.CreateIndex(
                name: "IX_courier_statement_lines_settledParcelId_kind",
                table: "courier_statement_lines",
                columns: new[] { "settledParcelId", "kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_courier_statement_lines_statementId_parcelId_kind",
                table: "courier_statement_lines",
                columns: new[] { "statementId", "parcelId", "kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_courier_statements_agencyId_createdAt",
                table: "courier_statements",
                columns: new[] { "agencyId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_courier_statements_bankAccountId",
                table: "courier_statements",
                column: "bankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_courier_statements_code",
                table: "courier_statements",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_courier_statements_courierId_periodStart",
                table: "courier_statements",
                columns: new[] { "courierId", "periodStart" });

            migrationBuilder.CreateIndex(
                name: "IX_courier_statements_paidById",
                table: "courier_statements",
                column: "paidById");

            migrationBuilder.CreateIndex(
                name: "IX_courier_statements_status",
                table: "courier_statements",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_courier_statements_validatedById",
                table: "courier_statements",
                column: "validatedById");

            migrationBuilder.CreateIndex(
                name: "IX_couriers_agencyId",
                table: "couriers",
                column: "agencyId");

            migrationBuilder.CreateIndex(
                name: "IX_couriers_cityId",
                table: "couriers",
                column: "cityId");

            migrationBuilder.CreateIndex(
                name: "IX_couriers_rejectedById",
                table: "couriers",
                column: "rejectedById");

            migrationBuilder.CreateIndex(
                name: "IX_couriers_userId",
                table: "couriers",
                column: "userId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_couriers_validatedById",
                table: "couriers",
                column: "validatedById");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_note_parcels_parcelId",
                table: "delivery_note_parcels",
                column: "parcelId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_note_removed_parcels_parcelId",
                table: "delivery_note_removed_parcels",
                column: "parcelId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_note_removed_parcels_routeId",
                table: "delivery_note_removed_parcels",
                column: "routeId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_notes_agencyId_createdAt",
                table: "delivery_notes",
                columns: new[] { "agencyId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_notes_clientId",
                table: "delivery_notes",
                column: "clientId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_notes_code",
                table: "delivery_notes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_notes_locationCityId",
                table: "delivery_notes",
                column: "locationCityId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_notes_type_status",
                table: "delivery_notes",
                columns: new[] { "type", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_slip_events_authorId",
                table: "delivery_slip_events",
                column: "authorId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_slip_events_slipId_createdAt",
                table: "delivery_slip_events",
                columns: new[] { "slipId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_slips_clientId_status",
                table: "delivery_slips",
                columns: new[] { "clientId", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_slips_courierId_status",
                table: "delivery_slips",
                columns: new[] { "courierId", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_slips_parcelId",
                table: "delivery_slips",
                column: "parcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_slips_status",
                table: "delivery_slips",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_types_name",
                table: "delivery_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_districts_cityId",
                table: "districts",
                column: "cityId");

            migrationBuilder.CreateIndex(
                name: "IX_exchange_events_authorId",
                table: "exchange_events",
                column: "authorId");

            migrationBuilder.CreateIndex(
                name: "IX_exchange_events_exchangeId_createdAt",
                table: "exchange_events",
                columns: new[] { "exchangeId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_exchanges_code",
                table: "exchanges",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exchanges_courierId",
                table: "exchanges",
                column: "courierId");

            migrationBuilder.CreateIndex(
                name: "IX_exchanges_createdById",
                table: "exchanges",
                column: "createdById");

            migrationBuilder.CreateIndex(
                name: "IX_exchanges_newParcelId",
                table: "exchanges",
                column: "newParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exchanges_originalParcelId",
                table: "exchanges",
                column: "originalParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_exchanges_status",
                table: "exchanges",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_field_changes_at",
                table: "field_changes",
                column: "at");

            migrationBuilder.CreateIndex(
                name: "IX_field_changes_authorId",
                table: "field_changes",
                column: "authorId");

            migrationBuilder.CreateIndex(
                name: "IX_field_changes_entity_entityId_at",
                table: "field_changes",
                columns: new[] { "entity", "entityId", "at" });

            migrationBuilder.CreateIndex(
                name: "IX_idempotent_requests_createdAt",
                table: "idempotent_requests",
                column: "createdAt");

            migrationBuilder.CreateIndex(
                name: "IX_idempotent_requests_userId_key",
                table: "idempotent_requests",
                columns: new[] { "userId", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_adjustments_deliveryNoteId",
                table: "invoice_adjustments",
                column: "deliveryNoteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_adjustments_invoiceId",
                table: "invoice_adjustments",
                column: "invoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_adjustments_kind",
                table: "invoice_adjustments",
                column: "kind");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_lines_invoicedParcelId",
                table: "invoice_lines",
                column: "invoicedParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_lines_invoiceId_parcelId",
                table: "invoice_lines",
                columns: new[] { "invoiceId", "parcelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_lines_parcelId",
                table: "invoice_lines",
                column: "parcelId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_agencyId_createdAt",
                table: "invoices",
                columns: new[] { "agencyId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_clientId_createdAt",
                table: "invoices",
                columns: new[] { "clientId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_code",
                table: "invoices",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_issuedById",
                table: "invoices",
                column: "issuedById");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_paidById",
                table: "invoices",
                column: "paidById");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_status",
                table: "invoices",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_label_jobs_expiresAt",
                table: "label_jobs",
                column: "expiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_label_jobs_requestedById_createdAt",
                table: "label_jobs",
                columns: new[] { "requestedById", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_preferences_userId",
                table: "notification_preferences",
                column: "userId");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_userId_createdAt",
                table: "notifications",
                columns: new[] { "userId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_userId_readAt",
                table: "notifications",
                columns: new[] { "userId", "readAt" });

            migrationBuilder.CreateIndex(
                name: "IX_order_lines_orderId_productId",
                table: "order_lines",
                columns: new[] { "orderId", "productId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_lines_productId",
                table: "order_lines",
                column: "productId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_agencyId",
                table: "orders",
                column: "agencyId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_clientId_createdAt",
                table: "orders",
                columns: new[] { "clientId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_clientId_externalRef",
                table: "orders",
                columns: new[] { "clientId", "externalRef" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_code",
                table: "orders",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_destinationCityId",
                table: "orders",
                column: "destinationCityId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_destinationDistrictId",
                table: "orders",
                column: "destinationDistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_parcelId",
                table: "orders",
                column: "parcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_status",
                table: "orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_parcel_events_authorId",
                table: "parcel_events",
                column: "authorId");

            migrationBuilder.CreateIndex(
                name: "IX_parcel_events_cityId",
                table: "parcel_events",
                column: "cityId");

            migrationBuilder.CreateIndex(
                name: "IX_parcel_events_parcelId_createdAt",
                table: "parcel_events",
                columns: new[] { "parcelId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_parcel_events_statusId_createdAt",
                table: "parcel_events",
                columns: new[] { "statusId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_parcel_fee_changes_authorId",
                table: "parcel_fee_changes",
                column: "authorId");

            migrationBuilder.CreateIndex(
                name: "IX_parcel_fee_changes_createdAt",
                table: "parcel_fee_changes",
                column: "createdAt");

            migrationBuilder.CreateIndex(
                name: "IX_parcel_fee_changes_parcelId_createdAt",
                table: "parcel_fee_changes",
                columns: new[] { "parcelId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_parcel_imports_clientId",
                table: "parcel_imports",
                column: "clientId");

            migrationBuilder.CreateIndex(
                name: "IX_parcel_imports_createdAt",
                table: "parcel_imports",
                column: "createdAt");

            migrationBuilder.CreateIndex(
                name: "IX_parcel_imports_createdById",
                table: "parcel_imports",
                column: "createdById");

            migrationBuilder.CreateIndex(
                name: "IX_parcel_items_productId",
                table: "parcel_items",
                column: "productId");

            migrationBuilder.CreateIndex(
                name: "IX_parcel_notes_authorId",
                table: "parcel_notes",
                column: "authorId");

            migrationBuilder.CreateIndex(
                name: "IX_parcel_notes_parcelId_createdAt",
                table: "parcel_notes",
                columns: new[] { "parcelId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_parcel_statuses_key",
                table: "parcel_statuses",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_parcel_transitions_fromStatusId",
                table: "parcel_transitions",
                column: "fromStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_parcel_transitions_toStatusId",
                table: "parcel_transitions",
                column: "toStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_parcels_agencyId_createdAt",
                table: "parcels",
                columns: new[] { "agencyId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_parcels_clientId_createdAt",
                table: "parcels",
                columns: new[] { "clientId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_parcels_clientId_externalCode",
                table: "parcels",
                columns: new[] { "clientId", "externalCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_parcels_clientId_reimbursedByCourier",
                table: "parcels",
                columns: new[] { "clientId", "reimbursedByCourier" });

            migrationBuilder.CreateIndex(
                name: "IX_parcels_code",
                table: "parcels",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_parcels_courierCommissionCourierId",
                table: "parcels",
                column: "courierCommissionCourierId");

            migrationBuilder.CreateIndex(
                name: "IX_parcels_courierId",
                table: "parcels",
                column: "courierId");

            migrationBuilder.CreateIndex(
                name: "IX_parcels_currentCityId",
                table: "parcels",
                column: "currentCityId");

            migrationBuilder.CreateIndex(
                name: "IX_parcels_deliveryTypeId",
                table: "parcels",
                column: "deliveryTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_parcels_destinationCityId",
                table: "parcels",
                column: "destinationCityId");

            migrationBuilder.CreateIndex(
                name: "IX_parcels_destinationDistrictId",
                table: "parcels",
                column: "destinationDistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_parcels_financialStatus",
                table: "parcels",
                column: "financialStatus");

            migrationBuilder.CreateIndex(
                name: "IX_parcels_idempotencyKey",
                table: "parcels",
                column: "idempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_parcels_importId",
                table: "parcels",
                column: "importId");

            migrationBuilder.CreateIndex(
                name: "IX_parcels_kind",
                table: "parcels",
                column: "kind");

            migrationBuilder.CreateIndex(
                name: "IX_parcels_originCityId",
                table: "parcels",
                column: "originCityId");

            migrationBuilder.CreateIndex(
                name: "IX_parcels_referenceParcelId",
                table: "parcels",
                column: "referenceParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_parcels_statusId",
                table: "parcels",
                column: "statusId");

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_requests_createdAt",
                table: "password_reset_requests",
                column: "createdAt");

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_requests_email",
                table: "password_reset_requests",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_requests_userId",
                table: "password_reset_requests",
                column: "userId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_agencyId_paidAt",
                table: "payments",
                columns: new[] { "agencyId", "paidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_payments_bankAccountId",
                table: "payments",
                column: "bankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_clientId_paidAt",
                table: "payments",
                columns: new[] { "clientId", "paidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_payments_invoiceId",
                table: "payments",
                column: "invoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_permissions_key",
                table: "permissions",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_days_agencyId_day",
                table: "petty_cash_days",
                columns: new[] { "agencyId", "day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_days_closedById",
                table: "petty_cash_days",
                column: "closedById");

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_days_day",
                table: "petty_cash_days",
                column: "day");

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_days_reopenedById",
                table: "petty_cash_days",
                column: "reopenedById");

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_days_status_day",
                table: "petty_cash_days",
                columns: new[] { "status", "day" });

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_movements_agencyId_occurredAt",
                table: "petty_cash_movements",
                columns: new[] { "agencyId", "occurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_movements_buyerId",
                table: "petty_cash_movements",
                column: "buyerId");

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_movements_createdById",
                table: "petty_cash_movements",
                column: "createdById");

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_movements_decidedById",
                table: "petty_cash_movements",
                column: "decidedById");

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_movements_occurredAt",
                table: "petty_cash_movements",
                column: "occurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_platform_audit_logs_actorId",
                table: "platform_audit_logs",
                column: "actorId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_audit_logs_createdAt",
                table: "platform_audit_logs",
                column: "createdAt");

            migrationBuilder.CreateIndex(
                name: "IX_platform_audit_logs_targetCompanyId_createdAt",
                table: "platform_audit_logs",
                columns: new[] { "targetCompanyId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_pricing_rules_destinationCityId",
                table: "pricing_rules",
                column: "destinationCityId");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_rules_originCityId_destinationCityId",
                table: "pricing_rules",
                columns: new[] { "originCityId", "destinationCityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_attribute_assignments_valueId",
                table: "product_attribute_assignments",
                column: "valueId");

            migrationBuilder.CreateIndex(
                name: "IX_product_attribute_values_attributeId_value",
                table: "product_attribute_values",
                columns: new[] { "attributeId", "value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_attributes_name",
                table: "product_attributes",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_clientId",
                table: "products",
                column: "clientId");

            migrationBuilder.CreateIndex(
                name: "IX_products_reference",
                table: "products",
                column: "reference");

            migrationBuilder.CreateIndex(
                name: "IX_provider_calls_at",
                table: "provider_calls",
                column: "at");

            migrationBuilder.CreateIndex(
                name: "IX_provider_calls_tokenId_alert_alertResolvedAt",
                table: "provider_calls",
                columns: new[] { "tokenId", "alert", "alertResolvedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_provider_calls_tokenId_at",
                table: "provider_calls",
                columns: new[] { "tokenId", "at" });

            migrationBuilder.CreateIndex(
                name: "IX_provider_calls_tokenId_parcelId_occurredAt",
                table: "provider_calls",
                columns: new[] { "tokenId", "parcelId", "occurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_provider_tokens_courierId",
                table: "provider_tokens",
                column: "courierId");

            migrationBuilder.CreateIndex(
                name: "IX_provider_tokens_previousTokenHash",
                table: "provider_tokens",
                column: "previousTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_provider_tokens_tokenHash",
                table: "provider_tokens",
                column: "tokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_provider_tokens_userId",
                table: "provider_tokens",
                column: "userId");

            migrationBuilder.CreateIndex(
                name: "IX_push_tokens_token",
                table: "push_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_push_tokens_userId",
                table: "push_tokens",
                column: "userId");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_sessions_expiresAt",
                table: "refresh_sessions",
                column: "expiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_sessions_familyId",
                table: "refresh_sessions",
                column: "familyId");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_sessions_userId",
                table: "refresh_sessions",
                column: "userId");

            migrationBuilder.CreateIndex(
                name: "IX_return_handover_events_authorId",
                table: "return_handover_events",
                column: "authorId");

            migrationBuilder.CreateIndex(
                name: "IX_return_handover_events_handoverId_createdAt",
                table: "return_handover_events",
                columns: new[] { "handoverId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_return_handover_items_confirmedParcelId",
                table: "return_handover_items",
                column: "confirmedParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_return_handover_items_handoverId_parcelId",
                table: "return_handover_items",
                columns: new[] { "handoverId", "parcelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_return_handover_items_parcelId",
                table: "return_handover_items",
                column: "parcelId");

            migrationBuilder.CreateIndex(
                name: "IX_return_handover_items_reservedParcelId",
                table: "return_handover_items",
                column: "reservedParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_return_handovers_agencyId_createdAt",
                table: "return_handovers",
                columns: new[] { "agencyId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_return_handovers_code",
                table: "return_handovers",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_return_handovers_confirmedById",
                table: "return_handovers",
                column: "confirmedById");

            migrationBuilder.CreateIndex(
                name: "IX_return_handovers_courierId_status",
                table: "return_handovers",
                columns: new[] { "courierId", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_return_handovers_createdById",
                table: "return_handovers",
                column: "createdById");

            migrationBuilder.CreateIndex(
                name: "IX_return_handovers_status",
                table: "return_handovers",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_return_note_parcels_parcelId",
                table: "return_note_parcels",
                column: "parcelId");

            migrationBuilder.CreateIndex(
                name: "IX_return_notes_clientId",
                table: "return_notes",
                column: "clientId");

            migrationBuilder.CreateIndex(
                name: "IX_return_notes_code",
                table: "return_notes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_return_notes_courierId",
                table: "return_notes",
                column: "courierId");

            migrationBuilder.CreateIndex(
                name: "IX_return_notes_status",
                table: "return_notes",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_return_slip_items_deliveredParcelId",
                table: "return_slip_items",
                column: "deliveredParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_return_slip_items_parcelId",
                table: "return_slip_items",
                column: "parcelId");

            migrationBuilder.CreateIndex(
                name: "IX_return_slip_items_reservedParcelId",
                table: "return_slip_items",
                column: "reservedParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_return_slip_items_slipId_parcelId",
                table: "return_slip_items",
                columns: new[] { "slipId", "parcelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_return_slips_agencyId_createdAt",
                table: "return_slips",
                columns: new[] { "agencyId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_return_slips_clientId_status",
                table: "return_slips",
                columns: new[] { "clientId", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_return_slips_code",
                table: "return_slips",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_return_slips_deliveredById",
                table: "return_slips",
                column: "deliveredById");

            migrationBuilder.CreateIndex(
                name: "IX_return_slips_status",
                table: "return_slips",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_permissionId",
                table: "role_permissions",
                column: "permissionId");

            migrationBuilder.CreateIndex(
                name: "IX_route_notes_deliveryNoteId",
                table: "route_notes",
                column: "deliveryNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_route_parcels_parcelId",
                table: "route_parcels",
                column: "parcelId");

            migrationBuilder.CreateIndex(
                name: "IX_routes_cityId",
                table: "routes",
                column: "cityId");

            migrationBuilder.CreateIndex(
                name: "IX_routes_code",
                table: "routes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_routes_courierId",
                table: "routes",
                column: "courierId");

            migrationBuilder.CreateIndex(
                name: "IX_routes_createdAt",
                table: "routes",
                column: "createdAt");

            migrationBuilder.CreateIndex(
                name: "IX_routes_originCityId",
                table: "routes",
                column: "originCityId");

            migrationBuilder.CreateIndex(
                name: "IX_routes_status",
                table: "routes",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_server_errors_at",
                table: "server_errors",
                column: "at");

            migrationBuilder.CreateIndex(
                name: "IX_server_errors_requestId",
                table: "server_errors",
                column: "requestId");

            migrationBuilder.CreateIndex(
                name: "IX_server_errors_route_at",
                table: "server_errors",
                columns: new[] { "route", "at" });

            migrationBuilder.CreateIndex(
                name: "IX_settings_agencyId_key",
                table: "settings",
                columns: new[] { "agencyId", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_settings_key",
                table: "settings",
                column: "key");

            migrationBuilder.CreateIndex(
                name: "IX_stock_items_agencyId",
                table: "stock_items",
                column: "agencyId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_items_clientId",
                table: "stock_items",
                column: "clientId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_items_productId_agencyId",
                table: "stock_items",
                columns: new[] { "productId", "agencyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_agencyId",
                table: "stock_movements",
                column: "agencyId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_authorId",
                table: "stock_movements",
                column: "authorId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_parcelId",
                table: "stock_movements",
                column: "parcelId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_productId_createdAt",
                table: "stock_movements",
                columns: new[] { "productId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_stockItemId_createdAt",
                table: "stock_movements",
                columns: new[] { "stockItemId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_storageRequestId",
                table: "stock_movements",
                column: "storageRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_storage_request_items_productId",
                table: "storage_request_items",
                column: "productId");

            migrationBuilder.CreateIndex(
                name: "IX_storage_request_items_requestId_productId",
                table: "storage_request_items",
                columns: new[] { "requestId", "productId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_storage_requests_agencyId",
                table: "storage_requests",
                column: "agencyId");

            migrationBuilder.CreateIndex(
                name: "IX_storage_requests_clientId_status",
                table: "storage_requests",
                columns: new[] { "clientId", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_storage_requests_code",
                table: "storage_requests",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_storage_requests_decidedById",
                table: "storage_requests",
                column: "decidedById");

            migrationBuilder.CreateIndex(
                name: "IX_storage_requests_status",
                table: "storage_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_transfer_order_parcels_parcelId",
                table: "transfer_order_parcels",
                column: "parcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transfer_orders_agencyId",
                table: "transfer_orders",
                column: "agencyId");

            migrationBuilder.CreateIndex(
                name: "IX_transfer_orders_code",
                table: "transfer_orders",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transfer_orders_createdAt",
                table: "transfer_orders",
                column: "createdAt");

            migrationBuilder.CreateIndex(
                name: "IX_transfer_orders_createdById",
                table: "transfer_orders",
                column: "createdById");

            migrationBuilder.CreateIndex(
                name: "IX_user_agencies_agencyId",
                table: "user_agencies",
                column: "agencyId");

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_overrides_permissionId",
                table: "user_permission_overrides",
                column: "permissionId");

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_overrides_userId_permissionId",
                table: "user_permission_overrides",
                columns: new[] { "userId", "permissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_agencyId",
                table: "users",
                column: "agencyId");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_invitedById",
                table: "users",
                column: "invitedById");

            migrationBuilder.CreateIndex(
                name: "IX_users_status",
                table: "users",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_zone_cities_cityId",
                table: "zone_cities",
                column: "cityId");

            migrationBuilder.CreateIndex(
                name: "IX_zone_rates_fromZoneId_toZoneId",
                table: "zone_rates",
                columns: new[] { "fromZoneId", "toZoneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_zone_rates_toZoneId",
                table: "zone_rates",
                column: "toZoneId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agency_cities");

            migrationBuilder.DropTable(
                name: "announcements");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "bl_note_events");

            migrationBuilder.DropTable(
                name: "bl_note_items");

            migrationBuilder.DropTable(
                name: "bulk_jobs");

            migrationBuilder.DropTable(
                name: "cash_remittance_parcels");

            migrationBuilder.DropTable(
                name: "cheque_events");

            migrationBuilder.DropTable(
                name: "claim_attachments");

            migrationBuilder.DropTable(
                name: "client_discount_cities");

            migrationBuilder.DropTable(
                name: "client_documents");

            migrationBuilder.DropTable(
                name: "client_pickup_addresses");

            migrationBuilder.DropTable(
                name: "cod_payout_parcels");

            migrationBuilder.DropTable(
                name: "companies");

            migrationBuilder.DropTable(
                name: "courier_balance_entries");

            migrationBuilder.DropTable(
                name: "courier_cash_session_parcels");

            migrationBuilder.DropTable(
                name: "courier_cities");

            migrationBuilder.DropTable(
                name: "courier_statement_lines");

            migrationBuilder.DropTable(
                name: "delivery_note_parcels");

            migrationBuilder.DropTable(
                name: "delivery_note_removed_parcels");

            migrationBuilder.DropTable(
                name: "delivery_slip_events");

            migrationBuilder.DropTable(
                name: "document_sequences");

            migrationBuilder.DropTable(
                name: "exchange_events");

            migrationBuilder.DropTable(
                name: "field_changes");

            migrationBuilder.DropTable(
                name: "idempotent_requests");

            migrationBuilder.DropTable(
                name: "invoice_adjustments");

            migrationBuilder.DropTable(
                name: "invoice_lines");

            migrationBuilder.DropTable(
                name: "label_jobs");

            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "order_lines");

            migrationBuilder.DropTable(
                name: "parcel_code_sequences");

            migrationBuilder.DropTable(
                name: "parcel_events");

            migrationBuilder.DropTable(
                name: "parcel_fee_changes");

            migrationBuilder.DropTable(
                name: "parcel_items");

            migrationBuilder.DropTable(
                name: "parcel_notes");

            migrationBuilder.DropTable(
                name: "parcel_transitions");

            migrationBuilder.DropTable(
                name: "password_reset_requests");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "petty_cash_days");

            migrationBuilder.DropTable(
                name: "petty_cash_movements");

            migrationBuilder.DropTable(
                name: "platform_audit_logs");

            migrationBuilder.DropTable(
                name: "pricing_rules");

            migrationBuilder.DropTable(
                name: "product_attribute_assignments");

            migrationBuilder.DropTable(
                name: "provider_calls");

            migrationBuilder.DropTable(
                name: "push_tokens");

            migrationBuilder.DropTable(
                name: "refresh_sessions");

            migrationBuilder.DropTable(
                name: "return_handover_events");

            migrationBuilder.DropTable(
                name: "return_handover_items");

            migrationBuilder.DropTable(
                name: "return_note_parcels");

            migrationBuilder.DropTable(
                name: "return_slip_items");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "route_notes");

            migrationBuilder.DropTable(
                name: "route_parcels");

            migrationBuilder.DropTable(
                name: "server_errors");

            migrationBuilder.DropTable(
                name: "settings");

            migrationBuilder.DropTable(
                name: "stock_movements");

            migrationBuilder.DropTable(
                name: "storage_request_items");

            migrationBuilder.DropTable(
                name: "transfer_order_parcels");

            migrationBuilder.DropTable(
                name: "user_agencies");

            migrationBuilder.DropTable(
                name: "user_permission_overrides");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "zone_cities");

            migrationBuilder.DropTable(
                name: "zone_rates");

            migrationBuilder.DropTable(
                name: "bl_notes");

            migrationBuilder.DropTable(
                name: "cheques");

            migrationBuilder.DropTable(
                name: "claim_events");

            migrationBuilder.DropTable(
                name: "client_discounts");

            migrationBuilder.DropTable(
                name: "cod_payouts");

            migrationBuilder.DropTable(
                name: "courier_statements");

            migrationBuilder.DropTable(
                name: "delivery_slips");

            migrationBuilder.DropTable(
                name: "exchanges");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "product_attribute_values");

            migrationBuilder.DropTable(
                name: "provider_tokens");

            migrationBuilder.DropTable(
                name: "return_handovers");

            migrationBuilder.DropTable(
                name: "return_notes");

            migrationBuilder.DropTable(
                name: "return_slips");

            migrationBuilder.DropTable(
                name: "delivery_notes");

            migrationBuilder.DropTable(
                name: "routes");

            migrationBuilder.DropTable(
                name: "stock_items");

            migrationBuilder.DropTable(
                name: "storage_requests");

            migrationBuilder.DropTable(
                name: "transfer_orders");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "cash_remittances");

            migrationBuilder.DropTable(
                name: "courier_cash_sessions");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "claims");

            migrationBuilder.DropTable(
                name: "bank_accounts");

            migrationBuilder.DropTable(
                name: "product_attributes");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "claim_types");

            migrationBuilder.DropTable(
                name: "parcels");

            migrationBuilder.DropTable(
                name: "banks");

            migrationBuilder.DropTable(
                name: "couriers");

            migrationBuilder.DropTable(
                name: "delivery_types");

            migrationBuilder.DropTable(
                name: "districts");

            migrationBuilder.DropTable(
                name: "parcel_imports");

            migrationBuilder.DropTable(
                name: "parcel_statuses");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "agencies");

            migrationBuilder.DropTable(
                name: "cities");

            migrationBuilder.DropTable(
                name: "zones");
        }
    }
}
