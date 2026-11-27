import React from 'react';
import { describe, expect, it, beforeEach } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { patientApiMock } from './patientTestFixtures';
import { bookingApi, PENDING_PAYMENT_KEY } from '../api/bookingApi';
import { ToastProvider } from '../shared/ui';
import { VnpayReturnPage } from '../pages/booking/VnpayReturnPage';
import { RescheduleAppointmentPage } from '../pages/appointments/RescheduleAppointmentPage';
import { CancelAppointmentPage } from '../pages/appointments/CancelAppointmentPage';
import { EncounterCostPage } from '../pages/billing/EncounterCostPage';
import { ClsPaymentsPage } from '../pages/billing/ClsPaymentsPage';
import { EncounterHistoryPage } from '../pages/records/EncounterHistoryPage';
import { EncounterDetailPage } from '../pages/records/EncounterDetailPage';
import { PrescriptionDetailPage } from '../pages/records/PrescriptionDetailPage';
import { ChatbotPage } from '../pages/assistant/ChatbotPage';
import { PublicDisplayPage } from '../pages/public/PublicDisplayPage';
import { BookingDoctorsPage } from '../pages/booking/BookingDoctorsPage';

// Không còn backend giả: luồng gọi API được kiểm bằng chạy thật với Backend (VNPAY sandbox + seed).
function renderWithProviders(ui: React.ReactElement, initialEntries: string[], pathPattern: string) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <ToastProvider>
        <MemoryRouter initialEntries={initialEntries}>
          <Routes>
            <Route path={pathPattern} element={ui} />
          </Routes>
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

