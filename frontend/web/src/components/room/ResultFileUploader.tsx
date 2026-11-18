import React, { useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertCircle, CheckCircle, FileText, RefreshCw, Trash2, Upload, X } from 'lucide-react';
import { documentsApi, ResultAttachmentDto, StoredFileDto } from '../../api/documentsApi';
import {
  Button,
  ConfirmDialog,
  StatusPill,
  useToast,
} from '../../shared/ui';
import { extractFriendlyError } from '../../shared/lib/errors';

const MAX_FILE_SIZE_BYTES = 20 * 1024 * 1024; // 20 MB (FR21)
const ALLOWED_EXTENSIONS = ['.pdf', '.jpg', '.jpeg', '.png'];
const ALLOWED_MIME_TYPES = new Set(['application/pdf', 'image/jpeg', 'image/png']);

export interface PatientMatchInfo {
  fullName: string;
  dateOfBirth?: string;
  ticketCode: string;
}

export interface ResultFileUploaderProps {
  queueTicketId: string;
  patientInfo?: PatientMatchInfo;
  onUploadSuccess?: (file: StoredFileDto) => void;
  disabled?: boolean;
}

interface UploadQueueItem {
  id: string;
  file: File;
  progress: number;
  status: 'pending' | 'uploading' | 'success' | 'error';
  errorMessage?: string;
  result?: StoredFileDto;
}

