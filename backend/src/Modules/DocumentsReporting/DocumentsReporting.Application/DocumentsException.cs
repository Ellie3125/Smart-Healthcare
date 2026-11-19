namespace DocumentsReporting.Application;

public enum DocumentsErrorCode
{
    InvalidFileType,
    FileTooLarge,
    TicketNotFound,
    TicketRoomMismatch,
    InvalidTicketStatus,
    FileNotFound,
    AccessDenied,
    ResultNotPublished,
    DoctorNotConcluded,
    RoomCannotViewPublished,
    InvalidOperation,
    StorageError
}

public sealed class DocumentsException(DocumentsErrorCode errorCode, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public DocumentsErrorCode ErrorCode { get; } = errorCode;
}
