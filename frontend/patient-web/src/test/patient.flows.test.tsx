import React from 'react';
import { describe, expect, it, beforeEach } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { resetPatientMockStore } from '../mocks/handlers';
import { bookingApi, PENDING_PAYMENT_KEY } from '../api/bookingApi';
import { ToastProvider } from '../shared/ui';
import { BookingConfirmPage } from '../pages/booking/BookingConfirmPage';
import { VnpayReturnPage } from '../pages/booking/VnpayReturnPage';
import { RescheduleAppointmentPage } from '../pages/appointments/RescheduleAppointmentPage';
import { CancelAppointmentPage } from '../pages/appointments/CancelAppointmentPage';
import { EncounterCostPage } from '../pages/billing/EncounterCostPage';
import { RefundRequestsPage } from '../pages/billing/RefundRequestsPage';
import { EncounterHistoryPage } from '../pages/records/EncounterHistoryPage';
import { EncounterDetailPage } from '../pages/records/EncounterDetailPage';
import { PrescriptionDetailPage } from '../pages/records/PrescriptionDetailPage';
import { ChatbotPage } from '../pages/assistant/ChatbotPage';
import { PublicDisplayPage } from '../pages/public/PublicDisplayPage';

function renderWithProviders(
  ui: React.ReactElement,
  initialEntries: (string | { pathname: string; state?: unknown })[] = ['/'],
  pathPattern = '/',
) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <ToastProvider>
        <MemoryRouter initialEntries={initialEntries}>
          <Routes>
            <Route path={pathPattern} element={ui} />
            <Route path="*" element={ui} />
          </Routes>
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

