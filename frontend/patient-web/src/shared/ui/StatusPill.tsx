import React from 'react';
import {
  AlertCircle,
  AlertTriangle,
  Ban,
  CheckCircle2,
  Clock,
  FileEdit,
  FlaskConical,
  Send,
  Stethoscope,
} from 'lucide-react';

export type StatusTone = 'neutral' | 'info' | 'success' | 'warning' | 'danger';

const STATUS_MAP: Record<string, { label: string; tone: StatusTone; icon?: string }> = {
  Hold: { label: 'Đang giữ chỗ', tone: 'warning', icon: 'clock' },
  PendingPayment: { label: 'Chờ thanh toán', tone: 'warning', icon: 'clock' },
  Confirmed: { label: 'Đã xác nhận', tone: 'success', icon: 'check' },
  CheckedIn: { label: 'Đã check-in', tone: 'info', icon: 'check' },
  Completed: { label: 'Hoàn tất', tone: 'success', icon: 'check' },
  Cancelled: { label: 'Đã hủy', tone: 'danger', icon: 'ban' },
  Expired: { label: 'Hết hạn', tone: 'neutral', icon: 'clock' },
  NoShow: { label: 'Vắng mặt', tone: 'danger', icon: 'alert' },

  Draft: { label: 'Nháp', tone: 'neutral', icon: 'edit' },
  InProgress: { label: 'Đang thực hiện', tone: 'info', icon: 'clinic' },
  AwaitingCls: { label: 'Chờ xét nghiệm / CĐHA', tone: 'warning', icon: 'cls' },
  ClsCompleted: { label: 'Đã có kết quả CLS', tone: 'info', icon: 'cls' },
  Published: { label: 'Đã phát hành', tone: 'success', icon: 'check' },
  Amended: { label: 'Đã bổ sung', tone: 'info', icon: 'edit' },
  Pending: { label: 'Chờ xác minh', tone: 'warning', icon: 'clock' },
  Verified: { label: 'Đã xác minh', tone: 'success', icon: 'check' },
  Rejected: { label: 'Từ chối', tone: 'danger', icon: 'ban' },

  Verifying: { label: 'Đang xác minh', tone: 'info', icon: 'clock' },
  Approved: { label: 'Đã duyệt hoàn', tone: 'info', icon: 'check' },
  Paid: { label: 'Đã thanh toán', tone: 'success', icon: 'check' },
  Failed: { label: 'Thanh toán thất bại', tone: 'danger', icon: 'alert' },
  Refunded: { label: 'Đã hoàn tiền', tone: 'success', icon: 'check' },
  AlreadyProcessed: { label: 'Đã xử lý trước đó', tone: 'info', icon: 'check' },
  Unknown: { label: 'Chưa xác định', tone: 'warning', icon: 'warn' },

  Waiting: { label: 'Đang chờ', tone: 'warning', icon: 'clock' },
  Called: { label: 'Mời vào phòng', tone: 'info', icon: 'send' },
  Deferred: { label: 'Hoãn lượt', tone: 'neutral', icon: 'clock' },
  InService: { label: 'Đang khám', tone: 'info', icon: 'clinic' },
  Sent: { label: 'Mới nhận', tone: 'info', icon: 'send' },
  Read: { label: 'Đã đọc', tone: 'neutral', icon: 'check' },
};

const TONE_CLASSES: Record<StatusTone, string> = {
  neutral: 'bg-[#e5f8ed] text-[#3e4941] border-[#bdcabf]',
  info: 'bg-[#e8f0fe] text-[#0b57d0] border-[#bdd4fb]',
  success: 'bg-[#d4f7e2] text-[#005231] border-[#8ef8bb]',
  warning: 'bg-[#fef3c7] text-[#9a5b00] border-[#fcd34d]',
  danger: 'bg-[#ffdad6] text-[#93000a] border-[#ffb4ab]',
};

function renderIcon(kind?: string) {
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
    default:
      return null;
  }
}

export const StatusPill: React.FC<{
  status: string;
  labelOverride?: string;
  toneOverride?: StatusTone;
  className?: string;
}> = ({ status, labelOverride, toneOverride, className = '' }) => {
  const meta = STATUS_MAP[status] ?? { label: status, tone: 'neutral' as StatusTone };
  const tone = toneOverride ?? meta.tone;
  const label = labelOverride ?? meta.label;
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs font-medium whitespace-nowrap ${TONE_CLASSES[tone]} ${className}`}
    >
      {renderIcon(meta.icon)}
      <span>{label}</span>
    </span>
  );
};
