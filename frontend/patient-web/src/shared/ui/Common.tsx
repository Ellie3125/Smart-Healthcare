import React, { useEffect, useState } from 'react';
import {
  AlertCircle,
  AlertTriangle,
  CheckCircle2,
  Clock,
  Info,
  Inbox,
  X,
  Check,
} from 'lucide-react';
import { formatVND } from '../lib/money';
import { formatCountdownMMSS, formatDateTimeVN, formatDateVN, formatTimeVN, remainingSecondsUntil } from '../lib/datetime';
import { Button, ButtonVariant } from './Button';

export const Card: React.FC<{
  title?: React.ReactNode;
  subtitle?: React.ReactNode;
  actions?: React.ReactNode;
  children: React.ReactNode;
  className?: string;
}> = ({ title, subtitle, actions, children, className = '' }) => (
  <section className={`rounded-2xl border border-[#bdcabf] bg-white shadow-sm ${className}`}>
    {(title || subtitle || actions) && (
      <header className="flex flex-wrap items-center justify-between gap-3 border-b border-[#e5f8ed] px-5 py-4">
        <div>
          {title ? <h2 className="text-lg font-semibold text-[#0f1f18]">{title}</h2> : null}
          {subtitle ? <p className="mt-0.5 text-sm text-[#3e4941]">{subtitle}</p> : null}
        </div>
        {actions ? <div className="flex items-center gap-2">{actions}</div> : null}
      </header>
    )}
    <div className="p-5">{children}</div>
  </section>
);

export const MoneyText: React.FC<{ amount: number | null | undefined; showZero?: boolean; className?: string }> = ({
  amount,
  showZero = true,
  className = '',
}) => <span className={`tabular-nums font-semibold ${className}`}>{formatVND(amount, { showZero })}</span>;

export const DateTimeText: React.FC<{
  value: string | Date | null | undefined;
  mode?: 'date' | 'time' | 'datetime';
  className?: string;
}> = ({ value, mode = 'datetime', className = '' }) => {
  const formatted =
    mode === 'date' ? formatDateVN(value) : mode === 'time' ? formatTimeVN(value) : formatDateTimeVN(value);
  return <span className={`tabular-nums ${className}`}>{formatted}</span>;
};

export type BannerTone = 'info' | 'success' | 'warning' | 'danger';

const BANNER_CLASSES: Record<BannerTone, string> = {
  info: 'bg-[#e8f0fe] text-[#0b57d0] border-[#bdd4fb]',
  success: 'bg-[#d4f7e2] text-[#005231] border-[#8ef8bb]',
  warning: 'bg-[#fef3c7] text-[#9a5b00] border-[#fcd34d]',
  danger: 'bg-[#ffdad6] text-[#93000a] border-[#ffb4ab]',
};

export const Banner: React.FC<{
  tone?: BannerTone;
  title?: string;
  children: React.ReactNode;
  action?: React.ReactNode;
  className?: string;
}> = ({ tone = 'info', title, children, action, className = '' }) => {
  const Icon =
    tone === 'success' ? CheckCircle2 : tone === 'warning' ? AlertTriangle : tone === 'danger' ? AlertCircle : Info;
  return (
    <div
      role={tone === 'danger' ? 'alert' : 'status'}
      className={`flex flex-wrap items-start justify-between gap-3 rounded-2xl border px-4 py-3.5 text-sm ${BANNER_CLASSES[tone]} ${className}`}
    >
      <div className="flex items-start gap-2.5">
        <Icon className="mt-0.5 h-5 w-5 shrink-0" aria-hidden="true" />
        <div>
          {title ? <p className="font-semibold">{title}</p> : null}
          <div>{children}</div>
        </div>
      </div>
      {action ? <div className="shrink-0">{action}</div> : null}
    </div>
  );
};

export const Skeleton: React.FC<{ rows?: number; className?: string }> = ({ rows = 3, className = '' }) => (
  <div className="space-y-2.5" aria-busy="true" aria-label="Đang tải dữ liệu">
    {Array.from({ length: rows }).map((_, idx) => (
      <div key={idx} className={`h-4 animate-pulse rounded-lg bg-[#daede1] ${className}`} />
    ))}
  </div>
);

