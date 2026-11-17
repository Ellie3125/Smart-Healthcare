import React from 'react';
import { describe, expect, it, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { PENDING_PAYMENT_KEY } from '../api/bookingApi';
import { ToastProvider } from '../shared/ui';
import { VnpayReturnPage } from '../pages/booking/VnpayReturnPage';

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
    sessionStorage.clear();
  });

  it('P10: starts VNPAY return in Verifying state without any mark-paid button', async () => {
    sessionStorage.setItem(PENDING_PAYMENT_KEY, 'pi-test-01');
    renderWithProviders(<VnpayReturnPage />, ['/payment/vnpay-return?vnp_TxnRef=VNP-TEST-01'], '/payment/vnpay-return');
    expect(await screen.findByText(/Đang xác minh thanh toán với VNPAY/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Đánh dấu đã trả/i })).not.toBeInTheDocument();
  });
});
