import React from 'react';
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';

export const PatientLayout: React.FC = () => {
  const { user, logout } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const navLinks = [
    { to: '/profiles', label: 'Hồ sơ y tế của tôi', icon: 'badge' },
    { to: '/profiles/add-relative', label: 'Thêm người thân', icon: 'group_add' },
  ];

  return (
    <div className="min-h-screen bg-[#F7FAF8] flex flex-col font-body-md text-[#1C2B24]">
      {/* Top Header */}
      <header className="h-16 bg-white border-b border-[#DDE7E1] px-4 lg:px-8 flex items-center justify-between sticky top-0 z-20 shadow-sm">
        <div className="flex items-center gap-3">
          <Link to="/profiles" className="flex items-center gap-2.5">
            <div className="w-10 h-10 rounded-lg bg-[#E8F5EE] border border-[#DDE7E1] flex items-center justify-center text-[#006a41]">
              <span className="material-symbols-outlined text-[24px]">local_hospital</span>
            </div>
            <div>
              <span className="font-headline-sm text-headline-sm font-bold text-[#1C2B24] tracking-tight">
                Smart Healthcare
              </span>
              <span className="hidden sm:inline-block ml-2 text-[12px] text-[#006a41] font-medium bg-[#E8F5EE] px-2 py-0.5 rounded-full border border-[#DDE7E1]">
                Cổng Bệnh Nhân
              </span>
            </div>
          </Link>
        </div>

        <nav className="hidden md:flex items-center gap-2">
          {navLinks.map((link) => {
            const isActive = location.pathname === link.to;
            return (
              <Link
                key={link.to}
                to={link.to}
                className={`flex items-center gap-1.5 px-3.5 py-2 rounded-lg text-sm font-medium transition-colors ${
                  isActive
                    ? 'bg-[#E8F5EE] text-[#006a41] font-semibold'
                    : 'text-[#5E6F66] hover:bg-[#F1F4F2] hover:text-[#1C2B24]'
                }`}
              >
                <span className="material-symbols-outlined text-[18px]">{link.icon}</span>
                <span>{link.label}</span>
              </Link>
            );
          })}
        </nav>

        <div className="flex items-center gap-3">
          <div className="hidden sm:flex flex-col text-right">
            <span className="text-sm font-semibold text-[#1C2B24]">{user?.userName}</span>
            <span className="text-[11px] text-[#5E6F66]">Bệnh nhân / Người giám hộ</span>
          </div>
          <button
            onClick={handleLogout}
            className="h-9 px-3 rounded-lg border border-[#DDE7E1] text-[#D32F2F] hover:bg-[#FDE8E8] transition-colors flex items-center gap-1.5 text-sm font-medium focus:outline-none focus:ring-2 focus:ring-[#D32F2F]/20"
            title="Đăng xuất"
          >
            <span className="material-symbols-outlined text-[18px]">logout</span>
            <span className="hidden sm:inline">Đăng xuất</span>
          </button>
        </div>
      </header>

      {/* Main Content */}
      <main className="flex-1 w-full max-w-6xl mx-auto px-4 sm:px-6 py-6 sm:py-8">
        <Outlet />
      </main>

      {/* Mobile Bottom Navigation */}
      <div className="md:hidden sticky bottom-0 bg-white border-t border-[#DDE7E1] px-4 py-2 flex justify-around items-center z-20">
        {navLinks.map((link) => {
          const isActive = location.pathname === link.to;
          return (
            <Link
              key={link.to}
              to={link.to}
              className={`flex flex-col items-center gap-1 text-[11px] font-medium ${
                isActive ? 'text-[#006a41] font-semibold' : 'text-[#5E6F66]'
              }`}
            >
              <span className="material-symbols-outlined text-[20px]">{link.icon}</span>
              <span>{link.label}</span>
            </Link>
          );
        })}
      </div>

      {/* Footer */}
      <footer className="bg-white border-t border-[#DDE7E1] py-6 px-4 text-center text-xs text-[#5E6F66]">
        <div className="max-w-6xl mx-auto flex flex-col sm:flex-row justify-between items-center gap-2">
          <p>© 2026 Smart Healthcare System. Phân hệ Identity & Patients.</p>
          <p>Đối chiếu căn cước & thẻ gốc trực tiếp tại quầy tiếp tân cơ sở y tế (FR01).</p>
        </div>
      </footer>
    </div>
  );
};
