namespace SmartHealthcare.Identity.Domain;


public enum Gender
{
    Male,
    Female,
    Other
}

public enum Relationship
{
    Self,
    Child,
    Parent,
    Spouse,
    Other
}

public enum RoomType
{
    Clinical,
    Laboratory,
    XRay,
    Ultrasound,
    Endoscopy
}

public enum ShiftName
{
    Morning,
    Afternoon,
    FullDay
}

public enum StaffType
{
    Doctor,
    Nurse,
    Receptionist,
    Technician
}

public enum VerificationStatus
{
    Pending,
    Verified,
    Rejected,
    Revoked
}
