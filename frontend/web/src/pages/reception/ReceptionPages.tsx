/** Trang chủ Lễ tân — placeholder Tuần 1 */
export function ReceptionHomePage() {
  return (
    <div>
      <div className="mb-6">
        <h1 className="text-[22px] font-bold text-[#1C2B24]">Trang chủ — Lễ tân</h1>
        <p className="text-[14px] text-[#5E6F66] mt-1">Tiếp nhận bệnh nhân và quản lý lịch hẹn.</p>
      </div>
      <div className="bg-white rounded-xl border border-[#DDE7E1] p-6">
        <div className="flex items-center gap-3 mb-4">
          <span className="material-symbols-outlined text-[#006a41] text-[28px]">assignment_ind</span>
          <h2 className="text-[16px] font-semibold text-[#1C2B24]">Chức năng Lễ tân</h2>
        </div>
        <ul className="space-y-2 text-[14px] text-[#3e4941]">
          <li className="flex items-center gap-2"><span className="material-symbols-outlined text-[16px] text-[#bdcabf]">circle</span>Tiếp nhận bệnh nhân (check-in) — TODO(Tuần 2): kết nối API</li>
          <li className="flex items-center gap-2"><span className="material-symbols-outlined text-[16px] text-[#bdcabf]">circle</span>Danh sách lịch hẹn hôm nay — TODO(Tuần 2): kết nối API</li>
          <li className="flex items-center gap-2"><span className="material-symbols-outlined text-[16px] text-[#bdcabf]">circle</span>Xác minh hồ sơ bệnh nhân — TODO(Tuần 2): kết nối API</li>
        </ul>
        <p className="mt-4 text-[12px] text-[#6e7a71] italic">Dữ liệu sẽ được kết nối từ Tuần 2 trở đi.</p>
      </div>
    </div>
  );
}

export function ReceptionCheckInPage() {
  return (
    <div>
      <h1 className="text-[22px] font-bold text-[#1C2B24] mb-6">Tiếp nhận bệnh nhân</h1>
      <div className="bg-white rounded-xl border border-[#DDE7E1] p-6">
        <p className="text-[14px] text-[#5E6F66]">// TODO(Tuần 2): Giao diện check-in bệnh nhân — kết nối POST /api/scheduling/queue-tickets/check-in</p>
      </div>
    </div>
  );
}

export function ReceptionAppointmentsPage() {
  return (
    <div>
      <h1 className="text-[22px] font-bold text-[#1C2B24] mb-6">Danh sách lịch hẹn</h1>
      <div className="bg-white rounded-xl border border-[#DDE7E1] p-6">
        <p className="text-[14px] text-[#5E6F66]">// TODO(Tuần 2): Danh sách lịch hẹn — kết nối GET /api/scheduling/appointments</p>
      </div>
    </div>
  );
}

