import React, { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { CreditCard, ShieldCheck } from 'lucide-react';
import { bookingApi } from '../../api/bookingApi';
import {
  Banner,
  Button,
  Card,
  Countdown,
  DateTimeText,
  ErrorState,
  MoneyText,
  Skeleton,
  StatusPill,
  Stepper,
  useToast,
} from '../../shared/ui';
import { createIdempotencyKey } from '../../shared/lib/money';
import { extractPatientError } from '../../shared/lib/errors';

export const BookingConfirmPage: React.FC = () => {
  const { bookingAttemptId = '' } = useParams<{ bookingAttemptId: string }>();
  const navigate = useNavigate();
  const { pushToast } = useToast();
  const [expired, setExpired] = useState(false);
  const [checkoutIdemKey] = useState(() => createIdempotencyKey('vnpay'));

  const attemptQuery = useQuery({
    queryKey: ['patient', 'booking-attempt', bookingAttemptId],
    queryFn: () => bookingApi.getBookingAttempt(bookingAttemptId),
    enabled: Boolean(bookingAttemptId),
  });

  const checkoutMutation = useMutation({
    mutationFn: () => bookingApi.createVnpayCheckout(bookingAttemptId, checkoutIdemKey),
    onSuccess: (res) => {
      if (res.paymentUrl.startsWith('/')) {
        navigate(res.paymentUrl);
      } else {
        window.location.assign(res.paymentUrl);
      }
    },
    onError: (err) => {
      pushToast(extractPatientError(err).message, 'danger');
    },
  });

  if (attemptQuery.isLoading) {
    return (
      <Card title="Đang tải thông tin giữ chỗ">
        <Skeleton rows={4} />
      </Card>
    );
  }

  if (attemptQuery.isError || !attemptQuery.data) {
    return (
      <ErrorState
        message={extractPatientError(attemptQuery.error).message}
        onRetry={() => attemptQuery.refetch()}
      />
    );
  }

  const attempt = attemptQuery.data;
  const isHoldExpired = expired || attempt.status === 'Expired';

  return (
    <div className="space-y-6">
      <Stepper
        currentStepIndex={1}
        steps={[
          { id: 'choose', label: 'Chọn bác sĩ' },
          { id: 'hold', label: 'Xác nhận & Giữ chỗ 5p' },
          { id: 'vnpay', label: 'Thanh toán VNPAY' },
        ]}
      />

      <Card
        title="Xác nhận thông tin đặt khám"
        subtitle={`Mã phiên giữ chỗ: ${attempt.bookingAttemptId}`}
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusPill status={isHoldExpired ? 'Expired' : attempt.status} />
            <Countdown
              expiresAt={attempt.expiresAt}
              label="Giữ chỗ còn"
              onExpire={() => setExpired(true)}
            />
          </div>
        }
      >
        {isHoldExpired ? (
          <Banner
            tone="danger"
            title="Phiên giữ chỗ đã hết hạn"
            action={
              <Link to="/booking">
                <Button size="sm" variant="primary">
                  Chọn lại lịch khám
                </Button>
              </Link>
            }
          >
            Thời gian giữ chỗ 5 phút đã kết thúc. Vui lòng quay lại để chọn suất khám mới.
          </Banner>
        ) : null}

        <dl className="mt-4 grid gap-4 sm:grid-cols-2">
          <div className="rounded-xl bg-[#f8fbf9] p-3.5">
            <dt className="text-xs text-[#3e4941]">Người khám</dt>
            <dd className="mt-1 font-semibold text-[#0f1f18]">{attempt.patientName}</dd>
          </div>
          <div className="rounded-xl bg-[#f8fbf9] p-3.5">
            <dt className="text-xs text-[#3e4941]">Bác sĩ & Chuyên khoa</dt>
            <dd className="mt-1 font-semibold text-[#0f1f18]">
              {attempt.doctorName} — {attempt.specialtyName}
            </dd>
          </div>
          <div className="rounded-xl bg-[#f8fbf9] p-3.5">
            <dt className="text-xs text-[#3e4941]">Ngày khám & Phòng</dt>
            <dd className="mt-1 font-semibold text-[#0f1f18]">
              <DateTimeText value={attempt.serviceDate} mode="date" /> • {attempt.roomName}
            </dd>
          </div>
          <div className="rounded-xl bg-[#e5f8ed] p-3.5">
            <dt className="text-xs text-[#005231]">Số tiền thanh toán</dt>
            <dd className="mt-1 text-xl font-bold text-[#006a41]">
              <MoneyText amount={attempt.amount} />
            </dd>
          </div>
        </dl>

        <div className="mt-5 flex flex-wrap items-center justify-between gap-3 border-t border-[#e5f8ed] pt-4">
          <p className="inline-flex items-center gap-1.5 text-xs text-[#3e4941]">
            <ShieldCheck className="h-4 w-4 text-[#006a41]" aria-hidden="true" />
            Giao dịch được xác thực qua cổng VNPAY và bảo vệ chống nhấn trùng bằng Idempotency-Key.
          </p>
          <div className="flex gap-2">
            <Link to="/booking">
              <Button variant="outline" disabled={checkoutMutation.isPending}>
                Chọn bác sĩ khác
              </Button>
            </Link>
            <Button
              variant="primary"
              size="lg"
              leftIcon={<CreditCard className="h-4 w-4" />}
              disabled={isHoldExpired}
              loading={checkoutMutation.isPending}
              onClick={() => checkoutMutation.mutate()}
            >
              Thanh toán qua VNPAY
            </Button>
          </div>
        </div>
      </Card>
    </div>
  );
};
