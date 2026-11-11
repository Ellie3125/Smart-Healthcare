import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
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

export const MyAppointmentsPage: React.FC = () => {
  const [statusFilter, setStatusFilter] = useState<string>('');
  const [page, setPage] = useState<number>(1);

  const query = useQuery({
    queryKey: ['patient', 'appointments', { statusFilter, page }],
    queryFn: () => bookingApi.listMyAppointments({ status: statusFilter || undefined, page, pageSize: 10 }),
  });

  const items = query.data?.items ?? [];

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-[#0f1f18]">Lịch khám của tôi</h1>
          <p className="mt-1 text-sm text-[#3e4941]">
            Quản lý vé QR, theo dõi số thứ tự sau check-in, đổi lịch trước 05:00 hoặc hủy lịch nhận hoàn tiền.
          </p>
        </div>
        <Link to="/booking">
          <Button variant="primary">Đặt lịch mới</Button>
        </Link>
      </div>

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
            <Card key={apt.id}>
              <div className="flex flex-wrap items-start justify-between gap-4">
                <div className="space-y-1.5">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="text-xs font-semibold tracking-wide text-[#006a41]">
                      {apt.appointmentCode}
                    </span>
                    <StatusPill status={apt.status} />
                    {apt.status === 'CheckedIn' && apt.queueNumber ? (
                      <span className="rounded-full bg-[#006a41] px-3 py-0.5 text-xs font-semibold tabular-nums text-white">
                        STT: {apt.queueNumber}
                      </span>
                    ) : null}
                  </div>
                  <h3 className="text-lg font-semibold text-[#0f1f18]">{apt.doctorName}</h3>
                  <p className="text-sm text-[#3e4941]">
                    Người khám: <strong>{apt.patientName}</strong> • {apt.specialtyName}
                  </p>
                  <p className="text-sm text-[#3e4941]">
                    Ngày khám: <DateTimeText value={apt.serviceDate} mode="date" /> • {apt.roomName} • Phí:{' '}
                    <MoneyText amount={apt.consultationFee} />
                  </p>
                </div>

                <div className="flex flex-wrap gap-2">
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
