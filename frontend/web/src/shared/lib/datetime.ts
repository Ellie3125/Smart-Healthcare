const VN_TZ = 'Asia/Ho_Chi_Minh';

export function formatDateVN(isoOrDate: string | Date | null | undefined): string {
  if (!isoOrDate) return '—';
  const date = typeof isoOrDate === 'string' ? parseDateInput(isoOrDate) : isoOrDate;
  if (!date || Number.isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat('vi-VN', {
    timeZone: VN_TZ,
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(date);
}

export function formatTimeVN(isoOrDate: string | Date | null | undefined): string {
  if (!isoOrDate) return '—';
  const date = typeof isoOrDate === 'string' ? parseDateInput(isoOrDate) : isoOrDate;
  if (!date || Number.isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat('vi-VN', {
    timeZone: VN_TZ,
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(date);
}

export function formatDateTimeVN(isoOrDate: string | Date | null | undefined): string {
  if (!isoOrDate) return '—';
  const date = typeof isoOrDate === 'string' ? parseDateInput(isoOrDate) : isoOrDate;
  if (!date || Number.isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat('vi-VN', {
    timeZone: VN_TZ,
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(date);
}

function parseDateInput(raw: string): Date | null {
  if (/^\d{4}-\d{2}-\d{2}$/.test(raw)) {
    return new Date(`${raw}T00:00:00+07:00`);
  }
  return new Date(raw);
}

export function getVNDateISO(now: Date = new Date()): string {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: VN_TZ,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(now);
  const year = parts.find((p) => p.type === 'year')?.value ?? '1970';
  const month = parts.find((p) => p.type === 'month')?.value ?? '01';
  const day = parts.find((p) => p.type === 'day')?.value ?? '01';
  return `${year}-${month}-${day}`;
}

export function getVNHoursMinutes(now: Date = new Date()): { hours: number; minutes: number } {
  const parts = new Intl.DateTimeFormat('en-GB', {
    timeZone: VN_TZ,
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).formatToParts(now);
  const hours = Number(parts.find((p) => p.type === 'hour')?.value ?? '0');
  const minutes = Number(parts.find((p) => p.type === 'minute')?.value ?? '0');
  return { hours, minutes };
}

/**
 * Kiểm tra đã qua ngưỡng cắt giờ trên ngày khám `serviceDate` (VD: 05:00 hoặc 06:30 GMT+7).
 */
export function isBeforeCutoffVN(
  serviceDate: string,
  cutoffHour: number,
  cutoffMinute: number = 0,
  now: Date = new Date(),
): boolean {
  const cutoff = new Date(
    `${serviceDate}T${String(cutoffHour).padStart(2, '0')}:${String(cutoffMinute).padStart(2, '0')}:00+07:00`,
  );
  if (Number.isNaN(cutoff.getTime())) return false;
  return now.getTime() < cutoff.getTime();
}

export function remainingSecondsUntil(expiresAtIso: string | null | undefined, nowMs: number = Date.now()): number {
  if (!expiresAtIso) return 0;
  const target = new Date(expiresAtIso).getTime();
  if (Number.isNaN(target)) return 0;
  return Math.max(0, Math.floor((target - nowMs) / 1000));
}

export function formatCountdownMMSS(totalSeconds: number): string {
  const clamped = Math.max(0, Math.floor(totalSeconds));
  const mins = Math.floor(clamped / 60);
  const secs = clamped % 60;
  return `${String(mins).padStart(2, '0')}:${String(secs).padStart(2, '0')}`;
}
