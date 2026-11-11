import React, { useState } from 'react';
import {
  Banner,
  Button,
  Card,
  ConfirmDialog,
  Countdown,
  DataTable,
  DatePicker,
  DateTimeText,
  EmptyState,
  ErrorState,
  FormField,
  Input,
  MoneyText,
  Select,
  Skeleton,
  StatusPill,
  Stepper,
  Tabs,
  Textarea,
  VersionConflictDialog,
} from '../../shared/ui';

const ALL_STATUSES = [
  'Hold',
  'PendingPayment',
  'Confirmed',
  'CheckedIn',
  'Completed',
  'Cancelled',
  'Expired',
  'NoShow',
  'Draft',
  'InProgress',
  'AwaitingCls',
  'ClsCompleted',
  'Published',
  'Amended',
  'Pending',
  'Verified',
  'Rejected',
  'Approved',
  'Paid',
  'Failed',
  'Refunded',
  'Matched',
  'Mismatch',
  'Unknown',
  'Waiting',
  'Called',
  'Deferred',
  'InService',
  'Sent',
  'Read',
];

export const StyleguidePage: React.FC = () => {
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [conflictOpen, setConflictOpen] = useState(false);
  const [tab, setTab] = useState('components');

  return (
    <div className="mx-auto max-w-6xl space-y-6 p-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-[#0f1f18]">Staff UI Styleguide (`/__styleguide`)</h1>
          <p className="text-sm text-[#44544c]">Kiểm tra component chuẩn hóa dùng chung cho cổng nhân viên.</p>
        </div>
        <Countdown expiresAt={new Date(Date.now() + 4 * 60 * 1000).toISOString()} label="Mẫu đếm ngược" />
      </div>

      <Tabs
        activeId={tab}
        onChange={setTab}
        tabs={[
          { id: 'components', label: 'Components', count: 12 },
          { id: 'statuses', label: 'Trạng thái', count: ALL_STATUSES.length },
          { id: 'feedback', label: 'Cảnh báo & Bảng' },
        ]}
      />

      {tab === 'components' && (
        <div className="grid gap-6 md:grid-cols-2">
          <Card title="Nút & Biến thể">
            <div className="flex flex-wrap gap-2">
              <Button variant="primary">Chính (Primary)</Button>
              <Button variant="secondary">Phụ (Secondary)</Button>
              <Button variant="outline">Viền (Outline)</Button>
              <Button variant="warning">Cảnh báo</Button>
              <Button variant="danger">Xóa / Hủy</Button>
              <Button loading>Đang xử lý</Button>
            </div>
            <div className="mt-4 flex flex-wrap gap-2">
              <Button size="sm" onClick={() => setConfirmOpen(true)}>
                Mở ConfirmDialog
              </Button>
              <Button size="sm" variant="warning" onClick={() => setConflictOpen(true)}>
                Mở VersionConflict 409
              </Button>
            </div>
          </Card>

          <Card title="Form Controls & Định dạng số/ngày VN">
            <div className="space-y-3">
              <FormField label="Họ tên bệnh nhân" required>
                {(props) => <Input {...props} defaultValue="Nguyễn Văn An" />}
              </FormField>
              <div className="grid grid-cols-2 gap-3">
                <FormField label="Chuyên khoa">
                  {(props) => (
                    <Select
                      {...props}
                      options={[
                        { value: 'noitongquat', label: 'Nội tổng quát' },
                        { value: 'timmach', label: 'Tim mạch' },
                      ]}
                    />
                  )}
                </FormField>
                <FormField label="Ngày khám">
                  {(props) => <DatePicker {...props} defaultValue="2026-11-12" />}
                </FormField>
              </div>
              <FormField label="Ghi chú" error="Ví dụ trạng thái lỗi nhập liệu">
                {(props) => <Textarea {...props} rows={2} placeholder="Nhập ghi chú..." />}
              </FormField>
              <div className="flex items-center justify-between rounded-lg bg-[#f4f8f6] px-3 py-2 text-sm">
                <span>Tiền khám: <MoneyText amount={250000} /></span>
                <span>Giờ VN: <DateTimeText value={new Date()} /></span>
              </div>
            </div>
          </Card>
        </div>
      )}

      {tab === 'statuses' && (
        <Card title="Bảng ánh xạ StatusPill">
          <div className="flex flex-wrap gap-2">
            {ALL_STATUSES.map((s) => (
              <StatusPill key={s} status={s} />
            ))}
          </div>
        </Card>
      )}

      {tab === 'feedback' && (
        <div className="space-y-4">
          <Stepper
            currentStepIndex={1}
            steps={[
              { id: '1', label: 'Chọn bác sĩ' },
              { id: '2', label: 'Xác nhận & giữ chỗ' },
              { id: '3', label: 'Hoàn tất' },
            ]}
          />
          <Banner tone="warning" title="Lưu ý giờ cutoff">
            Các thay đổi ca bác sĩ trong ngày chỉ thực hiện trước 06:30 GMT+7.
          </Banner>
          <DataTable
            rowKey={(r) => r.id}
            rows={[
              { id: '1', ticket: 'A-012', fee: 150000, status: 'Confirmed' },
              { id: '2', ticket: 'A-013', fee: 200000, status: 'AwaitingCls' },
            ]}
            columns={[
              { key: 'ticket', header: 'Số vé', render: (r) => <span className="font-semibold">{r.ticket}</span> },
              { key: 'fee', header: 'Chi phí', align: 'right', render: (r) => <MoneyText amount={r.fee} /> },
              { key: 'status', header: 'Trạng thái', render: (r) => <StatusPill status={r.status} /> },
            ]}
          />
          <div className="grid gap-4 md:grid-cols-3">
            <Card title="Skeleton"><Skeleton rows={3} /></Card>
            <EmptyState title="Chưa có hàng chờ" description="Hiện chưa có bệnh nhân mới check-in." />
            <ErrorState message="Mất kết nối SignalR, đang thử kết nối lại." onRetry={() => undefined} />
          </div>
        </div>
      )}

      <ConfirmDialog
        open={confirmOpen}
        onClose={() => setConfirmOpen(false)}
        onConfirm={() => setConfirmOpen(false)}
        title="Xác nhận phát hành hồ sơ"
        message="Sau khi phát hành, bệnh nhân có thể xem ngay kết quả trên cổng cá nhân."
      />
      <VersionConflictDialog
        open={conflictOpen}
        onClose={() => setConflictOpen(false)}
        onReloadLatest={() => setConflictOpen(false)}
        entityName="Phiếu sinh hiệu"
        currentVersion={4}
      />
    </div>
  );
};
