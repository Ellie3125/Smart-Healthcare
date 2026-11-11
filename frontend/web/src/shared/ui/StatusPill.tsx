import React from 'react';
import {
  AlertCircle,
  AlertTriangle,
  CheckCircle2,
  Clock,
  Ban,
  Send,
  FileEdit,
  Stethoscope,
  FlaskConical,
  PhoneCall,
} from 'lucide-react';

export type StatusTone = 'neutral' | 'info' | 'success' | 'warning' | 'danger';

export interface StatusMeta {
  label: string;
  tone: StatusTone;
  icon?: 'clock' | 'check' | 'alert' | 'warn' | 'ban' | 'send' | 'edit' | 'clinic' | 'cls' | 'call';
}

const STATUS_MAP: Record<string, StatusMeta> = {
  // Booking / Appointment
  Hold: { label: 'Đang giữ chỗ', tone: 'warning', icon: 'clock' },
  PendingPayment: { label: 'Chờ thanh toán', tone: 'warning', icon: 'clock' },
  Confirmed: { label: 'Đã xác nhận', tone: 'success', icon: 'check' },
  CheckedIn: { label: 'Đã check-in', tone: 'info', icon: 'check' },
  Completed: { label: 'Hoàn tất', tone: 'success', icon: 'check' },
  Cancelled: { label: 'Đã hủy', tone: 'danger', icon: 'ban' },
  Expired: { label: 'Hết hạn', tone: 'neutral', icon: 'clock' },
  NoShow: { label: 'Vắng mặt', tone: 'danger', icon: 'alert' },

  // Encounter / Verification
  Draft: { label: 'Nháp', tone: 'neutral', icon: 'edit' },
  InProgress: { label: 'Đang thực hiện', tone: 'info', icon: 'clinic' },
  AwaitingCls: { label: 'Chờ cận lâm sàng', tone: 'warning', icon: 'cls' },
  ClsCompleted: { label: 'Đã đủ kết quả CLS', tone: 'info', icon: 'cls' },
  Published: { label: 'Đã phát hành', tone: 'success', icon: 'check' },
  Amended: { label: 'Đã bổ sung', tone: 'info', icon: 'edit' },
  Pending: { label: 'Chờ duyệt', tone: 'warning', icon: 'clock' },
  Verified: { label: 'Đã xác minh', tone: 'success', icon: 'check' },
  Rejected: { label: 'Từ chối', tone: 'danger', icon: 'ban' },

  // Payment / Refund / Reconcile
  Verifying: { label: 'Đang xác minh', tone: 'info', icon: 'clock' },
  Approved: { label: 'Đã duyệt hoàn', tone: 'info', icon: 'check' },
  Paid: { label: 'Đã thanh toán', tone: 'success', icon: 'check' },
  Failed: { label: 'Thất bại', tone: 'danger', icon: 'alert' },
  Refunded: { label: 'Đã hoàn tiền', tone: 'success', icon: 'check' },
  Matched: { label: 'Khớp dữ liệu', tone: 'success', icon: 'check' },
  Mismatch: { label: 'Lệch đối soát', tone: 'danger', icon: 'warn' },
  Unknown: { label: 'Chưa xác định', tone: 'warning', icon: 'warn' },

  // Queue / Notification
  Waiting: { label: 'Đang chờ', tone: 'warning', icon: 'clock' },
  Called: { label: 'Đang gọi', tone: 'info', icon: 'call' },
  Deferred: { label: 'Hoãn gọi', tone: 'neutral', icon: 'clock' },
  InService: { label: 'Đang khám', tone: 'info', icon: 'clinic' },
  Sent: { label: 'Đã gửi', tone: 'info', icon: 'send' },
  Read: { label: 'Đã đọc', tone: 'neutral', icon: 'check' },
  Open: { label: 'Đang mở', tone: 'success', icon: 'check' },
  Closed: { label: 'Đã đóng', tone: 'neutral', icon: 'ban' },
  Active: { label: 'Hoạt động', tone: 'success', icon: 'check' },
  Inactive: { label: 'Ngưng hoạt động', tone: 'neutral', icon: 'ban' },
};

const TONE_CLASSES: Record<StatusTone, string> = {
  neutral: 'bg-[#eef3f0] text-[#44544c] border-[#cddbd3]',
  info: 'bg-[#e8f0fe] text-[#0b57d0] border-[#bdd4fb]',
  success: 'bg-[#d4f7e2] text-[#005231] border-[#8ef8bb]',
  warning: 'bg-[#fef3c7] text-[#9a5b00] border-[#fcd34d]',
  danger: 'bg-[#ffdad6] text-[#93000a] border-[#ffb4ab]',
};

function renderIcon(kind?: StatusMeta['icon']) {
  const cls = 'h-3.5 w-3.5 shrink-0';
  switch (kind) {
    case 'clock':
      return <Clock className={cls} aria-hidden="true" />;
    case 'check':
      return <CheckCircle2 className={cls} aria-hidden="true" />;
    case 'alert':
      return <AlertCircle className={cls} aria-hidden="true" />;
    case 'warn':
      return <AlertTriangle className={cls} aria-hidden="true" />;
    case 'ban':
      return <Ban className={cls} aria-hidden="true" />;
    case 'send':
      return <Send className={cls} aria-hidden="true" />;
    case 'edit':
      return <FileEdit className={cls} aria-hidden="true" />;
    case 'clinic':
      return <Stethoscope className={cls} aria-hidden="true" />;
    case 'cls':
      return <FlaskConical className={cls} aria-hidden="true" />;
    case 'call':
      return <PhoneCall className={cls} aria-hidden="true" />;
    default:
      return null;
  }
}

export interface StatusPillProps {
  status: string;
  labelOverride?: string;
  toneOverride?: StatusTone;
  className?: string;
}

export const StatusPill: React.FC<StatusPillProps> = ({
  status,
  labelOverride,
  toneOverride,
  className = '',
}) => {
  const meta = STATUS_MAP[status] ?? { label: status, tone: 'neutral' as StatusTone };
  const tone = toneOverride ?? meta.tone;
  const label = labelOverride ?? meta.label;
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-medium whitespace-nowrap ${TONE_CLASSES[tone]} ${className}`}
    >
      {renderIcon(meta.icon)}
      <span>{label}</span>
    </span>
  );
};
