import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { MessageSquareHeart, Minimize2, Maximize2 } from 'lucide-react';
import { ChatbotWorkspace } from './ChatbotPage';

export const ChatFloatingWidget: React.FC = () => {
  const [open, setOpen] = useState(false);

  return (
    <div className="fixed bottom-20 right-4 z-40 sm:bottom-5">
      {open ? (
        <div
          className="w-[360px] max-w-[92vw] rounded-2xl border border-[#006a41]/30 bg-white p-4 shadow-2xl"
          role="dialog"
          aria-label="Hộp thoại trợ lý ảo"
        >
          <div className="mb-3 flex items-center justify-between border-b border-[#e5f8ed] pb-2.5">
            <div className="flex items-center gap-2">
              <MessageSquareHeart className="h-5 w-5 text-[#006a41]" aria-hidden="true" />
              <span className="font-semibold text-[#0f1f18]">Trợ lý Smart Healthcare</span>
            </div>
            <div className="flex items-center gap-1">
              <Link
                to="/chatbot"
                onClick={() => setOpen(false)}
                aria-label="Mở toàn màn hình"
                className="rounded-lg p-1.5 text-[#3e4941] hover:bg-[#e5f8ed]"
              >
                <Maximize2 className="h-4 w-4" aria-hidden="true" />
              </Link>
              <button
                type="button"
                onClick={() => setOpen(false)}
                aria-label="Thu gọn trợ lý"
                className="rounded-lg p-1.5 text-[#3e4941] hover:bg-[#e5f8ed]"
              >
                <Minimize2 className="h-4 w-4" aria-hidden="true" />
              </button>
            </div>
          </div>
          <ChatbotWorkspace compact />
        </div>
      ) : (
        <button
          type="button"
          onClick={() => setOpen(true)}
          className="inline-flex min-h-[48px] items-center gap-2 rounded-full bg-[#006a41] px-4 py-3 text-sm font-semibold text-white shadow-lg hover:bg-[#005231] focus:outline-none focus-visible:ring-2 focus-visible:ring-[#006a41] focus-visible:ring-offset-2"
        >
          <MessageSquareHeart className="h-5 w-5" aria-hidden="true" />
          <span>Trợ lý sức khỏe</span>
        </button>
      )}
    </div>
  );
};
