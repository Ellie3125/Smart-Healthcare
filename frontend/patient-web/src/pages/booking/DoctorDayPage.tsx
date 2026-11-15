import React, { useMemo, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { ArrowLeft, ShieldAlert, UserCheck } from 'lucide-react';
import { bookingApi } from '../../api/bookingApi';
import { patientGet } from '../../shared/lib/http';
import type { BookingSummary } from '../../types/booking';
import {
  Banner,
  Button,
  Card,
  DateTimeText,
  ErrorState,
  FormField,
  MoneyText,
  Select,
  Skeleton,
  StatusPill,
} from '../../shared/ui';
import { extractPatientError } from '../../shared/lib/errors';
import { getVNDateISO } from '../../shared/lib/datetime';
import { createIdempotencyKey } from '../../shared/lib/money';
import { useToast } from '../../shared/ui';

interface SimpleProfile {
  patientId: string;
  fullName: string;
  relationship: string;
  verificationStatus?: string;
}

export const DoctorDayPage: React.FC = () => {
  const { doctorId = '' } = useParams<{ doctorId: string }>();
  const [searchParams] = useSearchParams();
  const serviceDate = searchParams.get('date') || getVNDateISO();
  const navigate = useNavigate();
  const { pushToast } = useToast();
  const [selectedProfileId, setSelectedProfileId] = useState<string>('');

  const detailQuery = useQuery({
    queryKey: ['patient', 'doctor-day', doctorId, serviceDate],
    queryFn: () => bookingApi.getDoctorDayDetail(doctorId, serviceDate),
    enabled: Boolean(doctorId),
  });

  const profilesQuery = useQuery({
    queryKey: ['patient', 'profiles'],
    queryFn: () => patientGet<SimpleProfile[]>('/api/patient/profiles'),
  });

  const profiles = profilesQuery.data ?? [];
  const activeProfileId = selectedProfileId || profiles[0]?.patientId || '';
  // Khóa chống lặp gắn với hồ sơ đang chọn: bấm lại không tạo tiến trình thứ hai, đổi hồ sơ thì là yêu cầu mới.
  const keys = useMemo(
    () => ({ attempt: createIdempotencyKey(), hold: createIdempotencyKey() }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [activeProfileId],
  );

  const detail = detailQuery.data;
  const isDuplicateSameDay = Boolean(detail?.sameDayBookedByProfileIds.includes(activeProfileId));

  const holdMutation = useMutation({
    mutationFn: async () => {
      const attempt = await bookingApi.createAttempt(activeProfileId, serviceDate, keys.attempt);
      await bookingApi.createHold(attempt.bookingAttemptId, { doctorId, serviceDate }, keys.hold);
      return attempt.bookingAttemptId;
    },
    onSuccess: (bookingAttemptId) => {
      const summary: BookingSummary = {
        doctorName: detail?.fullName ?? '',
        specialtyName: detail?.specialtyName ?? '',
        patientName: profiles.find((p) => p.patientId === activeProfileId)?.fullName ?? '',
        serviceDate,
        amount: detail?.consultationFee ?? 0,
      };
      navigate(`/booking/attempt/${bookingAttemptId}`, { state: summary });
    },
    onError: (err) => {
      pushToast(extractPatientError(err).message, 'danger');
    },
  });

  if (detailQuery.isLoading) {
    return (
      <Card title="Đang tải lịch khám bác sĩ">
        <Skeleton rows={5} />
      </Card>
    );
  }

  if (detailQuery.isError || !detail) {
    return (
      <ErrorState
        message={extractPatientError(detailQuery.error).message}
        onRetry={() => detailQuery.refetch()}
      />
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <Link
          to="/booking"
          className="inline-flex items-center gap-1.5 rounded-xl bg-surface-container-low px-3.5 py-2 text-sm font-semibold text-primary hover:bg-surface-container transition-colors"
        >
          <ArrowLeft className="h-4 w-4" aria-hidden="true" />
          Quay lại tìm bác sĩ
        </Link>
        <span className="text-xs font-medium text-on-surface-variant">
          Bước 1/3 • Chọn hồ sơ người khám &amp; giữ suất
        </span>
      </div>

      <div className="grid gap-6 lg:grid-cols-12 items-start">
        <div className="lg:col-span-7">
          <Card
            title={detail.fullName}
            subtitle={`${detail.doctorRankName} • Chuyên khoa ${detail.specialtyName}`}
            actions={<StatusPill status={detail.remainingSlots > 0 ? 'Open' : 'Closed'} />}
          >
            <div className="grid gap-3.5 sm:grid-cols-3">
              <div className="rounded-xl bg-surface-container-low p-4">
                <p className="text-xs text-on-surface-variant">Ngày khám</p>
                <p className="mt-1 text-base font-semibold text-on-surface">
                  <DateTimeText value={detail.serviceDate} mode="date" />
                </p>
              </div>
              <div className="rounded-xl bg-surface-container-low p-4">
                <p className="text-xs text-on-surface-variant">Phòng khám</p>
                <p className="mt-1 text-base font-semibold text-on-surface">{detail.roomName}</p>
              </div>
              <div className="rounded-xl bg-secondary-container/55 p-4">
                <p className="text-xs text-[#005231]">Phí khám</p>
                <p className="mt-1 text-lg font-bold text-primary">
                  <MoneyText amount={detail.consultationFee} />
                </p>
              </div>
            </div>

            <div className="mt-4 rounded-xl bg-surface-container-low/75 p-4 text-sm text-on-surface-variant">
              <p className="font-semibold text-on-surface">
                Suất khám còn lại trong ngày:{' '}
                <span className="tabular-nums text-primary font-bold">
                  {detail.remainingSlots} / {detail.totalSlots}
                </span>
              </p>
              <p className="mt-1 leading-relaxed">{detail.walkInReservedNote}</p>
            </div>
          </Card>
        </div>

        <div className="lg:col-span-5">
          <Card title="Thông tin người đi khám" subtitle="Giữ chỗ 5 phút trước khi thanh toán">
            <div className="space-y-4">
              <FormField
                label="Chọn hồ sơ người đi khám"
                required
                hint="Bạn có thể đặt lịch cho bản thân hoặc người thân đã thêm trong tài khoản."
              >
                {(props) => (
                  <Select
                    {...props}
                    value={activeProfileId}
                    onChange={(e) => setSelectedProfileId(e.target.value)}
                    options={profiles.map((p) => ({
                      value: p.id,
                      label: `${p.fullName} (${p.relationship})${p.verificationStatus === 'Pending' ? ' - Chờ xác minh' : ''}`,
                    }))}
                  />
                )}
              </FormField>

              {isDuplicateSameDay ? (
                <Banner tone="warning" title="Hồ sơ đã có lịch trong ngày này">
                  <div className="flex items-start gap-2">
                    <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
                    <span>
                      Hồ sơ được chọn đã giữ hoặc có một lịch khám trong ngày{' '}
                      <DateTimeText value={detail.serviceDate} mode="date" />. Vui lòng chọn ngày khác hoặc hồ sơ người thân khác.
                    </span>
                  </div>
                </Banner>
              ) : null}

              <div className="rounded-xl bg-surface-container-low/75 p-3.5 flex items-start gap-2.5 text-xs text-on-surface-variant">
                <UserCheck className="h-4 w-4 text-primary shrink-0 mt-0.5" aria-hidden="true" />
                <span>Bấm tiếp tục để giữ chỗ trong 5 phút và chuyển sang cổng thanh toán VNPAY.</span>
              </div>

              <Button
                variant="primary"
                size="lg"
                className="w-full"
                loading={holdMutation.isPending}
                disabled={detail.remainingSlots <= 0 || isDuplicateSameDay}
                onClick={() => holdMutation.mutate()}
              >
                Giữ chỗ 5 phút & Tiếp tục
              </Button>
            </div>
          </Card>
        </div>
      </div>
    </div>
  );
};
