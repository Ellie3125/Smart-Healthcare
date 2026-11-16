import React from 'react';
import { Download, Eye, FileText, Lock } from 'lucide-react';
import { Button } from '../shared/ui';
import type { ClsAttachmentMeta } from '../types/clinical';

export interface ClsAttachmentViewerProps {
  resultId: string;
  attachment: ClsAttachmentMeta;
  isDoctorConcluded: boolean;
  onPreview: (resultId: string, fileId: string, fileName: string) => void;
  onDownload: (resultId: string, fileId: string, fileName: string) => void;
  isLoading?: boolean;
}

export const ClsAttachmentViewer: React.FC<ClsAttachmentViewerProps> = ({
  resultId,
  attachment,
  isDoctorConcluded,
  onPreview,
  onDownload,
  isLoading = false,
}) => {
  const formatFileSize = (bytes: number): string => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  };

  return (
    <div className="flex flex-wrap items-center justify-between gap-2.5 rounded-xl border border-outline-variant/30 bg-surface-container-lowest p-3 text-xs shadow-sm">
      <div className="flex items-center gap-2.5 min-w-0">
        <FileText className="h-4 w-4 shrink-0 text-primary" />
        <div className="min-w-0">
          <p className="truncate font-medium text-on-surface">{attachment.fileName}</p>
          <p className="text-[11px] text-on-surface-variant">
            {formatFileSize(attachment.sizeBytes)}
            {attachment.mimeType && ` • ${attachment.mimeType}`}
          </p>
        </div>
      </div>

      <div className="flex items-center gap-2 shrink-0">
        {!isDoctorConcluded ? (
          <span
            className="inline-flex items-center gap-1 rounded-lg border border-amber-200 bg-amber-50 px-2 py-1 text-[11px] font-semibold text-amber-800"
            title="Bác sĩ khám ban đầu chưa hoàn tất kết luận sau CLS. Tệp chưa thể hiển thị."
          >
            <Lock className="h-3 w-3" />
            Chưa được xem
          </span>
        ) : (
          <>
            <Button
              size="sm"
              variant="outline"
              loading={isLoading}
              leftIcon={<Eye className="h-3.5 w-3.5" />}
              onClick={() => onPreview(resultId, attachment.fileId, attachment.fileName)}
            >
              Xem tệp
            </Button>
            <Button
              size="sm"
              variant="secondary"
              loading={isLoading}
              leftIcon={<Download className="h-3.5 w-3.5" />}
              onClick={() => onDownload(resultId, attachment.fileId, attachment.fileName)}
            >
              Tải về
            </Button>
          </>
        )}
      </div>
    </div>
  );
};
