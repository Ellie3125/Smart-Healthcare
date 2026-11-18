import React, { createContext, useContext, useEffect, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { LogOut, Menu, Radio, X } from 'lucide-react';
import { apiClient } from '../api/client';
import { useAuth } from '../hooks/useAuth';
import { createStaffRealtimeConnection, SignalRStatus } from '../shared/lib/signalr';
import { onRoomSessionReplaced } from '../shared/lib/http';
import { useToast } from '../shared/ui';
import { ROLE_MENUS } from './menus';

const StaffRealtimeContext = createContext<SignalRStatus>('connecting');

export function useStaffRealtimeStatus(): SignalRStatus {
  return useContext(StaffRealtimeContext);
}

function getRoleDisplayName(role: string): string {
  switch (role) {
    case 'Receptionist':
      return 'Tiếp tân';
    case 'Nurse':
      return 'Điều dưỡng';
    case 'Doctor':
      return 'Bác sĩ';
    case 'RoomAccount':
      return 'Phòng CLS';
    case 'Admin':
      return 'Quản trị viên';
    default:
      return role || 'Nhân viên y tế';
  }
}

export const StaffLayout: React.FC = () => {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [rtStatus, setRtStatus] = useState<SignalRStatus>('connecting');
  const [sidebarOpen, setSidebarOpen] = useState(false);

  const menus = user?.role ? ROLE_MENUS[user.role] ?? [] : [];

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

  const handleLogout = async () => {
    if (user?.role === 'RoomAccount') {
      try {
        await apiClient.post('/auth/room/logout');
      } catch {
        // Bỏ qua lỗi mạng khi đóng phiên phía server để luôn xóa phiên client sạch sẽ
      }
    }
    logout();
    navigate('/login');
  };

  const roleDisplay = getRoleDisplayName(user?.role ?? '');

  // Group menu items in definition order
  const groups: { title?: string; items: typeof menus }[] = [];
  for (const item of menus) {
    const lastGroup = groups[groups.length - 1];
    if (!lastGroup || lastGroup.title !== item.group) {
      groups.push({ title: item.group, items: [item] });
    } else {
      lastGroup.items.push(item);
    }
  }

  const sidebarContent = (
    <div className="flex h-full flex-col justify-between py-4">
      <div className="flex flex-col gap-4 min-h-0 flex-1">
        {/* Brand */}
        <div className="px-4 flex items-center justify-between gap-2.5 shrink-0">
          <div className="flex items-center gap-2.5 min-w-0">
            <div className="w-10 h-10 rounded-xl bg-primary flex items-center justify-center text-on-primary shrink-0 shadow-sm">
              <span className="material-symbols-outlined text-[24px]">local_hospital</span>
            </div>
            <div className="flex flex-col min-w-0">
              <span className="font-headline-sm text-headline-sm text-on-surface truncate leading-tight">
                Smart Healthcare
              </span>
              <span className="text-xs text-on-surface-variant truncate">{roleDisplay}</span>
            </div>
          </div>
          <button
            type="button"
            onClick={() => setSidebarOpen(false)}
            aria-label="Đóng thanh điều hướng"
            className="lg:hidden rounded-lg p-1.5 text-on-surface-variant hover:bg-surface-container"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* HIS Online pill */}
        <div className="px-4 shrink-0">
          <div className="inline-flex items-center gap-1.5 px-2.5 py-1 bg-secondary-container/60 rounded-full">
            <span className="w-2 h-2 rounded-full bg-primary animate-pulse" aria-hidden="true" />
            <span className="text-xs font-medium text-on-secondary-container">Nội bộ bệnh viện (HIS Online)</span>
          </div>
        </div>

        {/* Navigation links */}
        <nav aria-label="Điều hướng phân hệ nhân viên" className="flex-1 overflow-y-auto px-3 space-y-4">
          {groups.map((grp, gIdx) => (
            <div key={grp.title ?? gIdx} className="space-y-1">
              {grp.title ? (
                <div className="px-2.5 pt-1 text-[11px] font-semibold uppercase tracking-wider text-on-surface-variant">
                  {grp.title}
                </div>
              ) : null}
              {grp.items.map((item) => (
                <NavLink
                  key={item.path}
                  to={item.path}
                  end={item.end}
                  onClick={() => setSidebarOpen(false)}
                  className={({ isActive }) =>
                    `flex items-center gap-2.5 px-3 py-2 rounded-xl text-sm transition-colors ${
                      isActive
                        ? 'bg-primary-container text-on-primary-container font-semibold shadow-sm'
                        : 'text-on-surface-variant font-medium hover:bg-surface-container hover:text-on-surface'
                    }`
                  }
                >
                  <span className="material-symbols-outlined text-[20px] shrink-0">{item.icon}</span>
                  <span className="truncate">{item.title}</span>
                </NavLink>
              ))}
            </div>
          ))}
        </nav>
      </div>

      {/* Bottom HIS metadata box */}
      <div className="px-4 pt-3 shrink-0">
        <div className="rounded-xl bg-surface-container/80 p-3 text-xs text-on-surface-variant space-y-1">
          <div className="flex items-center gap-1.5 font-semibold text-primary">
            <span className="material-symbols-outlined text-[16px]">dns</span>
            <span>Mạng Nội Bộ Bệnh Viện</span>
          </div>
          <p className="leading-snug">Chỉ truy cập từ mạng LAN / Wi-Fi phòng khám (NET-06).</p>
        </div>
      </div>
    </div>
  );

  return (
    <StaffRealtimeContext.Provider value={rtStatus}>
      <div className="min-h-screen bg-surface font-body-md text-on-surface flex">
        {/* Desktop Fixed Sidebar */}
        <aside className="hidden lg:flex lg:fixed lg:left-0 lg:top-0 lg:h-screen lg:w-72 bg-surface-container-low z-40 flex-col shadow-[1px_0_8px_rgba(0,0,0,0.04)]">
          {sidebarContent}
        </aside>

        {/* Mobile Slide-Over Sidebar */}
        {sidebarOpen ? (
          <div className="lg:hidden fixed inset-0 z-50 flex">
            <div className="fixed inset-0 bg-black/35" onClick={() => setSidebarOpen(false)} aria-hidden="true" />
            <aside className="relative w-72 max-w-[85vw] bg-surface-container-low h-full z-10 shadow-2xl">
              {sidebarContent}
            </aside>
          </div>
        ) : null}

        {/* Content Column */}
        <div className="flex-1 flex flex-col min-w-0 lg:pl-72">
          {/* Translucent Header */}
          <header className="sticky top-0 h-16 bg-surface-container-lowest/90 backdrop-blur-xl shadow-[0_1px_8px_rgba(0,0,0,0.04)] z-30 flex items-center justify-between px-4 lg:px-6 gap-3">
            <div className="flex items-center gap-2.5 min-w-0">
              <button
                type="button"
                onClick={() => setSidebarOpen(true)}
                aria-label="Mở menu phân hệ"
                className="lg:hidden inline-flex h-9 w-9 items-center justify-center rounded-xl bg-surface-container-low text-on-surface hover:bg-surface-container"
              >
                <Menu className="h-5 w-5" />
              </button>

              <div className="hidden sm:inline-flex items-center gap-1.5 bg-surface-container-high px-3 py-1.5 rounded-lg text-xs font-medium text-on-surface">
                <span className="material-symbols-outlined text-primary text-[16px]">room</span>
                <span>Khu vận hành: {roleDisplay}</span>
              </div>

              <div className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-surface-container text-xs text-on-surface-variant">
                <Radio
                  className={`h-3.5 w-3.5 shrink-0 ${
                    rtStatus === 'connected' ? 'text-primary' : 'text-[#9a5b00]'
                  }`}
                  aria-hidden="true"
                />
                <span className="hidden md:inline">SignalR:</span>
                <strong className="font-semibold text-on-surface">
                  {rtStatus === 'connected'
                    ? 'Đã kết nối realtime'
                    : rtStatus === 'reconnecting'
                      ? 'Đang kết nối lại...'
                      : 'Mất kết nối realtime'}
                </strong>
              </div>
            </div>

            <div className="flex items-center gap-2 sm:gap-3 shrink-0">
              <div className="hidden sm:flex items-center gap-2 pl-2 border-l border-surface-container-high">
                <div className="flex flex-col text-right leading-tight">
                  <span className="text-sm font-semibold text-on-surface truncate max-w-[140px]">
                    {user?.userName ?? 'Nhân viên'}
                  </span>
                  <span className="text-[11px] text-primary font-medium">{roleDisplay}</span>
                </div>
                <div className="w-8 h-8 rounded-full bg-primary flex items-center justify-center text-on-primary shrink-0">
                  <span className="material-symbols-outlined text-[18px]">person</span>
                </div>
              </div>

              <button
                type="button"
                onClick={handleLogout}
                className="inline-flex h-9 items-center gap-1.5 rounded-xl bg-surface-container-low px-3 text-sm font-medium text-tertiary hover:bg-error-container hover:text-[#93000a] transition-colors focus:outline-none focus:ring-2 focus:ring-tertiary/20"
                title="Đăng xuất / Giao ca"
              >
                <LogOut className="h-4 w-4 shrink-0" aria-hidden="true" />
                <span className="hidden sm:inline">Giao ca</span>
              </button>
            </div>
          </header>

          {/* Main Workspace */}
          <main className="flex-1 w-full px-4 sm:px-6 py-5 sm:py-6">
            <Outlet />
          </main>
        </div>
      </div>
    </StaffRealtimeContext.Provider>
  );
};
