import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { receptionApi } from '../../api/receptionApi';
import { Card, DataTable, EmptyState, ErrorState, Skeleton, StatusPill } from '../../shared/ui';
import { extractFriendlyError } from '../../shared/lib/errors';

export const ReceptionCallListPage: React.FC = () => {

  const query = useQuery({
    queryKey: ['reception', 'call-list'],
    queryFn: () => receptionApi.listCallTasks(),
  });

  return (
    <div className="space-y-5">
      <div>
        <h1 className="font-headline-md text-2xl font-extrabold tracking-tight text-on-surface">
          Danh sách cần gọi điện (RC10)
        </h1>
        <p className="text-sm text-on-surface-variant">
          Liên hệ bệnh nhân khi phiên khám thay đổi/hủy trước 06:30. Chưa lưu kết quả cuộc gọi trên hệ thống.
        </p>
      </div>

      {query.isLoading ? (
        <Card><Skeleton rows={4} /></Card>
      ) : query.isError ? (
        <ErrorState message={extractFriendlyError(query.error).message} onRetry={() => query.refetch()} />
      ) : (
        <DataTable
          rowKey={(r) => r.id}
          rows={query.data ?? []}
          empty={<EmptyState title="Không có bệnh nhân cần gọi" />}
          columns={[
            {
              key: 'patient',
              header: 'Bệnh nhân & SĐT',
              render: (r) => (
                <div>
                  <p className="font-semibold text-[#0f1f18]">{r.patientName}</p>
                  <p className="text-xs font-mono text-[#006a41]">{r.phone}</p>
                </div>
              ),
            },
            { key: 'doc', header: 'Ngày khám', render: (r) => r.serviceDate },
            { key: 'reason', header: 'Lý do cần gọi', render: (r) => r.reason },
            {
              key: 'status',
              header: 'Trạng thái',
              render: (r) => (
                <StatusPill
                  status={r.status === 'Pending' ? 'Waiting' : r.status === 'Called' ? 'Called' : 'Completed'}
                  labelOverride={r.status === 'Pending' ? 'Chưa gọi' : r.status === 'Called' ? 'Đã gọi' : 'Đã chốt với khách'}
                />
              ),
            },
          ]}
        />
      )}

    </div>
  );
};
