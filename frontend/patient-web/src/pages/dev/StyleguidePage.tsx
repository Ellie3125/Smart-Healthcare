import React, { useState } from 'react';
import {
  Banner,
  Button,
  Card,
  ConfirmDialog,
  Countdown,
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
} from '../../shared/ui';

export const StyleguidePage: React.FC = () => {
  const [open, setOpen] = useState(false);
  const [tab, setTab] = useState('ui');

  return (
    <div className="mx-auto max-w-5xl space-y-6 p-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-[#0f1f18]">Patient UI Styleguide</h1>
          <p className="text-sm text-[#3e4941]">Hệ thống thành phần giao diện dành cho cổng bệnh nhân.</p>
        </div>
        <Countdown expiresAt={new Date(Date.now() + 5 * 60 * 1000).toISOString()} />
      </div>

      <Tabs
        activeId={tab}
        onChange={setTab}
        tabs={[
          { id: 'ui', label: 'Thành phần cơ bản' },
          { id: 'status', label: 'Trạng thái' },
        ]}
      />

      {tab === 'ui' ? (
        <div className="grid gap-6 md:grid-cols-2">
          <Card title="Nút & Tương tác">
            <div className="flex flex-wrap gap-2">
              <Button variant="primary">Đặt lịch ngay</Button>
              <Button variant="secondary">Chọn thêm</Button>
              <Button variant="outline">Quay lại</Button>
              <Button variant="danger" onClick={() => setOpen(true)}>
                Hủy lịch
              </Button>
            </div>
          </Card>

          <Card title="Biểu mẫu & Định dạng">
            <div className="space-y-3">
              <FormField label="Họ và tên" required>
                {(props) => <Input {...props} defaultValue="Trần Thị Bình" />}
              </FormField>
              <div className="grid grid-cols-2 gap-3">
                <FormField label="Chuyên khoa">
                  {(props) => (
                    <Select {...props} options={[{ value: 'noitongquat', label: 'Nội tổng quát' }]} />
                  )}
                </FormField>
                <FormField label="Ngày khám">
                  {(props) => <DatePicker {...props} defaultValue="2026-11-12" />}
                </FormField>
              </div>
              <div className="flex justify-between rounded-xl bg-[#e5f8ed] px-4 py-2.5 text-sm">
                <span>Phí khám: <MoneyText amount={150000} /></span>
                <DateTimeText value={new Date()} />
              </div>
            </div>
          </Card>
        </div>
      ) : (
        <Card title="StatusPill">
          <div className="flex flex-wrap gap-2">
            {['Hold', 'Confirmed', 'CheckedIn', 'AwaitingCls', 'Published', 'Pending', 'Verified', 'Refunded'].map(
              (s) => (
                <StatusPill key={s} status={s} />
              ),
            )}
          </div>
        </Card>
      )}

      <Stepper
        currentStepIndex={1}
        steps={[
          { id: '1', label: 'Chọn bác sĩ' },
          { id: '2', label: 'Giữ chỗ 5 phút' },
          { id: '3', label: 'Thanh toán VNPAY' },
        ]}
      />
      <Banner tone="warning" title="Lưu ý giờ đổi lịch">
        Bạn chỉ có thể đổi lịch cùng mức giá trước 05:00 sáng ngày khám.
      </Banner>
      <div className="grid gap-4 md:grid-cols-3">
        <Card title="Skeleton"><Skeleton /></Card>
        <EmptyState title="Chưa có lịch hẹn" description="Hãy bắt đầu chọn bác sĩ phù hợp." />
        <ErrorState message="Không thể tải thông tin thanh toán." onRetry={() => undefined} />
      </div>

      <ConfirmDialog
        open={open}
        onClose={() => setOpen(false)}
        onConfirm={() => setOpen(false)}
        confirmVariant="danger"
        title="Xác nhận hủy lịch khám"
        message="Hệ thống sẽ hoàn lại 150.000 ₫ về tài khoản theo chính sách hiện hành."
      />
    </div>
  );
};
