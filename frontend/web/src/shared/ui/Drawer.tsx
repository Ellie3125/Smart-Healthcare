import React, { useEffect } from 'react';
import { X } from 'lucide-react';

export const Drawer: React.FC<{
  open: boolean;
  onClose: () => void;
  title: string;
  children: React.ReactNode;
  footer?: React.ReactNode;
}> = ({ open, onClose, title, children, footer }) => {
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex justify-end bg-[#0f1f18]/45" role="dialog" aria-modal="true" aria-label={title}>
      <div className="flex h-full w-full max-w-xl flex-col bg-white shadow-2xl">
        <div className="flex items-center justify-between border-b border-[#e9f2ed] px-5 py-4">
          <h2 className="text-base font-semibold text-[#0f1f18]">{title}</h2>
          <button
            type="button"
            onClick={onClose}
            aria-label="Đóng ngăn trượt"
            className="rounded-lg p-1.5 text-[#44544c] hover:bg-[#e9f2ed]"
          >
            <X className="h-4 w-4" aria-hidden="true" />
          </button>
        </div>
        <div className="flex-1 overflow-y-auto p-5 text-sm">{children}</div>
        {footer ? <div className="border-t border-[#e9f2ed] px-5 py-3.5">{footer}</div> : null}
      </div>
    </div>
  );
};
