namespace IdentityPatients.Contracts.Dtos;

public sealed record DoctorDto(
    Guid DoctorId,
    string FullName,
    Guid SpecialtyId,
    string SpecialtyCode,
    Guid RankId,
    string RankName,
    Guid? DefaultRoomId,
    bool IsActive
);

public sealed record SpecialtyDto(
    Guid Id,
    string Code,
    string Name
);

public sealed record DoctorRankDto(
    Guid Id,
    string Code,
    string Name,
    int SortOrder
);

public sealed record RoomDto(
    Guid Id,
    string Code,
    string Name,
    string RoomType,
    bool IsActive
);

public sealed record DoctorSearch(
    Guid? SpecialtyId = null,
    Guid? RankId = null,
    string? Keyword = null
);

public sealed record CurrentUserDto(
    Guid Id,
    string UserName,
    string Role,
    Guid? DoctorId,
    Guid? RoomId
);
