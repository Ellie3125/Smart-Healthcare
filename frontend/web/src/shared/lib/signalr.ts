import { HttpTransportType, HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

export type SignalRStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

export interface StaffRealtimeHandlers {
  onStatusChange?: (status: SignalRStatus) => void;
  onEvent: (eventName: string, payload: unknown) => void;
}

/**
 * Kết nối SignalR với skipNegotiation: true theo kiến trúc 1 instance của Smart Healthcare.
 * Ở chế độ mock (`VITE_USE_MOCK=true`), giả lập kết nối an toàn và lắng nghe CustomEvent `sh-staff-realtime`.
 */
export function createStaffRealtimeConnection(
  hubPath: string,
  getToken: () => string | null,
  events: string[],
  handlers: StaffRealtimeHandlers,
): { start: () => Promise<void>; stop: () => Promise<void> } {
  const useMock = import.meta.env.VITE_USE_MOCK === 'true';

  if (useMock) {
    const domHandlers = events.map((eventName) => {
      const fn = (e: Event) => {
        const custom = e as CustomEvent<unknown>;
        handlers.onEvent(eventName, custom.detail);
      };
      window.addEventListener(`sh-staff-realtime:${eventName}`, fn);
      return { eventName, fn };
    });

    return {
      async start() {
        handlers.onStatusChange?.('connected');
      },
      async stop() {
        domHandlers.forEach(({ eventName, fn }) => {
          window.removeEventListener(`sh-staff-realtime:${eventName}`, fn);
        });
        handlers.onStatusChange?.('disconnected');
      },
    };
  }

  let connection: HubConnection | null = null;
  let stopped = false;

  return {
    async start() {
      handlers.onStatusChange?.('connecting');
      try {
        const base = (import.meta.env.VITE_API_BASE_URL as string | undefined) || '';
        const origin =
          typeof window !== 'undefined' && window.location?.origin && window.location.origin !== 'null'
            ? window.location.origin
            : 'http://localhost';
        const fullUrl = hubPath.startsWith('http') ? hubPath : `${base || origin}${hubPath}`;

        connection = new HubConnectionBuilder()
          .withUrl(fullUrl, {
            skipNegotiation: true,
            transport: HttpTransportType.WebSockets,
            accessTokenFactory: () => getToken() ?? '',
          })
          .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
          .configureLogging(LogLevel.Warning)
          .build();

        events.forEach((eventName) => {
          connection?.on(eventName, (payload: unknown) => {
            handlers.onEvent(eventName, payload);
          });
        });

        connection.onreconnecting(() => handlers.onStatusChange?.('reconnecting'));
        connection.onreconnected(() => handlers.onStatusChange?.('connected'));
        connection.onclose(() => handlers.onStatusChange?.('disconnected'));

        await connection.start();
        if (!stopped) handlers.onStatusChange?.('connected');
      } catch {
        if (!stopped) handlers.onStatusChange?.('disconnected');
      }
    },
    async stop() {
      stopped = true;
      if (connection) {
        try {
          await connection.stop();
        } catch {
          // ignore cleanup disconnect errors in offline/test mode
        }
        connection = null;
      }
      handlers.onStatusChange?.('disconnected');
    },
  };
}
