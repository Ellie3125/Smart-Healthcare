import React, { useEffect, useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  Activity,
  Bell,
  Bot,
  CalendarCheck,
  CalendarPlus,
  ChevronDown,
  CreditCard,
  FileHeart,
  LogOut,
  Menu,
  PhoneCall,
  UserPlus,
  UserRound,
  X,
} from 'lucide-react';
import { assistantApi } from '../api/assistantApi';
import { useAuth } from '../hooks/useAuth';

const PATIENT_NAV_ITEMS = [
  { to: '/booking', label: 'Đặt lịch', shortLabel: 'Đặt lịch', icon: CalendarPlus },
  { to: '/appointments', label: 'Lịch của tôi', shortLabel: 'Lịch hẹn', icon: CalendarCheck },
  { to: '/today-journey', label: 'Hôm nay', shortLabel: 'Hôm nay', icon: Activity },
  { to: '/cls-payments', label: 'Thanh toán CLS', shortLabel: 'Thanh toán', icon: CreditCard },
  { to: '/records', label: 'Kết quả khám', shortLabel: 'Kết quả', icon: FileHeart },
  { to: '/chatbot', label: 'Trợ lý', shortLabel: 'Trợ lý', icon: Bot },
];

const MOBILE_BOTTOM_TABS = [
  { to: '/booking', label: 'Đặt lịch', shortLabel: 'Đặt lịch', icon: CalendarPlus },
  { to: '/appointments', label: 'Lịch của tôi', shortLabel: 'Lịch hẹn', icon: CalendarCheck },
  { to: '/today-journey', label: 'Hôm nay', shortLabel: 'Hôm nay', icon: Activity },
  { to: '/records', label: 'Kết quả khám', shortLabel: 'Kết quả', icon: FileHeart },
  { to: '/profiles', label: 'Hồ sơ', shortLabel: 'Hồ sơ', icon: UserRound },
];

