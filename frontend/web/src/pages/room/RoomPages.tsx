/** Trang chủ Phòng CLS (RoomAccount) — placeholder Tuần 1 */
export function RoomHomePage() {
  return (
    <div>
      <div className="mb-6">
        <h1 className="text-[22px] font-bold text-[#1C2B24]">
          Trang chủ — Phòng CLS
        </h1>
        <p className="text-[14px] text-[#5E6F66] mt-1">
          Thực hiện dịch vụ và tải kết quả cận lâm sàng.
        </p>
      </div>
      <div className="bg-white rounded-xl border border-[#DDE7E1] p-6">
        <div className="flex items-center gap-3 mb-4">
          <span className="material-symbols-outlined text-[#006a41] text-[28px]">
            biotech
          </span>
          <h2 className="text-[16px] font-semibold text-[#1C2B24]">
            Chức năng Phòng CLS
          </h2>
        </div>
        <ul className="space-y-2 text-[14px] text-[#3e4941]">
          <li className="flex items-center gap-2">
            <span className="material-symbols-outlined text-[16px] text-[#bdcabf]">
              circle
            </span>
            Vé dịch vụ của phòng — TODO(Tuần 3): kết nối API
          </li>
          <li className="flex items-center gap-2">
            <span className="material-symbols-outlined text-[16px] text-[#bdcabf]">
              circle
            </span>
            Tải kết quả CLS (file) — TODO(Tuần 3): kết nối API Documents
          </li>
        </ul>
        <div className="mt-4 p-3 rounded-lg bg-[#FDE8E8] border border-[#F8B4B4] text-[13px] text-[#D32F2F]">
          <strong>Lưu ý:</strong> Tài khoản phòng CLS chỉ được tải kết quả theo
          vé của phòng, không xem lại sau khi phát hành (MED-06).
        </div>
      </div>
    </div>
  );
}

export function RoomTicketsPage() {
  return (
    <div>
      <h1 className="text-[22px] font-bold text-[#1C2B24] mb-6">Vé dịch vụ</h1>
      <div className="bg-white rounded-xl border border-[#DDE7E1] p-6">
        <p className="text-[14px] text-[#5E6F66]">
          // TODO(Tuần 3): Danh sách vé dịch vụ — kết nối GET
          /api/scheduling/service-tickets/my-room
        </p>
      </div>
    </div>
  );
}

export function RoomUploadPage() {
  return (
    <div>
      <h1 className="text-[22px] font-bold text-[#1C2B24] mb-6">Tải kết quả</h1>
      <div className="bg-white rounded-xl border border-[#DDE7E1] p-6">
        <p className="text-[14px] text-[#5E6F66]">
          // TODO(Tuần 3): Upload kết quả CLS — kết nối POST
          /api/documents/result-files
        </p>
      </div>
    </div>
  );
}
