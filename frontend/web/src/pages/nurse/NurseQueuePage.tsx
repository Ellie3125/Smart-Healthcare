import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { Stethoscope } from 'lucide-react';
import { clinicalStaffApi } from '../../api/clinicalStaffApi';
import {
  Button,
  Card,
  DataTable,
  EmptyState,
  ErrorState,
  Skeleton,
  StatusPill,
} from '../../shared/ui';
import { extractFriendlyError } from '../../shared/lib/errors';

export const NurseQueuePage: React.FC = () => {
  const navigate = useNavigate();

  const query = useQuery({
    queryKey: ['staff', 'nurse-queue'],
    queryFn: () => clinicalStaffApi.listNurseQueue(),
  });


  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="font-headline-md text-2xl font-extrabold tracking-tight text-on-surface">
            Hàng chờ đo sinh hiệu Điều dưỡng (NU01)
          </h1>
          <p className="text-sm text-on-surface-variant">
            Chọn bệnh nhân trong hàng chờ để đo sinh hiệu trước khi vào phòng bác sĩ; bác sĩ gọi số khám.
          </p>
        </div>
        <div className="flex items-center gap-2.5">
          <span className="inline-flex items-center gap-1.5 rounded-full bg-surface-container-low px-3.5 py-1.5 text-xs font-semibold text-primary">
            <span className="h-2 w-2 rounded-full bg-primary" />
            {(query.data ?? []).length} bệnh nhân trong hàng chờ
          </span>
          <Button variant="outline" size="sm" onClick={() => query.refetch()}>
            Làm mới hàng chờ
          </Button>
        </div>
      </div>

      {query.isLoading ? (
        <Card><Skeleton rows={4} /></Card>
      ) : query.isError ? (
        <ErrorState message={extractFriendlyError(query.error).message} onRetry={() => query.refetch()} />
      ) : (
        <DataTable
          rowKey={(r) => r.encounterId}
          rows={query.data ?? []}
          empty={<EmptyState title="Hàng chờ sinh hiệu đang trống" />}
          columns={[
            {
              key: 'ticket',
              header: 'Số vé',
              render: (r) => (
                <span className="inline-flex rounded-lg bg-surface-container-low px-2.5 py-1 font-data-tabular font-mono text-sm font-bold text-primary">
                  {r.ticketNumber}
                </span>
              ),
            },
            {
              key: 'patient',
              header: 'Bệnh nhân',
              render: (r) => r.patientName || '—',
            },
            {
              key: 'doctor',
              header: 'Chuyên khoa',
              render: (r) => r.specialtyName || '—',
            },
            { key: 'status', header: 'Trạng thái', render: (r) => <StatusPill status={r.status} /> },
            {
              key: 'act',
              header: 'Thao tác',
              render: (r) => (
                <div className="flex flex-wrap gap-2">
                  <Button
                    size="sm"
                    variant="primary"
                    leftIcon={<Stethoscope className="h-3.5 w-3.5" />}
                    onClick={() => navigate(`/nurse?encounterId=${r.encounterId}`)}
                  >
                    Đo sinh hiệu
                  </Button>
                </div>
              ),
            },
          ]}
        />
      )}
    </div>
  );
};
