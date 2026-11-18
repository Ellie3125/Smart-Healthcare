import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus } from 'lucide-react';
import { adminOpsApi } from '../../api/adminOpsApi';
import {
  Button,
  Card,
  DataTable,
  Dialog,
  EmptyState,
  ErrorState,
  FormField,
  Input,
  Select,
  Skeleton,
  StatusPill,
  useToast,
} from '../../shared/ui';
import { extractFriendlyError } from '../../shared/lib/errors';

/**
 * AD03 bổ sung: Quản lý Quầy tiếp đón / Thu ngân / Sinh hiệu / Màn sảnh (ServiceStation).
 */
export const ServiceStationsPage: React.FC = () => {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [open, setOpen] = useState(false);
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [roomId, setRoomId] = useState('');
  const roomsQuery = useQuery({ queryKey: ['admin', 'rooms'], queryFn: () => adminOpsApi.listRooms() });

  const query = useQuery({
    queryKey: ['admin', 'stations'],
    queryFn: () => adminOpsApi.listStations(),
  });

  const saveMutation = useMutation({
    mutationFn: () =>
      adminOpsApi.saveStation({
        code: code.trim(),
        name: name.trim(),
        roomId,
        isActive: true,
      }),
    onSuccess: () => {
      pushToast('Đã lưu thông tin quầy dịch vụ.', 'success');
      setOpen(false);
      setCode('');
      setName('');
      queryClient.invalidateQueries({ queryKey: ['admin', 'stations'] });
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="font-headline-md text-2xl font-extrabold tracking-tight text-on-surface">
            Quản lý Quầy &amp; Điểm dịch vụ (AD03)
          </h1>
          <p className="text-sm text-on-surface-variant">
            Cấu hình quầy tiếp đón, thu ngân, bàn đo sinh hiệu và điểm hiển thị.
          </p>
        </div>
        <Button leftIcon={<Plus className="h-4 w-4" />} onClick={() => setOpen(true)}>
          Thêm quầy mới
        </Button>
      </div>

      {query.isLoading ? (
        <Card><Skeleton rows={4} /></Card>
      ) : query.isError ? (
        <ErrorState message={extractFriendlyError(query.error).message} onRetry={() => query.refetch()} />
      ) : (
        <DataTable
          rowKey={(r) => r.id}
          rows={query.data ?? []}
          empty={<EmptyState title="Chưa có quầy dịch vụ nào" />}
          columns={[
            { key: 'code', header: 'Mã quầy', render: (r) => <span className="font-mono font-semibold">{r.code}</span> },
            { key: 'name', header: 'Tên quầy', render: (r) => r.name },
            { key: 'room', header: 'Phòng', render: (r) => r.roomName || '—' },
            { key: 'status', header: 'Trạng thái', render: (r) => <StatusPill status={r.isActive ? 'Active' : 'Inactive'} /> },
          ]}
        />
      )}

      <Dialog
        open={open}
        onClose={() => setOpen(false)}
        title="Thêm quầy / trạm mới"
        footer={
          <>
            <Button variant="outline" onClick={() => setOpen(false)}>Hủy</Button>
            <Button
              variant="primary"
              loading={saveMutation.isPending}
              disabled={!code.trim() || !name.trim() || !roomId}
              onClick={() => saveMutation.mutate()}
            >
              Lưu quầy
            </Button>
          </>
        }
      >
        <div className="space-y-3">
          <FormField label="Mã quầy" required>
            {(props) => <Input {...props} value={code} onChange={(e) => setCode(e.target.value)} placeholder="VD: Q02" />}
          </FormField>
          <FormField label="Tên quầy" required>
            {(props) => <Input {...props} value={name} onChange={(e) => setName(e.target.value)} placeholder="Quầy tiếp đón số 2" />}
          </FormField>
          <FormField label="Phòng" required>
            {(props) => (
              <Select
                {...props}
                value={roomId}
                placeholder="Chọn phòng"
                onChange={(e) => setRoomId(e.target.value)}
                options={(roomsQuery.data ?? []).filter((r) => r.isActive).map((r) => ({ value: r.id, label: r.name }))}
              />
            )}
          </FormField>
        </div>
      </Dialog>
    </div>
  );
};
