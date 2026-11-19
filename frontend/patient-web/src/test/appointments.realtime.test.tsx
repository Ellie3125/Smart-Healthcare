import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { patientApiMock } from './patientTestFixtures';
import { bookingApi, PENDING_PAYMENT_KEY } from '../api/bookingApi';
import { ToastProvider } from '../shared/ui';

// Thay kết nối SignalR thật bằng bộ ghi handler để phát sự kiện trong test.
const hub = vi.hoisted(() => ({ handlers: new Map<string, (payload: unknown) => void>() }));
vi.mock('../shared/lib/signalr', () => ({
  usePatientHubEvent: (eventName: string, onEvent: (payload: unknown) => void) => {
    hub.handlers.set(eventName, onEvent);
  },
}));

import { MyAppointmentsPage } from '../pages/appointments/MyAppointmentsPage';
import { VnpayReturnPage } from '../pages/booking/VnpayReturnPage';

function renderPage(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <ToastProvider>
        <MemoryRouter>{ui}</MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

describe('AppointmentsUpdated qua /hubs/patient', () => {
  beforeEach(() => {
    patientApiMock.reset();
    sessionStorage.clear();
    hub.handlers.clear();
  });

  it('trang Lịch khám của tôi tải lại danh sách khi nhận sự kiện, không cần F5', async () => {
    const list = vi.spyOn(bookingApi, 'listMyAppointments');
    renderPage(<MyAppointmentsPage />);
    await waitFor(() => expect(list).toHaveBeenCalledTimes(1));

    act(() => hub.handlers.get('AppointmentsUpdated')!({ appointmentId: 'apt-1', kind: 'AppointmentConfirmed' }));

    await waitFor(() => expect(list).toHaveBeenCalledTimes(2));
    list.mockRestore();
  });

  it('trang kết quả VNPAY hiện bác sĩ và ngày khám khi lịch được xác nhận', async () => {
    sessionStorage.setItem(PENDING_PAYMENT_KEY, 'pi-test-02');
    patientApiMock.override('GET', '/api/billing/patient/payments/:paymentIntentId', () => ({
      status: 200,
      data: { paymentIntentId: 'pi-test-02', intentNumber: 'PI-TEST-02', amount: 150000, status: 'Succeeded', paidAtUtc: '2026-11-19T10:00:00Z' },
    }));
    renderPage(<VnpayReturnPage />);
    expect(await screen.findByRole('button', { name: /Về lịch khám của tôi/i })).toBeInTheDocument();
    expect(screen.queryByText(/Lịch khám đã được xác nhận/i)).not.toBeInTheDocument();

    // Sự kiện hủy lịch khác không được hiện như lịch vừa xác nhận.
    act(() => hub.handlers.get('AppointmentsUpdated')!({ appointmentId: 'apt-1', kind: 'AppointmentCancelled' }));
    act(() => hub.handlers.get('AppointmentsUpdated')!({ appointmentId: 'apt-1', kind: 'AppointmentConfirmed' }));

    expect(await screen.findByText(/Lịch khám đã được xác nhận/i)).toBeInTheDocument();
    expect(screen.getByText(/BS.CKI Trần Quốc Khánh/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Xem vé khám QR/i })).toBeInTheDocument();
  });
});
