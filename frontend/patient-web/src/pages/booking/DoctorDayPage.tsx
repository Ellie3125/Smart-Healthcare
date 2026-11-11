import React, { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { ArrowLeft, ShieldAlert, UserCheck } from 'lucide-react';
import { bookingApi } from '../../api/bookingApi';
import { patientGet } from '../../shared/lib/http';
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
  id: string;
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
  const [idemKey] = useState(() => createIdempotencyKey('hold'));
  const [bookingAttemptId] = useState(() => createIdempotencyKey('attempt'));

  const detailQuery = useQuery({
    queryKey: ['patient', 'doctor-day', doctorId, serviceDate],
    queryFn: () => bookingApi.getDoctorDayDetail(doctorId, serviceDate),
    enabled: Boolean(doctorId),
  });

  const profilesQuery = useQuery({
    queryKey: ['patient', 'profiles'],
    queryFn: () => patientGet<SimpleProfile[]>('/api/identity/patient/profiles'),
  });

  const profiles = profilesQuery.data ?? [];
  const activeProfileId = selectedProfileId || profiles[0]?.id || 'prof-self';

  const detail = detailQuery.data;
  const isDuplicateSameDay = Boolean(detail?.sameDayBookedByProfileIds.includes(activeProfileId));

  const holdMutation = useMutation({
    mutationFn: () =>
      bookingApi.createHold(
        bookingAttemptId,
        { doctorId, serviceDate, patientProfileId: activeProfileId },
        idemKey,
      ),
    onSuccess: (res) => {
      const targetAttemptId = res.bookingAttemptId || bookingAttemptId;
      navigate(`/booking/attempt/${targetAttemptId}`);
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
    <div className="space-y-5">
      <Link to="/booking" className="inline-flex items-center gap-1.5 text-sm font-medium text-[#006a41] hover:underline">
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        Quay lại tìm bác sĩ
      </Link>

      <Card
        title={detail.fullName}
        subtitle={`${detail.doctorRankName} • Chuyên khoa ${detail.specialtyName}`}
        actions={<StatusPill status={detail.remainingSlots > 0 ? 'Open' : 'Closed'} />}
      >
        <div className="grid gap-4 sm:grid-cols-3">
          <div className="rounded-xl bg-[#e5f8ed] p-4">
            <p className="text-xs text-[#3e4941]">Ngày khám</p>
            <p className="mt-1 text-base font-semibold text-[#0f1f18]">
              <DateTimeText value={detail.serviceDate} mode="date" />
            </p>
          </div>
          <div className="rounded-xl bg-[#e5f8ed] p-4">
            <p className="text-xs text-[#3e4941]">Phòng khám</p>
            <p className="mt-1 text-base font-semibold text-[#0f1f18]">{detail.roomName}</p>
          </div>
          <div className="rounded-xl bg-[#e5f8ed] p-4">
            <p className="text-xs text-[#3e4941]">Phí khám</p>
            <p className="mt-1 text-base font-semibold text-[#006a41]">
              <MoneyText amount={detail.consultationFee} />
            </p>
          </div>
        </div>

        <div className="mt-4 rounded-xl border border-[#bdcabf] bg-[#f8fbf9] p-4 text-sm text-[#3e4941]">
          <p className="font-medium text-[#0f1f18]">
            Suất khám còn lại trong ngày: <span className="tabular-nums text-[#006a41]">{detail.remainingSlots} / {detail.totalSlots}</span>
          </p>
          <p className="mt-1">{detail.walkInReservedNote}</p>
        </div>

        <div className="mt-5 space-y-4">
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

          <div className="flex flex-wrap items-center justify-between gap-3 pt-2">
            <div className="flex items-center gap-2 text-xs text-[#3e4941]">
              <UserCheck className="h-4 w-4 text-[#006a41]" aria-hidden="true" />
              <span>Bấm tiếp tục để giữ chỗ trong 5 phút và chuyển sang cổng thanh toán VNPAY.</span>
            </div>
            <Button
              variant="primary"
              size="lg"
              loading={holdMutation.isPending}
              disabled={detail.remainingSlots <= 0 || isDuplicateSameDay}
              onClick={() => holdMutation.mutate()}
            >
              Giữ chỗ 5 phút & Tiếp tục
            </Button>
          </div>
        </div>
      </Card>
    </div>
  );
};
