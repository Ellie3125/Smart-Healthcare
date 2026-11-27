import React, { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { QrCode, RefreshCw, XCircle } from 'lucide-react';
import { bookingApi } from '../../api/bookingApi';
import {
  Button,
  Card,
  DateTimeText,
  EmptyState,
  ErrorState,
  MoneyText,
  PaginationBar,
  Skeleton,
  StatusPill,
  Tabs,
} from '../../shared/ui';
import { extractPatientError } from '../../shared/lib/errors';
import { usePatientHubEvent } from '../../shared/lib/signalr';

export const MyAppointmentsPage: React.FC = () => {
  const [statusFilter, setStatusFilter] = useState<string>('');
  const [page, setPage] = useState<number>(1);
  const queryClient = useQueryClient();

  // Lịch vừa được xác nhận/hủy ở Worker: tải lại danh sách, không cần F5.
  usePatientHubEvent('AppointmentsUpdated', () => {
    queryClient.invalidateQueries({ queryKey: ['patient', 'appointments'] });
  });

  const query = useQuery({
    queryKey: ['patient', 'appointments', { statusFilter, page }],
    queryFn: () => bookingApi.listMyAppointments({ status: statusFilter || undefined, page, pageSize: 10 }),
  });

  const items = query.data?.items ?? [];

  const confirmedCount = items.filter((a) => a.status === 'Confirmed' || a.status === 'PendingPayment').length;
  const checkedInCount = items.filter((a) => a.status === 'CheckedIn').length;
  const completedCount = items.filter((a) => a.status === 'Completed').length;

  return (
    <div className="space-y-6 max-w-full min-w-0 overflow-x-clip">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div className="min-w-0 flex-1">
          <span className="inline-flex items-center gap-1.5 rounded-full bg-secondary-container/75 px-3 py-0.5 text-xs font-semibold text-[#005231] mb-2">
            Quản lý ngoại trú
          </span>
          <h1 className="text-2xl sm:text-3xl font-bold text-on-surface tracking-tight">Lịch khám của tôi</h1>
        </div>
        <Link to="/booking" className="shrink-0">
          <Button variant="primary">Đặt lịch mới</Button>
        </Link>
      </div>

      {/* Stitch Policy Banner */}
      <div className="rounded-2xl bg-surface-container-low p-4 flex flex-wrap items-center justify-between gap-3 min-w-0">
        <p className="text-sm text-on-surface-variant min-w-0 flex-1 break-words">
          <strong className="text-on-surface">Chính sách đổi lịch &amp; hoàn tiền:</strong> Miễn phí đổi ca khám cùng mức phí và hoàn tiền theo quy định nếu thực hiện trước 05:00 sáng ngày khám.
        </p>
        <span className="rounded-full bg-secondary-container/80 px-3 py-1 text-xs font-semibold text-[#005231] shrink-0">
          Hỗ trợ 24/7
        </span>
      </div>

      {/* 4 Stitch KPI Stat Cards */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4">
        <div className="rounded-2xl bg-surface-container-lowest p-3 sm:p-4 shadow-[0_1px_8px_rgba(15,31,24,0.04)] flex items-center justify-between gap-2 min-w-0">
          <div className="min-w-0">
            <p className="text-[11px] sm:text-xs font-medium uppercase tracking-wide text-on-surface-variant truncate">Tổng lịch</p>
            <p className="mt-0.5 text-xl sm:text-2xl font-bold text-on-surface tabular-nums">{items.length}</p>
          </div>
          <div className="w-9 h-9 sm:w-10 sm:h-10 rounded-xl bg-surface-container-low flex items-center justify-center text-primary shrink-0">
            <span className="material-symbols-outlined text-[20px] sm:text-[22px]">event_note</span>
          </div>
        </div>
        <div className="rounded-2xl bg-surface-container-lowest p-3 sm:p-4 shadow-[0_1px_8px_rgba(15,31,24,0.04)] flex items-center justify-between gap-2 min-w-0">
          <div className="min-w-0">
            <p className="text-[11px] sm:text-xs font-medium uppercase tracking-wide text-on-surface-variant truncate">Sắp khám</p>
            <p className="mt-0.5 text-xl sm:text-2xl font-bold text-primary tabular-nums">{confirmedCount}</p>
          </div>
          <div className="w-9 h-9 sm:w-10 sm:h-10 rounded-xl bg-secondary-container/70 flex items-center justify-center text-[#005231] shrink-0">
            <span className="material-symbols-outlined text-[20px] sm:text-[22px]">calendar_clock</span>
          </div>
        </div>
        <div className="rounded-2xl bg-surface-container-lowest p-3 sm:p-4 shadow-[0_1px_8px_rgba(15,31,24,0.04)] flex items-center justify-between gap-2 min-w-0">
          <div className="min-w-0">
            <p className="text-[11px] sm:text-xs font-medium uppercase tracking-wide text-on-surface-variant truncate">Đã check-in</p>
            <p className="mt-0.5 text-xl sm:text-2xl font-bold text-[#0b57d0] tabular-nums">{checkedInCount}</p>
          </div>
          <div className="w-9 h-9 sm:w-10 sm:h-10 rounded-xl bg-[#e8f0fe] flex items-center justify-center text-[#0b57d0] shrink-0">
            <span className="material-symbols-outlined text-[20px] sm:text-[22px]">how_to_reg</span>
          </div>
        </div>
        <div className="rounded-2xl bg-surface-container-lowest p-3 sm:p-4 shadow-[0_1px_8px_rgba(15,31,24,0.04)] flex items-center justify-between gap-2 min-w-0">
          <div className="min-w-0">
            <p className="text-[11px] sm:text-xs font-medium uppercase tracking-wide text-on-surface-variant truncate">Hoàn tất</p>
            <p className="mt-0.5 text-xl sm:text-2xl font-bold text-on-surface tabular-nums">{completedCount}</p>
          </div>
          <div className="w-9 h-9 sm:w-10 sm:h-10 rounded-xl bg-surface-container-low flex items-center justify-center text-primary shrink-0">
            <span className="material-symbols-outlined text-[20px] sm:text-[22px]">task_alt</span>
          </div>
        </div>
      </div>

      <div className="overflow-x-auto max-w-full pb-1">
        <Tabs
          activeId={statusFilter}
          onChange={(id) => {
            setStatusFilter(id);
            setPage(1);
          }}
          tabs={[
            { id: '', label: 'Tất cả' },
            { id: 'Confirmed', label: 'Đã xác nhận' },
            { id: 'CheckedIn', label: 'Đã check-in' },
            { id: 'Completed', label: 'Hoàn tất' },
            { id: 'Cancelled', label: 'Đã hủy' },
          ]}
        />
      </div>

      {query.isLoading ? (
        <Card><Skeleton rows={4} /></Card>
      ) : query.isError ? (
        <ErrorState message={extractPatientError(query.error).message} onRetry={() => query.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState
          title="Chưa có lịch khám nào"
          description="Bạn có thể bắt đầu đặt lịch với bác sĩ chuyên khoa ngay bây giờ."
          action={
            <Link to="/booking">
              <Button variant="primary">Tìm bác sĩ</Button>
            </Link>
          }
        />
      ) : (
        <div className="space-y-4">
          {items.map((apt) => (
            <Card key={apt.id} className="hover:border-primary/40 transition-colors overflow-hidden">
              <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div className="flex flex-col sm:flex-row items-start gap-4">
                  {/* Date & Code badge matching Stitch 594644e2 */}
                  <div className="rounded-xl bg-surface-container-low px-4 py-3 text-center min-w-[118px] shrink-0">
                    <p className="text-[11px] font-semibold uppercase tracking-wide text-primary">
                      {apt.appointmentCode}
                    </p>
                    <p className="mt-1 text-sm font-bold text-on-surface tabular-nums">
                      <DateTimeText value={apt.serviceDate} mode="date" />
                    </p>
                  </div>

                  <div className="space-y-1.5">
                    <div className="flex flex-wrap items-center gap-2">
                      <StatusPill status={apt.status} />
                      {apt.status === 'CheckedIn' && apt.queueNumber ? (
                        <span className="rounded-full bg-primary px-3 py-0.5 text-xs font-semibold tabular-nums text-white shadow-sm">
                          STT: {apt.queueNumber}
                        </span>
                      ) : null}
                    </div>
                    <h3 className="text-lg font-semibold text-on-surface">{apt.doctorName}</h3>
                    <p className="text-sm text-on-surface-variant">
                      Người khám: <strong className="text-on-surface">{apt.patientName}</strong> • {apt.specialtyName}
                    </p>
                    <p className="text-sm text-on-surface-variant">
                      {apt.roomName} • Phí khám: <MoneyText amount={apt.consultationFee} className="font-semibold text-primary" />
                    </p>
                  </div>
                </div>

                <div className="flex flex-wrap gap-2 md:justify-end">
                  <Link to={`/appointments/${apt.id}/ticket`}>
                    <Button variant="secondary" size="sm" leftIcon={<QrCode className="h-4 w-4" />}>
                      Vé QR
                    </Button>
                  </Link>
                  {apt.canReschedule ? (
                    <Link to={`/appointments/${apt.id}/reschedule`}>
                      <Button variant="outline" size="sm" leftIcon={<RefreshCw className="h-4 w-4" />}>
                        Đổi lịch
                      </Button>
                    </Link>
                  ) : null}
                  {apt.canCancel ? (
                    <Link to={`/appointments/${apt.id}/cancel`}>
                      <Button variant="danger" size="sm" leftIcon={<XCircle className="h-4 w-4" />}>
                        Hủy lịch
                      </Button>
                    </Link>
                  ) : null}
                </div>
              </div>
            </Card>
          ))}
          <PaginationBar page={page} hasMore={Boolean(query.data?.hasMore)} onPageChange={setPage} />
        </div>
      )}
    </div>
  );
};
