import React from "react";

export const NursePage: React.FC = () => {
  // TODO(tuần 2): Nối API /api/clinical/vitalsigns
  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-headline-md text-headline-md font-bold text-[#1C2B24]">
          Bàn Đo Sinh Hiệu &amp; Phân Luồng Khám
        </h1>
        <p className="font-body-md text-[#5E6F66] mt-1">
          Cập nhật huyết áp, mạch, nhiệt độ, SpO2 trước khi bệnh nhân vào phòng
          khám bác sĩ.
        </p>
      </div>

      <div className="bg-white rounded-xl border border-[#DDE7E1] p-6 shadow-sm">
        <div className="text-sm text-[#5E6F66] mb-4">
          // TODO(tuần 2): Nối API VitalSigns của module Clinical
        </div>
        <div className="p-8 text-center text-[#5E6F66] border-2 border-dashed border-[#DDE7E1] rounded-lg">
          <span className="material-symbols-outlined text-[48px] text-[#006a41] mb-2">
            vital_signs
          </span>
          <h2 className="text-lg font-semibold text-[#1C2B24]">
            Sẵn sàng tiếp nhận bệnh nhân kế tiếp
          </h2>
          <p className="text-sm mt-1">
            Giao diện tĩnh tuần 1 phục vụ kiểm thử phân quyền và khung ứng dụng.
          </p>
        </div>
      </div>
    </div>
  );
};
