import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus } from 'lucide-react';
import { adminOpsApi } from '../../api/adminOpsApi';
import {
  Button,
  Card,
  DataTable,
  Dialog,
  ErrorState,
  FormField,
  Input,
  Select,
  Skeleton,
  StatusPill,
  useToast,
} from '../../shared/ui';
import { extractFriendlyError } from '../../shared/lib/errors';

export const MedicalServicesPage: React.FC = () => {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [open, setOpen] = useState(false);
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [category, setCategory] = useState('Laboratory');
  const [roomId, setRoomId] = useState('');
  const roomsQuery = useQuery({ queryKey: ['admin', 'rooms'], queryFn: () => adminOpsApi.listRooms() });

  const query = useQuery({
    queryKey: ['admin', 'cls-services'],
    queryFn: () => adminOpsApi.listMedicalServices(),
  });

  const saveMutation = useMutation({
    mutationFn: () =>
      adminOpsApi.saveMedicalService({
        code: code.trim(),
        name: name.trim(),
        category,
        roomId,
      }),
    onSuccess: () => {
      pushToast('Đã lưu dịch vụ cận lâm sàng.', 'success');
      setOpen(false);
      setCode('');
      setName('');
      queryClient.invalidateQueries({ queryKey: ['admin', 'cls-services'] });
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="font-headline-md text-2xl font-extrabold tracking-tight text-on-surface">
            Danh mục dịch vụ Cận lâm sàng (AD08)
          </h1>
          <p className="text-sm text-on-surface-variant">Bác sĩ chỉ được chỉ định dịch vụ gắn phòng máy đang hoạt động.</p>
        </div>
        <Button leftIcon={<Plus className="h-4 w-4" />} onClick={() => setOpen(true)}>
          Thêm dịch vụ CLS
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
          columns={[
            { key: 'code', header: 'Mã DV', render: (r) => <span className="font-mono font-semibold">{r.code}</span> },
            { key: 'name', header: 'Tên dịch vụ CLS', render: (r) => r.name },
            { key: 'room', header: 'Phòng thực hiện', render: (r) => r.roomName },
            {
              key: 'activeRoom',
              header: 'Phòng hoạt động',
              render: (r) => (
                <StatusPill
                  status={r.hasActiveRoom ? 'Active' : 'Inactive'}
                  labelOverride={r.hasActiveRoom ? 'Phòng đang mở' : 'Ngưng phòng máy'}
                />
              ),
            },
          ]}
        />
      )}

      <Dialog
        open={open}
        onClose={() => setOpen(false)}
        title="Thêm dịch vụ Cận lâm sàng"
        footer={
          <>
            <Button variant="outline" onClick={() => setOpen(false)}>Hủy</Button>
            <Button variant="primary" loading={saveMutation.isPending} disabled={!code || !name || !roomId} onClick={() => saveMutation.mutate()}>
              Lưu dịch vụ
            </Button>
          </>
        }
      >
        <div className="space-y-3">
          <FormField label="Mã dịch vụ" required>
            {(props) => <Input {...props} value={code} onChange={(e) => setCode(e.target.value)} placeholder="VD: XQ-PHOI" />}
          </FormField>
          <FormField label="Tên dịch vụ" required>
            {(props) => <Input {...props} value={name} onChange={(e) => setName(e.target.value)} placeholder="Chụp X-Quang ngực thẳng" />}
          </FormField>
          <FormField label="Nhóm">
            {(props) => (
              <Select
                {...props}
                value={category}
                onChange={(e) => setCategory(e.target.value)}
                options={[
                  { value: 'Laboratory', label: 'Xét nghiệm' },
                  { value: 'Radiology', label: 'Chẩn đoán hình ảnh' },
                  { value: 'Ultrasound', label: 'Siêu âm' },
                  { value: 'Endoscopy', label: 'Nội soi' },
                ]}
              />
            )}
          </FormField>
          <FormField label="Phòng thực hiện mặc định" required>
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
          <p className="text-xs text-on-surface-variant">Đơn giá dịch vụ CLS quản lý trong bảng giá, không nhập ở đây.</p>
        </div>
      </Dialog>
    </div>
  );
};
