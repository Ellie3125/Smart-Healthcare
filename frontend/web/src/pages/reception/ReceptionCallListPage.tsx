import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { PhoneCall } from 'lucide-react';
import { receptionApi } from '../../api/receptionApi';
import {
  Button,
  Card,
  DataTable,
  DateTimeText,
  Dialog,
  EmptyState,
  ErrorState,
  FormField,
  Skeleton,
  StatusPill,
  Textarea,
  useToast,
} from '../../shared/ui';
import { extractFriendlyError } from '../../shared/lib/errors';
import type { ReceptionCallTaskItem } from '../../types/staffWorkflow';

export const ReceptionCallListPage: React.FC = () => {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [selected, setSelected] = useState<ReceptionCallTaskItem | null>(null);
  const [note, setNote] = useState('');

  const query = useQuery({
    queryKey: ['reception', 'call-list'],
    queryFn: () => receptionApi.listCallTasks(),
  });

  const updateMutation = useMutation({
    mutationFn: () =>
      receptionApi.updateCallTask(selected!.id, { status: 'Resolved', note: note.trim() }),
    onSuccess: () => {
      pushToast('Đã cập nhật kết quả liên hệ bệnh nhân.', 'success');
      setSelected(null);
      setNote('');
      queryClient.invalidateQueries({ queryKey: ['reception', 'call-list'] });
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold text-[#0f1f18]">Danh sách cần gọi điện (RC10)</h1>
        <p className="text-xs text-[#44544c]">Liên hệ bệnh nhân khi phiên khám thay đổi/hủy trước 06:30 hoặc cần xác nhận lại lịch.</p>
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
            { key: 'doc', header: 'Bác sĩ / Ngày', render: (r) => `${r.doctorName} (${r.serviceDate})` },
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
            {
              key: 'act',
              header: 'Cập nhật',
              render: (r) => (
                <Button size="sm" variant="primary" leftIcon={<PhoneCall className="h-3.5 w-3.5" />} onClick={() => setSelected(r)}>
                  Ghi nhận cuộc gọi
                </Button>
              ),
            },
          ]}
        />
      )}

      <Dialog
        open={Boolean(selected)}
        onClose={() => setSelected(null)}
        title={`Ghi nhận liên hệ — ${selected?.patientName}`}
        footer={
          <>
            <Button variant="outline" onClick={() => setSelected(null)}>Hủy</Button>
            <Button variant="primary" loading={updateMutation.isPending} disabled={!note.trim()} onClick={() => updateMutation.mutate()}>
              Lưu kết quả
            </Button>
          </>
        }
      >
        <div className="space-y-3">
          <p className="text-xs text-[#44544c]">
            Số điện thoại: <strong>{selected?.phone}</strong> • Ngày khám <DateTimeText value={selected?.serviceDate} mode="date" />
          </p>
          <FormField label="Kết quả trao đổi với bệnh nhân" required>
            {(props) => (
              <Textarea
                {...props}
                value={note}
                onChange={(e) => setNote(e.target.value)}
                placeholder="VD: Khách đồng ý chuyển sang khám BS. Khánh lúc 09:00..."
              />
            )}
          </FormField>
        </div>
      </Dialog>
    </div>
  );
};
