import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link, useSearchParams } from 'react-router-dom';
import { AlertCircle, CheckCircle2, Clock, RefreshCw } from 'lucide-react';
import { bookingApi } from '../../api/bookingApi';
import {
  Banner,
  Button,
  Card,
  DateTimeText,
  MoneyText,
  StatusPill,
} from '../../shared/ui';
import { extractPatientError } from '../../shared/lib/errors';

/**
 * P10 — Màn Return URL VNPAY.
 * Luôn khởi đầu ở trạng thái "Đang xác minh" (Verifying) và tự động hỏi Backend.
 * Tuyệt đối KHÔNG khẳng định thành công từ query string trình duyệt và KHÔNG có nút đánh dấu đã trả.
 */
export const VnpayReturnPage: React.FC = () => {
  const [searchParams] = useSearchParams();
  const paramsObj: Record<string, string> = {};
  searchParams.forEach((value, key) => {
    paramsObj[key] = value;
  });

  const verifyQuery = useQuery({
    queryKey: ['patient', 'vnpay-return', paramsObj],
    queryFn: () => bookingApi.verifyVnpayReturn(paramsObj),
    refetchInterval: (query) => {
      const state = query.state.data?.state;
      return !state || state === 'Verifying' ? 2000 : false;
    },
  });

  const result = verifyQuery.data;
  const currentState = result?.state ?? 'Verifying';

  return (
    <div className="mx-auto max-w-2xl space-y-5">
      <Card
        title="Kết quả giao dịch thanh toán"
        subtitle={`Mã tham chiếu: ${result?.vnpTxnRef || paramsObj.vnp_TxnRef || 'Đang kiểm tra'}`}
        actions={<StatusPill status={currentState} />}
      >
        {verifyQuery.isError ? (
          <Banner tone="danger" title="Không thể xác minh giao dịch lúc này">
            {extractPatientError(verifyQuery.error).message}
          </Banner>
        ) : null}

        {currentState === 'Verifying' ? (
          <div aria-live="polite" className="flex flex-col items-center gap-3 py-6 text-center">
            <Clock className="h-10 w-10 text-[#0b57d0]" aria-hidden="true" />
            <h3 className="text-lg font-semibold text-[#0f1f18]">Đang xác minh thanh toán với VNPAY</h3>
            <p className="max-w-md text-sm text-[#3e4941]">
              Hệ thống đang đối chiếu kết quả thực tế từ máy chủ thanh toán. Vui lòng không đóng trình duyệt trong giây lát.
            </p>
          </div>
        ) : null}

        {result && currentState !== 'Verifying' ? (
          <div className="space-y-4">
            <div className="flex items-start gap-3 rounded-2xl border border-[#bdcabf] bg-[#f8fbf9] p-4">
              {currentState === 'Paid' || currentState === 'AlreadyProcessed' ? (
                <CheckCircle2 className="mt-0.5 h-6 w-6 shrink-0 text-[#006a41]" aria-hidden="true" />
              ) : (
                <AlertCircle className="mt-0.5 h-6 w-6 shrink-0 text-[#ba1a1a]" aria-hidden="true" />
              )}
              <div className="space-y-1 text-sm">
                <p className="font-semibold text-[#0f1f18]">{result.message}</p>
                {result.amount ? (
                  <p className="text-[#3e4941]">
                    Số tiền: <MoneyText amount={result.amount} />
                  </p>
                ) : null}
                {result.paidAt ? (
                  <p className="text-[#3e4941]">
                    Thời gian ghi nhận: <DateTimeText value={result.paidAt} />
                  </p>
                ) : null}
              </div>
            </div>

            {currentState === 'AlreadyProcessed' ? (
              <Banner tone="info">
                Giao dịch đã được hệ thống xử lý trước đó nên bạn không bị trừ tiền lần hai.
              </Banner>
            ) : null}

            {currentState === 'Expired' ? (
              <Banner tone="warning">
                Phiên thanh toán đã hết hạn. Nếu tiền chưa bị trừ, bạn có thể tạo phiên đặt lịch mới.
              </Banner>
            ) : null}

            {currentState === 'Unknown' ? (
              <Banner tone="warning">
                Hệ thống chưa nhận được phản hồi cuối cùng từ cổng VNPAY. Bạn có thể bấm kiểm tra lại sau ít giây.
              </Banner>
            ) : null}

            <div className="flex flex-wrap justify-end gap-2 pt-2">
              <Button
                variant="outline"
                leftIcon={<RefreshCw className="h-4 w-4" />}
                onClick={() => verifyQuery.refetch()}
                loading={verifyQuery.isFetching}
              >
                Kiểm tra lại trạng thái
              </Button>
              {result.appointmentId ? (
                <Link to={`/appointments/${result.appointmentId}/ticket`}>
                  <Button variant="primary">Xem vé khám QR</Button>
                </Link>
              ) : (
                <Link to="/appointments">
                  <Button variant="primary">Về lịch khám của tôi</Button>
                </Link>
              )}
            </div>
          </div>
        ) : null}
      </Card>
    </div>
  );
};
