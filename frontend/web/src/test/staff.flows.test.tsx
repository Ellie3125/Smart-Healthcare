import React from 'react';
import { describe, expect, it, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ToastProvider } from '../shared/ui';
import { SessionReplacedPage } from '../pages/common/SessionReplacedPage';

// Không còn backend giả: các màn gọi API được kiểm bằng chạy thật với Backend + seed.
function renderStaff(ui: React.ReactElement) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <ToastProvider>
        <MemoryRouter>
          <Routes>
            <Route path="*" element={ui} />
          </Routes>
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

describe('Staff Web', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('S04b: clears local token and informs user when RoomAccount session is replaced', () => {
    localStorage.setItem('token', 'old-room-token');
    localStorage.setItem('user', '{"role":"RoomAccount"}');
    renderStaff(<SessionReplacedPage />);
    expect(screen.getByText(/Phiên phòng máy đã bị thay thế/i)).toBeInTheDocument();
    expect(localStorage.getItem('token')).toBeNull();
  });
});
