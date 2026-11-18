import React, { useEffect, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { Activity, CreditCard, Radio } from 'lucide-react';
import { clinicalApi } from '../../api/clinicalApi';
import { createPatientRealtimeConnection } from '../../shared/lib/signalr';
import {
  Banner,
  Button,
  Card,
  DateTimeText,
  ErrorState,
  Skeleton,
  StatusPill,
} from '../../shared/ui';
import { extractPatientError } from '../../shared/lib/errors';

export const TodayJourneyPage: React.FC = () => {
  const queryClient = useQueryClient();
  const [rtStatus, setRtStatus] = useState<'connecting' | 'connected' | 'reconnecting' | 'disconnected'>('connecting');
  const [liveAnnouncement, setLiveAnnouncement] = useState<string>('');

  const journeyQuery = useQuery({
    queryKey: ['patient', 'today-journey'],
    queryFn: () => clinicalApi.getTodayJourney(),
  });

  useEffect(() => {
    const conn = createPatientRealtimeConnection(
      '/hubs/patient',
      () => localStorage.getItem('patient_token'),
      ['JourneyUpdated', 'QueueCalled'],
      {
        onStatusChange: setRtStatus,
        onEvent: (eventName, payload) => {
          const text =
            typeof payload === 'object' && payload !== null && 'message' in payload
              ? String((payload as { message?: string }).message)
              : `Cập nhật trạng thái khám (${eventName})`;
          setLiveAnnouncement(text);
          queryClient.invalidateQueries({ queryKey: ['patient', 'today-journey'] });
        },
      },
    );
    conn.start();
    return () => {
      conn.stop();
    };
  }, [queryClient]);

  if (journeyQuery.isLoading) {
    return (
      <Card title="Hành trình khám hôm nay">
        <Skeleton rows={5} />
      </Card>
    );
  }

  if (journeyQuery.isError || !journeyQuery.data) {
    return (
      <ErrorState
        message={extractPatientError(journeyQuery.error).message}
        onRetry={() => journeyQuery.refetch()}
      />
    );
  }

  const j = journeyQuery.data;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <span className="inline-flex items-center gap-1.5 rounded-full bg-secondary-container/75 px-3 py-0.5 text-xs font-semibold text-[#005231] mb-2">
            Theo dõi thời gian thực
          </span>
          <h1 className="text-2xl sm:text-3xl font-bold text-on-surface tracking-tight">Hành trình khám hôm nay</h1>
          <p className="mt-1 text-sm text-on-surface-variant">
            Người khám: <strong className="text-on-surface">{j.patientName}</strong> • Ngày{' '}
            <DateTimeText value={j.serviceDate} mode="date" />
          </p>
        </div>
        <span className="inline-flex items-center gap-2 rounded-full bg-surface-container-lowest px-3.5 py-1.5 text-xs font-semibold text-on-surface shadow-sm">
          <span
            className={`w-2 h-2 rounded-full ${rtStatus === 'connected' ? 'bg-primary animate-pulse' : 'bg-[#9a5b00]'}`}
            aria-hidden="true"
          />
          <Radio className={`h-3.5 w-3.5 ${rtStatus === 'connected' ? 'text-primary' : 'text-[#9a5b00]'}`} aria-hidden="true" />
          {rtStatus === 'connected' ? 'Đang kết nối trực tiếp' : 'Đang đồng bộ lại'}
        </span>
      </div>

      <div aria-live="polite" className="rounded-2xl bg-secondary-container/55 p-5 text-sm text-[#005231] shadow-sm">
        <div className="flex items-start gap-3">
          <div className="w-10 h-10 rounded-xl bg-primary text-white flex items-center justify-center shrink-0">
            <Activity className="h-5 w-5" aria-hidden="true" />
          </div>
          <div>
            <p className="font-bold text-base">Thông báo lượt gọi hiện tại</p>
            <p className="mt-1">{liveAnnouncement || j.currentCallText || 'Vui lòng theo dõi bảng gọi số tại khu vực chờ.'}</p>
          </div>
        </div>
      </div>

      {j.unpaidClsCount > 0 ? (
        <Banner
          tone="warning"
          title={`Bạn có ${j.unpaidClsCount} dịch vụ cận lâm sàng chờ thanh toán`}
          action={
            <Link to="/cls-payments">
              <Button size="sm" variant="primary" leftIcon={<CreditCard className="h-4 w-4" />}>
                Thanh toán ngay
              </Button>
            </Link>
          }
        >
          Hoàn tất thanh toán trong thời hạn để được gọi vào phòng xét nghiệm / chẩn đoán hình ảnh.
        </Banner>
      ) : null}

      <Card title="Các chặng thực hiện trong ngày" subtitle="Tiến trình khám chữa bệnh tại phòng khám">
        <ol className="relative space-y-4">
          {j.steps.map((step, index) => (
            <li
              key={step.id}
              className="flex flex-wrap items-start justify-between gap-3 rounded-2xl bg-surface-container-low/75 p-4 transition-colors hover:bg-surface-container-low"
            >
              <div className="flex items-start gap-3.5">
                <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-primary text-xs font-bold text-white shadow-sm">
                  {index + 1}
                </span>
                <div>
                  <p className="font-semibold text-on-surface">{step.title}</p>
                  <p className="text-sm text-on-surface-variant">{step.location}</p>
                  {step.detail ? <p className="mt-1 text-xs text-on-surface-variant">{step.detail}</p> : null}
                </div>
              </div>
              <div className="flex items-center gap-2">
                {step.displayNumber || step.queueNumber ? (
                  <span className="rounded-xl bg-surface-container-lowest px-3 py-1 text-xs font-bold tabular-nums text-primary shadow-sm">
                    Số {step.displayNumber || step.queueNumber}
                  </span>
                ) : null}
                <StatusPill status={step.status} />
              </div>
            </li>
          ))}
        </ol>
      </Card>
    </div>
  );
};
