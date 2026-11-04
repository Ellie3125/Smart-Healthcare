/** Trang chủ Quản trị (Admin) — placeholder Tuần 1 */
export function AdminHomePage() {
  return (
    <div>
      <div className="mb-6">
        <h1 className="text-[22px] font-bold text-[#1C2B24]">
          Trang chủ — Quản trị
        </h1>
        <p className="text-[14px] text-[#5E6F66] mt-1">
          Quản lý hệ thống. Lưu ý: quyền Admin kỹ thuật không mặc nhiên đọc/sửa
          nội dung chuyên môn (SEC-01).
        </p>
      </div>
      <div className="bg-white rounded-xl border border-[#DDE7E1] p-6">
        <div className="flex items-center gap-3 mb-4">
          <span className="material-symbols-outlined text-[#006a41] text-[28px]">
            admin_panel_settings
          </span>
          <h2 className="text-[16px] font-semibold text-[#1C2B24]">
            Chức năng Quản trị
          </h2>
        </div>
        <ul className="space-y-2 text-[14px] text-[#3e4941]">
          <li className="flex items-center gap-2">
            <span className="material-symbols-outlined text-[16px] text-[#bdcabf]">
              circle
            </span>
            Quản lý người dùng (tạo/kích hoạt/vô hiệu hóa) — TODO(Tuần 2): kết
            nối API
          </li>
          <li className="flex items-center gap-2">
            <span className="material-symbols-outlined text-[16px] text-[#bdcabf]">
              circle
            </span>
            Danh mục chuyên khoa & cấp bậc — TODO(Tuần 2): kết nối API
          </li>
          <li className="flex items-center gap-2">
            <span className="material-symbols-outlined text-[16px] text-[#bdcabf]">
              circle
            </span>
            Quản lý phòng khám — TODO(Tuần 2): kết nối API
          </li>
        </ul>
        <div className="mt-4 p-3 rounded-lg bg-[#FDE8E8] border border-[#F8B4B4] text-[13px] text-[#D32F2F]">
          <strong>SEC-01:</strong> Admin kỹ thuật không được phép đọc hồ sơ bệnh
          án hay kết quả CLS. Mọi phân quyền thực tế được kiểm tra tại Backend.
        </div>
      </div>
    </div>
  );
}

export function AdminUsersPage() {
  return (
    <div>
      <h1 className="text-[22px] font-bold text-[#1C2B24] mb-6">
        Quản lý người dùng
      </h1>
      <div className="bg-white rounded-xl border border-[#DDE7E1] p-6">
        <p className="text-[14px] text-[#5E6F66]">
          // TODO(Tuần 2): Danh sách người dùng — kết nối GET
          /api/identity/users
        </p>
      </div>
    </div>
  );
}

export function AdminCatalogPage() {
  return (
    <div>
      <h1 className="text-[22px] font-bold text-[#1C2B24] mb-6">Danh mục</h1>
      <div className="bg-white rounded-xl border border-[#DDE7E1] p-6">
        <p className="text-[14px] text-[#5E6F66]"></p>
      </div>
    </div>
  );
}
