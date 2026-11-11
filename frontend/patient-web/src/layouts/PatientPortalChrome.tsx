import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link, NavLink, Outlet } from 'react-router-dom';
import {
  Activity,
  Bell,
  Bot,
  CalendarPlus,
  CalendarCheck,
  CreditCard,
  FileHeart,
  Receipt,
  UserRound,
} from 'lucide-react';
import { assistantApi } from '../api/assistantApi';
import { ChatFloatingWidget } from '../pages/assistant/ChatFloatingWidget';

const PATIENT_NAV_ITEMS = [
  { to: '/profiles', label: 'Hồ sơ', icon: UserRound },
  { to: '/booking', label: 'Đặt lịch', icon: CalendarPlus },
  { to: '/appointments', label: 'Lịch của tôi', icon: CalendarCheck },
  { to: '/today-journey', label: 'Hôm nay', icon: Activity },
  { to: '/cls-payments', label: 'Thanh toán CLS', icon: CreditCard },
  { to: '/records', label: 'Kết quả khám', icon: FileHeart },
  { to: '/refunds', label: 'Hoàn tiền', icon: Receipt },
  { to: '/chatbot', label: 'Trợ lý', icon: Bot },
];

export const PatientPortalChrome: React.FC = () => {
  const notiQuery = useQuery({
    queryKey: ['patient', 'notifications', 'badge'],
    queryFn: () => assistantApi.listNotifications({ page: 1, pageSize: 5 }),
    staleTime: 30_000,
  });

  const unreadCount = notiQuery.data?.unreadCount ?? 0;

  return (
    <div className="space-y-5 pb-20 sm:pb-6">
      <nav
        aria-label="Điều hướng cổng bệnh nhân"
        className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-[#bdcabf] bg-white px-4 py-2.5 shadow-sm"
      >
        <div className="flex flex-wrap items-center gap-1.5">
          {PATIENT_NAV_ITEMS.map(({ to, label, icon: Icon }) => (
            <NavLink
              key={to}
              to={to}
              className={({ isActive }) =>
                `inline-flex min-h-[40px] items-center gap-1.5 rounded-xl px-3 py-1.5 text-sm font-medium transition-colors ${
                  isActive
                    ? 'bg-[#006a41] text-white'
                    : 'text-[#3e4941] hover:bg-[#e5f8ed] hover:text-[#0f1f18]'
                }`
              }
            >
              <Icon className="h-4 w-4 shrink-0" aria-hidden="true" />
              <span>{label}</span>
            </NavLink>
          ))}
        </div>

        <Link
          to="/notifications"
          className="inline-flex min-h-[40px] items-center gap-2 rounded-xl border border-[#bdcabf] bg-[#f8fbf9] px-3 py-1.5 text-sm font-medium text-[#0f1f18] hover:bg-[#e5f8ed]"
        >
          <Bell className="h-4 w-4 text-[#006a41]" aria-hidden="true" />
          <span>Thông báo</span>
          {unreadCount > 0 ? (
            <span className="rounded-full bg-[#ba1a1a] px-2 py-0.5 text-xs font-semibold tabular-nums text-white">
              {unreadCount}
            </span>
          ) : null}
        </Link>
      </nav>

      <Outlet />
      <ChatFloatingWidget />
    </div>
  );
};
