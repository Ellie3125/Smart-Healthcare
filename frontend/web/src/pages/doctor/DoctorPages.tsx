/** Trang chủ Bác sĩ — placeholder Tuần 1 */
export function DoctorHomePage() {
  return (
    <div>
      <div className="mb-6">
        <h1 className="text-[22px] font-bold text-[#1C2B24]">Trang chủ — Bác sĩ</h1>
        <p className="text-[14px] text-[#5E6F66] mt-1">Hàng chờ khám và hồ sơ bệnh án.</p>
      </div>
      <div className="bg-white rounded-xl border border-[#DDE7E1] p-6">
        <div className="flex items-center gap-3 mb-4">
          <span className="material-symbols-outlined text-[#006a41] text-[28px]">stethoscope</span>
          <h2 className="text-[16px] font-semibold text-[#1C2B24]">Chức năng Bác sĩ</h2>
        </div>
        <ul className="space-y-2 text-[14px] text-[#3e4941]">
          <li className="flex items-center gap-2"><span className="material-symbols-outlined text-[16px] text-[#bdcabf]">circle</span>Hàng chờ khám hôm nay — TODO(Tuần 2): kết nối API</li>
          <li className="flex items-center gap-2"><span className="material-symbols-outlined text-[16px] text-[#bdcabf]">circle</span>Lập hồ sơ khám (Encounter) — TODO(Tuần 3): kết nối API Clinical</li>
          <li className="flex items-center gap-2"><span className="material-symbols-outlined text-[16px] text-[#bdcabf]">circle</span>Chỉ định CLS — TODO(Tuần 3): kết nối API Clinical</li>
        </ul>
        <p className="mt-4 text-[12px] text-[#6e7a71] italic">Dữ liệu sẽ được kết nối từ Tuần 2–3 trở đi.</p>
      </div>
    </div>
  );
}

export function DoctorQueuePage() {
  return (
    <div>
      <h1 className="text-[22px] font-bold text-[#1C2B24] mb-6">Hàng chờ khám</h1>
      <div className="bg-white rounded-xl border border-[#DDE7E1] p-6">
        <p className="text-[14px] text-[#5E6F66]">// TODO(Tuần 2): Hàng chờ khám — kết nối GET /api/scheduling/queue/my-queue</p>
      </div>
    </div>
  );
}

export function DoctorRecordsPage() {
  return (
    <div>
      <h1 className="text-[22px] font-bold text-[#1C2B24] mb-6">Lịch sử bệnh án</h1>
      <div className="bg-white rounded-xl border border-[#DDE7E1] p-6">
        <p className="text-[14px] text-[#5E6F66]">// TODO(Tuần 3): Lịch sử bệnh án — kết nối GET /api/clinical/encounters</p>
      </div>
    </div>
  );
}

