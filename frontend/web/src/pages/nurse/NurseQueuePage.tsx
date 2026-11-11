import React from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { PhoneCall, Stethoscope } from 'lucide-react';
import { clinicalStaffApi } from '../../api/clinicalStaffApi';
import {
  Button,
  Card,
  DataTable,
  EmptyState,
  ErrorState,
  Skeleton,
  StatusPill,
  useToast,
} from '../../shared/ui';
import { extractFriendlyError } from '../../shared/lib/errors';

export const NurseQueuePage: React.FC = () => {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();

  const query = useQuery({
    queryKey: ['staff', 'nurse-queue'],
    queryFn: () => clinicalStaffApi.listNurseQueue(),
  });

  const callMutation = useMutation({
    mutationFn: (encounterId: string) => clinicalStaffApi.callNursePatient(encounterId),
    onSuccess: (res) => {
      pushToast(`Đã gọi vé ${res.ticketNumber} (${res.patientName}) vào bàn sinh hiệu.`, 'success');
      queryClient.invalidateQueries({ queryKey: ['staff', 'nurse-queue'] });
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold text-[#0f1f18]">Hàng chờ đo sinh hiệu Điều dưỡng (NU01)</h1>
          <p className="text-xs text-[#44544c]">Gọi bệnh nhân vào bàn đo sinh hiệu và mở biểu mẫu ghi nhận trước khi vào phòng bác sĩ.</p>
        </div>
        <Button variant="outline" size="sm" onClick={() => query.refetch()}>
          Làm mới hàng chờ
        </Button>
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
              render: (r) => <span className="font-mono text-sm font-bold text-[#006a41]">{r.ticketNumber}</span>,
            },
            {
              key: 'patient',
              header: 'Bệnh nhân',
              render: (r) => `${r.patientName} (SN ${r.yearOfBirth})`,
            },
            {
              key: 'doctor',
              header: 'Bác sĩ phụ trách',
              render: (r) => `${r.doctorName} — ${r.specialtyName}`,
            },
            { key: 'status', header: 'Trạng thái', render: (r) => <StatusPill status={r.status} /> },
            {
              key: 'act',
              header: 'Thao tác',
              render: (r) => (
                <div className="flex flex-wrap gap-2">
                  <Button
                    size="sm"
                    variant="secondary"
                    leftIcon={<PhoneCall className="h-3.5 w-3.5" />}
                    loading={callMutation.isPending}
                    onClick={() => callMutation.mutate(r.encounterId)}
                  >
                    Gọi loa
                  </Button>
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
