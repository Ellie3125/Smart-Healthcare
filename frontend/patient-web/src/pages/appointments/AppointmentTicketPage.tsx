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
    <div className="mx-auto max-w-2xl space-y-5">
      <Link
        to="/appointments"
        className="inline-flex items-center gap-1.5 rounded-xl bg-surface-container-low px-3.5 py-2 text-sm font-semibold text-primary hover:bg-surface-container transition-colors"
      >
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        Quay lại danh sách lịch khám
      </Link>

      {/* Stitch Perforated E-Ticket Card matching de952b08 */}
      <div className="rounded-2xl overflow-hidden bg-surface-container-lowest border border-surface-container-highest/70 shadow-[0_8px_28px_rgba(15,31,24,0.06)]">
        <div className="bg-gradient-to-r from-[#006a41] to-[#028654] text-white px-6 py-5 flex flex-wrap items-center justify-between gap-3">
          <div>
            <span className="text-[11px] font-semibold uppercase tracking-wider text-[#9ef5c0]">
              Vé Khám Bệnh Điện Tử
            </span>
            <h1 className="text-xl font-bold mt-0.5">Vé khám {t.appointmentCode}</h1>
            <p className="text-xs text-white/85 mt-0.5">
              {t.specialtyName} • {t.roomName}
            </p>
          </div>
          <StatusPill status={t.status} />
        </div>

        <div className="p-6 sm:p-8 flex flex-col items-center gap-5 text-center">
          <div className="flex h-48 w-48 flex-col items-center justify-center rounded-2xl border-2 border-dashed border-primary bg-surface p-4 shadow-inner">
            <QrCode className="h-28 w-28 text-primary" aria-hidden="true" />
            <span className="mt-2 break-all font-mono text-[11px] font-semibold text-on-surface">
              {t.qrPayload}
            </span>
          </div>

          {hasCheckedIn && t.queueNumber ? (
            <div className="w-full rounded-2xl bg-primary px-5 py-4 text-white shadow-sm">
              <p className="text-xs uppercase tracking-wider text-[#9ef5c0]">Số thứ tự của bạn</p>
              <p className="mt-1 text-4xl font-bold tabular-nums">{t.queueNumber}</p>
            </div>
          ) : (
            <Banner tone="info" title="Chưa cấp số thứ tự">
              {t.checkInInstruction}
            </Banner>
          )}

          {/* Perforated divider */}
          <div className="w-full flex items-center gap-3 py-1" aria-hidden="true">
            <div className="flex-1 border-t-2 border-dashed border-surface-container-highest" />
          </div>

          <dl className="grid w-full grid-cols-2 gap-3 text-left text-sm">
            <div className="rounded-xl bg-surface-container-low/75 p-3.5">
              <dt className="text-xs text-on-surface-variant">Người khám</dt>
              <dd className="mt-0.5 font-semibold text-on-surface">{t.patientName}</dd>
            </div>
            <div className="rounded-xl bg-surface-container-low/75 p-3.5">
              <dt className="text-xs text-on-surface-variant">Ngày khám</dt>
              <dd className="mt-0.5 font-semibold text-on-surface tabular-nums">
                <DateTimeText value={t.serviceDate} mode="date" />
              </dd>
            </div>
            <div className="col-span-2 rounded-xl bg-surface-container-low/75 p-3.5">
              <dt className="text-xs text-on-surface-variant">Bác sĩ &amp; Phòng khám</dt>
              <dd className="mt-0.5 font-semibold text-on-surface">
                {t.doctorName} — {t.roomName}
              </dd>
            </div>
          </dl>

          <div className="w-full rounded-xl bg-surface-container-low/65 p-3.5 text-left text-xs text-on-surface-variant">
            <strong className="text-on-surface">Lưu ý khi đến khám:</strong> Vui lòng mang theo bản gốc CCCD/VNeID để quét mã và đối chiếu tại quầy tiếp tân trước giờ khám 15 phút.
          </div>

          {hasCheckedIn ? (
            <Link to="/today-journey" className="w-full">
              <Button variant="primary" size="lg" className="w-full">
                Theo dõi hành trình hôm nay
              </Button>
            </Link>
          ) : null}
        </div>
      </div>
    </div>
  );
};
