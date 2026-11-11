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
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-[#0f1f18]">Hành trình khám hôm nay</h1>
          <p className="mt-1 text-sm text-[#3e4941]">
            Người khám: <strong>{j.patientName}</strong> • Ngày <DateTimeText value={j.serviceDate} mode="date" />
          </p>
        </div>
        <span className="inline-flex items-center gap-1.5 rounded-full border border-[#bdcabf] bg-white px-3 py-1.5 text-xs font-medium text-[#3e4941]">
          <Radio className={`h-3.5 w-3.5 ${rtStatus === 'connected' ? 'text-[#006a41]' : 'text-[#9a5b00]'}`} aria-hidden="true" />
          {rtStatus === 'connected' ? 'Đang kết nối trực tiếp' : 'Đang đồng bộ lại'}
        </span>
      </div>

      <div aria-live="polite" className="rounded-2xl border border-[#8ef8bb] bg-[#e5f8ed] p-4 text-sm text-[#005231]">
        <div className="flex items-start gap-2.5">
          <Activity className="mt-0.5 h-5 w-5 shrink-0" aria-hidden="true" />
          <div>
            <p className="font-semibold">Thông báo lượt gọi hiện tại</p>
            <p className="mt-0.5">{liveAnnouncement || j.currentCallText || 'Vui lòng theo dõi bảng gọi số tại khu vực chờ.'}</p>
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

      <Card title="Các chặng thực hiện trong ngày">
        <ol className="space-y-4">
          {j.steps.map((step, index) => (
            <li
              key={step.id}
              className="flex flex-wrap items-start justify-between gap-3 rounded-xl border border-[#e5f8ed] bg-[#f8fbf9] p-4"
            >
              <div className="flex items-start gap-3">
                <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-[#006a41] text-xs font-semibold text-white">
                  {index + 1}
                </span>
                <div>
                  <p className="font-semibold text-[#0f1f18]">{step.title}</p>
                  <p className="text-sm text-[#3e4941]">{step.location}</p>
                  {step.detail ? <p className="mt-1 text-xs text-[#3e4941]">{step.detail}</p> : null}
                </div>
              </div>
              <div className="flex items-center gap-2">
                {step.queueNumber ? (
                  <span className="rounded-lg bg-white px-2.5 py-1 text-xs font-semibold tabular-nums text-[#006a41] border border-[#8ef8bb]">
                    Số {step.queueNumber}
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
