import { apiClient } from './client';
import { apiDelete, apiGet } from '../shared/lib/http';
import { createIdempotencyKey } from '../shared/lib/idempotency';
import { ApiHttpError, ProblemDetails } from '../shared/lib/errors';

export interface StoredFileDto {
  fileId: string;
  queueTicketId: string;
  encounterId: string;
  patientId: string;
  clsResultId: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  sha256: string;
  uploadStatus: 'Draft' | 'Uploaded' | 'Removed';
  isPublished: boolean;
  uploadedAtUtc?: string | null;
  attachmentVersion: number;
}

export interface ResultAttachmentDto {
  id: string;
  clsResultId: string;
  queueTicketId: string;
  encounterId: string;
  patientId: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  sha256Hash: string;
  attachmentVersion: number;
  uploadStatus: 'Draft' | 'Uploaded' | 'Removed';
  isPublished: boolean;
  uploadedAtUtc?: string | null;
}

export const documentsApi = {
  /**
   * Tải tệp kết quả theo vé phòng CLS (PDF/JPG/PNG <= 20MB)
   * Client có thể theo dõi tiến trình upload qua callback onProgress (0-100%)
   */
  uploadResultFile: async (
    queueTicketId: string,
    file: File,
    onProgress?: (progressPercent: number) => void,
    idempotencyKey?: string,
  ): Promise<StoredFileDto> => {
    const formData = new FormData();
    formData.append('file', file);

    const key = idempotencyKey ?? createIdempotencyKey(`upload-cls-${queueTicketId}-${file.name}`);

    try {
      const res = await apiClient.post<StoredFileDto>(
        `/api/documents/staff/room/results/${queueTicketId}/files`,
        formData,
        {
          headers: {
            'Idempotency-Key': key,
            'Content-Type': 'multipart/form-data',
          },
          onUploadProgress: (progressEvent) => {
            if (progressEvent.total && onProgress) {
              const percentCompleted = Math.round((progressEvent.loaded * 100) / progressEvent.total);
              onProgress(percentCompleted);
            }
          },
        },
      );
      return res.data;
    } catch (err: unknown) {
      if (typeof err === 'object' && err !== null && 'response' in err) {
        const resp = (err as { response?: { status?: number; data?: ProblemDetails } }).response;
        const status = resp?.status ?? 500;
        const problem = resp?.data;
        const code = (problem?.code || (status === 413 ? 'PAYLOAD_TOO_LARGE' : status === 415 ? 'UNSUPPORTED_MEDIA_TYPE' : status === 403 ? 'FORBIDDEN' : 'HTTP_ERROR')).toUpperCase();
        throw new ApiHttpError(
          status,
          code,
          problem?.detail || problem?.title || `Lỗi tải tệp (${status})`,
          problem,
        );
      }
      throw err;
    }
  },

  /**
   * Lấy danh sách tệp đính kèm của một vé phòng CLS
   * (Chỉ tài khoản phòng xem được tệp Draft của chính phòng mình, không xem lại tệp Published)
   */
  getTicketFiles: (queueTicketId: string): Promise<ResultAttachmentDto[]> =>
    apiGet<ResultAttachmentDto[]>(`/api/documents/staff/room/tickets/${queueTicketId}/files`),

  /**
   * Xóa tệp nháp của phòng trước khi phát hành kết quả
   */
  deleteDraftFile: (fileId: string): Promise<boolean> =>
    apiDelete<boolean>(`/api/documents/staff/room/files/${fileId}`),
};
