import React, { useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { MessageSquareHeart, Minimize2, Maximize2 } from 'lucide-react';
import { ChatbotWorkspace } from './ChatbotPage';

export const ChatFloatingWidget: React.FC = () => {
  const [open, setOpen] = useState(false);
  const location = useLocation();

  // Ẩn floating widget khi người dùng đang ở trang /chatbot để không đè lên khung chat và nút gửi
  if (location.pathname.startsWith('/chatbot')) {
    return null;
  }

  return (
    <div className="hidden md:block fixed bottom-6 right-6 z-40">
      {open ? (
        <div
          className="w-[380px] max-w-[92vw] overflow-hidden rounded-2xl bg-surface-container-lowest shadow-[0_16px_40px_-8px_rgba(0,106,65,0.22),0_8px_24px_-4px_rgba(15,31,24,0.08)] ring-1 ring-outline-variant/25"
          role="dialog"
          aria-label="Hộp thoại trợ lý ảo"
        >
          <div className="flex items-center justify-between bg-surface-container-low px-4 py-3.5">
            <div className="flex items-center gap-2.5">
              <div className="relative flex h-9 w-9 items-center justify-center rounded-xl bg-primary text-white shadow-sm">
                <MessageSquareHeart className="h-5 w-5" aria-hidden="true" />
                <span className="absolute -bottom-0.5 -right-0.5 h-2.5 w-2.5 rounded-full bg-secondary ring-2 ring-surface-container-lowest" />
              </div>
              <div>
                <div className="flex items-center gap-1.5">
                  <span className="font-semibold text-on-surface">Trợ lý Smart Healthcare</span>
                  <span className="rounded bg-primary-container px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-primary">
                    24/7
                  </span>
                </div>
                <p className="text-[11px] text-on-surface-variant">Hỗ trợ gợi ý chuyên khoa &amp; đặt lịch</p>
              </div>
            </div>
            <div className="flex items-center gap-1">
              <Link
                to="/chatbot"
                onClick={() => setOpen(false)}
                aria-label="Mở toàn màn hình"
                className="rounded-lg p-1.5 text-on-surface-variant transition-colors hover:bg-surface-container hover:text-on-surface"
              >
                <Maximize2 className="h-4 w-4" aria-hidden="true" />
              </Link>
              <button
                type="button"
                onClick={() => setOpen(false)}
                aria-label="Thu gọn trợ lý"
                className="rounded-lg p-1.5 text-on-surface-variant transition-colors hover:bg-surface-container hover:text-on-surface"
              >
                <Minimize2 className="h-4 w-4" aria-hidden="true" />
              </button>
            </div>
          </div>
          <div className="p-4">
            <ChatbotWorkspace compact />
          </div>
        </div>
      ) : (
        <button
          type="button"
          onClick={() => setOpen(true)}
          className="inline-flex min-h-[48px] items-center gap-2 rounded-full bg-primary px-5 py-3 text-sm font-semibold text-white shadow-[0_8px_24px_rgba(0,106,65,0.28)] transition-all hover:bg-[#005231] focus:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2"
        >
          <MessageSquareHeart className="h-5 w-5" aria-hidden="true" />
          <span>Trợ lý sức khỏe</span>
        </button>
      )}
    </div>
  );
};
