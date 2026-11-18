# Contract DocumentsReporting — tệp nháp phòng CLS

Phiên bản v0.1 Draft,18/11/2026. Contract hóa endpoint đã tồn tại trên dev; không thêm route hoặc thay đổi nghiệp vụ. Nguồn: DocumentsEndpoints, DocumentsService, ResultAttachmentDto, ERD documents.ResultAttachment và Scheduling QueueTicket. Contract báo cáo là tài liệu riêng trong PR#161.

## Quyền và định danh

Nhóm `/api/documents/staff/room` dùng RoomAccountOnly, kênh Staff. 401 nếu ẩn danh/token không hợp lệ;403 nếu sai vai trò/kênh. Handler cần claim room_id hợp lệ; nghiệp vụ xác minh vé thuộc phòng qua ISchedulingModule.ValidateClsTicketForRoomAsync. Chỉ tệp Draft của phòng mình; tệp Published không được phòng xem lại/xóa. Quy tắc phiên thiết bị do Identity kiểm; contract này không mở rộng quyền phòng.

`queueTicketId` là Scheduling.QueueTicket.Id, không phải Clinical.ServiceOrderItem.Id hoặc CLSResult.Id. Giữ queueTicketId cùng fileId khi frontend gọi xóa. Service hiện tìm tệp bằng fileId rồi kiểm vé gốc của tệp; path queueTicketId không thay được vé gốc hoặc quyền phòng.

## GET `/api/documents/staff/room/results/{queueTicketId}/files`

Không body/query. Response200 là mảng ResultAttachmentDto (camelCase):
`{id,clsResultId,queueTicketId,encounterId,patientId,fileName,contentType,sizeBytes,sha256Hash,attachmentVersion,uploadStatus,isPublished,uploadedAtUtc,fileId,mimeType,uploadedAt}`.

fileId alias của id; mimeType alias contentType; uploadedAt alias uploadedAtUtc. Timestamp UTC ISO8601; uploadedAt nullable khi chưa upload xong. Chỉ trả IsPublished=false và UploadStatus khác Removed. Không trả URL/key truy cập object storage. Không có tệp nháp trả mảng rỗng.

403 khi thiếu room_id, vé không thuộc phòng hoặc không xác minh được vé; lỗi backend dùng ProblemDetails theo handler hiện có. Không giả lỗi404 cho trường hợp mà service hiện trả TicketRoomMismatch403.

## DELETE `/api/documents/staff/room/results/{queueTicketId}/files/{fileId}`

Không body. Response200 `{ "success": true }`, không phải JSON boolean. Service tìm fileId, kiểm chưa Published và vé gốc thuộc phòng, đánh dấu Removed rồi lưu metadata. Xóa vật lý trên R2 có thể thất bại sau metadata, service hiện giữ Removed và bỏ qua lỗi xóa vật lý; client không gọi lại để suy trạng thái xóa vật lý.

404 nếu file không tồn tại/đã Removed;403 nếu sai phòng;409 InvalidOperation nếu đã Published. ProblemDetails hiện có title/status/detail, không cam kết extension code mà handler chưa trả. Không thay quy tắc transaction/audit/storage trong đợt này.

## Ánh xạ cho frontend

| Route web đang gọi | Route backend hiện có / khác biệt |
|---|---|
| GET /api/documents/staff/room/tickets/{queueTicketId}/files | GET /api/documents/staff/room/results/{queueTicketId}/files; cùng mảng ResultAttachmentDto |
| DELETE /api/documents/staff/room/files/{fileId} | DELETE /api/documents/staff/room/results/{queueTicketId}/files/{fileId}; client giữ queueTicketId theo hàng/file, đọc response.success thay boolean |

Không tạo alias. Không cần schema, migration hay sửa lõi. API đọc/xóa chỉ được đối chiếu với code/test; UI/E2E và R2 thật chưa kiểm trong đợt này.

