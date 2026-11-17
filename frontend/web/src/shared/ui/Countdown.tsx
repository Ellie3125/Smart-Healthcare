import React, { useEffect, useState } from 'react';
import { Clock } from 'lucide-react';
import { formatCountdownMMSS, remainingSecondsUntil } from '../lib/datetime';

export interface CountdownProps {
  expiresAt: string;
  label?: string;
  onExpire?: () => void;
  warnUnderSeconds?: number;
  className?: string;
}

export const Countdown: React.FC<CountdownProps> = ({
  expiresAt,
  label = 'Thời gian còn lại',
  onExpire,
  warnUnderSeconds = 60,
  className = '',
}) => {
  const [secondsLeft, setSecondsLeft] = useState<number>(() => remainingSecondsUntil(expiresAt));

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

  const isExpired = secondsLeft <= 0;
  const isWarning = !isExpired && secondsLeft <= warnUnderSeconds;

  return (
    <div
      aria-live="polite"
      className={`inline-flex items-center gap-2 rounded-lg border px-3 py-1.5 text-sm font-medium tabular-nums ${
        isExpired
          ? 'border-[#ffb4ab] bg-[#ffdad6] text-[#93000a]'
          : isWarning
            ? 'border-[#fcd34d] bg-[#fef3c7] text-[#9a5b00]'
            : 'border-[#bdd4fb] bg-[#e8f0fe] text-[#0b57d0]'
      } ${className}`}
    >
      <Clock className="h-4 w-4 shrink-0" aria-hidden="true" />
      <span>
        {isExpired ? 'Đã hết hạn' : `${label}: ${formatCountdownMMSS(secondsLeft)}`}
      </span>
    </div>
  );
};
