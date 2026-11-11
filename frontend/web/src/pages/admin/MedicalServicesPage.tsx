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
  MoneyText,
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
  const [category, setCategory] = useState<'Lab' | 'Imaging' | 'Functional'>('Lab');
  const [unitPrice, setUnitPrice] = useState('150000');

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
        unitPrice: Number(unitPrice) || 100000,
        hasActiveRoom: true,
        isActive: true,
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
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold text-[#0f1f18]">Danh mục dịch vụ Cận lâm sàng (AD08)</h1>
          <p className="text-xs text-[#44544c]">Bác sĩ chỉ được chỉ định dịch vụ gắn phòng máy đang hoạt động.</p>
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
            { key: 'price', header: 'Đơn giá', align: 'right', render: (r) => <MoneyText amount={r.unitPrice} /> },
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
            <Button variant="primary" loading={saveMutation.isPending} disabled={!code || !name} onClick={() => saveMutation.mutate()}>
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
                onChange={(e) => setCategory(e.target.value as 'Lab' | 'Imaging' | 'Functional')}
                options={[
                  { value: 'Lab', label: 'Xét nghiệm' },
                  { value: 'Imaging', label: 'Chẩn đoán hình ảnh' },
                  { value: 'Functional', label: 'Thăm dò chức năng' },
                ]}
              />
            )}
          </FormField>
          <FormField label="Đơn giá (VNĐ)" required>
            {(props) => <Input {...props} type="number" value={unitPrice} onChange={(e) => setUnitPrice(e.target.value)} />}
          </FormField>
        </div>
      </Dialog>
    </div>
  );
};