export const ResultFileUploader: React.FC<ResultFileUploaderProps> = ({
  queueTicketId,
  patientInfo,
  onUploadSuccess,
  disabled = false,
}) => {
  const { pushToast } = useToast();
  const queryClient = useQueryClient();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [queue, setQueue] = useState<UploadQueueItem[]>([]);
  const [deleteCandidate, setDeleteCandidate] = useState<ResultAttachmentDto | null>(null);

  // Lấy danh sách tệp kết quả Draft từ server
  const ticketFilesQuery = useQuery({
    queryKey: ['staff', 'ticket-files', queueTicketId],
    queryFn: () => documentsApi.getTicketFiles(queueTicketId),
    enabled: Boolean(queueTicketId),
  });

  // Mutation xóa tệp Draft
  const deleteMutation = useMutation({
    mutationFn: (fileId: string) => documentsApi.deleteDraftFile(queueTicketId, fileId),
    onSuccess: () => {
      pushToast('Đã xóa tệp nháp thành công.', 'success');
      setDeleteCandidate(null);
      queryClient.invalidateQueries({ queryKey: ['staff', 'ticket-files', queueTicketId] });
      queryClient.invalidateQueries({ queryKey: ['staff', 'room-draft'] });
    },
    onError: (err) => {
      pushToast(extractFriendlyError(err).message, 'danger');
    },
  });

  const uploadSingleItem = async (item: UploadQueueItem) => {
    setQueue((prev) =>
      prev.map((q) => (q.id === item.id ? { ...q, status: 'uploading', progress: 5, errorMessage: undefined } : q)),
    );

    try {
      const stored = await documentsApi.uploadResultFile(
        queueTicketId,
        item.file,
        (progress) => {
          setQueue((prev) =>
            prev.map((q) => (q.id === item.id ? { ...q, progress } : q)),
          );
        },
      );

      setQueue((prev) =>
        prev.map((q) => (q.id === item.id ? { ...q, status: 'success', progress: 100, result: stored } : q)),
      );

      pushToast(`Tải lên thành công: ${item.file.name}`, 'success');
      queryClient.invalidateQueries({ queryKey: ['staff', 'ticket-files', queueTicketId] });
      queryClient.invalidateQueries({ queryKey: ['staff', 'room-draft'] });
      onUploadSuccess?.(stored);
    } catch (err) {
      const friendly = extractFriendlyError(err);
      setQueue((prev) =>
        prev.map((q) =>
          q.id === item.id ? { ...q, status: 'error', progress: 0, errorMessage: friendly.message } : q,
        ),
      );
      pushToast(`Lỗi khi tải ${item.file.name}: ${friendly.message}`, 'danger');
    }
  };

  const handleFilesSelected = (files: FileList | null) => {
    if (!files || files.length === 0) return;

    const newItems: UploadQueueItem[] = [];

    Array.from(files).forEach((file) => {
      const ext = '.' + file.name.split('.').pop()?.toLowerCase();
      const isValidExt = ALLOWED_EXTENSIONS.includes(ext);
      const isValidType = ALLOWED_MIME_TYPES.has(file.type) || isValidExt;

      if (!isValidType) {
        pushToast(`Tệp "${file.name}" không hợp lệ. Chỉ chấp nhận PDF, JPG hoặc PNG.`, 'danger');
        return;
      }

      if (file.size > MAX_FILE_SIZE_BYTES) {
        pushToast(`Tệp "${file.name}" (${(file.size / (1024 * 1024)).toFixed(1)}MB) vượt quá giới hạn tối đa 20MB.`, 'danger');
        return;
      }

      const item: UploadQueueItem = {
        id: `${file.name}-${file.lastModified}-${Math.random().toString(36).slice(2, 7)}`,
        file,
        progress: 0,
        status: 'pending',
      };
      newItems.push(item);
    });

    if (newItems.length > 0) {
      setQueue((prev) => [...prev, ...newItems]);
      // Bắt đầu tải lên từng tệp
      newItems.forEach((it) => uploadSingleItem(it));
    }

    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  const removeFromQueue = (id: string) => {
    setQueue((prev) => prev.filter((q) => q.id !== id));
  };

  const formatFileSize = (bytes: number): string => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  };

  const serverFiles = ticketFilesQuery.data ?? [];

  return (
    <div className="space-y-4">
      {/* Đối chiếu thông tin bệnh nhân */}
      {patientInfo && (
        <div className="rounded-xl border border-[#b2d9c4] bg-[#f0fbf5] p-3 text-xs sm:text-sm text-[#005231]">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <span className="font-semibold">
              Đối chiếu hồ sơ: <span className="font-bold text-[#006a41]">{patientInfo.fullName}</span>
              {patientInfo.dateOfBirth && ` (NS: ${patientInfo.dateOfBirth})`}
            </span>
            <span className="rounded bg-[#d5f4e3] px-2 py-0.5 font-mono font-bold text-[#006a41]">
              Mã vé: {patientInfo.ticketCode}
            </span>
          </div>
          <p className="mt-1 text-[11px] text-[#44544c]">
            Vui lòng kiểm tra kỹ họ tên và mã vé trên chứng từ cận lâm sàng trước khi tải lên.
          </p>
        </div>
      )}

      {/* Vùng kéo thả & chọn tệp */}
      <div className="rounded-2xl border-2 border-dashed border-outline-variant/60 bg-surface-container-low/50 p-5 text-center transition-colors hover:border-primary/60">
        <Upload className="mx-auto h-8 w-8 text-primary" aria-hidden="true" />
        <p className="mt-2 text-sm font-semibold text-on-surface">
          Kéo thả tệp hoặc bấm chọn tệp kết quả
        </p>
        <p className="mt-1 text-xs text-on-surface-variant">
          Chấp nhận định dạng <strong>PDF, JPG, PNG</strong> — Tối đa <strong>20 MB</strong> mỗi tệp (cho phép chọn nhiều tệp)
        </p>

        <input
          ref={fileInputRef}
          type="file"
          id="result-files-input"
          multiple
          accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png"
          disabled={disabled}
          onChange={(e) => handleFilesSelected(e.target.files)}
          className="hidden"
        />

        <div className="mt-3 flex justify-center">
          <Button
            size="sm"
            variant="secondary"
            disabled={disabled}
            onClick={() => fileInputRef.current?.click()}
            leftIcon={<Upload className="h-4 w-4" />}
          >
            Chọn tệp từ máy
          </Button>
        </div>
      </div>

      {/* Tiến trình Upload (Upload Queue) */}
      {queue.length > 0 && (
        <div className="space-y-2 rounded-xl bg-surface-container-lowest p-4 shadow-sm border border-outline-variant/30">
          <h4 className="text-xs font-semibold uppercase tracking-wider text-on-surface-variant">
            Tiến trình tải lên ({queue.length} tệp)
          </h4>
          <div className="space-y-2">
            {queue.map((item) => (
              <div key={item.id} className="rounded-lg bg-surface-container-low/60 p-2.5 text-xs">
                <div className="flex items-center justify-between gap-2">
                  <div className="flex items-center gap-2 min-w-0">
                    <FileText className="h-4 w-4 shrink-0 text-primary" />
                    <span className="truncate font-medium text-on-surface">{item.file.name}</span>
                    <span className="shrink-0 text-on-surface-variant">({formatFileSize(item.file.size)})</span>
                  </div>
                  <div className="flex items-center gap-1.5 shrink-0">
                    {item.status === 'uploading' && (
                      <span className="font-semibold text-primary">{item.progress}%</span>
                    )}
                    {item.status === 'success' && (
                      <span className="inline-flex items-center gap-1 text-emerald-700 font-semibold">
                        <CheckCircle className="h-3.5 w-3.5" /> Xong
                      </span>
                    )}
                    {item.status === 'error' && (
                      <Button
                        size="sm"
                        variant="danger"
                        leftIcon={<RefreshCw className="h-3 w-3" />}
                        onClick={() => uploadSingleItem(item)}
                      >
                        Thử lại
                      </Button>
                    )}
                    <button
                      type="button"
                      onClick={() => removeFromQueue(item.id)}
                      className="p-1 text-on-surface-variant hover:text-on-surface rounded"
                    >
                      <X className="h-3.5 w-3.5" />
                    </button>
                  </div>
                </div>

                {/* Progress bar */}
                {item.status === 'uploading' && (
                  <div className="mt-1.5 h-1.5 w-full overflow-hidden rounded-full bg-outline-variant/30">
                    <div
                      className="h-full bg-primary transition-all duration-200"
                      style={{ width: `${item.progress}%` }}
                    />
                  </div>
                )}

                {item.errorMessage && (
                  <p className="mt-1 flex items-center gap-1 text-[11px] text-red-600 font-medium">
                    <AlertCircle className="h-3 w-3 shrink-0" />
                    {item.errorMessage}
                  </p>
                )}
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Danh sách tệp Draft hiện có trên server */}
      <div className="space-y-2">
        <h4 className="text-xs font-semibold uppercase tracking-wider text-on-surface-variant">
          Tệp đính kèm hiện tại ({serverFiles.length})
        </h4>

        {ticketFilesQuery.isLoading ? (
          <div className="p-3 text-xs text-on-surface-variant animate-pulse">Đang tải danh sách tệp...</div>
        ) : serverFiles.length === 0 ? (
          <p className="rounded-lg bg-surface-container-low/40 p-3 text-xs text-on-surface-variant text-center">
            Chưa có tệp kết quả nào được tải lên cho vé này.
          </p>
        ) : (
          <div className="space-y-1.5">
            {serverFiles.map((f) => (
              <div
                key={f.id}
                className="flex flex-wrap items-center justify-between gap-2 rounded-xl bg-surface-container-lowest p-3 text-xs shadow-sm border border-outline-variant/20"
              >
                <div className="flex items-center gap-2.5 min-w-0">
                  <FileText className="h-4 w-4 shrink-0 text-primary" />
                  <div className="min-w-0">
                    <p className="truncate font-medium text-on-surface">
                      {f.fileName}
                      {f.attachmentVersion > 1 && (
                        <span className="ml-1.5 rounded bg-amber-100 px-1 py-0.2 text-[10px] font-bold text-amber-800">
                          v{f.attachmentVersion} (đính chính)
                        </span>
                      )}
                    </p>
                    <p className="text-[11px] text-on-surface-variant">
                      {formatFileSize(f.sizeBytes)} • {f.contentType} • SHA: <span className="font-mono">{f.sha256Hash?.slice(0, 8)}...</span>
                    </p>
                  </div>
                </div>

                <div className="flex items-center gap-2 shrink-0">
                  <StatusPill status={f.uploadStatus} />
                  {!f.isPublished && !disabled && (
                    <Button
                      size="sm"
                      variant="outline"
                      leftIcon={<Trash2 className="h-3 w-3 text-red-600" />}
                      onClick={() => setDeleteCandidate(f)}
                    >
                      Xóa
                    </Button>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Dialog xác nhận xóa tệp */}
      <ConfirmDialog
        open={Boolean(deleteCandidate)}
        onClose={() => setDeleteCandidate(null)}
        onConfirm={() => {
          if (deleteCandidate) {
            deleteMutation.mutate(deleteCandidate.id);
          }
        }}
        loading={deleteMutation.isPending}
        title="Xác nhận xóa tệp kết quả nháp"
        message={`Bạn có chắc chắn muốn xóa tệp "${deleteCandidate?.fileName}" khỏi kết quả của vé này? Thao tác này sẽ xóa tệp nháp khỏi hệ thống.`}
      />
    </div>
  );
};
