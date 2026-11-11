import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus } from 'lucide-react';
import { adminOpsApi } from '../../api/adminOpsApi';
import {
  Button,
  Card,
  DatePicker,
  DateTimeText,
  Dialog,
  ErrorState,
  FormField,
  Input,
  MoneyText,
  Skeleton,
  StatusPill,
  useToast,
} from '../../shared/ui';
import { extractFriendlyError } from '../../shared/lib/errors';

export const PriceListsPage: React.FC = () => {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [open, setOpen] = useState(false);
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [effectiveFrom, setEffectiveFrom] = useState('2026-12-01');
  const [baseFee, setBaseFee] = useState('150000');

  const query = useQuery({
    queryKey: ['admin', 'price-lists'],
    queryFn: () => adminOpsApi.listPriceLists(),
  });

  const createMutation = useMutation({
    mutationFn: () =>
      adminOpsApi.createPriceList({
        code: code.trim(),
        name: name.trim(),
        effectiveFrom,
        consultationPrices: [{ rankName: 'Bác sĩ CKI', price: Number(baseFee) || 150000 }],
        isActive: true,
      }),
    onSuccess: () => {
      pushToast('Đã tạo bảng giá mới.', 'success');
      setOpen(false);
      queryClient.invalidateQueries({ queryKey: ['admin', 'price-lists'] });
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold text-[#0f1f18]">Quản lý Bảng giá (AD09)</h1>
          <p className="text-xs text-[#44544c]">Áp dụng khung giá khám theo học hàm/học vị và ngày hiệu lực.</p>
        </div>
        <Button leftIcon={<Plus className="h-4 w-4" />} onClick={() => setOpen(true)}>
          Tạo bảng giá
        </Button>
      </div>

      {query.isLoading ? (
        <Card><Skeleton rows={4} /></Card>
      ) : query.isError ? (
        <ErrorState message={extractFriendlyError(query.error).message} onRetry={() => query.refetch()} />
      ) : (
        <div className="space-y-4">
          {(query.data ?? []).map((pl) => (
            <Card
              key={pl.id}
              title={`${pl.code} — ${pl.name}`}
              subtitle={
                <>
                  Hiệu lực từ <DateTimeText value={pl.effectiveFrom} mode="date" />
                </>
              }
              actions={<StatusPill status={pl.isActive ? 'Active' : 'Inactive'} />}
            >
              <div className="grid gap-3 sm:grid-cols-3">
                {pl.consultationPrices.map((p) => (
                  <div key={p.rankName} className="rounded-lg bg-[#f4f8f6] p-3">
                    <p className="text-xs text-[#44544c]">{p.rankName}</p>
                    <p className="mt-1 text-base font-semibold text-[#006a41]">
                      <MoneyText amount={p.price} />
                    </p>
                  </div>
                ))}
              </div>
            </Card>
          ))}
        </div>
      )}

      <Dialog
        open={open}
        onClose={() => setOpen(false)}
        title="Tạo bảng giá mới"
        footer={
          <>
            <Button variant="outline" onClick={() => setOpen(false)}>Hủy</Button>
            <Button variant="primary" loading={createMutation.isPending} disabled={!code || !name} onClick={() => createMutation.mutate()}>
              Lưu bảng giá
            </Button>
          </>
        }
      >
        <div className="space-y-3">
          <FormField label="Mã bảng giá" required>
            {(props) => <Input {...props} value={code} onChange={(e) => setCode(e.target.value)} placeholder="BG-2027-Q1" />}
          </FormField>
          <FormField label="Tên bảng giá" required>
            {(props) => <Input {...props} value={name} onChange={(e) => setName(e.target.value)} />}
          </FormField>
          <FormField label="Ngày hiệu lực" required>
            {(props) => <DatePicker {...props} value={effectiveFrom} onChange={(e) => setEffectiveFrom(e.target.value)} />}
          </FormField>
          <FormField label="Giá khám Bác sĩ CKI (VNĐ)">
            {(props) => <Input {...props} type="number" value={baseFee} onChange={(e) => setBaseFee(e.target.value)} />}
          </FormField>
        </div>
      </Dialog>
    </div>
  );
};
