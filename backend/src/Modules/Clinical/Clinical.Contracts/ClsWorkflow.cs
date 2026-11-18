namespace Clinical.Contracts;

public sealed record ClsServiceCommand(Guid ServiceOrderItemId, Guid RoomId, Guid PerformedByUserId, Guid? OperationId = null);
