import React from "react";

export const ReceptionPage: React.FC = () => {
  // TODO(tuần 2): Nối API /api/scheduling/queue và /api/identity/patients/checkin
  const mockTickets = [
    {
      ticketNumber: "A-001",
      patientName: "Nguyễn Văn Bệnh Nhân",
      serviceTime: "07:00",
      status: "Chờ tiếp đón",
    },
    {
      ticketNumber: "A-002",
      patientName: "Lê Hoàng Minh",
      serviceTime: "07:15",
      status: "Đã check-in",
    },
    {
      ticketNumber: "A-003",
      patientName: "Phạm Thị Mai",
      serviceTime: "07:30",
      status: "Chờ tiếp đón",
    },
  ];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-headline-md text-headline-md font-bold text-[#1C2B24]">
          Bàn Tiếp Đón &amp; Check-in Bệnh Nhân
        </h1>
        <p className="font-body-md text-[#5E6F66] mt-1">
          Đối chiếu thẻ CCCD thực tế, xác nhận danh tính và gọi số vào quầy tiếp
          nhận.
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div className="bg-white p-5 rounded-xl border border-[#DDE7E1] shadow-sm">
          <span className="text-[12px] font-semibold text-[#5E6F66] uppercase">
            Số vé trong ngày
          </span>
          <div className="text-3xl font-bold text-[#006a41] mt-2">24 / 30</div>
          <span className="text-xs text-[#5E6F66] mt-1 block">
            Hạn mức phiên sáng (POL-01)
          </span>
        </div>
        <div className="bg-white p-5 rounded-xl border border-[#DDE7E1] shadow-sm">
          <span className="text-[12px] font-semibold text-[#5E6F66] uppercase">
            Đã check-in
          </span>
          <div className="text-3xl font-bold text-[#1C2B24] mt-2">18</div>
          <span className="text-xs text-[#5E6F66] mt-1 block">
            Sẵn sàng phân luồng đo sinh hiệu
          </span>
        </div>
        <div className="bg-white p-5 rounded-xl border border-[#DDE7E1] shadow-sm">
          <span className="text-[12px] font-semibold text-[#5E6F66] uppercase">
            Thời gian khóa đăng ký
          </span>
          <div className="text-3xl font-bold text-[#D32F2F] mt-2">09:30</div>
          <span className="text-xs text-[#5E6F66] mt-1 block">
            BookingCutoff ngày D (POL-02)
          </span>
        </div>
      </div>

      <div className="bg-white rounded-xl border border-[#DDE7E1] overflow-hidden shadow-sm">
        <div className="p-4 border-b border-[#DDE7E1] flex items-center justify-between">
          <h2 className="font-semibold text-[#1C2B24]">
            Danh sách bệnh nhân đăng ký hôm nay
          </h2>
          <span className="text-xs text-[#5E6F66]">
            Dữ liệu mẫu tuần 1 // TODO(tuần 2): Nối API
          </span>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="bg-[#F7FAF8] text-[#5E6F66] border-b border-[#DDE7E1]">
              <tr>
                <th className="py-3 px-4 font-semibold">Số vé</th>
                <th className="py-3 px-4 font-semibold">Họ và tên</th>
                <th className="py-3 px-4 font-semibold">Giờ hẹn</th>
                <th className="py-3 px-4 font-semibold">Trạng thái</th>
                <th className="py-3 px-4 font-semibold text-right">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#DDE7E1]">
              {mockTickets.map((t) => (
                <tr key={t.ticketNumber} className="hover:bg-[#F7FAF8]/60">
                  <td className="py-3 px-4 font-bold text-[#006a41]">
                    {t.ticketNumber}
                  </td>
                  <td className="py-3 px-4 font-medium text-[#1C2B24]">
                    {t.patientName}
                  </td>
                  <td className="py-3 px-4 text-[#5E6F66]">{t.serviceTime}</td>
                  <td className="py-3 px-4">
                    <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold bg-[#E8F5EE] text-[#006a41]">
                      {t.status}
                    </span>
                  </td>
                  <td className="py-3 px-4 text-right">
                    <button className="px-3 py-1 bg-[#006a41] text-white rounded text-xs font-medium hover:bg-[#005231]">
                      Check-in CCCD
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
