using Microsoft.EntityFrameworkCore;

namespace SmartHealthcare.Database.Schema;

/// <summary>Trạm phục vụ, vé hàng đợi và phân bổ trạm đang hoạt động (sơ đồ 3).</summary>
internal static class QueueTables
{
    public static void Configure(ModelBuilder model)
    {
        model.Table("ServiceStation", t =>
        {
            t.Id();
            t.Ref("RoomId", "Room");
            t.Req<string>("StationCode", "varchar(30)");
            t.Req<string>("StationName", "nvarchar(150)");
            t.Flag("IsActive", true);
            t.Unique("StationCode");
        });

        model.Table("QueueTicket", t =>
        {
            t.Id();
            t.Ref("EncounterId", "Encounter");
            t.Ref("TargetRoomId", "Room");
            t.OptRef("TargetStationId", "ServiceStation");       // null cho tới khi được phân trạm
            t.OptRef("ServiceOrderId", "ServiceOrder");          // vé quay lại bác sĩ
            t.OptRef("ServiceOrderItemId", "ServiceOrderItem");  // vé CLS
            t.Req<string>("TicketNumber", "varchar(20)");
            t.Enum("TicketType", 20, "InitialConsultation", "CLS", "ReturnConsultation");
            t.Req<int>("SequenceNumber", "int");
            t.Enum("QueueStatus", 10, "Waiting", "Called", "Deferred", "InProgress", "Finished", "Cancelled").Default("N'Waiting'");
            t.Utc("IssuedAtUtc");
            t.OptUtc("CalledAtUtc");
            t.OptUtc("ServedAtUtc");
            t.OptUtc("CompletedAtUtc");
            t.RowVersion();
            t.Rule("SequenceNumber", "SequenceNumber >= 1");
            t.Check("CK_QueueTicket_Link", """
                (TicketType = N'InitialConsultation' AND ServiceOrderId IS NULL AND ServiceOrderItemId IS NULL)
                OR (TicketType = N'CLS' AND ServiceOrderItemId IS NOT NULL AND ServiceOrderId IS NULL)
                OR (TicketType = N'ReturnConsultation' AND ServiceOrderId IS NOT NULL AND ServiceOrderItemId IS NULL)
                """);
        });

        model.Table("ActiveStationAllocation", t =>
        {
            t.Ref("StationId", "ServiceStation");
            t.Ref("CurrentPatientId", "Patient");
            t.Ref("CurrentTicketId", "QueueTicket");
            t.Utc("AllocatedAtUtc");
            t.HasKey("StationId").HasName("PK_ActiveStationAllocation");
            t.Unique("CurrentPatientId");
            t.Unique("CurrentTicketId");
        });

        model.Table("ActivePatientEngagement", t =>
        {
            t.Ref("PatientId", "Patient");
            t.Ref("EncounterId", "Encounter");
            t.Ref("CurrentStationId", "ServiceStation");
            t.Ref("CurrentTicketId", "QueueTicket");
            t.Utc("AcquiredAtUtc");
            t.HasKey("PatientId").HasName("PK_ActivePatientEngagement");
            t.Unique("CurrentStationId");
            t.Unique("CurrentTicketId");
        });
    }
}