describe('Patient Web', () => {
  beforeEach(() => {
    patientApiMock.reset();
    sessionStorage.clear();
  });

  it('P10: starts VNPAY return in Verifying state without any mark-paid button', async () => {
    sessionStorage.setItem(PENDING_PAYMENT_KEY, 'pi-test-01');
    patientApiMock.override('GET', '/api/billing/patient/payments/:paymentIntentId', () => ({
      status: 200,
      data: { paymentIntentId: 'pi-test-01', intentNumber: 'PI-TEST-01', amount: 150000, status: 'Pending', paidAtUtc: null },
    }));
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

  it('P19: appointment cancellation operates automatically without exposing separate refund channels', async () => {
    renderWithProviders(<CancelAppointmentPage />, ['/appointments/apt-2/cancel'], '/appointments/:id/cancel');
    expect(await screen.findByText(/Số tiền hoàn dự kiến \(100%\)/i)).toBeInTheDocument();
    expect(screen.queryByText(/Chọn kênh nhận tiền/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/Chuyển khoản ngân hàng/i)).not.toBeInTheDocument();
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

  it('P22-DOC: renders published CLS attachments with preview and download buttons for concluded encounters', async () => {
    renderWithProviders(<EncounterDetailPage />, ['/records/enc-hist-1'], '/records/:encounterId');
    expect(await screen.findByText(/Kết quả Cận lâm sàng/i)).toBeInTheDocument();
    expect(await screen.findByText(/Phieu-ket-qua-sinh-hoa-20261015\.pdf/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Xem tệp/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Tải về/i })).toBeInTheDocument();
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
    await waitFor(() => expect(input).toBeEnabled());
    fireEvent.change(input, { target: { value: 'Tôi bị đau ngực dữ dội và khó thở' } });
    fireEvent.click(screen.getByRole('button', { name: /Gửi/i }));

    expect(await screen.findByText(/CẢNH BÁO CẤP CỨU/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Gọi cấp cứu 115 ngay/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Xác nhận đặt lịch/i })).not.toBeInTheDocument();
  });

  it('P25: disables chatbot input when /health is down', async () => {
    patientApiMock.override('GET', '/api/notifications/patient/chat/status', () => ({
      status: 200,
      data: { available: false },
    }));
    renderWithProviders(<ChatbotPage />, ['/chatbot'], '/chatbot');
    expect(await screen.findByText(/Trợ lý ảo tạm thời ngưng phục vụ/i)).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.getByPlaceholderText(/Trợ lý đang bảo trì/i)).toBeDisabled();
    });
  });

  it('P25: suggestion button sends a turn and appointment option links to ticket, reschedule, cancel', async () => {
    renderWithProviders(<ChatbotPage />, ['/chatbot'], '/chatbot');
    const suggestion = await screen.findByRole('button', { name: /Xem lịch của tôi/i });
    await waitFor(() => expect(suggestion).toBeEnabled());
    fireEvent.click(suggestion);

    expect(await screen.findByText(/Bạn có 1 lịch hẹn sắp tới/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Xem vé' })).toHaveAttribute('href', '/appointments/apt-2/ticket');
    expect(screen.getByRole('link', { name: 'Đổi lịch' })).toHaveAttribute('href', '/appointments/apt-2/reschedule');
    expect(screen.getByRole('link', { name: 'Hủy lịch' })).toHaveAttribute('href', '/appointments/apt-2/cancel');
  });

  it('P25: doctor option opens the manual booking page for that day', async () => {
    renderWithProviders(<ChatbotPage />, ['/chatbot'], '/chatbot');
    const input = await screen.findByPlaceholderText(/Mô tả triệu chứng/i);
    await waitFor(() => expect(input).toBeEnabled());
    fireEvent.change(input, { target: { value: 'ngày mai còn bác sĩ nào' } });
    fireEvent.click(screen.getByRole('button', { name: /Gửi/i }));

    const link = await screen.findByRole('link', { name: /Đặt lịch/i });
    expect(link.getAttribute('href')).toMatch(/^\/booking\/doctors\/doc-1\?date=\d{4}-\d{2}-\d{2}$/);
  });

  it('P17: lists CLS batches of the selected profile from the Billing DTO', async () => {
    const asked: (string | null)[] = [];
    patientApiMock.override('GET', '/api/billing/patient/cls-batches', (_url, _body, _params, query) => {
      const patientId = query?.get('patientId') ?? null;
      asked.push(patientId);
      return {
        data: [
          {
            serviceOrderId: 'so-1',
            encounterId: 'enc-1',
            patientId,
            orderedAtUtc: new Date().toISOString(),
            payByUtc: new Date(Date.now() + 86400000).toISOString(),
            status: 0,
            totalAmount: 250000,
            paidAmount: 0,
            outstanding: 250000,
            currency: 'VND',
            canPay: true,
            items: [
              { chargeId: 'ch-1', serviceOrderItemId: 'soi-1', serviceName: 'Siêu âm ổ bụng', unitPrice: 250000, quantity: 1, totalAmount: 250000, outstanding: 250000, status: 0 },
            ],
          },
        ],
      };
    });
    renderWithProviders(<ClsPaymentsPage />, ['/cls-payments'], '/cls-payments');

    expect(await screen.findByText('Siêu âm ổ bụng')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Thanh toán qua VNPAY/i })).toBeEnabled();
    expect(asked).toContain('prof-self');

    fireEvent.change(screen.getByLabelText(/Hồ sơ người khám/i), { target: { value: 'prof-mom' } });
    await waitFor(() => expect(asked).toContain('prof-mom'));
  });

  it('TV01: renders queue ticket numbers only with zero patient PII', async () => {
    renderWithProviders(<PublicDisplayPage />, ['/display'], '/display');
    expect(await screen.findByText('A-012')).toBeInTheDocument();
    expect(screen.getByText(/Phòng khám P201/i)).toBeInTheDocument();
    expect(screen.queryByText(/Nguyễn Minh Tâm/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/Lê Thị Hoa/i)).not.toBeInTheDocument();
  });

  it('B01: booking doctors page omits removed notes, subtitle, and policy box', async () => {
    patientApiMock.override('GET', '/api/scheduling/patient/doctors', () => ({
      data: { serviceDate: '2026-11-27', page: 1, pageSize: 12, hasMore: false, items: [] },
    }));
    renderWithProviders(<BookingDoctorsPage />, ['/booking'], '/booking');
    expect(await screen.findByText(/Không có bác sĩ mở lịch trong ngày đã chọn/i)).toBeInTheDocument();
    expect(screen.queryByText(/Bạn có thể thử chọn ngày khám kế tiếp hoặc bỏ bớt điều kiện lọc chuyên khoa\./i)).not.toBeInTheDocument();
    expect(screen.queryByText(/Lọc nhanh theo lịch mở khám/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/Chọn ngày khám, chuyên khoa và bác sĩ phù hợp\. Mỗi bác sĩ phục vụ 30 lượt khám mỗi ngày\./i)).not.toBeInTheDocument();
    expect(screen.queryByText(/Quy trình đặt khám minh bạch/i)).not.toBeInTheDocument();
  });
});
