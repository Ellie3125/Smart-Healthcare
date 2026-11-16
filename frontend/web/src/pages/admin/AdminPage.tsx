import React, { useState } from 'react';
import { StaffTab } from './StaffTab';
import { DoctorsTab } from './DoctorsTab';
import { SpecialtiesTab } from './SpecialtiesTab';
import { DoctorRanksTab } from './DoctorRanksTab';
import { RoomsTab } from './RoomsTab';
import { ShiftsTab } from './ShiftsTab';

type AdminTab = 'staff' | 'doctors' | 'specialties' | 'doctor-ranks' | 'rooms' | 'shifts';

export const AdminPage: React.FC = () => {
  const [activeTab, setActiveTab] = useState<AdminTab>('staff');

  const tabs: { id: AdminTab; label: string; icon: string }[] = [
    { id: 'staff', label: 'Nhân Sự & AppUser', icon: 'badge' },
    { id: 'doctors', label: 'Hồ Sơ Bác Sĩ', icon: 'stethoscope' },
    { id: 'specialties', label: 'Chuyên Khoa', icon: 'medical_services' },
    { id: 'doctor-ranks', label: 'Cấp Bậc Bác Sĩ', icon: 'military_tech' },
    { id: 'rooms', label: 'Phòng & Tài Khoản Phòng', icon: 'meeting_room' },
    { id: 'shifts', label: 'Lịch & Phân Ca Trực', icon: 'calendar_month' },
  ];

  return (
    <div className="space-y-6">
      {/* Top Banner & Header */}
      <div className="bg-surface-container-lowest rounded-2xl p-6 shadow-[0_1px_10px_rgba(15,31,24,0.04)] flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
        <div>
          <div className="flex items-center gap-2.5 mb-1">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-surface-container-low text-primary">
              <span className="material-symbols-outlined text-[22px]">admin_panel_settings</span>
            </div>
            <h1 className="font-headline-md text-2xl font-extrabold tracking-tight text-on-surface">
              Cổng Quản Trị Hệ Thống Y Tế
            </h1>
          </div>
          <p className="font-body-md text-on-surface-variant text-sm">
            Quản trị nhân sự, bác sĩ chuyên khoa, cơ sở phòng ban, tài khoản thiết bị phòng và phân công ca trực.
          </p>
        </div>
        <div className="px-4 py-2 rounded-full bg-surface-container-low text-xs text-primary font-semibold flex items-center gap-2 shrink-0">
          <span className="w-2 h-2 rounded-full bg-primary animate-pulse"></span>
          <span>Cấu hình Danh mục &amp; Nhân sự</span>
        </div>
      </div>

      {/* Stitch Pill Segmented Tab Navigation */}
      <div className="flex flex-wrap gap-1.5 rounded-2xl bg-surface-container-low p-1.5 overflow-x-auto">
        {tabs.map((t) => {
          const isActive = activeTab === t.id;
          return (
            <button
              key={t.id}
              onClick={() => setActiveTab(t.id)}
              className={`flex items-center gap-2 px-4 py-2 rounded-xl text-xs sm:text-sm font-semibold transition-all shrink-0 cursor-pointer ${
                isActive
                  ? 'bg-surface-container-lowest text-primary shadow-sm'
                  : 'text-on-surface-variant hover:text-on-surface hover:bg-surface-container-lowest/50'
              }`}
            >
              <span className="material-symbols-outlined text-[18px]">{t.icon}</span>
              <span>{t.label}</span>
            </button>
          );
        })}
      </div>

      {/* Tab Content Panel */}
      <div className="bg-transparent">
        {activeTab === 'staff' && <StaffTab />}
        {activeTab === 'doctors' && <DoctorsTab />}
        {activeTab === 'specialties' && <SpecialtiesTab />}
        {activeTab === 'doctor-ranks' && <DoctorRanksTab />}
        {activeTab === 'rooms' && <RoomsTab />}
        {activeTab === 'shifts' && <ShiftsTab />}
      </div>
    </div>
  );
};
