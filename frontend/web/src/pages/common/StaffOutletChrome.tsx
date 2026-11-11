import React, { useEffect, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { Bell, Radio, WifiOff } from 'lucide-react';
import { adminOpsApi } from '../../api/adminOpsApi';
import { useAuth } from '../../hooks/useAuth';
import { createStaffRealtimeConnection, SignalRStatus } from '../../shared/lib/signalr';
import { onRoomSessionReplaced } from '../../shared/lib/http';
import { Banner, useToast } from '../../shared/ui';

const ROLE_SUB_NAVS: Record<string, { to: string; label: string; end?: boolean }[]> = {
  Admin: [
    { to: '/admin', label: 'Nhân sự & Phòng (AD01–05)', end: true },
    { to: '/admin/stations', label: 'Quầy (AD03)' },
    { to: '/admin/quotas', label: 'Suất & Phiên BS (AD06–07)' },
    { to: '/admin/services', label: 'DV CLS (AD08)' },
    { to: '/admin/price-lists', label: 'Bảng giá (AD09)' },
    { to: '/admin/devices', label: 'Quyền & Thiết bị (AD10)' },
    { to: '/admin/exceptions', label: 'Ngoại lệ (AD11)' },
    { to: '/admin/refunds', label: 'Duyệt hoàn (AD12)' },
    { to: '/admin/reconcile', label: 'Đối soát (AD13)' },
    { to: '/admin/reports', label: 'Báo cáo (AD14)' },
    { to: '/admin/audit-logs', label: 'AuditLog (AD15)' },
    { to: '/admin/cls-waivers', label: 'Miễn giảm CLS (AD16)' },
    { to: '/admin/chatbot-config', label: 'Chatbot (AD17)' },
  ],
  Receptionist: [
    { to: '/reception', label: 'Xác minh hồ sơ (RC05)', end: true },
    { to: '/reception/search', label: 'Tìm kiếm (RC01)' },
    { to: '/reception/walk-in', label: 'Walk-in (RC02)' },
    { to: '/reception/checkin', label: 'Check-in CCCD (RC03)' },
    { to: '/reception/cashier', label: 'Thu ngân (RC04)' },
    { to: '/reception/querydr', label: 'Tra cứu VNPAY (RC06)' },
    { to: '/reception/cancel', label: 'Hủy lịch (RC07)' },
    { to: '/reception/refunds', label: 'Chi hoàn mặt (RC08)' },
    { to: '/reception/shift-close', label: 'Chốt ca (RC09)' },
    { to: '/reception/call-list', label: 'DS cần gọi (RC10)' },
  ],
  Nurse: [
    { to: '/nurse/queue', label: 'Hàng chờ sinh hiệu (NU01)' },
    { to: '/nurse', label: 'Đo sinh hiệu (NU02)', end: true },
  ],
  Doctor: [
    { to: '/doctor', label: 'Bàn làm việc & Hàng đợi (DR01–08)', end: true },
    { to: '/doctor/records', label: 'Lịch sử khám (DR09)' },
  ],
  RoomAccount: [
    { to: '/room', label: 'Hàng đợi CLS (CL01–03)', end: true },
    { to: '/room/upload', label: 'Nhập & Upload KQ (CL04)' },
  ],
};

/**
 * Bao bọc các route bên trong StaffLayout để bổ sung SignalR (`skipNegotiation`),
 * chuông thông báo S03, điều hướng chức năng theo vai trò và S04(b).
 */
export const StaffOutletChrome: React.FC = () => {
  const { user } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [rtStatus, setRtStatus] = useState<SignalRStatus>('connecting');

  const notiQuery = useQuery({
    queryKey: ['staff', 'notifications'],
    queryFn: () => adminOpsApi.listNotifications(),
    staleTime: 30_000,
  });

  useEffect(() => {
    const unsubHttp = onRoomSessionReplaced(() => {
      navigate('/session-replaced', { replace: true });
    });

    const conn = createStaffRealtimeConnection(
      '/hubs/staff',
      () => localStorage.getItem('token'),
      ['NotificationReceived', 'QueueUpdated', 'SessionReplaced'],
      {
        onStatusChange: setRtStatus,
        onEvent: (eventName, payload) => {
          if (eventName === 'SessionReplaced') {
            navigate('/session-replaced', { replace: true });
            return;
          }
          if (eventName === 'NotificationReceived') {
            queryClient.invalidateQueries({ queryKey: ['staff', 'notifications'] });
            const msg =
              typeof payload === 'object' && payload !== null && 'title' in payload
                ? String((payload as { title?: string }).title)
                : 'Có thông báo mới';
            pushToast(msg, 'info');
          } else if (eventName === 'QueueUpdated') {
            queryClient.invalidateQueries({ queryKey: ['staff'] });
          }
        },
      },
    );
    conn.start();
    return () => {
      unsubHttp();
      conn.stop();
    };
  }, [navigate, pushToast, queryClient]);

  const unread = notiQuery.data?.unreadCount ?? 0;
  const subNavs = user?.role ? ROLE_SUB_NAVS[user.role] ?? [] : [];

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-[#cddbd3] bg-white px-4 py-2 text-xs">
        <div className="flex items-center gap-2 text-[#44544c]">
          <Radio
            className={`h-3.5 w-3.5 ${rtStatus === 'connected' ? 'text-[#006a41]' : 'text-[#9a5b00]'}`}
            aria-hidden="true"
          />
          <span>
            SignalR:{' '}
            <strong>
              {rtStatus === 'connected'
                ? 'Đã kết nối realtime'
                : rtStatus === 'reconnecting'
                  ? 'Đang kết nối lại...'
                  : 'Mất kết nối realtime'}
            </strong>
          </span>
        </div>
        <Link
          to="/notifications"
          className="inline-flex items-center gap-1.5 rounded-lg border border-[#cddbd3] bg-[#f4f8f6] px-3 py-1.5 font-medium text-[#0f1f18] hover:bg-[#e9f2ed]"
        >
          <Bell className="h-3.5 w-3.5 text-[#006a41]" aria-hidden="true" />
          <span>Thông báo</span>
          {unread > 0 ? (
            <span className="rounded-full bg-[#ba1a1a] px-1.5 py-0.5 text-[11px] font-semibold tabular-nums text-white">
              {unread}
            </span>
          ) : null}
        </Link>
      </div>

      {subNavs.length > 0 ? (
        <nav aria-label="Điều hướng phân hệ nhân viên" className="flex flex-wrap gap-1.5 rounded-xl border border-[#cddbd3] bg-white p-2">
          {subNavs.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) =>
                `rounded-lg px-3 py-1.5 text-xs font-medium transition-colors ${
                  isActive
                    ? 'bg-[#006a41] text-white'
                    : 'text-[#44544c] hover:bg-[#e9f2ed] hover:text-[#0f1f18]'
                }`
              }
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
      ) : null}

      {rtStatus === 'disconnected' ? (
        <Banner tone="warning" title="Mất kết nối thông báo trực tiếp">
          <div className="flex items-center gap-2">
            <WifiOff className="h-4 w-4 shrink-0" aria-hidden="true" />
            <span>Kênh SignalR đang gián đoạn. Vui lòng bấm làm mới định kỳ khi thao tác hàng đợi.</span>
          </div>
        </Banner>
      ) : null}

      <Outlet />
    </div>
  );
};
