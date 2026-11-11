import React from 'react';
import { Link } from 'react-router-dom';
import { AlertCircle, Clock, ShieldOff, ServerCrash, WifiOff } from 'lucide-react';
import { Button, Card } from '../../shared/ui';

export type StatusScreenKind =
  | '401'
  | '403'
  | '404'
  | 'pending-verification'
  | 'offline'
  | '500'
  | 'maintenance';

const STATUS_META: Record<
  StatusScreenKind,
  { title: string; message: string; ctaLabel: string; ctaTo: string }
> = {
  '401': {
    title: 'Phiên đăng nhập đã hết hạn',
    message: 'Vui lòng đăng nhập lại để tiếp tục quản lý hồ sơ và lịch khám của bạn.',
    ctaLabel: 'Đăng nhập lại',
    ctaTo: '/login',
  },
  '403': {
    title: 'Không có quyền truy cập',
    message: 'Bạn không thể truy cập hồ sơ hoặc thông tin không thuộc quyền quản lý của tài khoản này.',
    ctaLabel: 'Về danh sách hồ sơ',
    ctaTo: '/profiles',
  },
  '404': {
    title: 'Không tìm thấy trang yêu cầu',
    message: 'Đường dẫn có thể đã thay đổi hoặc thông tin không còn tồn tại.',
    ctaLabel: 'Về trang đặt lịch',
    ctaTo: '/booking',
  },
  'pending-verification': {
    title: 'Hồ sơ đang chờ xác minh định danh',
    message:
      'Để bảo vệ dữ liệu y tế, bạn cần xuất trình CCCD hoặc giấy tờ tùy thân tại quầy tiếp đón khi đến khám để mở khóa lịch sử hồ sơ và đơn thuốc.',
    ctaLabel: 'Quản lý hồ sơ',
    ctaTo: '/profiles',
  },
  offline: {
    title: 'Mất kết nối mạng',
    message: 'Thiết bị của bạn hiện đang ngoại tuyến. Vui lòng kiểm tra Wi-Fi hoặc dữ liệu di động.',
    ctaLabel: 'Thử tải lại',
    ctaTo: '/today-journey',
  },
  '500': {
    title: 'Hệ thống đang gặp sự cố tạm thời',
    message: 'Rất xin lỗi vì sự bất tiện. Vui lòng thử lại sau ít phút.',
    ctaLabel: 'Về trang chính',
    ctaTo: '/profiles',
  },
  maintenance: {
    title: 'Hệ thống đang bảo trì định kỳ',
    message: 'Chúng tôi đang nâng cấp chất lượng dịch vụ và sẽ hoạt động trở lại sớm nhất.',
    ctaLabel: 'Xem màn gọi số',
    ctaTo: '/display',
  },
};

export const StatusScreenPage: React.FC<{ kind: StatusScreenKind }> = ({ kind }) => {
  const meta = STATUS_META[kind];
  const Icon =
    kind === 'pending-verification'
      ? Clock
      : kind === '403' || kind === '401'
        ? ShieldOff
        : kind === 'offline'
          ? WifiOff
          : kind === '500' || kind === 'maintenance'
            ? ServerCrash
            : AlertCircle;

  return (
    <div className="mx-auto max-w-lg py-10">
      <Card>
        <div className="flex flex-col items-center gap-3 text-center">
          <div className="rounded-full bg-[#e5f8ed] p-4 text-[#006a41]" aria-hidden="true">
            <Icon className="h-8 w-8" />
          </div>
          <h1 className="text-xl font-semibold text-[#0f1f18]">{meta.title}</h1>
          <p className="text-sm text-[#3e4941]">{meta.message}</p>
          <Link to={meta.ctaTo} className="mt-2">
            <Button variant="primary">{meta.ctaLabel}</Button>
          </Link>
        </div>
      </Card>
    </div>
  );
};
