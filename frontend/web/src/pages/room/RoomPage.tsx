import React from "react";

export const RoomPage: React.FC = () => {
  // TODO(tuần 2): Nối API /api/documents/upload và /api/clinical/service-orders/complete
  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-headline-md text-headline-md font-bold text-[#1C2B24]">
          Phòng Kỹ Thuật Cận Lâm Sàng (X-Quang)
        </h1>
        <p className="font-body-md text-[#5E6F66] mt-1">
          Tài khoản phòng CLS thực hiện dịch vụ theo vé phòng, tải tệp kết quả
          (UploadResultFileAsync) và cập nhật tiến độ.
        </p>
      </div>

      <div className="bg-white rounded-xl border border-[#DDE7E1] p-6 shadow-sm">
        <div className="text-sm text-[#5E6F66] mb-4">
          // TODO(tuần 2): Nối API Upload tệp của module DocumentsReporting
        </div>
        <div className="p-8 text-center text-[#5E6F66] border-2 border-dashed border-[#DDE7E1] rounded-lg">
          <span className="material-symbols-outlined text-[48px] text-[#006a41] mb-2">
            radiology
          </span>
          <h2 className="text-lg font-semibold text-[#1C2B24]">
            Phòng Chụp X-Quang Kỹ Thuật Số (XQ01 / XQ02)
          </h2>
          <p className="text-sm mt-1">
            Chỉ được tải lên tệp kết quả theo vé của phòng; không được xem lại
            sau khi phát hành (MED-06).
          </p>
        </div>
      </div>
    </div>
  );
};
