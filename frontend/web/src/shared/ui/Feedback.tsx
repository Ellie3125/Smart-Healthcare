import React from 'react';
import { AlertCircle, AlertTriangle, CheckCircle2, Info, Inbox } from 'lucide-react';
import { Button } from './Button';

export type BannerTone = 'info' | 'success' | 'warning' | 'danger';

const BANNER_STYLES: Record<BannerTone, string> = {
  info: 'bg-[#e8f0fe] text-[#0b57d0] border-[#bdd4fb]',
  success: 'bg-[#d4f7e2] text-[#005231] border-[#8ef8bb]',
  warning: 'bg-[#fef3c7] text-[#9a5b00] border-[#fcd34d]',
  danger: 'bg-[#ffdad6] text-[#93000a] border-[#ffb4ab]',
};

export interface BannerProps {
  tone?: BannerTone;
  title?: string;
  children: React.ReactNode;
  action?: React.ReactNode;
  className?: string;
}

export const Banner: React.FC<BannerProps> = ({
  tone = 'info',
  title,
  children,
  action,
  className = '',
}) => {
  const Icon =
    tone === 'success'
      ? CheckCircle2
      : tone === 'warning'
        ? AlertTriangle
        : tone === 'danger'
          ? AlertCircle
          : Info;

  return (
    <div
      role={tone === 'danger' ? 'alert' : 'status'}
      className={`flex flex-wrap items-start justify-between gap-3 rounded-xl border px-4 py-3 text-sm ${BANNER_STYLES[tone]} ${className}`}
    >
      <div className="flex items-start gap-2.5">
        <Icon className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
        <div>
          {title ? <p className="font-semibold">{title}</p> : null}
          <div>{children}</div>
        </div>
      </div>
      {action ? <div className="shrink-0">{action}</div> : null}
    </div>
  );
};

export const Skeleton: React.FC<{ className?: string; rows?: number }> = ({ className = '', rows = 1 }) => (
  <div className="space-y-2" aria-busy="true" aria-label="Đang tải dữ liệu">
    {Array.from({ length: rows }).map((_, i) => (
      <div key={i} className={`h-4 animate-pulse rounded bg-[#e0f2e7] ${className}`} />
    ))}
  </div>
);

export const EmptyState: React.FC<{
  title: string;
  description?: string;
  action?: React.ReactNode;
  icon?: React.ReactNode;
}> = ({ title, description, action, icon }) => (
  <div className="flex flex-col items-center justify-center rounded-xl border border-dashed border-[#cddbd3] bg-[#f8fbf9] px-6 py-10 text-center">
    <div className="mb-3 rounded-full bg-[#e9f2ed] p-3 text-[#006a41]" aria-hidden="true">
      {icon ?? <Inbox className="h-6 w-6" />}
    </div>
    <h3 className="text-sm font-semibold text-[#0f1f18]">{title}</h3>
    {description ? <p className="mt-1 max-w-md text-xs text-[#44544c]">{description}</p> : null}
    {action ? <div className="mt-4">{action}</div> : null}
  </div>
);

export const ErrorState: React.FC<{
  title?: string;
  message: string;
  onRetry?: () => void;
}> = ({ title = 'Không thể tải dữ liệu', message, onRetry }) => (
  <div role="alert" className="flex flex-col items-center justify-center rounded-xl border border-[#ffb4ab] bg-[#fff5f4] px-6 py-8 text-center">
    <AlertCircle className="mb-2 h-7 w-7 text-[#ba1a1a]" aria-hidden="true" />
    <h3 className="text-sm font-semibold text-[#93000a]">{title}</h3>
    <p className="mt-1 max-w-md text-xs text-[#93000a]/90">{message}</p>
    {onRetry ? (
      <Button variant="outline" size="sm" onClick={onRetry} className="mt-4">
        Thử lại
      </Button>
    ) : null}
  </div>
);
