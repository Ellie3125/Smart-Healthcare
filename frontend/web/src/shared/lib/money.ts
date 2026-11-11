export function formatVND(amount: number | null | undefined, options?: { showZero?: boolean }): string {
  if (amount === null || amount === undefined || Number.isNaN(amount)) {
    return options?.showZero ? '0 ₫' : '—';
  }
  return `${new Intl.NumberFormat('vi-VN').format(Math.round(amount))} ₫`;
}

export function formatSignedVND(amount: number | null | undefined): string {
  if (amount === null || amount === undefined || Number.isNaN(amount)) return '—';
  const rounded = Math.round(amount);
  const absFormatted = `${new Intl.NumberFormat('vi-VN').format(Math.abs(rounded))} ₫`;
  if (rounded > 0) return `+${absFormatted}`;
  if (rounded < 0) return `-${absFormatted}`;
  return absFormatted;
}
