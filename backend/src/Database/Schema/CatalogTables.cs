using Microsoft.EntityFrameworkCore;

namespace SmartHealthcare.Database.Schema;

/// <summary>Danh mục thuốc, dịch vụ CLS và bảng giá (sơ đồ 2).</summary>
internal static class CatalogTables
{
    public static void Configure(ModelBuilder model)
    {
        model.Table("MedicationCatalog", t =>
        {
            t.Id();
            t.Req<string>("MedicationCode", "varchar(30)");
            t.Req<string>("MedicationName", "nvarchar(200)");
            t.Req<string>("ActiveIngredient", "nvarchar(300)");
            t.Enum("DosageForm", 20, "Tablet", "Capsule", "Liquid", "Injection");
            t.Opt<string>("Strength", "nvarchar(50)");
            t.Enum("Unit", 10, "Vien", "Goi", "Chai", "Ong");
            t.Flag("IsActive", true);
            t.Unique("MedicationCode");
        });

        model.Table("PriceList", t =>
        {
            t.Id();
            t.Req<string>("PriceListCode", "varchar(30)");
            t.Req<string>("PriceListName", "nvarchar(150)");
            t.Currency();
            t.Utc("EffectiveFromUtc");
            t.OptUtc("EffectiveToUtc");
            t.Flag("IsActive", true);
            t.Unique("PriceListCode");
            t.Check("CK_PriceList_EffectiveRange", "EffectiveToUtc IS NULL OR EffectiveToUtc > EffectiveFromUtc");
        });

        model.Table("MedicalService", t =>
        {
            t.Id();
            t.Req<string>("ServiceCode", "varchar(30)");
            t.Req<string>("ServiceName", "nvarchar(200)");
            t.Enum("ServiceCategory", 20, "Laboratory", "Radiology", "Ultrasound", "Endoscopy");
            t.Ref("DefaultRoomId", "Room");
            t.Flag("IsMandatoryDefault", false);
            t.Flag("IsActive", true);
            t.Unique("ServiceCode");
        });

        model.Table("MedicalServiceRoom", t =>
        {
            t.Id();
            t.Ref("MedicalServiceId", "MedicalService");
            t.Ref("RoomId", "Room");
            t.Flag("IsDefaultRoom", false);
            t.Flag("IsActive", true);
            t.UniqueKey("UQ_MedicalServiceRoom_ServiceRoom", "MedicalServiceId", "RoomId");
            t.UniqueWhere("UX_MedicalServiceRoom_DefaultPerService", "IsDefaultRoom = 1 AND IsActive = 1", "MedicalServiceId");
        });

        model.Table("PriceListItem", t =>
        {
            t.Id();
            t.Ref("PriceListId", "PriceList");
            t.Enum("ItemType", 20, "ConsultationFee", "CLSService");
            t.OptRef("DoctorRankId", "DoctorRank");
            t.OptRef("DoctorId", "Doctor");
            t.OptRef("MedicalServiceId", "MedicalService");
            t.Money("UnitPrice");
            t.Flag("IsActive", true);
            t.Rule("UnitPrice", "UnitPrice >= 0");
            // ConsultationFee: giá theo hạng hoặc giá riêng bác sĩ, đúng một; CLSService: chỉ MedicalServiceId.
            t.Check("CK_PriceListItem_Target", """
                (ItemType = N'ConsultationFee' AND MedicalServiceId IS NULL
                    AND ((DoctorRankId IS NOT NULL AND DoctorId IS NULL) OR (DoctorRankId IS NULL AND DoctorId IS NOT NULL)))
                OR (ItemType = N'CLSService' AND MedicalServiceId IS NOT NULL AND DoctorRankId IS NULL AND DoctorId IS NULL)
                """);
        });
    }
}
