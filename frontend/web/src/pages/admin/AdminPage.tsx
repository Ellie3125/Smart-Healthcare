import React, { useState } from 'react';
import { SpecialtiesAndRanksTab } from './SpecialtiesAndRanksTab';
import { RoomsTab } from './RoomsTab';
import { StaffAndDoctorsTab } from './StaffAndDoctorsTab';
import { RoomAccountsTab } from './RoomAccountsTab';
import { ShiftAssignmentsTab } from './ShiftAssignmentsTab';

type AdminTabKey = 'specialties_ranks' | 'rooms' | 'staff_doctors' | 'room_accounts' | 'shifts';

export const AdminPage: React.FC = () => {
  const [activeTab, setActiveTab] = useState<AdminTabKey>('specialties_ranks');

  const tabs: { key: AdminTabKey; title: string; icon: string; desc: string }[] = [
    {
      key: 'specialties_ranks',
      title: 'Chuyên khoa & Cấp bậc',
      icon: 'medical_services',
      desc: 'Quản lý mã chuyên khoa và cấp bậc bác sĩ',
    },
    {
      key: 'rooms',
      title: 'Quản lý Phòng',
      icon: 'meeting_room',
      desc: 'Phòng khám lâm sàng và cận lâm sàng',
    },
    {
      key: 'staff_doctors',
      title: 'Nhân sự & Bác sĩ',
      icon: 'badge',
      desc: 'Bác sĩ, điều dưỡng, tiếp tân & hồ sơ chuyên khoa',
    },
    {
      key: 'room_accounts',
      title: 'Tài khoản phòng & Phiên CLS',
      icon: 'devices',
      desc: 'Tài khoản phòng CLS (1 máy 1 phiên duy nhất)',
    },
    {
      key: 'shifts',
      title: 'Phân ca trực',
      icon: 'calendar_month',
      desc: 'Lập lịch trực và trưởng ca hàng ngày',
    },
  ];

  return (
    <div className="space-y-6">
      {/* Top Header Banner */}
      <div className="flex flex-col md:flex-row md:items-center justify-between pb-4 border-b border-[#DDE7E1] gap-3">
        <div>
          <div className="inline-flex items-center gap-1.5 text-xs text-[#5E6F66] font-medium mb-1">
            <span className="material-symbols-outlined text-[16px] text-[#006a41]">admin_panel_settings</span>
            <span>Hệ thống Quản trị Bệnh viện</span>
            <span>/</span>
            <span className="text-[#006a41] font-semibold">Cấu hình Danh mục &amp; Nhân sự (Tuần 2)</span>
          </div>
          <h1 className="font-headline-md text-headline-md font-bold text-[#1C2B24]">
            Trung Tâm Điều Phối &amp; Quản Trị Hệ Thống
          </h1>
          <p className="font-body-md text-xs text-[#5E6F66] mt-0.5">
            Quản lý các thực thể thuộc phân hệ Identity &amp; Patients: Nhân sự, Bác sĩ, Chuyên khoa, Cấp bậc, Phòng và Ca trực.
          </p>
        </div>

        {/* Security & Integrity Chip */}
        <div className="flex items-center gap-2 bg-[#E8F5EE] border border-[#DDE7E1] px-3 py-1.5 rounded-lg text-xs font-semibold text-[#006a41] shrink-0">
          <span className="material-symbols-outlined text-[18px]">lock</span>
          <span>Bảo vệ Cloudflare Access Zero Trust</span>
        </div>
      </div>

      {/* Navigation Tabs */}
      <div className="bg-white rounded-xl border border-[#DDE7E1] p-1.5 flex flex-wrap gap-1 shadow-sm">
        {tabs.map((tab) => {
          const isActive = activeTab === tab.key;
          return (
            <button
              key={tab.key}
              onClick={() => setActiveTab(tab.key)}
              className={`flex-1 min-w-[180px] py-2.5 px-3 rounded-lg text-xs font-semibold flex items-center justify-center gap-2 transition-all ${
                isActive
                  ? 'bg-[#006a41] text-white shadow-sm'
                  : 'text-[#5E6F66] hover:text-[#1C2B24] hover:bg-[#F1F4F2]'
              }`}
            >
              <span className="material-symbols-outlined text-[18px]">{tab.icon}</span>
              <span>{tab.title}</span>
            </button>
          );
        })}
      </div>

      {/* Tab Contents */}
      <div>
        {activeTab === 'specialties_ranks' && <SpecialtiesAndRanksTab />}
        {activeTab === 'rooms' && <RoomsTab />}
        {activeTab === 'staff_doctors' && <StaffAndDoctorsTab />}
        {activeTab === 'room_accounts' && <RoomAccountsTab />}
        {activeTab === 'shifts' && <ShiftAssignmentsTab />}
      </div>
    </div>
  );
};
