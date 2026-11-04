import { Link, useLocation, useNavigate } from 'react-router-dom';
import type { ReactNode } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { ROLE_MENUS } from './menus';

interface StaffLayoutProps {
  readonly children: ReactNode;
}

export function StaffLayout({ children }: StaffLayoutProps) {
  const { user, logout } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();

  const menu = user ? ROLE_MENUS[user.role] : null;

  const handleLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };

  return (
    <div className="min-h-screen flex bg-[#F7FAF8]">
      {/* Sidebar */}
      <aside className="hidden md:flex flex-col w-64 bg-white border-r border-[#DDE7E1] fixed inset-y-0 left-0 z-30">
        {/* Brand */}
        <div className="flex items-center gap-3 px-5 py-4 border-b border-[#DDE7E1]">
          <div className="w-9 h-9 rounded-lg bg-[#E8F5EE] border border-[#DDE7E1] flex items-center justify-center">
            <span className="material-symbols-outlined text-[#006a41] text-[22px]">local_hospital</span>
          </div>
          <div>
            <span className="block text-[15px] font-bold text-[#1C2B24] leading-tight tracking-tight">
              Smart Healthcare
            </span>
            <span className="block text-[11px] text-[#5E6F66]">Hệ thống Y tế Kỹ thuật số</span>
          </div>
        </div>

        {/* Role Badge */}
        {menu && (
          <div className="flex items-center gap-2 mx-4 mt-4 mb-2 px-3 py-2 rounded-lg bg-[#E8F5EE] border border-[#DDE7E1]">
            <span className="material-symbols-outlined text-[#006a41] text-[18px]">{menu.roleIcon}</span>
            <div>
              <span className="block text-[11px] text-[#5E6F66]">Vai trò</span>
              <span className="block text-[13px] font-semibold text-[#1C2B24]">{menu.roleLabel}</span>
            </div>
          </div>
        )}

        {/* Nav Items */}
        <nav className="flex-1 overflow-y-auto px-3 py-2 space-y-0.5">
          {menu?.items.map((item) => {
            const isActive = location.pathname === item.path || location.pathname.startsWith(item.path + '/');
            return (
              <Link
                key={item.path}
                to={item.path}
                className={[
                  'flex items-center gap-3 px-3 py-2.5 rounded-lg text-[14px] font-medium transition-colors duration-150',
                  isActive
                    ? 'bg-[#E8F5EE] text-[#006a41]'
                    : 'text-[#3e4941] hover:bg-[#F1F4F2] hover:text-[#1C2B24]',
                ].join(' ')}
              >
                <span
                  className={[
                    'material-symbols-outlined text-[20px]',
                    isActive ? 'text-[#006a41]' : 'text-[#6e7a71]',
                  ].join(' ')}
                >
                  {item.icon}
                </span>
                {item.label}
              </Link>
            );
          })}
        </nav>

        {/* User + Logout */}
        <div className="border-t border-[#DDE7E1] p-4">
          <div className="flex items-center gap-3 mb-3">
            <div className="w-8 h-8 rounded-full bg-[#028654] flex items-center justify-center shrink-0">
              <span className="text-white text-[13px] font-bold uppercase">
                {user?.userName?.charAt(0) ?? '?'}
              </span>
            </div>
            <div className="flex-1 min-w-0">
              <span className="block text-[13px] font-semibold text-[#1C2B24] truncate">
                {user?.userName ?? '—'}
              </span>
              <span className="block text-[11px] text-[#5E6F66] truncate">
                {menu?.roleLabel ?? user?.role}
              </span>
            </div>
          </div>
          <button
            onClick={handleLogout}
            className="w-full flex items-center justify-center gap-2 py-2 rounded-lg border border-[#DDE7E1] text-[13px] font-medium text-[#5E6F66] hover:text-[#D32F2F] hover:border-[#F8B4B4] hover:bg-[#FDE8E8] transition-colors duration-150"
          >
            <span className="material-symbols-outlined text-[18px]">logout</span>
            Đăng xuất
          </button>
        </div>
      </aside>

      {/* Mobile Header */}
      <header className="md:hidden fixed top-0 left-0 right-0 z-30 bg-white border-b border-[#DDE7E1] h-14 flex items-center justify-between px-4">
        <div className="flex items-center gap-2">
          <span className="material-symbols-outlined text-[#006a41] text-[22px]">local_hospital</span>
          <span className="text-[15px] font-bold text-[#1C2B24]">Smart Healthcare</span>
        </div>
        <button
          onClick={handleLogout}
          className="flex items-center gap-1 text-[13px] text-[#5E6F66] hover:text-[#D32F2F] transition-colors"
          aria-label="Đăng xuất"
        >
          <span className="material-symbols-outlined text-[18px]">logout</span>
        </button>
      </header>

      {/* Main Content */}
      <main className="flex-1 md:ml-64 mt-14 md:mt-0 p-4 md:p-6">
        {children}
      </main>
    </div>
  );
}