describe('Patient Web critical workflows (P06–P26 & TV01)', () => {
  beforeEach(() => {
    resetPatientMockStore();
    sessionStorage.clear();
  });

  it('P06: maps the real doctor search response and derives the doctor day from it', async () => {
    const date = new Date(Date.now() + 7 * 3600_000).toISOString().slice(0, 10);
    const page = await bookingApi.searchDoctors({ serviceDate: date });
    const first = page.items.find((x) => x.doctorId === 'doc-1');
    expect(first).toMatchObject({ remainingSlots: 8, totalSlots: 30, consultationFee: 150000, status: 'Open', serviceDate: date });

    const detail = await bookingApi.getDoctorDayDetail('doc-1', date);
    expect(detail).toMatchObject({ remainingSlots: 8, consultationFee: 150000, specialtyName: 'Nội tổng quát' });
    await expect(bookingApi.getDoctorDayDetail('doc-khong-co', date)).rejects.toMatchObject({ status: 404 });
  });

  it('P09: creates the payment then the VNPAY checkout for the same PaymentIntent', async () => {
    const res = await bookingApi.createVnpayCheckout('attempt-recover-1');
    expect(res.paymentIntentId).toBe('pi-attempt-recover-1');
    expect(res.paymentUrl).toContain('/payment/vnpay-return');
  });

  it('P08: renders booking hold details and 5-minute countdown', async () => {
    renderWithProviders(
      <BookingConfirmPage />,
      [
        {
          pathname: '/booking/attempt/attempt-recover-1',
          state: { doctorName: 'ThS.BS Phạm Thu Hà', specialtyName: 'Tim mạch', patientName: 'Nguyễn Minh Tâm', serviceDate: '2026-11-13', amount: 200000 },
        },
      ],
      '/booking/attempt/:bookingAttemptId',
    );
    expect(await screen.findByText(/ThS\.BS Phạm Thu Hà/i)).toBeInTheDocument();
    expect(screen.getByText(/Giữ chỗ còn:/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Thanh toán qua VNPAY/i })).toBeEnabled();
  });

  it('P10: starts VNPAY return in Verifying state without any mark-paid button', async () => {
    sessionStorage.setItem(PENDING_PAYMENT_KEY, 'pi-test-01');
    server.use(
      http.get('/api/billing/patient/payments/:paymentIntentId', () =>
        HttpResponse.json({ paymentIntentId: 'pi-test-01', intentNumber: 'PI-TEST-01', amount: 150000, status: 'Pending', paidAtUtc: null }),
      ),
    );
    renderWithProviders(<VnpayReturnPage />, ['/payment/vnpay-return?vnp_TxnRef=VNP-TEST-01'], '/payment/vnpay-return');
    expect(await screen.findByText(/Đang xác minh thanh toán với VNPAY/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Đánh dấu đã trả/i })).not.toBeInTheDocument();
  });

  it('P10b: maps the numeric payment state of Billing (0 pending, 2 succeeded)', async () => {
    expect((await bookingApi.verifyVnpayReturn('pi-so-01')).state).toBe('Verifying');
    expect(await bookingApi.verifyVnpayReturn('pi-so-01')).toMatchObject({ state: 'Paid', amount: 150000 });
  });

  it('P14: reschedule disables different-fee doctors', async () => {
    renderWithProviders(<RescheduleAppointmentPage />, ['/appointments/apt-2/reschedule'], '/appointments/:id/reschedule');
    expect(await screen.findByText(/BS\.CKI Trần Quốc Khánh/i)).toBeInTheDocument();
    expect(screen.getAllByText(/Khác mức giá/i).length).toBeGreaterThan(0);
  });

  it('P15: cancel displays server refund preview', async () => {
    renderWithProviders(<CancelAppointmentPage />, ['/appointments/apt-2/cancel'], '/appointments/:id/cancel');
    expect(await screen.findByText(/Số tiền hoàn dự kiến \(100%\)/i)).toBeInTheDocument();
    expect(screen.getAllByText(/150\.000 ₫/i).length).toBeGreaterThan(0);
  });

  it('P18: shows all 5 encounter cost metrics', async () => {
    renderWithProviders(<EncounterCostPage />, ['/encounters/enc-hist-1/cost'], '/encounters/:encounterId/cost');
    expect(await screen.findByText(/Tổng đã trả/i)).toBeInTheDocument();
    expect(screen.getByText(/Tổng đã duyệt hoàn/i)).toBeInTheDocument();
    expect(screen.getByText(/Còn giữ ròng/i)).toBeInTheDocument();
    expect(screen.getAllByText(/Miễn giảm/i).length).toBeGreaterThan(0);
    expect(screen.getByText(/Hoàn thành công/i)).toBeInTheDocument();
  });

  it('P19: refund requests screen has no file upload inputs', async () => {
    const { container } = renderWithProviders(<RefundRequestsPage />, ['/refunds'], '/refunds');
    await screen.findByText(/RF-20261110-003/i);
    expect(container.querySelector('input[type="file"]')).toBeNull();
  });

  it('P20: blocks encounter history for Pending verification profile', async () => {
    renderWithProviders(<EncounterHistoryPage />, ['/records'], '/records');
    await screen.findByRole('option', { name: /Nguyễn Bảo Khang/i });
    const select = screen.getByLabelText(/Hồ sơ người khám/i);
    fireEvent.change(select, { target: { value: 'prof-pending' } });
    expect(await screen.findByText(/Hồ sơ đang chờ xác minh định danh/i)).toBeInTheDocument();
  });

  it('P21: hides clinical conclusion when encounter is unpublished', async () => {
    renderWithProviders(<EncounterDetailPage />, ['/records/enc-today-1'], '/records/:encounterId');
    expect(await screen.findByText(/Hồ sơ đang trong quá trình khám và hoàn thiện/i)).toBeInTheDocument();
    expect(screen.queryByText(/Kết luận & Hướng dẫn theo dõi/i)).not.toBeInTheDocument();
  });

  it('P23: renders read-only prescription with zero online drug store actions', async () => {
    renderWithProviders(<PrescriptionDetailPage />, ['/records/enc-hist-1/prescription'], '/records/:encounterId/prescription');
    expect(await screen.findByText(/Amlodipin 5mg/i)).toBeInTheDocument();
    expect(screen.getByText(/Hệ thống bệnh viện không bán thuốc trực tuyến/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Mua thuốc/i })).not.toBeInTheDocument();
  });

  it('P25: shows 115 red-flag without booking CTA', async () => {
    renderWithProviders(<ChatbotPage />, ['/chatbot'], '/chatbot');
    const input = await screen.findByPlaceholderText(/Mô tả triệu chứng/i);
    fireEvent.change(input, { target: { value: 'Tôi bị đau ngực dữ dội và khó thở' } });
    fireEvent.click(screen.getByRole('button', { name: /Gửi/i }));

    expect(await screen.findByText(/CẢNH BÁO CẤP CỨU/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Gọi cấp cứu 115 ngay/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Xác nhận đặt lịch/i })).not.toBeInTheDocument();
  });

  it('P25: disables chatbot input when /health is down', async () => {
    server.use(http.get('/api/notifications/patient/chat/health', () => HttpResponse.json({ available: false })));
    renderWithProviders(<ChatbotPage />, ['/chatbot'], '/chatbot');
    expect(await screen.findByText(/Trợ lý ảo tạm thời ngưng phục vụ/i)).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.getByPlaceholderText(/Trợ lý đang bảo trì/i)).toBeDisabled();
    });
  });

  it('TV01: renders queue ticket numbers only with zero patient PII', async () => {
    renderWithProviders(<PublicDisplayPage />, ['/display'], '/display');
    expect(await screen.findByText('A-012')).toBeInTheDocument();
    expect(screen.getByText(/Phòng khám P201/i)).toBeInTheDocument();
    expect(screen.queryByText(/Nguyễn Minh Tâm/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/Lê Thị Hoa/i)).not.toBeInTheDocument();
  });
});
