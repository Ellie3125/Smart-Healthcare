import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { LoginPage } from './pages/LoginPage';
import { RegisterPage } from './pages/RegisterPage';
import { ProfilesPage } from './pages/ProfilesPage';
import { EditProfilePage } from './pages/EditProfilePage';
import { AddRelativePage } from './pages/AddRelativePage';
import { VerificationStatusPage } from './pages/VerificationStatusPage';
import { PatientLayout } from './layouts/PatientLayout';
import { PatientPortalChrome } from './layouts/PatientPortalChrome';
import { RequirePatientAuth } from './components/RequirePatientAuth';
import { useAuth } from './hooks/useAuth';
import { StyleguidePage } from './pages/dev/StyleguidePage';
import { BookingDoctorsPage } from './pages/booking/BookingDoctorsPage';
import { DoctorDayPage } from './pages/booking/DoctorDayPage';
import { BookingConfirmPage } from './pages/booking/BookingConfirmPage';
import { VnpayReturnPage } from './pages/booking/VnpayReturnPage';
import { MyAppointmentsPage } from './pages/appointments/MyAppointmentsPage';
import { AppointmentTicketPage } from './pages/appointments/AppointmentTicketPage';
import { RescheduleAppointmentPage } from './pages/appointments/RescheduleAppointmentPage';
import { CancelAppointmentPage } from './pages/appointments/CancelAppointmentPage';
import { TodayJourneyPage } from './pages/journey/TodayJourneyPage';
import { ClsPaymentsPage } from './pages/billing/ClsPaymentsPage';
import { EncounterCostPage } from './pages/billing/EncounterCostPage';
import { RefundRequestsPage } from './pages/billing/RefundRequestsPage';
import { EncounterHistoryPage } from './pages/records/EncounterHistoryPage';
import { EncounterDetailPage } from './pages/records/EncounterDetailPage';
import { PrescriptionDetailPage } from './pages/records/PrescriptionDetailPage';
import { NotificationsPage } from './pages/assistant/NotificationsPage';
import { ChatbotPage } from './pages/assistant/ChatbotPage';
import { StatusScreenPage } from './pages/assistant/StatusScreenPage';
import { PublicDisplayPage } from './pages/public/PublicDisplayPage';

export const App: React.FC = () => {
  const { isAuthenticated, isLoading } = useAuth();

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-[#F7FAF8]">
        <div className="flex flex-col items-center gap-3">
          <div className="w-8 h-8 border-3 border-[#006a41] border-t-transparent rounded-full animate-spin"></div>
          <span className="text-sm font-medium text-[#5E6F66]">Đang khởi tạo hệ thống...</span>
        </div>
      </div>
    );
  }

  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route path="/display" element={<PublicDisplayPage />} />
      <Route path="/__styleguide" element={<StyleguidePage />} />

      <Route path="/status/401" element={<StatusScreenPage kind="401" />} />
      <Route path="/status/403" element={<StatusScreenPage kind="403" />} />
      <Route path="/status/404" element={<StatusScreenPage kind="404" />} />
      <Route path="/status/pending-verification" element={<StatusScreenPage kind="pending-verification" />} />
      <Route path="/status/offline" element={<StatusScreenPage kind="offline" />} />
      <Route path="/status/500" element={<StatusScreenPage kind="500" />} />
      <Route path="/status/maintenance" element={<StatusScreenPage kind="maintenance" />} />

      <Route
        element={
          <RequirePatientAuth>
            <PatientLayout />
          </RequirePatientAuth>
        }
      >
        <Route element={<PatientPortalChrome />}>
          <Route path="/profiles" element={<ProfilesPage />} />
          <Route path="/profiles/new" element={<EditProfilePage />} />
          <Route path="/profiles/:id/edit" element={<EditProfilePage />} />
          <Route path="/profiles/add-relative" element={<AddRelativePage />} />
          <Route path="/profiles/:id/verification" element={<VerificationStatusPage />} />

          <Route path="/booking" element={<BookingDoctorsPage />} />
          <Route path="/booking/doctors/:doctorId" element={<DoctorDayPage />} />
          <Route path="/booking/attempt/:bookingAttemptId" element={<BookingConfirmPage />} />
          <Route path="/payment/vnpay-return" element={<VnpayReturnPage />} />

          <Route path="/appointments" element={<MyAppointmentsPage />} />
          <Route path="/appointments/:id/ticket" element={<AppointmentTicketPage />} />
          <Route path="/appointments/:id/reschedule" element={<RescheduleAppointmentPage />} />
          <Route path="/appointments/:id/cancel" element={<CancelAppointmentPage />} />

          <Route path="/today-journey" element={<TodayJourneyPage />} />
          <Route path="/cls-payments" element={<ClsPaymentsPage />} />
          <Route path="/encounters/:encounterId/cost" element={<EncounterCostPage />} />
          <Route path="/refunds" element={<RefundRequestsPage />} />

          <Route path="/records" element={<EncounterHistoryPage />} />
          <Route path="/records/:encounterId" element={<EncounterDetailPage />} />
          <Route path="/records/:encounterId/prescription" element={<PrescriptionDetailPage />} />

          <Route path="/notifications" element={<NotificationsPage />} />
          <Route path="/chatbot" element={<ChatbotPage />} />
        </Route>
      </Route>

      <Route
        path="/"
        element={<Navigate to={isAuthenticated ? '/profiles' : '/login'} replace />}
      />
      <Route
        path="*"
        element={isAuthenticated ? <StatusScreenPage kind="404" /> : <Navigate to="/login" replace />}
      />
    </Routes>
  );
};
