import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { LoginPage } from './pages/LoginPage';
import { ForbiddenPage } from './pages/ForbiddenPage';
import { StaffLayout } from './layouts/StaffLayout';
import { RequireRole } from './components/RequireRole';
import { ReceptionPage } from './pages/reception/ReceptionPage';
import { ReceptionSearchPage } from './pages/reception/ReceptionSearchPage';
import { WalkInRegisterPage } from './pages/reception/WalkInRegisterPage';
import { ReceptionCheckInPage } from './pages/reception/ReceptionCheckInPage';
import { ReceptionCashierPage } from './pages/reception/ReceptionCashierPage';
import { ReceptionQueryTxnPage } from './pages/reception/ReceptionQueryTxnPage';
import { ReceptionCancelPage } from './pages/reception/ReceptionCancelPage';
import { ReceptionRefundsPage } from './pages/reception/ReceptionRefundsPage';
import { ReceptionShiftReconcilePage } from './pages/reception/ReceptionShiftReconcilePage';
import { ReceptionCallListPage } from './pages/reception/ReceptionCallListPage';
import { NursePage } from './pages/nurse/NursePage';
import { NurseQueuePage } from './pages/nurse/NurseQueuePage';
import { DoctorPage } from './pages/doctor/DoctorPage';
import { DoctorRecordsPage } from './pages/doctor/DoctorRecordsPage';
import { RoomPage } from './pages/room/RoomPage';
import { RoomResultUploadPage } from './pages/room/RoomResultUploadPage';
import { AdminPage } from './pages/admin/AdminPage';
import { ServiceStationsPage } from './pages/admin/ServiceStationsPage';
import { DoctorDayQuotasPage } from './pages/admin/DoctorDayQuotasPage';
import { MedicalServicesPage } from './pages/admin/MedicalServicesPage';
import { PriceListsPage } from './pages/admin/PriceListsPage';
import { PermissionsDevicesPage } from './pages/admin/PermissionsDevicesPage';
import { ExceptionsPage } from './pages/admin/ExceptionsPage';
import { RefundApprovalsPage } from './pages/admin/RefundApprovalsPage';
import { PaymentReconcilePage } from './pages/admin/PaymentReconcilePage';
import { ReportsPage } from './pages/admin/ReportsPage';
import { AuditLogsPage } from './pages/admin/AuditLogsPage';
import { ClsWaiversPage } from './pages/admin/ClsWaiversPage';
import { ChatbotConfigPage } from './pages/admin/ChatbotConfigPage';
import { StaffNotificationsPage } from './pages/common/StaffNotificationsPage';
import { SessionReplacedPage } from './pages/common/SessionReplacedPage';
import { StaffOutletChrome } from './pages/common/StaffOutletChrome';
import { StyleguidePage } from './pages/dev/StyleguidePage';
import { useAuth } from './hooks/useAuth';

