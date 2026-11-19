import { describe, expect, it, vi } from 'vitest';
import { act, render, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { ToastProvider } from '../shared/ui';

// Ghi lại handler của kết nối /hubs/staff để phát sự kiện trong test, không mở WebSocket thật.
const hub = vi.hoisted(() => ({ onEvent: null as null | ((name: string, payload: unknown) => void), events: [] as string[] }));
vi.mock('../shared/lib/signalr', () => ({
  createStaffRealtimeConnection: (_path: string, _token: () => string | null, events: string[],
    handlers: { onEvent: (name: string, payload: unknown) => void }) => {
    hub.events = events;
    hub.onEvent = handlers.onEvent;
    return { start: async () => {}, stop: async () => {} };
  },
}));
vi.mock('../hooks/useAuth', () => ({
  useAuth: () => ({ user: { role: 'Receptionist', userName: 'letan01' }, logout: vi.fn() }),
}));

import { StaffLayout } from '../layouts/StaffLayout';

describe('AppointmentsUpdated trên /hubs/staff', () => {
  it('màn check-in tiếp tân tải lại khi lịch vừa được xác nhận', async () => {
    const client = new QueryClient();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    render(
      <QueryClientProvider client={client}>
        <ToastProvider>
          <MemoryRouter>
            <StaffLayout />
          </MemoryRouter>
        </ToastProvider>
      </QueryClientProvider>,
    );
    await waitFor(() => expect(hub.onEvent).not.toBeNull());
    expect(hub.events).toContain('AppointmentsUpdated');

    act(() => hub.onEvent!('AppointmentsUpdated', { appointmentId: 'apt-1', kind: 'AppointmentConfirmed' }));

    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['reception'] });
  });
});
