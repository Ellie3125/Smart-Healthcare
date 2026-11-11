import React, { useEffect } from 'react';
import { Link } from 'react-router-dom';
import { AlertTriangle, LogIn } from 'lucide-react';
import { Button, Card } from '../../shared/ui';

/**
 * S04(b) — Phiên RoomAccount bị đá/thay thế bởi đăng nhập trên thiết bị khác.
 * Tự động xóa token cục bộ để tránh gửi request lỗi liên hoàn.
 */
export const SessionReplacedPage: React.FC = () => {
  useEffect(() => {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
  }, []);

  return (
    <div className="flex min-h-screen items-center justify-center bg-[#f4f8f6] p-4">
      <div className="w-full max-w-md">
        <Card>
          <div className="flex flex-col items-center gap-3 text-center">
            <div className="rounded-full bg-[#ffdad6] p-4 text-[#ba1a1a]" aria-hidden="true">
              <AlertTriangle className="h-8 w-8" />
            </div>
            <h1 className="text-xl font-semibold text-[#0f1f18]">
              Phiên phòng máy đã bị thay thế
            </h1>
            <p className="text-sm text-[#44544c]">
              Tài khoản phòng này vừa được đăng nhập tại một thiết bị khác (mỗi phòng chỉ duy trì một phiên hoạt động). Phiên hiện tại đã đăng xuất an toàn để bảo vệ dữ liệu bệnh nhân.
            </p>
            <Link to="/login" className="mt-2 w-full">
              <Button variant="primary" className="w-full" leftIcon={<LogIn className="h-4 w-4" />}>
                Đăng nhập lại
              </Button>
            </Link>
          </div>
        </Card>
      </div>
    </div>
  );
};
