import React, { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom';
import { CreditCard, ShieldCheck } from 'lucide-react';
import { bookingApi, PENDING_PAYMENT_KEY } from '../../api/bookingApi';
import type { BookingSummary } from '../../types/booking';
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
import { extractPatientError } from '../../shared/lib/errors';

export const BookingConfirmPage: React.FC = () => {
  const { bookingAttemptId = '' } = useParams<{ bookingAttemptId: string }>();
  const navigate = useNavigate();
  const { pushToast } = useToast();
  const [expired, setExpired] = useState(false);
  // Thông tin đã chọn ở màn trước; mất khi mở lại bằng đường dẫn trực tiếp vì Backend chưa trả trong tiến trình.
  const summary = useLocation().state as BookingSummary | null;

  const attemptQuery = useQuery({
    queryKey: ['patient', 'booking-attempt', bookingAttemptId],
    queryFn: () => bookingApi.getBookingAttempt(bookingAttemptId),
    enabled: Boolean(bookingAttemptId),
  });

  const checkoutMutation = useMutation({
    mutationFn: () => bookingApi.createVnpayCheckout(bookingAttemptId),
    onSuccess: (res) => {
      sessionStorage.setItem(PENDING_PAYMENT_KEY, res.paymentIntentId);
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
  const isPaid = attempt.status === 'Confirming' || attempt.status === 'Confirmed';
  const isHoldExpired = !isPaid && (expired || (attempt.status !== 'Held' && attempt.status !== 'WaitingPayment'));
  const pillStatus = isPaid ? 'Confirmed' : isHoldExpired ? 'Expired' : attempt.status === 'Held' ? 'Hold' : 'PendingPayment';

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
            <StatusPill status={pillStatus} />
            {isPaid || !attempt.expiresAtUtc ? null : (
              <Countdown
                expiresAt={attempt.expiresAtUtc}
                label="Giữ chỗ còn"
                onExpire={() => setExpired(true)}
              />
            )}
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

        {isPaid ? (
          <Banner
            tone="success"
            title="Lịch khám đã được thanh toán"
            action={
              <Link to="/appointments">
                <Button size="sm" variant="primary">
                  Xem lịch của tôi
                </Button>
              </Link>
            }
          >
            Hệ thống đã ghi nhận thanh toán cho phiên đặt lịch này.
          </Banner>
        ) : null}

        {summary ? (
          <dl className="mt-4 grid gap-4 sm:grid-cols-2">
            <div className="rounded-xl bg-[#f8fbf9] p-3.5">
              <dt className="text-xs text-[#3e4941]">Người khám</dt>
              <dd className="mt-1 font-semibold text-[#0f1f18]">{summary.patientName}</dd>
            </div>
            <div className="rounded-xl bg-[#f8fbf9] p-3.5">
              <dt className="text-xs text-[#3e4941]">Bác sĩ & Chuyên khoa</dt>
              <dd className="mt-1 font-semibold text-[#0f1f18]">
                {[summary.doctorName, summary.specialtyName].filter(Boolean).join(' — ')}
              </dd>
            </div>
            <div className="rounded-xl bg-[#f8fbf9] p-3.5">
              <dt className="text-xs text-[#3e4941]">Ngày khám</dt>
              <dd className="mt-1 font-semibold text-[#0f1f18]">
                <DateTimeText value={summary.serviceDate} mode="date" />
              </dd>
            </div>
            <div className="rounded-xl bg-[#e5f8ed] p-3.5">
              <dt className="text-xs text-[#005231]">Số tiền thanh toán</dt>
              <dd className="mt-1 text-xl font-bold text-[#006a41]">
                <MoneyText amount={summary.amount} />
              </dd>
            </div>
          </dl>
        ) : (
          <p className="mt-4 text-sm text-[#3e4941]">
            Số tiền phí khám sẽ hiển thị trên trang thanh toán VNPAY.
          </p>
        )}

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
              disabled={isHoldExpired || isPaid}
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
