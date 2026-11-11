import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { adminOpsApi } from '../../api/adminOpsApi';
import {
  Card,
  DataTable,
  DateTimeText,
  Dialog,
  ErrorState,
  FormField,
  Input,
  PaginationBar,
  Skeleton,
  Button,
} from '../../shared/ui';
import { maskKeepLast3 } from '../../shared/lib/mask';
import { extractFriendlyError } from '../../shared/lib/errors';
import type { AuditLogEntry } from '../../types/staffWorkflow';

/**
 * AD15 — Nhật ký kiểm toán (AuditLog).
 * Che CCCD và SĐT bệnh nhân, chỉ giữ 3 ký tự cuối (`maskKeepLast3`).
 */
export const AuditLogsPage: React.FC = () => {
  const [keyword, setKeyword] = useState('');
  const [page, setPage] = useState(1);
  const [selectedDiff, setSelectedDiff] = useState<AuditLogEntry | null>(null);

  const query = useQuery({
    queryKey: ['admin', 'audit-logs', keyword, page],
    queryFn: () => adminOpsApi.listAuditLogs({ keyword: keyword || undefined, page }),
  });

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold text-[#0f1f18]">Nhật ký kiểm toán AuditLog (AD15)</h1>
          <p className="text-xs text-[#44544c]">CCCD và SĐT bệnh nhân được che tự động, chỉ giữ lại 3 ký tự cuối theo chuẩn bảo mật.</p>
        </div>
        <div className="w-72">
          <FormField label="Tìm theo người thao tác / mã">
            {(props) => (
              <Input
                {...props}
                value={keyword}
                onChange={(e) => {
                  setKeyword(e.target.value);
                  setPage(1);
                }}
                placeholder="VD: DUYET_HOAN_TIEN..."
              />
            )}
          </FormField>
        </div>
      </div>

      {query.isLoading ? (
        <Card><Skeleton rows={4} /></Card>
      ) : query.isError ? (
        <ErrorState message={extractFriendlyError(query.error).message} onRetry={() => query.refetch()} />
      ) : (
        <>
          <DataTable
            rowKey={(r) => r.id}
            rows={query.data?.items ?? []}
            columns={[
              { key: 'time', header: 'Thời gian', render: (r) => <DateTimeText value={r.occurredAt} /> },
              { key: 'actor', header: 'Người thực hiện', render: (r) => `${r.actorName} (${r.actorRole})` },
              { key: 'action', header: 'Hành động', render: (r) => <span className="font-mono text-xs font-semibold">{r.action}</span> },
              { key: 'entity', header: 'Đối tượng', render: (r) => `${r.entityType}: ${r.entityId}` },
              {
                key: 'pii',
                header: 'CCCD / SĐT (Đã che)',
                render: (r) => (
                  <div className="text-xs tabular-nums">
                    <div>CCCD: {maskKeepLast3(r.patientCccd)}</div>
                    <div>SĐT: {maskKeepLast3(r.patientPhone)}</div>
                  </div>
                ),
              },
              {
                key: 'diff',
                header: 'Chi tiết thay đổi',
                render: (r) => (
                  <Button size="sm" variant="outline" onClick={() => setSelectedDiff(r)}>
                    Xem Diff Trước / Sau
                  </Button>
                ),
              },
            ]}
          />
          <PaginationBar page={page} hasMore={Boolean(query.data?.hasMore)} onPageChange={setPage} />
        </>
      )}

      <Dialog
        open={Boolean(selectedDiff)}
        onClose={() => setSelectedDiff(null)}
        title={`Diff thay đổi — ${selectedDiff?.action}`}
        maxWidthClass="max-w-2xl"
      >
        {selectedDiff ? (
          <div className="grid gap-3 md:grid-cols-2">
            <div className="rounded-lg border border-[#ffb4ab] bg-[#fff5f4] p-3">
              <p className="mb-1 text-xs font-semibold text-[#93000a]">Trước (Before)</p>
              <pre className="overflow-x-auto text-xs">{JSON.stringify(selectedDiff.beforeJson, null, 2)}</pre>
            </div>
            <div className="rounded-lg border border-[#8ef8bb] bg-[#ebfef2] p-3">
              <p className="mb-1 text-xs font-semibold text-[#005231]">Sau (After)</p>
              <pre className="overflow-x-auto text-xs">{JSON.stringify(selectedDiff.afterJson, null, 2)}</pre>
            </div>
          </div>
        ) : null}
      </Dialog>
    </div>
  );
};
