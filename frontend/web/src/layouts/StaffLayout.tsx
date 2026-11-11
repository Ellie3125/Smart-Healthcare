import React from 'react';
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { ROLE_MENUS } from './menus';

export const StaffLayout: React.FC = () => {
  const { user, logout } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();

  const menus = user?.role ? ROLE_MENUS[user.role] || [] : [];

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const getRoleDisplayName = (role: string) => {
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
        return role;
    }
  };

  return (
    <div className="min-h-screen bg-[#F7FAF8] flex flex-col font-body-md text-[#1C2B24]">
      {/* Top Header */}
      <header className="h-16 bg-white border-b border-[#DDE7E1] px-4 lg:px-8 flex items-center justify-between sticky top-0 z-20">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-lg bg-[#E8F5EE] border border-[#DDE7E1] flex items-center justify-center">
            <span className="material-symbols-outlined text-[#006a41] text-[24px]">local_hospital</span>
          </div>
          <div>
            <span className="font-headline-sm text-headline-sm font-bold text-[#1C2B24] tracking-tight">
              Smart Healthcare
            </span>
            <span className="hidden sm:inline-block ml-2 text-[12px] text-[#5E6F66] border-l border-[#DDE7E1] pl-2">
              Kênh Nhân viên Y tế
            </span>
          </div>
        </div>

        <div className="flex items-center gap-4">
          <div className="hidden sm:flex flex-col text-right">
            <span className="font-label-md font-semibold text-[#1C2B24]">{user?.userName}</span>
            <span className="font-label-sm text-[12px] text-[#006a41] font-medium">
              {getRoleDisplayName(user?.role || '')}
            </span>
          </div>
          <button
            onClick={handleLogout}
            className="h-9 px-3 rounded-lg border border-[#DDE7E1] text-[#D32F2F] hover:bg-[#FDE8E8] transition-colors flex items-center gap-1.5 text-sm font-medium focus:outline-none focus:ring-2 focus:ring-[#D32F2F]/20"
            title="Đăng xuất khỏi hệ thống"
          >
            <span className="material-symbols-outlined text-[18px]">logout</span>
            <span className="hidden sm:inline">Đăng xuất</span>
          </button>
        </div>
      </header>

      {/* Main Container */}
      <div className="flex-1 flex overflow-hidden">
        {/* Sidebar */}
        <aside className="w-64 bg-white border-r border-[#DDE7E1] p-4 flex flex-col justify-between shrink-0">
          <nav className="space-y-1">
            <div className="px-3 py-2 text-[11px] font-semibold text-[#5E6F66] uppercase tracking-wider">
              Chức năng ({getRoleDisplayName(user?.role || '')})
            </div>
            {menus.map((item) => {
              const isActive = location.pathname === item.path;
              return (
                <Link
                  key={item.path}
                  to={item.path}
                  className={`flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors ${
                    isActive
                      ? 'bg-[#E8F5EE] text-[#006a41] font-semibold'
                      : 'text-[#5E6F66] hover:bg-[#F1F4F2] hover:text-[#1C2B24]'
                  }`}
                >
                  <span className="material-symbols-outlined text-[20px]">{item.icon}</span>
                  <span>{item.title}</span>
                </Link>
              );
            })}
          </nav>

          <div className="p-3 bg-[#E8F5EE] rounded-lg border border-[#DDE7E1]/80 text-[12px] text-[#5E6F66]">
            <div className="font-semibold text-[#006a41] mb-1">Mạng Nội Bộ Bệnh Viện</div>
            <p>Hệ thống chỉ truy cập được từ mạng LAN / Wi-Fi phòng khám (NET-06).</p>
          </div>
        </aside>

        {/* Page Content */}
        <main className="flex-1 p-6 lg:p-8 overflow-y-auto">
          <Outlet />
        </main>
      </div>
    </div>
  );
};

