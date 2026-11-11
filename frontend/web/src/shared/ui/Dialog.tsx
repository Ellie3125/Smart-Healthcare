import React, { useEffect } from 'react';
import { X } from 'lucide-react';
import { Button, ButtonVariant } from './Button';

export interface DialogProps {
  open: boolean;
  onClose: () => void;
  title: string;
  description?: string;
  children: React.ReactNode;
  footer?: React.ReactNode;
  maxWidthClass?: string;
}

export const Dialog: React.FC<DialogProps> = ({
  open,
  onClose,
  title,
  description,
  children,
  footer,
  maxWidthClass = 'max-w-lg',
}) => {
  useEffect(() => {
    if (!open) return;
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', handleKey);
    return () => window.removeEventListener('keydown', handleKey);
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-[#0f1f18]/50 p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby="dialog-title"
    >
      <div className={`w-full ${maxWidthClass} rounded-xl border border-[#cddbd3] bg-white shadow-xl`}>
        <div className="flex items-start justify-between gap-3 border-b border-[#e9f2ed] px-5 py-4">
          <div>
            <h2 id="dialog-title" className="text-base font-semibold text-[#0f1f18]">
              {title}
            </h2>
            {description ? <p className="mt-0.5 text-xs text-[#44544c]">{description}</p> : null}
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Đóng hộp thoại"
            className="rounded-lg p-1 text-[#44544c] hover:bg-[#e9f2ed]"
          >
            <X className="h-4 w-4" aria-hidden="true" />
          </button>
        </div>
        <div className="max-h-[75vh] overflow-y-auto px-5 py-4 text-sm text-[#0f1f18]">{children}</div>
        {footer ? (
          <div className="flex flex-wrap justify-end gap-2 border-t border-[#e9f2ed] bg-[#f8fbf9] px-5 py-3 rounded-b-xl">
            {footer}
          </div>
        ) : null}
      </div>
    </div>
  );
};

export interface ConfirmDialogProps {
  open: boolean;
  onClose: () => void;
  onConfirm: () => void | Promise<void>;
  title: string;
  message: React.ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  confirmVariant?: ButtonVariant;
  loading?: boolean;
  disabled?: boolean;
}

export const ConfirmDialog: React.FC<ConfirmDialogProps> = ({
  open,
  onClose,
  onConfirm,
  title,
  message,
  confirmLabel = 'Xác nhận',
  cancelLabel = 'Hủy bỏ',
  confirmVariant = 'primary',
  loading = false,
  disabled = false,
}) => (
  <Dialog
    open={open}
    onClose={loading ? () => undefined : onClose}
    title={title}
    footer={
      <>
        <Button variant="outline" onClick={onClose} disabled={loading}>
          {cancelLabel}
        </Button>
        <Button variant={confirmVariant} onClick={onConfirm} loading={loading} disabled={disabled}>
          {confirmLabel}
        </Button>
      </>
    }
  >
    <div className="space-y-2">{message}</div>
  </Dialog>
);
