export function formatVND(amount: number | null | undefined, options?: { showZero?: boolean }): string {
  if (amount === null || amount === undefined || Number.isNaN(amount)) {
    return options?.showZero ? '0 ₫' : '—';
  }
  return `${new Intl.NumberFormat('vi-VN').format(Math.round(amount))} ₫`;
}

export function createIdempotencyKey(prefix?: string): string {
  const uuid =
    typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
      ? crypto.randomUUID()
      : `${Date.now()}-${Math.random().toString(16).slice(2)}-${Math.random().toString(16).slice(2)}`;
  return prefix ? `${prefix}-${uuid}` : uuid;
}
