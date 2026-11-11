import React from 'react';
import { Button } from './Button';

export interface TableColumn<T> {
  key: string;
  header: React.ReactNode;
  align?: 'left' | 'right' | 'center';
  render: (row: T, index: number) => React.ReactNode;
  widthClass?: string;
}

export interface DataTableProps<T> {
  columns: TableColumn<T>[];
  rows: T[];
  rowKey: (row: T, index: number) => string;
  compact?: boolean;
  onRowClick?: (row: T) => void;
  empty?: React.ReactNode;
}

export function DataTable<T>({
  columns,
  rows,
  rowKey,
  compact = false,
  onRowClick,
  empty,
}: DataTableProps<T>) {
  if (rows.length === 0 && empty) {
    return <>{empty}</>;
  }

  return (
    <div className="overflow-x-auto rounded-xl border border-[#cddbd3] bg-white">
      <table className="min-w-full divide-y divide-[#e9f2ed] text-sm">
        <thead className="bg-[#e9f2ed]">
          <tr>
            {columns.map((col) => (
              <th
                key={col.key}
                scope="col"
                className={`px-4 ${compact ? 'py-2.5' : 'py-3'} text-xs font-semibold uppercase tracking-wide text-[#3e4941] ${
                  col.align === 'right' ? 'text-right' : col.align === 'center' ? 'text-center' : 'text-left'
                } ${col.widthClass ?? ''}`}
              >
                {col.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-[#e9f2ed]">
          {rows.map((row, idx) => (
            <tr
              key={rowKey(row, idx)}
              onClick={onRowClick ? () => onRowClick(row) : undefined}
              className={onRowClick ? 'cursor-pointer hover:bg-[#f4f8f6]' : 'hover:bg-[#f8fbf9]'}
            >
              {columns.map((col) => (
                <td
                  key={col.key}
                  className={`px-4 ${compact ? 'py-2' : 'py-3'} text-[#0f1f18] ${
                    col.align === 'right' ? 'text-right' : col.align === 'center' ? 'text-center' : 'text-left'
                  }`}
                >
                  {col.render(row, idx)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/**
 * Điều khiển phân trang chuẩn Backend conventions (`page` + `hasMore`, không dựa vào `totalCount`).
 */
export const PaginationBar: React.FC<{
  page: number;
  hasMore: boolean;
  onPageChange: (nextPage: number) => void;
  loading?: boolean;
}> = ({ page, hasMore, onPageChange, loading }) => (
  <div className="flex items-center justify-between gap-3 pt-3 text-xs text-[#44544c]">
    <span className="tabular-nums">Trang {page}</span>
    <div className="flex items-center gap-2">
      <Button
        variant="outline"
        size="sm"
        disabled={page <= 1 || loading}
        onClick={() => onPageChange(page - 1)}
      >
        Trang trước
      </Button>
      <Button
        variant="outline"
        size="sm"
        disabled={!hasMore || loading}
        onClick={() => onPageChange(page + 1)}
      >
        Trang sau
      </Button>
    </div>
  </div>
);
