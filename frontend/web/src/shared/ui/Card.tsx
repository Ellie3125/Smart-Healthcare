import React from 'react';
import { formatVND, formatSignedVND } from '../lib/money';
import { formatDateTimeVN, formatDateVN, formatTimeVN } from '../lib/datetime';

export interface CardProps {
  title?: React.ReactNode;
  subtitle?: React.ReactNode;
  actions?: React.ReactNode;
  children: React.ReactNode;
  className?: string;
  padded?: boolean;
}

export const Card: React.FC<CardProps> = ({
  title,
  subtitle,
  actions,
  children,
  className = '',
  padded = true,
}) => (
  <section className={`rounded-2xl border border-surface-container-highest/70 bg-white shadow-[0_1px_10px_rgba(15,31,24,0.04)] ${className}`}>
    {(title || subtitle || actions) && (
      <header className="flex flex-wrap items-center justify-between gap-3 border-b border-surface-container-low px-4 sm:px-5 py-3.5">
        <div className="min-w-0 flex-1">
          {title ? <h2 className="text-base font-semibold text-[#0f1f18] break-words">{title}</h2> : null}
          {subtitle ? <p className="mt-0.5 text-xs text-[#44544c] break-words">{subtitle}</p> : null}
        </div>
        {actions ? <div className="flex flex-wrap items-center gap-2 max-w-full">{actions}</div> : null}
      </header>
    )}
    <div className={padded ? 'p-5' : ''}>{children}</div>
  </section>
);

export const MoneyText: React.FC<{
  amount: number | null | undefined;
  signed?: boolean;
  showZero?: boolean;
  className?: string;
}> = ({ amount, signed = false, showZero = true, className = '' }) => (
  <span className={`tabular-nums font-medium ${className}`}>
    {signed ? formatSignedVND(amount) : formatVND(amount, { showZero })}
  </span>
);

export const DateTimeText: React.FC<{
  value: string | Date | null | undefined;
  mode?: 'date' | 'time' | 'datetime';
  className?: string;
}> = ({ value, mode = 'datetime', className = '' }) => {
  const formatted =
    mode === 'date' ? formatDateVN(value) : mode === 'time' ? formatTimeVN(value) : formatDateTimeVN(value);
  return <span className={`tabular-nums ${className}`}>{formatted}</span>;
};
