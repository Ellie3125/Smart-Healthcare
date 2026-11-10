import React from 'react';

export const AdminPage: React.FC = () => {
  // TODO(tuần 2): Nối API cấu hình hệ thống và audit logs
  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-headline-md text-headline-md font-bold text-[#1C2B24]">
          Bảng Điều Khiển Quản Trị Hệ Thống
        </h1>
        <p className="font-body-md text-[#5E6F66] mt-1">
          Giám sát trạng thái dịch vụ, quản lý tài khoản nhân viên, phân ca trực phòng và cấu hình kỹ thuật.
        </p>
      </div>

      <div className="bg-white rounded-xl border border-[#DDE7E1] p-6 shadow-sm">
        <div className="text-sm text-[#5E6F66] mb-4">// TODO(tuần 2): Nối API quản trị tài khoản và ca trực</div>
        <div className="p-8 text-center text-[#5E6F66] border-2 border-dashed border-[#DDE7E1] rounded-lg">
          <span className="material-symbols-outlined text-[48px] text-[#006a41] mb-2">admin_panel_settings</span>
          <h2 className="text-lg font-semibold text-[#1C2B24]">Hệ Thống Đang Vận Hành Bình Thường</h2>
          <p className="text-sm mt-1">Admin kỹ thuật không mặc nhiên có quyền đọc/sửa dữ liệu bệnh án chuyên môn (SEC-01).</p>
        </div>
      </div>
    </div>
  );
};

