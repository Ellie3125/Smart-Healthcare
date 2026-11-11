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
  <section className={`rounded-xl border border-[#cddbd3] bg-white shadow-sm ${className}`}>
    {(title || subtitle || actions) && (
      <header className="flex flex-wrap items-center justify-between gap-3 border-b border-[#e9f2ed] px-5 py-3.5">
        <div>
          {title ? <h2 className="text-base font-semibold text-[#0f1f18]">{title}</h2> : null}
          {subtitle ? <p className="mt-0.5 text-xs text-[#44544c]">{subtitle}</p> : null}
        </div>
        {actions ? <div className="flex items-center gap-2">{actions}</div> : null}
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
