/** Trang chủ Điều dưỡng — placeholder Tuần 1 */
export function NurseHomePage() {
  return (
    <div>
      <div className="mb-6">
        <h1 className="text-[22px] font-bold text-[#1C2B24]">Trang chủ — Điều dưỡng</h1>
        <p className="text-[14px] text-[#5E6F66] mt-1">Ghi nhận sinh hiệu và hỗ trợ hàng chờ.</p>
      </div>
      <div className="bg-white rounded-xl border border-[#DDE7E1] p-6">
        <div className="flex items-center gap-3 mb-4">
          <span className="material-symbols-outlined text-[#006a41] text-[28px]">vaccines</span>
          <h2 className="text-[16px] font-semibold text-[#1C2B24]">Chức năng Điều dưỡng</h2>
        </div>
        <ul className="space-y-2 text-[14px] text-[#3e4941]">
          <li className="flex items-center gap-2"><span className="material-symbols-outlined text-[16px] text-[#bdcabf]">circle</span>Ghi nhận sinh hiệu — TODO(Tuần 2): kết nối API</li>
          <li className="flex items-center gap-2"><span className="material-symbols-outlined text-[16px] text-[#bdcabf]">circle</span>Quản lý hàng chờ — TODO(Tuần 2): kết nối API</li>
        </ul>
        <p className="mt-4 text-[12px] text-[#6e7a71] italic">Dữ liệu sẽ được kết nối từ Tuần 2 trở đi.</p>
      </div>
    </div>
  );
}

export function NurseVitalsPage() {
  return (
    <div>
      <h1 className="text-[22px] font-bold text-[#1C2B24] mb-6">Sinh hiệu bệnh nhân</h1>
      <div className="bg-white rounded-xl border border-[#DDE7E1] p-6">
        <p className="text-[14px] text-[#5E6F66]">// TODO(Tuần 2): Ghi nhận sinh hiệu — kết nối POST /api/clinical/vital-signs</p>
      </div>
    </div>
  );
}

export function NurseQueuePage() {
  return (
    <div>
      <h1 className="text-[22px] font-bold text-[#1C2B24] mb-6">Hàng chờ</h1>
      <div className="bg-white rounded-xl border border-[#DDE7E1] p-6">
        <p className="text-[14px] text-[#5E6F66]">// TODO(Tuần 2): Hàng chờ bệnh nhân — kết nối GET /api/scheduling/queue</p>
      </div>
    </div>
  );
}

