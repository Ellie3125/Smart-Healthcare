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
      <div className="rounded-2xl bg-surface-container-lowest p-4 sm:px-6 shadow-[0_1px_10px_rgba(15,31,24,0.04)]">
        <Stepper
          currentStepIndex={1}
          steps={[
            { id: 'choose', label: 'Chọn bác sĩ' },
            { id: 'hold', label: 'Xác nhận & Giữ chỗ 5p' },
            { id: 'vnpay', label: 'Thanh toán VNPAY' },
          ]}
        />
      </div>

      <div className="grid gap-6 lg:grid-cols-12 items-start">
        {/* Left Details + Cancellation Policy matching Stitch 0c3f3b12 */}
        <div className="lg:col-span-7 space-y-5">
          <Card
            title="Xác nhận thông tin đặt khám"
            subtitle={`Mã phiên giữ chỗ: ${attempt.bookingAttemptId}`}
            actions={<StatusPill status={isHoldExpired ? 'Expired' : attempt.status} />}
          >
            {isHoldExpired ? (
              <div className="mb-4">
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
              </div>
            ) : null}

            <dl className="grid gap-4 sm:grid-cols-2">
              <div className="rounded-xl bg-surface-container-low/75 p-4">
                <dt className="text-xs font-medium text-on-surface-variant">Người khám</dt>
                <dd className="mt-1 font-semibold text-on-surface">{attempt.patientName}</dd>
              </div>
              <div className="rounded-xl bg-surface-container-low/75 p-4">
                <dt className="text-xs font-medium text-on-surface-variant">Bác sĩ &amp; Chuyên khoa</dt>
                <dd className="mt-1 font-semibold text-on-surface">
                  {attempt.doctorName} — {attempt.specialtyName}
                </dd>
              </div>
              <div className="rounded-xl bg-surface-container-low/75 p-4 sm:col-span-2">
                <dt className="text-xs font-medium text-on-surface-variant">Ngày khám &amp; Phòng khám</dt>
                <dd className="mt-1 font-semibold text-on-surface">
                  <DateTimeText value={attempt.serviceDate} mode="date" /> • {attempt.roomName}
                </dd>
              </div>
            </dl>

            <div className="mt-4 rounded-xl bg-surface-container-low/70 p-4 text-xs text-on-surface-variant space-y-1">
              <p className="font-semibold text-on-surface">Chính sách đổi lịch &amp; hoàn tiền</p>
              <p>Miễn phí đổi lịch hoặc hủy hoàn 100% viện phí trước 05:00 sáng cùng ngày khám qua cổng bệnh nhân.</p>
            </div>
          </Card>
        </div>

        {/* Right Hold Timer & Payment Breakdown */}
        <div className="lg:col-span-5">
          <Card
            title="Thanh toán giữ chỗ"
            actions={
              <Countdown
                expiresAt={attempt.expiresAt}
                label="Giữ chỗ còn"
                onExpire={() => setExpired(true)}
              />
            }
          >
            <div className="space-y-4">
              <div className="rounded-2xl bg-secondary-container/50 p-4 flex items-center justify-between">
                <span className="text-sm font-medium text-[#005231]">Số tiền thanh toán</span>
                <span className="text-2xl font-bold text-primary">
                  <MoneyText amount={attempt.amount} />
                </span>
              </div>

              <div className="rounded-xl bg-surface-container-low/75 p-3.5 flex items-start gap-2.5 text-xs text-on-surface-variant">
                <ShieldCheck className="h-4 w-4 text-primary shrink-0 mt-0.5" aria-hidden="true" />
                <span>Giao dịch được xác thực qua cổng VNPAY và bảo vệ chống nhấn trùng bằng Idempotency-Key.</span>
              </div>

              <div className="flex flex-col gap-2.5 pt-2">
                <Button
                  variant="primary"
                  size="lg"
                  className="w-full"
                  leftIcon={<CreditCard className="h-4 w-4" />}
                  disabled={isHoldExpired}
                  loading={checkoutMutation.isPending}
                  onClick={() => checkoutMutation.mutate()}
                >
                  Thanh toán qua VNPAY
                </Button>
                <Link to="/booking" className="w-full">
                  <Button variant="outline" className="w-full" disabled={checkoutMutation.isPending}>
                    Chọn bác sĩ khác
                  </Button>
                </Link>
              </div>
            </div>
          </Card>
        </div>
      </div>
    </div>
  );
};