export const App: React.FC = () => {
  const { isAuthenticated, user } = useAuth();

  const getDefaultRoute = () => {
    if (!isAuthenticated || !user) return '/login';
    switch (user.role) {
      case 'Receptionist':
        return '/reception';
      case 'Nurse':
        return '/nurse';
      case 'Doctor':
        return '/doctor';
      case 'RoomAccount':
        return '/room';
      case 'Admin':
        return '/admin';
      default:
        return '/forbidden';
    }
  };

  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/forbidden" element={<ForbiddenPage />} />
      <Route path="/session-replaced" element={<SessionReplacedPage />} />
      <Route path="/__styleguide" element={<StyleguidePage />} />

      {/* Mỗi khu vực chỉ cho đúng một vai trò (Giải pháp v2.14 §8.6). Chặn ở đây chỉ để không bấm nhầm; quyền thật do Backend kiểm. */}
      <Route element={<StaffLayout />}>
        <Route element={<StaffOutletChrome />}>
          <Route path="/notifications" element={<StaffNotificationsPage />} />

          {/* Reception */}
          <Route path="/reception/search" element={<RequireRole allow={['Receptionist']}><ReceptionSearchPage /></RequireRole>} />
          <Route path="/reception/walk-in" element={<RequireRole allow={['Receptionist']}><WalkInRegisterPage /></RequireRole>} />
          <Route path="/reception/checkin" element={<RequireRole allow={['Receptionist']}><ReceptionCheckInPage /></RequireRole>} />
          <Route path="/reception/cashier" element={<RequireRole allow={['Receptionist']}><ReceptionCashierPage /></RequireRole>} />
          <Route path="/reception/querydr" element={<RequireRole allow={['Receptionist']}><ReceptionQueryTxnPage /></RequireRole>} />
          <Route path="/reception/cancel" element={<RequireRole allow={['Receptionist']}><ReceptionCancelPage /></RequireRole>} />
          <Route path="/reception/refunds" element={<RequireRole allow={['Receptionist']}><ReceptionRefundsPage /></RequireRole>} />
          <Route path="/reception/shift-close" element={<RequireRole allow={['Receptionist']}><ReceptionShiftReconcilePage /></RequireRole>} />
          <Route path="/reception/call-list" element={<RequireRole allow={['Receptionist']}><ReceptionCallListPage /></RequireRole>} />
          <Route
            path="/reception/*"
            element={
              <RequireRole allow={['Receptionist', 'Admin']}>
                <ReceptionPage />
              </RequireRole>
            }
          />

          {/* Nurse */}
          <Route path="/nurse/queue" element={<RequireRole allow={['Nurse']}><NurseQueuePage /></RequireRole>} />
          <Route
            path="/nurse/*"
            element={
              <RequireRole allow={['Nurse']}>
                <NursePage />
              </RequireRole>
            }
          />

          {/* Doctor */}
          <Route path="/doctor/records" element={<RequireRole allow={['Doctor']}><DoctorRecordsPage /></RequireRole>} />
          <Route
            path="/doctor/*"
            element={
              <RequireRole allow={['Doctor']}>
                <DoctorPage />
              </RequireRole>
            }
          />

          {/* CLS Room */}
          <Route path="/room/upload" element={<RequireRole allow={['RoomAccount']}><RoomResultUploadPage /></RequireRole>} />
          <Route
            path="/room/*"
            element={
              <RequireRole allow={['RoomAccount']}>
                <RoomPage />
              </RequireRole>
            }
          />

          {/* Admin */}
          <Route path="/admin/stations" element={<RequireRole allow={['Admin']}><ServiceStationsPage /></RequireRole>} />
          <Route path="/admin/quotas" element={<RequireRole allow={['Admin']}><DoctorDayQuotasPage /></RequireRole>} />
          <Route path="/admin/services" element={<RequireRole allow={['Admin']}><MedicalServicesPage /></RequireRole>} />
          <Route path="/admin/price-lists" element={<RequireRole allow={['Admin']}><PriceListsPage /></RequireRole>} />
          <Route path="/admin/devices" element={<RequireRole allow={['Admin']}><PermissionsDevicesPage /></RequireRole>} />
          <Route path="/admin/exceptions" element={<RequireRole allow={['Admin']}><ExceptionsPage /></RequireRole>} />
          <Route path="/admin/refunds" element={<RequireRole allow={['Admin']}><RefundApprovalsPage /></RequireRole>} />
          <Route path="/admin/reconcile" element={<RequireRole allow={['Admin']}><PaymentReconcilePage /></RequireRole>} />
          <Route path="/admin/reports" element={<RequireRole allow={['Admin']}><ReportsPage /></RequireRole>} />
          <Route path="/admin/audit-logs" element={<RequireRole allow={['Admin']}><AuditLogsPage /></RequireRole>} />
          <Route path="/admin/cls-waivers" element={<RequireRole allow={['Admin']}><ClsWaiversPage /></RequireRole>} />
          <Route path="/admin/chatbot-config" element={<RequireRole allow={['Admin']}><ChatbotConfigPage /></RequireRole>} />
          <Route
            path="/admin/*"
            element={
              <RequireRole allow={['Admin']}>
                <AdminPage />
              </RequireRole>
            }
          />
        </Route>
      </Route>

      <Route path="/" element={<Navigate to={getDefaultRoute()} replace />} />
      <Route path="*" element={<Navigate to={getDefaultRoute()} replace />} />
    </Routes>
  );
};

