import React from 'react';
import { AlertTriangle, RefreshCw } from 'lucide-react';
import { Dialog } from './Dialog';
import { Button } from './Button';

export interface VersionConflictDialogProps {
  open: boolean;
  onClose: () => void;
  onReloadLatest: () => void;
  entityName?: string;
  currentVersion?: number;
  serverDetail?: string;
}

/**
 * Dialog chuẩn cho lỗi HTTP 409 Conflict (`VERSION_CONFLICT`) — tuyệt đối không tự ghi đè dữ liệu.
 */
export const VersionConflictDialog: React.FC<VersionConflictDialogProps> = ({
  open,
  onClose,
  onReloadLatest,
  entityName = 'dữ liệu',
  currentVersion,
  serverDetail,
}) => (
  <Dialog
    open={open}
    onClose={onClose}
    title="Xung đột phiên bản dữ liệu (409)"
    description="Bản ghi đã được cập nhật từ một phiên làm việc khác."
    footer={
      <>
        <Button variant="outline" onClick={onClose}>
          Giữ màn hình hiện tại để đối chiếu
        </Button>
        <Button
          variant="primary"
          leftIcon={<RefreshCw className="h-4 w-4" />}
          onClick={onReloadLatest}
        >
          Tải lại bản mới nhất
        </Button>
      </>
    }
  >
    <div className="flex items-start gap-3 rounded-xl border border-[#fcd34d] bg-[#fef3c7] p-4 text-[#9a5b00]">
      <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0" aria-hidden="true" />
      <div className="space-y-1.5 text-sm">
        <p className="font-semibold">Không thể lưu thay đổi lên {entityName}</p>
        <p>
          Hệ thống phát hiện {entityName} đã thay đổi kể từ lúc bạn mở màn hình này. Để tránh ghi đè nhầm
          thông tin lâm sàng hoặc giao dịch, thay đổi của bạn chưa được lưu.
        </p>
        {currentVersion !== undefined ? (
          <p className="text-xs tabular-nums">Phiên bản mới trên máy chủ: v{currentVersion}</p>
        ) : null}
        {serverDetail ? <p className="text-xs opacity-90">Chi tiết: {serverDetail}</p> : null}
      </div>
    </div>
  </Dialog>
);
