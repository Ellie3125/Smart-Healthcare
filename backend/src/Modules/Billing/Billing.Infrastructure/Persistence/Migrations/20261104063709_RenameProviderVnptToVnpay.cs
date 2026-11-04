using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameProviderVnptToVnpay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Provider lưu dạng string (HasConversion<string>), nên chỉ cần đổi dữ liệu, không đổi schema.
            migrationBuilder.Sql("UPDATE [billing].[PaymentIntent] SET [Provider] = N'VNPAY' WHERE [Provider] = N'VNPT';");
            migrationBuilder.Sql("UPDATE [billing].[PaymentEvent] SET [Provider] = N'VNPAY' WHERE [Provider] = N'VNPT';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [billing].[PaymentIntent] SET [Provider] = N'VNPT' WHERE [Provider] = N'VNPAY';");
            migrationBuilder.Sql("UPDATE [billing].[PaymentEvent] SET [Provider] = N'VNPT' WHERE [Provider] = N'VNPAY';");
        }
    }
}
