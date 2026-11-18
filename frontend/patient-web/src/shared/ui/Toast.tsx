import React, { createContext, useCallback, useContext, useState } from 'react';
import { AlertCircle, CheckCircle2, Info, X } from 'lucide-react';

export type PatientToastTone = 'info' | 'success' | 'warning' | 'danger';

interface ToastItem {
  id: string;
  tone: PatientToastTone;
  message: string;
}

const ToastContext = createContext<{ pushToast: (message: string, tone?: PatientToastTone) => void }>({
  pushToast: () => undefined,
});

export function useToast() {
  return useContext(ToastContext);
}

export const ToastProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [toasts, setToasts] = useState<ToastItem[]>([]);

  const pushToast = useCallback((message: string, tone: PatientToastTone = 'info') => {
    const id = `${Date.now()}-${Math.random().toString(16).slice(2)}`;
    setToasts((prev) => [...prev.slice(-3), { id, tone, message }]);
    window.setTimeout(() => {
      setToasts((prev) => prev.filter((item) => item.id !== id));
    }, 4500);
  }, []);

  return (
    <ToastContext.Provider value={{ pushToast }}>
      {children}
      <div
        aria-live="polite"
        className="pointer-events-none fixed bottom-20 right-4 z-50 flex w-full max-w-sm flex-col gap-2 px-4 sm:bottom-5 sm:px-0"
      >
        {toasts.map((toast) => (
          <div
            key={toast.id}
            className={`pointer-events-auto flex items-start gap-2.5 rounded-2xl border px-4 py-3 text-sm shadow-lg ${
              toast.tone === 'success'
                ? 'border-[#8ef8bb] bg-[#d4f7e2] text-[#005231]'
                : toast.tone === 'danger'
                  ? 'border-[#ffb4ab] bg-[#ffdad6] text-[#93000a]'
                  : toast.tone === 'warning'
                    ? 'border-[#fcd34d] bg-[#fef3c7] text-[#9a5b00]'
                    : 'border-[#bdd4fb] bg-[#e8f0fe] text-[#0b57d0]'
            }`}
          >
            {toast.tone === 'success' ? (
              <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
            ) : toast.tone === 'danger' ? (
              <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
            ) : (
              <Info className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
            )}
            <p className="flex-1">{toast.message}</p>
            <button
              type="button"
              onClick={() => setToasts((prev) => prev.filter((t) => t.id !== toast.id))}
              aria-label="Đóng thông báo"
              className="rounded p-0.5 hover:bg-black/5"
            >
              <X className="h-3.5 w-3.5" aria-hidden="true" />
            </button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
};
