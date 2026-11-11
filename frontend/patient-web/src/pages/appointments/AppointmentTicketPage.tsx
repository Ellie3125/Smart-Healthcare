import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { ArrowLeft, QrCode } from 'lucide-react';
import { bookingApi } from '../../api/bookingApi';
import { Banner, Button, Card, DateTimeText, ErrorState, Skeleton, StatusPill } from '../../shared/ui';
import { extractPatientError } from '../../shared/lib/errors';

/**
 * P13 — Vé khám QR. Số thứ tự CHỈ được cấp và hiển thị sau khi Check-in thành công.
 */
export const AppointmentTicketPage: React.FC = () => {
  const { id = '' } = useParams<{ id: string }>();
  const ticketQuery = useQuery({
    queryKey: ['patient', 'appointment-ticket', id],
    queryFn: () => bookingApi.getAppointmentTicket(id),
    enabled: Boolean(id),
  });

  if (ticketQuery.isLoading) {
    return (
      <Card title="Đang tải vé khám">
        <Skeleton rows={4} />
      </Card>
    );
  }

  if (ticketQuery.isError || !ticketQuery.data) {
    return (
      <ErrorState
        message={extractPatientError(ticketQuery.error).message}
        onRetry={() => ticketQuery.refetch()}
      />
    );
  }

  const t = ticketQuery.data;
  const hasCheckedIn = t.status === 'CheckedIn' || t.status === 'Completed';

  return (
    <div className="mx-auto max-w-xl space-y-5">
      <Link to="/appointments" className="inline-flex items-center gap-1.5 text-sm font-medium text-[#006a41] hover:underline">
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        Quay lại danh sách lịch khám
      </Link>

      <Card
        title={`Vé khám ${t.appointmentCode}`}
        subtitle={`${t.specialtyName} • ${t.roomName}`}
        actions={<StatusPill status={t.status} />}
      >
        <div className="flex flex-col items-center gap-4 text-center">
          <div className="flex h-44 w-44 flex-col items-center justify-center rounded-2xl border-2 border-dashed border-[#006a41] bg-[#ebfef2] p-4">
            <QrCode className="h-24 w-24 text-[#006a41]" aria-hidden="true" />
            <span className="mt-2 break-all font-mono text-[11px] font-semibold text-[#0f1f18]">
              {t.qrPayload}
            </span>
          </div>

          {hasCheckedIn && t.queueNumber ? (
            <div className="w-full rounded-2xl bg-[#006a41] px-5 py-4 text-white">
              <p className="text-xs uppercase tracking-wider opacity-90">Số thứ tự của bạn</p>
              <p className="mt-1 text-4xl font-bold tabular-nums">{t.queueNumber}</p>
            </div>
          ) : (
            <Banner tone="info" title="Chưa cấp số thứ tự">
              {t.checkInInstruction}
            </Banner>
          )}

          <dl className="grid w-full grid-cols-2 gap-3 text-left text-sm">
            <div className="rounded-xl bg-[#f8fbf9] p-3">
              <dt className="text-xs text-[#3e4941]">Người khám</dt>
              <dd className="mt-0.5 font-semibold text-[#0f1f18]">{t.patientName}</dd>
            </div>
            <div className="rounded-xl bg-[#f8fbf9] p-3">
              <dt className="text-xs text-[#3e4941]">Ngày khám</dt>
              <dd className="mt-0.5 font-semibold text-[#0f1f18]">
                <DateTimeText value={t.serviceDate} mode="date" />
              </dd>
            </div>
            <div className="col-span-2 rounded-xl bg-[#f8fbf9] p-3">
              <dt className="text-xs text-[#3e4941]">Bác sĩ & Phòng khám</dt>
              <dd className="mt-0.5 font-semibold text-[#0f1f18]">
                {t.doctorName} — {t.roomName}
              </dd>
            </div>
          </dl>

          {hasCheckedIn ? (
            <Link to="/today-journey" className="w-full">
              <Button variant="primary" className="w-full">
                Theo dõi hành trình hôm nay
              </Button>
            </Link>
          ) : null}
        </div>
      </Card>
    </div>
  );
};
