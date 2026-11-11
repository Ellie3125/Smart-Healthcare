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
import type { ServiceStationItem } from '../../types/staffWorkflow';

/**
 * AD03 bổ sung: Quản lý Quầy tiếp đón / Thu ngân / Sinh hiệu / Màn sảnh (ServiceStation).
 */
export const ServiceStationsPage: React.FC = () => {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [open, setOpen] = useState(false);
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [stationType, setStationType] = useState<ServiceStationItem['stationType']>('Reception');
  const [floor, setFloor] = useState('Tầng 1');

  const query = useQuery({
    queryKey: ['admin', 'stations'],
    queryFn: () => adminOpsApi.listStations(),
  });

  const saveMutation = useMutation({
    mutationFn: () =>
      adminOpsApi.saveStation({
        code: code.trim(),
        name: name.trim(),
        stationType,
        floor: floor.trim(),
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
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold text-[#0f1f18]">Quản lý Quầy & Điểm dịch vụ (AD03)</h1>
          <p className="text-xs text-[#44544c]">Cấu hình quầy tiếp đón, thu ngân, bàn đo sinh hiệu và điểm hiển thị.</p>
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
            { key: 'type', header: 'Loại', render: (r) => r.stationType },
            { key: 'floor', header: 'Vị trí', render: (r) => r.floor },
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
              disabled={!code.trim() || !name.trim()}
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
          <FormField label="Phân loại">
            {(props) => (
              <Select
                {...props}
                value={stationType}
                onChange={(e) => setStationType(e.target.value as ServiceStationItem['stationType'])}
                options={[
                  { value: 'Reception', label: 'Quầy tiếp đón' },
                  { value: 'Cashier', label: 'Quầy thu ngân' },
                  { value: 'Vitals', label: 'Bàn đo sinh hiệu' },
                  { value: 'QueueDisplay', label: 'Màn gọi số sảnh chờ' },
                ]}
              />
            )}
          </FormField>
          <FormField label="Tầng / Khu vực">
            {(props) => <Input {...props} value={floor} onChange={(e) => setFloor(e.target.value)} />}
          </FormField>
        </div>
      </Dialog>
    </div>
  );
};