export const EmptyState: React.FC<{
  title: string;
  description?: string;
  action?: React.ReactNode;
  icon?: React.ReactNode;
}> = ({ title, description, action, icon }) => (
  <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-[#bdcabf] bg-white px-6 py-10 text-center">
    <div className="mb-3 rounded-full bg-[#e5f8ed] p-3 text-[#006a41]" aria-hidden="true">
      {icon ?? <Inbox className="h-6 w-6" />}
    </div>
    <h3 className="text-base font-semibold text-[#0f1f18]">{title}</h3>
    {description ? <p className="mt-1 max-w-md text-sm text-[#3e4941]">{description}</p> : null}
    {action ? <div className="mt-4">{action}</div> : null}
  </div>
);

export const ErrorState: React.FC<{ title?: string; message: string; onRetry?: () => void }> = ({
  title = 'Đã có sự cố xảy ra',
  message,
  onRetry,
}) => (
  <div
    role="alert"
    className="flex flex-col items-center justify-center rounded-2xl border border-[#ffb4ab] bg-[#fff5f4] px-6 py-8 text-center"
  >
    <AlertCircle className="mb-2 h-8 w-8 text-[#ba1a1a]" aria-hidden="true" />
    <h3 className="text-base font-semibold text-[#93000a]">{title}</h3>
    <p className="mt-1 max-w-md text-sm text-[#93000a]/90">{message}</p>
    {onRetry ? (
      <Button variant="outline" size="sm" onClick={onRetry} className="mt-4">
        Thử lại
      </Button>
    ) : null}
  </div>
);

export const Countdown: React.FC<{
  expiresAt: string;
  label?: string;
  onExpire?: () => void;
  warnUnderSeconds?: number;
}> = ({ expiresAt, label = 'Thời gian giữ chỗ', onExpire, warnUnderSeconds = 60 }) => {
  const [secondsLeft, setSecondsLeft] = useState(() => remainingSecondsUntil(expiresAt));

  useEffect(() => {
    const initial = remainingSecondsUntil(expiresAt);
    setSecondsLeft(initial);
    if (initial <= 0) {
      onExpire?.();
      return;
    }
    const timer = window.setInterval(() => {
      const next = remainingSecondsUntil(expiresAt);
      setSecondsLeft(next);
      if (next <= 0) {
        window.clearInterval(timer);
        onExpire?.();
      }
    }, 1000);
    return () => window.clearInterval(timer);
  }, [expiresAt, onExpire]);

  const expired = secondsLeft <= 0;
  const warning = !expired && secondsLeft <= warnUnderSeconds;

  return (
    <div
      aria-live="polite"
      className={`inline-flex items-center gap-2 rounded-xl border px-3.5 py-2 text-sm font-semibold tabular-nums ${
        expired
          ? 'border-[#ffb4ab] bg-[#ffdad6] text-[#93000a]'
          : warning
            ? 'border-[#fcd34d] bg-[#fef3c7] text-[#9a5b00]'
            : 'border-[#bdd4fb] bg-[#e8f0fe] text-[#0b57d0]'
      }`}
    >
      <Clock className="h-4 w-4 shrink-0" aria-hidden="true" />
      <span>{expired ? 'Đã hết thời gian' : `${label}: ${formatCountdownMMSS(secondsLeft)}`}</span>
    </div>
  );
};

export const Dialog: React.FC<{
  open: boolean;
  onClose: () => void;
  title: string;
  description?: string;
  children: React.ReactNode;
  footer?: React.ReactNode;
}> = ({ open, onClose, title, description, children, footer }) => {
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
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-[#0f1f18]/50 p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby="patient-dialog-title"
    >
      <div className="w-full max-w-lg rounded-2xl border border-[#bdcabf] bg-white shadow-xl">
        <div className="flex items-start justify-between gap-3 border-b border-[#e5f8ed] px-5 py-4">
          <div>
            <h2 id="patient-dialog-title" className="text-lg font-semibold text-[#0f1f18]">
              {title}
            </h2>
            {description ? <p className="mt-0.5 text-sm text-[#3e4941]">{description}</p> : null}
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Đóng hộp thoại"
            className="rounded-lg p-1.5 text-[#3e4941] hover:bg-[#e5f8ed]"
          >
            <X className="h-4 w-4" aria-hidden="true" />
          </button>
        </div>
        <div className="max-h-[75vh] overflow-y-auto px-5 py-4 text-sm text-[#0f1f18]">{children}</div>
        {footer ? (
          <div className="flex flex-wrap justify-end gap-2 rounded-b-2xl border-t border-[#e5f8ed] bg-[#f8fbf9] px-5 py-3.5">
            {footer}
          </div>
        ) : null}
      </div>
    </div>
  );
};

export const ConfirmDialog: React.FC<{
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
}> = ({
  open,
  onClose,
  onConfirm,
  title,
  message,
  confirmLabel = 'Xác nhận',
  cancelLabel = 'Quay lại',
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

export const Stepper: React.FC<{ steps: { id: string; label: string }[]; currentStepIndex: number }> = ({
  steps,
  currentStepIndex,
}) => (
  <ol className="flex flex-wrap items-center gap-3" aria-label="Các bước thực hiện">
    {steps.map((step, idx) => {
      const done = idx < currentStepIndex;
      const active = idx === currentStepIndex;
      return (
        <li key={step.id} className="flex items-center gap-2">
          <span
            className={`flex h-7 w-7 items-center justify-center rounded-full text-xs font-semibold ${
              done
                ? 'bg-[#006a41] text-white'
                : active
                  ? 'border-2 border-[#006a41] bg-[#e5f8ed] text-[#005231]'
                  : 'border border-[#bdcabf] bg-white text-[#3e4941]'
            }`}
          >
            {done ? <Check className="h-3.5 w-3.5" aria-hidden="true" /> : idx + 1}
          </span>
          <span className={`text-sm ${active || done ? 'font-semibold text-[#0f1f18]' : 'text-[#3e4941]'}`}>
            {step.label}
          </span>
          {idx < steps.length - 1 ? <span className="h-px w-6 bg-[#bdcabf]" aria-hidden="true" /> : null}
        </li>
      );
    })}
  </ol>
);

export const Tabs: React.FC<{
  tabs: { id: string; label: string; count?: number }[];
  activeId: string;
  onChange: (id: string) => void;
}> = ({ tabs, activeId, onChange }) => (
  <div role="tablist" className="flex flex-wrap gap-2 border-b border-[#bdcabf]">
    {tabs.map((tab) => {
      const active = tab.id === activeId;
      return (
        <button
          key={tab.id}
          type="button"
          role="tab"
          aria-selected={active}
          onClick={() => onChange(tab.id)}
          className={`inline-flex min-h-[44px] items-center gap-1.5 border-b-2 px-4 text-sm font-medium ${
            active ? 'border-[#006a41] text-[#006a41]' : 'border-transparent text-[#3e4941] hover:text-[#0f1f18]'
          }`}
        >
          <span>{tab.label}</span>
          {tab.count !== undefined ? (
            <span className="rounded-full bg-[#e5f8ed] px-2 py-0.5 text-xs tabular-nums">{tab.count}</span>
          ) : null}
        </button>
      );
    })}
  </div>
);

export const PaginationBar: React.FC<{
  page: number;
  hasMore: boolean;
  onPageChange: (nextPage: number) => void;
  loading?: boolean;
}> = ({ page, hasMore, onPageChange, loading }) => (
  <div className="flex items-center justify-between pt-3 text-sm text-[#3e4941]">
    <span className="tabular-nums">Trang {page}</span>
    <div className="flex gap-2">
      <Button variant="outline" size="sm" disabled={page <= 1 || loading} onClick={() => onPageChange(page - 1)}>
        Trang trước
      </Button>
      <Button variant="outline" size="sm" disabled={!hasMore || loading} onClick={() => onPageChange(page + 1)}>
        Trang sau
      </Button>
    </div>
  </div>
);
