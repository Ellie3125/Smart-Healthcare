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
      <div className="bg-white rounded-2xl border border-[#DDE7E1] p-6 shadow-sm flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <span className="material-symbols-outlined text-[#006a41] text-[24px]">admin_panel_settings</span>
            <h1 className="font-headline-md text-headline-md font-bold text-[#1C2B24]">
              Cổng Quản Trị Hệ Thống Y Tế
            </h1>
          </div>
          <p className="font-body-md text-[#5E6F66] text-xs sm:text-sm">
            Quản trị nhân sự, bác sĩ chuyên khoa, cơ sở phòng ban, tài khoản thiết bị phòng và phân công ca trực.
          </p>
        </div>
        <div className="px-3.5 py-1.5 rounded-lg bg-[#E8F5EE] border border-[#DDE7E1] text-xs text-[#006a41] font-semibold flex items-center gap-2">
          <span className="w-2 h-2 rounded-full bg-[#10B981] animate-pulse"></span>
          <span>Hệ thống Module Identity & Patients (Schema identity)</span>
        </div>
      </div>

      {/* Tab Navigation */}
      <div className="border-b border-[#DDE7E1] flex gap-2 overflow-x-auto pb-px">
        {tabs.map((t) => {
          const isActive = activeTab === t.id;
          return (
            <button
              key={t.id}
              onClick={() => setActiveTab(t.id)}
              className={`flex items-center gap-2 px-4 py-2.5 rounded-t-xl text-xs sm:text-sm font-semibold transition-colors shrink-0 cursor-pointer ${
                isActive
                  ? 'bg-white text-[#006a41] border-t-2 border-l border-r border-[#006a41] border-l-[#DDE7E1] border-r-[#DDE7E1] shadow-xs'
                  : 'text-[#5E6F66] hover:text-[#1C2B24] hover:bg-[#E8F5EE]/50'
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
