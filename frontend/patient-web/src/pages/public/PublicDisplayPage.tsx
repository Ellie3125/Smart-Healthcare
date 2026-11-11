import React, { useEffect } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Tv } from 'lucide-react';
import { bookingApi } from '../../api/bookingApi';
import { createPatientRealtimeConnection } from '../../shared/lib/signalr';
import { DateTimeText, ErrorState, Skeleton } from '../../shared/ui';
import { extractPatientError } from '../../shared/lib/errors';

/**
 * TV01 (`/display`) — Màn hình gọi số công khai tại sảnh chờ.
 * NGUYÊN TẮC BẢO MẬT CỨNG: Chỉ hiển thị số vé và phòng/quầy, TUYỆT ĐỐI KHÔNG chứa tên hoặc bất kỳ PII nào của bệnh nhân.
 */
export const PublicDisplayPage: React.FC = () => {
  const queryClient = useQueryClient();

  const boardQuery = useQuery({
    queryKey: ['public', 'display-board'],
    queryFn: () => bookingApi.getPublicDisplayBoard(),
    refetchInterval: 10000,
  });

  useEffect(() => {
    const conn = createPatientRealtimeConnection(
      '/hubs/public-display',
      () => null,
      ['DisplayBoardUpdated'],
      {
        onEvent: () => {
          queryClient.invalidateQueries({ queryKey: ['public', 'display-board'] });
        },
      },
    );
    conn.start();
    return () => {
      conn.stop();
    };
  }, [queryClient]);

  if (boardQuery.isLoading) {
    return (
      <div className="min-h-screen bg-[#0f1f18] p-8 text-white">
        <Skeleton rows={6} />
      </div>
    );
  }

  if (boardQuery.isError || !boardQuery.data) {
    return (
      <div className="min-h-screen bg-[#ebfef2] p-8">
        <ErrorState message={extractPatientError(boardQuery.error).message} onRetry={() => boardQuery.refetch()} />
      </div>
    );
  }

  const board = boardQuery.data;

  return (
    <main className="min-h-screen bg-[#0f1f18] p-6 text-white sm:p-10">
      <header className="mb-8 flex flex-wrap items-center justify-between gap-4 border-b border-white/15 pb-5">
        <div className="flex items-center gap-3">
          <div className="rounded-2xl bg-[#006a41] p-3">
            <Tv className="h-7 w-7 text-white" aria-hidden="true" />
          </div>
          <div>
            <h1 className="text-2xl font-bold tracking-tight sm:text-3xl">{board.hospitalName}</h1>
            <p className="text-sm text-[#8ef8bb]">Bảng gọi số thứ tự công khai tại sảnh chờ</p>
          </div>
        </div>
        <div className="text-right text-sm text-white/80">
          <span>Cập nhật: </span>
          <DateTimeText value={board.generatedAt} />
        </div>
      </header>

      <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3" aria-live="polite">
        {board.rooms.map((room) => (
          <section
            key={room.roomCode}
            className="flex flex-col justify-between rounded-3xl border border-white/15 bg-[#182d23] p-6 shadow-xl"
          >
            <div>
              <p className="text-xs font-semibold uppercase tracking-wider text-[#8ef8bb]">{room.roomCode}</p>
              <h2 className="mt-1 text-xl font-semibold text-white">{room.roomName}</h2>
            </div>

            <div className="my-6 rounded-2xl bg-[#006a41] px-5 py-6 text-center">
              <p className="text-xs uppercase tracking-widest text-[#d4f7e2]">Đang mời số</p>
              <p className="mt-2 text-5xl font-bold tracking-tight tabular-nums text-white sm:text-6xl">
                {room.currentTicketNumber || '—'}
              </p>
            </div>

            <div>
              <p className="text-xs uppercase tracking-wider text-white/70">Chuẩn bị kế tiếp</p>
              <div className="mt-2 flex flex-wrap gap-2">
                {room.waitingTicketNumbers.length > 0 ? (
                  room.waitingTicketNumbers.map((ticket) => (
                    <span
                      key={ticket}
                      className="rounded-xl bg-white/10 px-3.5 py-1.5 text-lg font-semibold tabular-nums text-[#8ef8bb]"
                    >
                      {ticket}
                    </span>
                  ))
                ) : (
                  <span className="text-sm text-white/60">Chưa có lượt kế tiếp</span>
                )}
              </div>
            </div>
          </section>
        ))}
      </div>
    </main>
  );
};
