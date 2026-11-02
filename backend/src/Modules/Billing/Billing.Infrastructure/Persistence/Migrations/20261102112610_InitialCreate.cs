using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "billing");

            migrationBuilder.CreateTable(
                name: "PaymentIntent",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntentNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PayerAppUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TotalExpectedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PaymentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentIntent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriceList",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PriceListCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PriceListName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceList", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTransaction",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentIntentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionReference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TransactionAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PaymentStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProviderRawData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransaction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTransaction_PaymentIntent_PaymentIntentId",
                        column: x => x.PaymentIntentId,
                        principalSchema: "billing",
                        principalTable: "PaymentIntent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PriceListItem",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PriceListId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DoctorRankId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MedicalServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceListItem", x => x.Id);
                    table.CheckConstraint("CK_PriceListItem_ItemType_Targets", "([ItemType] = 'ConsultationFee' AND [MedicalServiceId] IS NULL AND ([DoctorRankId] IS NOT NULL OR [DoctorId] IS NOT NULL)) OR ([ItemType] = 'CLSService' AND [MedicalServiceId] IS NOT NULL AND [DoctorRankId] IS NULL AND [DoctorId] IS NULL)");
                    table.CheckConstraint("CK_PriceListItem_UnitPrice_NonNegative", "[UnitPrice] >= 0");
                    table.ForeignKey(
                        name: "FK_PriceListItem_PriceList_PriceListId",
                        column: x => x.PriceListId,
                        principalSchema: "billing",
                        principalTable: "PriceList",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentEvent",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentEvent_PaymentTransaction_PaymentTransactionId",
                        column: x => x.PaymentTransactionId,
                        principalSchema: "billing",
                        principalTable: "PaymentTransaction",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BillingItem",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ServiceOrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PriceListItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ItemNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillingItem_PriceListItem_PriceListItemId",
                        column: x => x.PriceListItemId,
                        principalSchema: "billing",
                        principalTable: "PriceListItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAllocation",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillingItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AllocatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAllocation", x => x.Id);
                    table.CheckConstraint("CK_PaymentAllocation_AllocatedAmount_Positive", "[AllocatedAmount] > 0");
                    table.ForeignKey(
                        name: "FK_PaymentAllocation_BillingItem_BillingItemId",
                        column: x => x.BillingItemId,
                        principalSchema: "billing",
                        principalTable: "BillingItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentAllocation_PaymentTransaction_PaymentTransactionId",
                        column: x => x.PaymentTransactionId,
                        principalSchema: "billing",
                        principalTable: "PaymentTransaction",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefundRequest",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillingItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalPaymentTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundReason = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PolicyRuleApplied = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecisionByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefundRequest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefundRequest_BillingItem_BillingItemId",
                        column: x => x.BillingItemId,
                        principalSchema: "billing",
                        principalTable: "BillingItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RefundRequest_PaymentTransaction_OriginalPaymentTransactionId",
                        column: x => x.OriginalPaymentTransactionId,
                        principalSchema: "billing",
                        principalTable: "PaymentTransaction",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefundTransaction",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RefundRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalPaymentTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderRefundTransactionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RefundedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProviderResponsePayload = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefundTransaction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefundTransaction_PaymentTransaction_OriginalPaymentTransactionId",
                        column: x => x.OriginalPaymentTransactionId,
                        principalSchema: "billing",
                        principalTable: "PaymentTransaction",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RefundTransaction_RefundRequest_RefundRequestId",
                        column: x => x.RefundRequestId,
                        principalSchema: "billing",
                        principalTable: "RefundRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillingItem_AppointmentId",
                schema: "billing",
                table: "BillingItem",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingItem_EncounterId",
                schema: "billing",
                table: "BillingItem",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingItem_PriceListItemId",
                schema: "billing",
                table: "BillingItem",
                column: "PriceListItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingItem_ServiceOrderItemId",
                schema: "billing",
                table: "BillingItem",
                column: "ServiceOrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocation_BillingItemId",
                schema: "billing",
                table: "PaymentAllocation",
                column: "BillingItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocation_PaymentTransactionId",
                schema: "billing",
                table: "PaymentAllocation",
                column: "PaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEvent_PayloadHash",
                schema: "billing",
                table: "PaymentEvent",
                column: "PayloadHash");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEvent_PaymentTransactionId",
                schema: "billing",
                table: "PaymentEvent",
                column: "PaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEvent_Provider_EventId",
                schema: "billing",
                table: "PaymentEvent",
                columns: new[] { "Provider", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntent_BookingAttemptId",
                schema: "billing",
                table: "PaymentIntent",
                column: "BookingAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntent_EncounterId",
                schema: "billing",
                table: "PaymentIntent",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntent_IntentNumber",
                schema: "billing",
                table: "PaymentIntent",
                column: "IntentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntent_PatientId",
                schema: "billing",
                table: "PaymentIntent",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntent_PayerAppUserId",
                schema: "billing",
                table: "PaymentIntent",
                column: "PayerAppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransaction_PaymentIntentId",
                schema: "billing",
                table: "PaymentTransaction",
                column: "PaymentIntentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransaction_ProviderTransactionId",
                schema: "billing",
                table: "PaymentTransaction",
                column: "ProviderTransactionId",
                unique: true,
                filter: "[ProviderTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransaction_TransactionReference",
                schema: "billing",
                table: "PaymentTransaction",
                column: "TransactionReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceList_PriceListCode",
                schema: "billing",
                table: "PriceList",
                column: "PriceListCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceListItem_DoctorId",
                schema: "billing",
                table: "PriceListItem",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceListItem_DoctorRankId",
                schema: "billing",
                table: "PriceListItem",
                column: "DoctorRankId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceListItem_MedicalServiceId",
                schema: "billing",
                table: "PriceListItem",
                column: "MedicalServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceListItem_PriceListId",
                schema: "billing",
                table: "PriceListItem",
                column: "PriceListId");

            migrationBuilder.CreateIndex(
                name: "IX_RefundRequest_AppointmentId",
                schema: "billing",
                table: "RefundRequest",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RefundRequest_BillingItemId",
                schema: "billing",
                table: "RefundRequest",
                column: "BillingItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RefundRequest_DecisionByStaffId",
                schema: "billing",
                table: "RefundRequest",
                column: "DecisionByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_RefundRequest_OriginalPaymentTransactionId",
                schema: "billing",
                table: "RefundRequest",
                column: "OriginalPaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_RefundTransaction_OriginalPaymentTransactionId",
                schema: "billing",
                table: "RefundTransaction",
                column: "OriginalPaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_RefundTransaction_ProviderRefundTransactionId",
                schema: "billing",
                table: "RefundTransaction",
                column: "ProviderRefundTransactionId",
                unique: true,
                filter: "[ProviderRefundTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RefundTransaction_RefundRequestId",
                schema: "billing",
                table: "RefundTransaction",
                column: "RefundRequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentAllocation",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "PaymentEvent",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "RefundTransaction",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "RefundRequest",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "BillingItem",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "PaymentTransaction",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "PriceListItem",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "PaymentIntent",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "PriceList",
                schema: "billing");
        }
    }
}
