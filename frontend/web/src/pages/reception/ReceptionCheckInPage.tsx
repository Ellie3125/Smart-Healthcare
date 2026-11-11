import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckSquare, ShieldAlert } from 'lucide-react';
import { receptionApi } from '../../api/receptionApi';
import {
  Banner,
  Button,
  Card,
  DataTable,
  DateTimeText,
  EmptyState,
  ErrorState,
  FormField,
  Input,
  Skeleton,
  StatusPill,
  useToast,
} from '../../shared/ui';
import { createIdempotencyKey } from '../../shared/lib/idempotency';
import { extractFriendlyError } from '../../shared/lib/errors';

/**
 * RC03 — Check-in tại quầy tiếp đón:
 * - Bắt buộc tick xác nhận đã đối chiếu CCCD/giấy tờ gốc
 * - Tự động chặn nếu đã quá CheckInDeadline (chuyển hướng xử lý ngoại lệ AD11)
 */
export const ReceptionCheckInPage: React.FC = () => {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [keyword, setKeyword] = useState('');
  const [verifiedIds, setVerifiedIds] = useState<Record<string, boolean>>({});

  const query = useQuery({
    queryKey: ['reception', 'checkin-list', keyword],
    queryFn: () => receptionApi.searchUnified(keyword),
  });

  const checkInMutation = useMutation({
    mutationFn: (appointmentId: string) =>
      receptionApi.checkInAppointment(
        appointmentId,
        { identityDocumentVerified: true },
        createIdempotencyKey(`checkin-${appointmentId}`),
      ),
    onSuccess: (res) => {
      pushToast(`Check-in thành công! Số thứ tự ${res.queueNumber} (${res.roomName})`, 'success');
      queryClient.invalidateQueries({ queryKey: ['reception'] });
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  const rows = (query.data ?? []).filter((r) => Boolean(r.appointmentId));

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold text-[#0f1f18]">Check-in tại quầy & Cấp số thứ tự (RC03)</h1>
        <p className="text-xs text-[#44544c]">
          Bắt buộc đối chiếu giấy tờ tùy thân gốc trước khi xác nhận check-in. Ca quá CheckInDeadline phải xử lý tại trung tâm ngoại lệ.
        </p>
      </div>

      <Card>
        <FormField label="Tìm theo SĐT / Tên / Mã lịch hẹn">
          {(props) => (
            <Input
              {...props}
              value={keyword}
              onChange={(e) => setKeyword(e.target.value)}
              placeholder="Quét QR hoặc nhập mã lịch..."
            />
          )}
        </FormField>
      </Card>

      {query.isLoading ? (
        <Card><Skeleton rows={4} /></Card>
      ) : query.isError ? (
        <ErrorState message={extractFriendlyError(query.error).message} onRetry={() => query.refetch()} />
      ) : (
        <DataTable
          rowKey={(r) => r.appointmentId || r.id}
          rows={rows}
          empty={<EmptyState title="Không có lịch hẹn cần check-in" />}
          columns={[
            {
              key: 'apt',
              header: 'Mã lịch & Bệnh nhân',
              render: (r) => (
                <div>
                  <p className="font-mono text-xs font-semibold text-[#006a41]">{r.appointmentCode}</p>
                  <p className="font-semibold text-[#0f1f18]">{r.patientName}</p>
                  <p className="text-xs text-[#44544c]">{r.doctorName} • CCCD {r.cccdMasked}</p>
                </div>
              ),
            },
            {
              key: 'deadline',
              header: 'Hạn check-in',
              render: (r) =>
                r.isPastCheckInDeadline ? (
                  <span className="inline-flex items-center gap-1 rounded-full bg-[#ffdad6] px-2.5 py-0.5 text-xs font-semibold text-[#93000a]">
                    <ShieldAlert className="h-3.5 w-3.5" aria-hidden="true" />
                    Quá hạn CheckInDeadline
                  </span>
                ) : r.checkInDeadline ? (
                  <DateTimeText value={r.checkInDeadline} />
                ) : (
                  'Đúng hạn'
                ),
            },
            {
              key: 'status',
              header: 'Trạng thái / STT',
              render: (r) => (
                <div className="flex items-center gap-2">
                  <StatusPill status={r.appointmentStatus || 'Confirmed'} />
                  {r.queueNumber ? (
                    <span className="rounded bg-[#006a41] px-2 py-0.5 text-xs font-semibold tabular-nums text-white">
                      STT {r.queueNumber}
                    </span>
                  ) : null}
                </div>
              ),
            },
            {
              key: 'verify',
              header: 'Đối chiếu CCCD gốc',
              render: (r) => {
                const aptId = r.appointmentId!;
                if (r.appointmentStatus !== 'Confirmed' || r.isPastCheckInDeadline) return '—';
                const checkboxId = `doc-check-${aptId}`;
                return (
                  <label htmlFor={checkboxId} className="inline-flex items-center gap-2 text-xs font-medium text-[#0f1f18]">
                    <input
                      id={checkboxId}
                      type="checkbox"
                      checked={Boolean(verifiedIds[aptId])}
                      onChange={(e) => setVerifiedIds((prev) => ({ ...prev, [aptId]: e.target.checked }))}
                      className="h-4 w-4 accent-[#006a41]"
                    />
                    <span>Đã xem CCCD gốc</span>
                  </label>
                );
              },
            },
            {
              key: 'act',
              header: 'Thao tác',
              render: (r) => {
                const aptId = r.appointmentId!;
                if (r.appointmentStatus === 'CheckedIn') {
                  return <span className="text-xs font-medium text-[#006a41]">Đã cấp STT</span>;
                }
                if (r.isPastCheckInDeadline) {
                  return (
                    <Banner tone="danger" className="py-1.5 text-xs">
                      Chặn check-in — Báo Quản trị ngoại lệ
                    </Banner>
                  );
                }
                return (
                  <Button
                    size="sm"
                    variant="primary"
                    leftIcon={<CheckSquare className="h-3.5 w-3.5" />}
                    disabled={!verifiedIds[aptId]}
                    loading={checkInMutation.isPending}
                    onClick={() => checkInMutation.mutate(aptId)}
                  >
                    Xác nhận Check-in
                  </Button>
                );
              },
            },
          ]}
        />
      )}
    </div>
  );
};
