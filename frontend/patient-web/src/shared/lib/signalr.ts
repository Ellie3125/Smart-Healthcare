import { HttpTransportType, HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

export type PatientSignalRStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

export function createPatientRealtimeConnection(
  hubPath: string,
  getToken: () => string | null,
  events: string[],
  handlers: {
    onStatusChange?: (status: PatientSignalRStatus) => void;
    onEvent: (eventName: string, payload: unknown) => void;
  },
): { start: () => Promise<void>; stop: () => Promise<void> } {
  const useMock = import.meta.env.VITE_USE_MOCK === 'true';

  if (useMock) {
    const listeners = events.map((eventName) => {
      const fn = (e: Event) => {
        const custom = e as CustomEvent<unknown>;
        handlers.onEvent(eventName, custom.detail);
      };
      window.addEventListener(`sh-patient-realtime:${eventName}`, fn);
      return { eventName, fn };
    });

    return {
      async start() {
        handlers.onStatusChange?.('connected');
      },
      async stop() {
        listeners.forEach(({ eventName, fn }) => window.removeEventListener(`sh-patient-realtime:${eventName}`, fn));
        handlers.onStatusChange?.('disconnected');
      },
    };
  }

  let conn: HubConnection | null = null;
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

        conn = new HubConnectionBuilder()
          .withUrl(fullUrl, {
            skipNegotiation: true,
            transport: HttpTransportType.WebSockets,
            accessTokenFactory: () => getToken() ?? '',
          })
          .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
          .configureLogging(LogLevel.Warning)
          .build();

        events.forEach((eventName) => {
          conn?.on(eventName, (payload: unknown) => handlers.onEvent(eventName, payload));
        });
        conn.onreconnecting(() => handlers.onStatusChange?.('reconnecting'));
        conn.onreconnected(() => handlers.onStatusChange?.('connected'));
        conn.onclose(() => handlers.onStatusChange?.('disconnected'));

        await conn.start();
        if (!stopped) handlers.onStatusChange?.('connected');
      } catch {
        if (!stopped) handlers.onStatusChange?.('disconnected');
      }
    },
    async stop() {
      stopped = true;
      if (conn) {
        try {
          await conn.stop();
        } catch {
          // ignore cleanup disconnect errors in offline/test mode
        }
        conn = null;
      }
      handlers.onStatusChange?.('disconnected');
    },
  };
}
