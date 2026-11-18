import React from 'react';
import { Outlet } from 'react-router-dom';
import { ChatFloatingWidget } from '../pages/assistant/ChatFloatingWidget';

/**
 * Bao bọc các route cổng bệnh nhân để giữ widget Trợ lý 24/7 nổi bên dưới giao diện chính.
 */
export const PatientPortalChrome: React.FC = () => (
  <div className="pb-16 md:pb-0">
    <Outlet />
    <ChatFloatingWidget />
  </div>
);
