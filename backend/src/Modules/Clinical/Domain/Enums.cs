namespace SmartHealthcare.Clinical.Domain;


public enum DiagnosisType
{
    Initial,
    Final
}

public enum EncounterClinicalRecordStatus
{
    Draft,
    Published
}

public enum EncounterStatus
{
    /// <summary>Từ lúc check-in đến khi chỉ định CLS hoặc kết luận.</summary>
    InProgress,
    /// <summary>Đã có ServiceOrder ở trạng thái Confirmed.</summary>
    WaitingCLS,
    /// <summary>Đã phát vé quay lại gặp bác sĩ ban đầu.</summary>
    ReturningDoctor,
    /// <summary>Trạng thái cuối: đã kết luận.</summary>
    Completed,
    /// <summary>Trạng thái cuối: do nhân viên có thẩm quyền đóng, không tự gán theo giờ.</summary>
    Abandoned
}

public enum ItemStatus
{
    Ordered,
    SampleCollected,
    InProgress,
    ServiceFinished,
    Completed,
    Cancelled
}

public enum OrderBatchStatus
{
    Draft,
    Confirmed,
    Completed,
    Cancelled
}

public enum PrescriptionStatus
{
    Draft,
    Issued,
    Cancelled
}

public enum ResultStatus
{
    Draft,
    Approved,
    Published,
    Cancelled
}

public enum Route
{
    Oral,
    Topical,
    Injection,
    Inhalation
}
