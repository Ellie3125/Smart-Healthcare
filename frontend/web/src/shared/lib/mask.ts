/**
 * Che CCCD hoặc SĐT ở lớp trình bày/AuditLog, chỉ giữ 3 ký tự cuối.
 */
export function maskKeepLast3(raw: string | null | undefined): string {
  if (!raw) return '—';
  const trimmed = raw.trim();
  if (trimmed.length <= 3) return '***';
  return `${'*'.repeat(trimmed.length - 3)}${trimmed.slice(-3)}`;
}

/**
 * Chống CSV formula injection khi xuất file mở trên Excel/Sheets.
 */
export function escapeCsvCell(value: unknown): string {
  const str = value === null || value === undefined ? '' : String(value);
  const neutralized = /^[=+\-@\t\r]/.test(str) ? `'${str}` : str;
  if (/[",\n\r]/.test(neutralized)) {
    return `"${neutralized.replace(/"/g, '""')}"`;
  }
  return neutralized;
}

export function downloadCsvFile(filename: string, headers: string[], rows: unknown[][]): void {
  const bom = '\uFEFF';
  const lines = [
    headers.map(escapeCsvCell).join(','),
    ...rows.map((row) => row.map(escapeCsvCell).join(',')),
  ];
  const blob = new Blob([bom + lines.join('\r\n')], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}
