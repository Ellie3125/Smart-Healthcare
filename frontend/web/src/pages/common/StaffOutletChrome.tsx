import React from 'react';
import { Outlet } from 'react-router-dom';
import { WifiOff } from 'lucide-react';
import { useStaffRealtimeStatus } from '../../layouts/StaffLayout';
import { Banner } from '../../shared/ui';

/**
 * Bao bọc các route nhân viên để hiển thị cảnh báo khi kênh SignalR realtime bị gián đoạn.
 */
export const StaffOutletChrome: React.FC = () => {
  const rtStatus = useStaffRealtimeStatus();

  return (
    <div className="space-y-4">
      {rtStatus === 'disconnected' ? (
        <Banner tone="warning" title="Mất kết nối thông báo trực tiếp">
          <div className="flex items-center gap-2">
            <WifiOff className="h-4 w-4 shrink-0" aria-hidden="true" />
            <span>Kênh SignalR đang gián đoạn. Vui lòng bấm làm mới định kỳ khi thao tác hàng đợi.</span>
          </div>
        </Banner>
      ) : null}

      <Outlet />
    </div>
  );
};
