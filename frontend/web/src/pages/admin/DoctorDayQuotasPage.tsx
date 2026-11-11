import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Lock, Unlock, ArrowLeftRight } from 'lucide-react';
import { adminOpsApi } from '../../api/adminOpsApi';
import {
  Banner,
  Button,
  Card,
  DataTable,
  DatePicker,
  Dialog,
  ErrorState,
  FormField,
  Select,
  Skeleton,
  StatusPill,
  Textarea,
  useToast,
} from '../../shared/ui';
import { getVNDateISO, isBeforeCutoffVN } from '../../shared/lib/datetime';
import { createIdempotencyKey } from '../../shared/lib/idempotency';
import { extractFriendlyError } from '../../shared/lib/errors';
import type { DoctorDayQuotaItem } from '../../types/staffWorkflow';

/**
 * AD06 + AD07 — Quản lý suất khám DoctorDay theo ngày + Chuyển bác sĩ / Hủy phiên trước 06:30 GMT+7.
 */
export const DoctorDayQuotasPage: React.FC = () => {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [serviceDate, setServiceDate] = useState(getVNDateISO());
  const [sessionModal, setSessionModal] = useState<DoctorDayQuotaItem | null>(null);
  const [actionType, setActionType] = useState<'Swap' | 'Cancel'>('Swap');
  const [substituteDoctorId, setSubstituteDoctorId] = useState('');
  const [reason, setReason] = useState('');

  const query = useQuery({
    queryKey: ['admin', 'doctor-days', serviceDate],
    queryFn: () => adminOpsApi.listDoctorDayQuotas(serviceDate),
  });

  const toggleMutation = useMutation({
    mutationFn: async (row: DoctorDayQuotaItem): Promise<void> => {
      if (row.status === 'Open') {
        await adminOpsApi.closeDoctorDay(row.doctorId, serviceDate);
      } else {
        await adminOpsApi.openDoctorDay(row.doctorId, serviceDate);
      }
    },
    onSuccess: () => {
      pushToast('Đã cập nhật trạng thái ngày khám bác sĩ.', 'success');
      queryClient.invalidateQueries({ queryKey: ['admin', 'doctor-days', serviceDate] });
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  const sessionMutation = useMutation({
    mutationFn: () =>
      adminOpsApi.swapOrCancelDoctorSession(
        sessionModal!.doctorId,
        serviceDate,
        { action: actionType, substituteDoctorId: substituteDoctorId || undefined, reason: reason.trim() },
        createIdempotencyKey(`session-${sessionModal!.doctorId}`),
      ),
    onSuccess: (res) => {
      pushToast(
        actionType === 'Cancel'
          ? `Đã hủy phiên và đưa ${res.affectedCount} lịch vào danh sách tiếp tân cần gọi.`
          : 'Đã chuyển phiên sang bác sĩ thay thế.',
        'success',
      );
      setSessionModal(null);
      setReason('');
      queryClient.invalidateQueries({ queryKey: ['admin', 'doctor-days', serviceDate] });
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  const rows = query.data ?? [];
  const before0630 = isBeforeCutoffVN(serviceDate, 6, 30);

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold text-[#0f1f18]">Suất khám theo ngày & Điều chỉnh phiên (AD06–AD07)</h1>
          <p className="text-xs text-[#44544c]">
            Mỗi ngày bác sĩ nhận tối đa 30 lượt dùng chung giữa đặt online và đăng ký trực tiếp tại quầy.
          </p>
        </div>
        <div className="w-48">
          <FormField label="Ngày khám">
            {(props) => <DatePicker {...props} value={serviceDate} onChange={(e) => setServiceDate(e.target.value)} />}
          </FormField>
        </div>
      </div>

      {!before0630 ? (
        <Banner tone="warning" title="Đã qua mốc 06:30 ngày khám">
          Sau 06:30 GMT+7, không thể chuyển bác sĩ hoặc hủy phiên khám trực tiếp từ bảng này.
        </Banner>
      ) : null}

      {query.isLoading ? (
        <Card><Skeleton rows={4} /></Card>
      ) : query.isError ? (
        <ErrorState message={extractFriendlyError(query.error).message} onRetry={() => query.refetch()} />
      ) : (
        <DataTable
          rowKey={(r) => r.id}
          rows={rows}
          columns={[
            {
              key: 'doctor',
              header: 'Bác sĩ & Chuyên khoa',
              render: (r) => (
                <div>
                  <p className="font-semibold text-[#0f1f18]">{r.doctorName}</p>
                  <p className="text-xs text-[#44544c]">{r.doctorRankName} • {r.specialtyName}</p>
                </div>
              ),
            },
            { key: 'room', header: 'Phòng', render: (r) => r.roomName },
            {
              key: 'quota',
              header: 'Quota (Online / Quầy / Còn)',
              align: 'right',
              render: (r) => (
                <span className="tabular-nums font-medium">
                  {r.onlineBookedCount} / {r.walkInBookedCount} / <strong className="text-[#006a41]">{r.remainingSlots}</strong> ({r.totalSlots})
                </span>
              ),
            },
            { key: 'status', header: 'Trạng thái', render: (r) => <StatusPill status={r.status} /> },
            {
              key: 'actions',
              header: 'Thao tác quản trị',
              render: (r) => (
                <div className="flex flex-wrap gap-2">
                  <Button
                    size="sm"
                    variant={r.status === 'Open' ? 'outline' : 'secondary'}
                    leftIcon={r.status === 'Open' ? <Lock className="h-3.5 w-3.5" /> : <Unlock className="h-3.5 w-3.5" />}
                    onClick={() => toggleMutation.mutate(r)}
                  >
                    {r.status === 'Open' ? 'Đóng ngày' : 'Mở ngày'}
                  </Button>
                  <Button
                    size="sm"
                    variant="warning"
                    disabled={!before0630}
                    leftIcon={<ArrowLeftRight className="h-3.5 w-3.5" />}
                    onClick={() => setSessionModal(r)}
                  >
                    Đổi / Hủy phiên
                  </Button>
                </div>
              ),
            },
          ]}
        />
      )}

      <Dialog
        open={Boolean(sessionModal)}
        onClose={() => setSessionModal(null)}
        title={`Điều chỉnh phiên — ${sessionModal?.doctorName}`}
        footer={
          <>
            <Button variant="outline" onClick={() => setSessionModal(null)}>Đóng</Button>
            <Button
              variant={actionType === 'Cancel' ? 'danger' : 'primary'}
              loading={sessionMutation.isPending}
              disabled={!reason.trim() || (actionType === 'Swap' && !substituteDoctorId)}
              onClick={() => sessionMutation.mutate()}
            >
              Xác nhận thực hiện
            </Button>
          </>
        }
      >
        <div className="space-y-3">
          <FormField label="Hình thức xử lý">
            {(props) => (
              <Select
                {...props}
                value={actionType}
                onChange={(e) => setActionType(e.target.value as 'Swap' | 'Cancel')}
                options={[
                  { value: 'Swap', label: 'Chuyển sang bác sĩ cùng chuyên khoa thay thế' },
                  { value: 'Cancel', label: 'Hủy phiên & tạo danh sách tiếp tân cần gọi' },
                ]}
              />
            )}
          </FormField>
          {actionType === 'Swap' ? (
            <FormField label="Bác sĩ thay thế" required>
              {(props) => (
                <Select
                  {...props}
                  placeholder="Chọn bác sĩ thay thế"
                  value={substituteDoctorId}
                  onChange={(e) => setSubstituteDoctorId(e.target.value)}
                  options={rows
                    .filter((d) => d.doctorId !== sessionModal?.doctorId)
                    .map((d) => ({ value: d.doctorId, label: `${d.doctorName} (${d.specialtyName})` }))}
                />
              )}
            </FormField>
          ) : null}
          <FormField label="Lý do điều chỉnh" required>
            {(props) => (
              <Textarea
                {...props}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                placeholder="Ghi rõ lý do..."
              />
            )}
          </FormField>
        </div>
      </Dialog>
    </div>
  );
};