export const PatientLayout: React.FC = () => {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [userMenuOpen, setUserMenuOpen] = useState(false);
  const userMenuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (userMenuRef.current && !userMenuRef.current.contains(event.target as Node)) {
        setUserMenuOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  useEffect(() => {
    setUserMenuOpen(false);
  }, [location.pathname]);

  const notiQuery = useQuery({
    queryKey: ['patient', 'notifications', 'badge'],
    queryFn: () => assistantApi.listNotifications({ page: 1, pageSize: 5 }),
    staleTime: 30_000,
    enabled: Boolean(user),
  });

  const unreadCount = notiQuery.data?.unreadCount ?? 0;

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <div className="min-h-screen bg-surface flex flex-col font-body-md text-on-surface">
      {/* Stitch web_standard translucent header */}
      <header className="sticky top-0 w-full z-40 bg-surface-container-lowest/95 backdrop-blur-xl shadow-[0_1px_8px_rgba(0,0,0,0.04)]">
        <div className="h-20 max-w-[1440px] 2xl:max-w-[1600px] mx-auto px-4 sm:px-6 lg:px-8 flex items-center justify-between gap-3">
          <div className="flex items-center gap-2 xl:gap-4 min-w-0 flex-1">
            <button
              type="button"
              onClick={() => setMobileMenuOpen((prev) => !prev)}
              aria-label="Mở menu điều hướng"
              className="lg:hidden inline-flex h-10 w-10 items-center justify-center rounded-xl bg-surface-container-low text-on-surface hover:bg-surface-container shrink-0"
            >
              {mobileMenuOpen ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
            </button>

            <Link to="/booking" className="flex items-center gap-2 shrink-0 group focus:outline-none">
              <div className="w-10 h-10 rounded-xl bg-primary flex items-center justify-center text-on-primary shadow-sm">
                <span className="material-symbols-outlined text-[24px]">local_hospital</span>
              </div>
              <div className="flex flex-col">
                <span className="font-headline-sm text-headline-sm text-primary leading-tight font-semibold tracking-tight whitespace-nowrap">
                  Smart Healthcare
                </span>
                <span className="hidden 3xl:block text-[11px] text-on-surface-variant font-normal">
                  Phòng khám Ngoại trú Chuẩn Quốc tế
                </span>
              </div>
            </Link>

            <nav aria-label="Điều hướng cổng bệnh nhân" className="hidden lg:flex items-center gap-1 min-w-0 overflow-hidden">
              {PATIENT_NAV_ITEMS.map(({ to, label, shortLabel, icon: Icon }) => (
                <NavLink
                  key={to}
                  to={to}
                  title={label}
                  aria-label={label}
                  className={({ isActive }) =>
                    `inline-flex items-center gap-1.5 rounded-xl px-2.5 py-1.5 text-xs xl:text-sm font-medium whitespace-nowrap transition-colors shrink-0 ${
                      isActive
                        ? 'bg-primary-container text-on-primary-container font-semibold shadow-sm'
                        : 'text-on-surface-variant hover:text-on-surface hover:bg-surface-container'
                    }`
                  }
                >
                  <Icon className="h-4 w-4 shrink-0" aria-hidden="true" />
                  <span className="hidden xl:inline">{shortLabel}</span>
                </NavLink>
              ))}
            </nav>
          </div>

          <div className="flex items-center gap-2 sm:gap-3 shrink-0">
            <a
              href="tel:19006868"
              className="hidden 2xl:flex items-center gap-2 px-3 py-1.5 rounded-full bg-surface-container hover:bg-surface-container-high transition-colors text-on-surface shrink-0"
              title="Cấp cứu 24/7: 1900 6868"
            >
              <PhoneCall className="h-3.5 w-3.5 text-primary shrink-0" aria-hidden="true" />
              <div className="flex flex-col leading-none">
                <span className="text-[9px] text-on-surface-variant font-medium">Cấp cứu 24/7</span>
                <span className="text-xs font-semibold text-primary tabular-nums">1900 6868</span>
              </div>
            </a>

            <Link
              to="/notifications"
              className="relative inline-flex h-9 w-9 items-center justify-center rounded-full bg-surface-container-low text-on-surface hover:bg-surface-container transition-colors shrink-0"
              title="Thông báo"
              aria-label="Thông báo"
            >
              <Bell className="h-4 w-4 text-primary shrink-0" aria-hidden="true" />
              {unreadCount > 0 ? (
                <span className="absolute -top-1 -right-1 flex h-4 min-w-4 items-center justify-center rounded-full bg-error px-1 text-[10px] font-bold tabular-nums text-white shadow-sm">
                  {unreadCount > 99 ? '99+' : unreadCount}
                </span>
              ) : null}
            </Link>

            <div className="hidden sm:block h-6 w-px bg-surface-container-highest shrink-0" aria-hidden="true" />

            {/* User Avatar with Profile Dropdown */}
            <div className="relative hidden md:block" ref={userMenuRef}>
              <button
                type="button"
                onClick={() => setUserMenuOpen((prev) => !prev)}
                className={`flex items-center gap-2 p-1.5 rounded-xl hover:bg-surface-container transition-colors focus:outline-none focus:ring-2 focus:ring-primary/20 ${
                  location.pathname.startsWith('/profiles') ? 'bg-surface-container' : ''
                }`}
                aria-expanded={userMenuOpen}
                aria-haspopup="true"
                title="Hồ sơ & Tài khoản"
              >
                <div
                  className={`w-8 h-8 rounded-full bg-primary flex items-center justify-center text-on-primary shrink-0 shadow-xs ${
                    location.pathname.startsWith('/profiles') ? 'ring-2 ring-primary ring-offset-2' : ''
                  }`}
                >
                  <span className="material-symbols-outlined text-[18px]">person</span>
                </div>
                <div className="hidden 2xl:flex items-center gap-1.5 text-left leading-tight">
                  <span className="text-sm font-semibold text-on-surface truncate max-w-[120px]">
                    {user?.userName ?? 'Bệnh nhân'}
                  </span>
                  <ChevronDown
                    className={`h-3.5 w-3.5 text-on-surface-variant transition-transform duration-150 ${
                      userMenuOpen ? 'rotate-180' : ''
                    }`}
                  />
                </div>
              </button>

              {userMenuOpen && (
                <div className="absolute right-0 mt-2 w-56 rounded-2xl bg-surface-container-lowest border border-surface-container-highest shadow-[0_4px_20px_rgba(0,0,0,0.08)] py-1.5 z-50">
                  <div className="px-4 py-2 border-b border-surface-container-high/60">
                    <p className="text-[11px] text-on-surface-variant font-medium">Tài khoản</p>
                    <p className="text-sm font-bold text-on-surface truncate">{user?.userName ?? 'Bệnh nhân'}</p>
                  </div>
                  <div className="py-1">
                    <Link
                      to="/profiles"
                      onClick={() => setUserMenuOpen(false)}
                      className={`flex items-center gap-2.5 px-4 py-2 text-sm font-medium transition-colors ${
                        location.pathname === '/profiles' || location.pathname.startsWith('/profiles/')
                          ? 'bg-primary-container text-on-primary-container font-semibold'
                          : 'text-on-surface hover:bg-surface-container'
                      }`}
                    >
                      <UserRound className="h-4 w-4 text-primary shrink-0" />
                      <span>Hồ sơ của tôi</span>
                    </Link>
                    <Link
                      to="/profiles/add-relative"
                      onClick={() => setUserMenuOpen(false)}
                      className="flex items-center gap-2.5 px-4 py-2 text-sm font-medium text-on-surface hover:bg-surface-container transition-colors"
                    >
                      <UserPlus className="h-4 w-4 text-primary shrink-0" />
                      <span>Thêm người thân</span>
                    </Link>
                  </div>
                  <div className="border-t border-surface-container-high/60 pt-1">
                    <button
                      type="button"
                      onClick={() => {
                        setUserMenuOpen(false);
                        handleLogout();
                      }}
                      className="w-full flex items-center gap-2.5 px-4 py-2 text-sm font-medium text-tertiary hover:bg-error-container hover:text-[#93000a] transition-colors"
                    >
                      <LogOut className="h-4 w-4 shrink-0" />
                      <span>Đăng xuất</span>
                    </button>
                  </div>
                </div>
              )}
            </div>

            <button
              type="button"
              onClick={handleLogout}
              className="inline-flex h-9 items-center gap-1.5 rounded-xl bg-surface-container-low px-2.5 2xl:px-3 text-sm font-medium text-tertiary hover:bg-error-container hover:text-[#93000a] transition-colors focus:outline-none focus:ring-2 focus:ring-tertiary/20 shrink-0"
              title="Đăng xuất"
              aria-label="Đăng xuất"
            >
              <LogOut className="h-4 w-4 shrink-0" aria-hidden="true" />
              <span className="hidden 2xl:inline">Đăng xuất</span>
            </button>
          </div>
        </div>

        {/* Mobile/Tablet expandable navigation drawer */}
        {mobileMenuOpen ? (
          <div className="lg:hidden border-t border-surface-container bg-surface-container-lowest px-4 py-3 shadow-md">
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
              <Link
                to="/profiles"
                onClick={() => setMobileMenuOpen(false)}
                className={`flex items-center gap-2 rounded-xl px-3 py-2.5 text-sm font-medium ${
                  location.pathname === '/profiles' || location.pathname.startsWith('/profiles/')
                    ? 'bg-primary-container text-on-primary-container font-semibold'
                    : 'bg-surface-container-low text-on-surface-variant hover:text-on-surface'
                }`}
              >
                <UserRound className="h-4 w-4 shrink-0" aria-hidden="true" />
                <span>Hồ sơ của tôi</span>
              </Link>
              {PATIENT_NAV_ITEMS.map(({ to, label, icon: Icon }) => {
                const isActive = location.pathname === to || location.pathname.startsWith(`${to}/`);
                return (
                  <Link
                    key={to}
                    to={to}
                    onClick={() => setMobileMenuOpen(false)}
                    className={`flex items-center gap-2 rounded-xl px-3 py-2.5 text-sm font-medium ${
                      isActive
                        ? 'bg-primary-container text-on-primary-container font-semibold'
                        : 'bg-surface-container-low text-on-surface-variant hover:text-on-surface'
                    }`}
                  >
                    <Icon className="h-4 w-4 shrink-0" aria-hidden="true" />
                    <span>{label}</span>
                  </Link>
                );
              })}
            </div>
          </div>
        ) : null}
      </header>

      {/* Main Content */}
      <main className="flex-1 w-full max-w-[1440px] mx-auto px-4 sm:px-6 lg:px-8 pt-6 pb-24 md:py-8">
        <Outlet />
      </main>

      {/* Mobile Bottom Navigation */}
      <nav
        aria-label="Thanh truy cập nhanh di động"
        className="md:hidden fixed bottom-0 left-0 right-0 bg-surface-container-lowest/95 backdrop-blur-lg border-t border-surface-container-high px-2 py-1.5 flex justify-around items-center z-30 shadow-[0_-2px_10px_rgba(15,31,24,0.06)]"
      >
        {MOBILE_BOTTOM_TABS.map(({ to, shortLabel, icon: Icon }) => {
          const isActive = location.pathname === to || location.pathname.startsWith(`${to}/`);
          return (
            <Link
              key={to}
              to={to}
              className={`flex flex-col items-center gap-0.5 rounded-xl px-2.5 py-1 text-[11px] font-medium transition-colors ${
                isActive ? 'text-primary font-semibold' : 'text-on-surface-variant'
              }`}
            >
              <Icon className="h-5 w-5" aria-hidden="true" />
              <span>{shortLabel}</span>
            </Link>
          );
        })}
      </nav>

      {/* Stitch 3-Column Footer */}
      <footer className="hidden md:block w-full bg-surface-container-lowest shadow-[0_-1px_8px_rgba(0,0,0,0.03)] mt-8">
        <div className="max-w-[1440px] mx-auto px-4 sm:px-6 lg:px-8 py-10">
          <div className="grid grid-cols-1 md:grid-cols-4 gap-8 pb-8">
            <div className="md:col-span-2 flex flex-col gap-3">
              <div className="flex items-center gap-2.5">
                <div className="w-8 h-8 rounded-lg bg-primary flex items-center justify-center text-on-primary">
                  <span className="material-symbols-outlined text-[20px]">local_hospital</span>
                </div>
                <span className="font-headline-sm text-headline-sm text-primary font-semibold">
                  Smart Healthcare Vietnam
                </span>
              </div>
              <p className="text-sm text-on-surface-variant max-w-xl leading-relaxed">
                Hệ thống phòng khám đa khoa số hóa đạt chuẩn Bộ Y Tế. Quy trình khám chữa bệnh ngoại trú khép kín, minh
                bạch thời gian chờ và kết nối hồ sơ y bạ điện tử tức thời.
              </p>
              <div className="flex flex-wrap items-center gap-2 pt-1">
                <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-lg bg-surface-container text-on-surface-variant text-xs font-medium">
                  <span className="material-symbols-outlined text-[16px] text-primary">verified</span>
                  <span>Bộ Y Tế Cấp Phép GP: 08842/BYT-CCHN</span>
                </div>
                <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-lg bg-surface-container text-on-surface-variant text-xs font-medium">
                  <span className="material-symbols-outlined text-[16px] text-primary">health_and_safety</span>
                  <span>Tiêu chuẩn JCI Outpatient Clinic</span>
                </div>
              </div>
            </div>

            <div className="flex flex-col gap-2">
              <span className="text-xs font-semibold text-on-surface uppercase tracking-wider mb-1">
                Dịch Vụ &amp; Truy Cập Nhanh
              </span>
              <Link to="/booking" className="text-sm text-on-surface-variant hover:text-primary transition-colors">
                Đặt lịch khám chuyên khoa
              </Link>
              <Link to="/appointments" className="text-sm text-on-surface-variant hover:text-primary transition-colors">
                Quản lý lịch hẹn &amp; vé khám
              </Link>
              <Link to="/cls-payments" className="text-sm text-on-surface-variant hover:text-primary transition-colors">
                Thanh toán chỉ định cận lâm sàng
              </Link>
              <Link to="/records" className="text-sm text-on-surface-variant hover:text-primary transition-colors">
                Tra cứu kết quả &amp; đơn thuốc
              </Link>
            </div>

            <div className="flex flex-col gap-2">
              <span className="text-xs font-semibold text-on-surface uppercase tracking-wider mb-1">
                Hỗ Trợ &amp; Liên Hệ
              </span>
              <div className="flex items-center gap-2 text-sm text-on-surface">
                <span className="material-symbols-outlined text-[18px] text-primary">call</span>
                <span className="font-medium">Hotline CSKH: 1900 6868</span>
              </div>
              <div className="flex items-center gap-2 text-sm text-on-surface-variant">
                <span className="material-symbols-outlined text-[18px] text-primary">location_on</span>
                <span>Tòa nhà Medical Center, Q.1, TP. Hồ Chí Minh</span>
              </div>
              <div className="flex items-center gap-2 text-sm text-on-surface-variant">
                <span className="material-symbols-outlined text-[18px] text-primary">schedule</span>
                <span>Giờ làm việc: 07:00 - 20:30 (Thứ 2 - CN)</span>
              </div>
            </div>
          </div>

          <div className="pt-5 border-t border-surface-container-low flex flex-col sm:flex-row items-center justify-between gap-2 text-xs text-on-surface-variant md:pr-48">
            <p>© 2026 Smart Healthcare Clinic. Đối chiếu căn cước &amp; thẻ gốc trực tiếp tại quầy tiếp tân cơ sở y tế (FR01).</p>
            <p>Hồ sơ &amp; Dữ liệu bệnh án được mã hóa bảo mật tiêu chuẩn y tế.</p>
          </div>
        </div>
      </footer>
    </div>
  );
};
