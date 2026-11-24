import React from 'react';
import { describe, expect, it, beforeEach } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { staffApiMock } from './staffTestFixtures';
import { ToastProvider } from '../shared/ui';
import { SessionReplacedPage } from '../pages/common/SessionReplacedPage';
import { NurseQueuePage } from '../pages/nurse/NurseQueuePage';
import { DoctorDayQuotasPage } from '../pages/admin/DoctorDayQuotasPage';
import { PaymentReconcilePage } from '../pages/admin/PaymentReconcilePage';
import { AuditLogsPage } from '../pages/admin/AuditLogsPage';
import { ReceptionCheckInPage } from '../pages/reception/ReceptionCheckInPage';
import { ReceptionCashierPage } from '../pages/reception/ReceptionCashierPage';
import { ReceptionCancelPage } from '../pages/reception/ReceptionCancelPage';
import { ReceptionShiftReconcilePage } from '../pages/reception/ReceptionShiftReconcilePage';
import { NursePage } from '../pages/nurse/NursePage';
import { DoctorPage } from '../pages/doctor/DoctorPage';
import { RoomResultUploadPage } from '../pages/room/RoomResultUploadPage';
import { RoomResultsPage } from '../pages/room/RoomResultsPage';
import { ROLE_MENUS } from '../layouts/menus';

// Không còn backend giả: các màn gọi API được kiểm bằng chạy thật với Backend + seed.
function renderStaff(ui: React.ReactElement, initialEntries: string[] = ['/']) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <ToastProvider>
        <MemoryRouter initialEntries={initialEntries}>
          <Routes>
            <Route path="*" element={ui} />
          </Routes>
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

describe('Staff Web', () => {
  beforeEach(() => {
    staffApiMock.reset();
    localStorage.clear();
  });

  it('S04b: clears local token and informs user when RoomAccount session is replaced', () => {
    localStorage.setItem('token', 'old-room-token');
    localStorage.setItem('user', '{"role":"RoomAccount"}');
    renderStaff(<SessionReplacedPage />);
    expect(screen.getByText(/Phiên phòng máy đã bị thay thế/i)).toBeInTheDocument();
    expect(localStorage.getItem('token')).toBeNull();
  });

  it('AD06, AD13, AD15: quotas, querydr reconcile without mark-paid button, and AuditLog PII masking keeping last 3 chars', async () => {
    renderStaff(<DoctorDayQuotasPage />);
    expect(await screen.findByText(/BS\.CKI Trần Quốc Khánh/i)).toBeInTheDocument();

    renderStaff(<PaymentReconcilePage />);
    expect(await screen.findByText(/VNP-20261111-8891/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Đánh dấu đã trả/i })).not.toBeInTheDocument();

    renderStaff(<AuditLogsPage />);
    expect((await screen.findAllByText(/CCCD: \*{9}789/)).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/SĐT: \*{7}321/).length).toBeGreaterThan(0);
  });

  it('RC03: check-in ID gate & deadline block', async () => {
    renderStaff(<ReceptionCheckInPage />);
    expect(await screen.findByText(/Quá hạn CheckInDeadline/i)).toBeInTheDocument();
    const checkInBtn = screen.getByRole('button', { name: /Xác nhận Check-in/i });
    expect(checkInBtn).toBeDisabled();
    fireEvent.click(screen.getByRole('checkbox'));
    expect(checkInBtn).toBeEnabled();
  });

  it('RC04: cashier uses read-only fixed amounts with no editable number inputs', async () => {
    const { container } = renderStaff(<ReceptionCashierPage />);
    await screen.findByText(/CLS-BATCH-09/i);
    expect(container.querySelector('input[type="number"]')).toBeNull();
  });

  it('RC07: requires reason when cancelling CheckedIn appointment', async () => {
    renderStaff(<ReceptionCancelPage />);
    await screen.findByText(/SH-261111-001/i);
    const cancelBtns = screen.getAllByRole('button', { name: /^Hủy lịch$/i });
    fireEvent.click(cancelBtns[0]); // SH-261111-001 is CheckedIn
    expect(await screen.findByText(/Bệnh nhân ĐÃ CHECK-IN/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Xác nhận hủy/i })).toBeDisabled();
  });

  it('RC09: detects cash shift difference and disables close button without reconciliation note', async () => {
    renderStaff(<ReceptionShiftReconcilePage />);
    const actualInput = await screen.findByLabelText(/Tiền mặt thực đếm/i);
    fireEvent.change(actualInput, { target: { value: '1200000' } });
    expect(screen.getByText(/Phát hiện chênh lệch tiền mặt/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Xác nhận chốt ca/i })).toBeDisabled();
  });

  it('NU02: prompts confirmation on empty fields, shows threshold warning, and shows 409 dialog on conflict', async () => {
    renderStaff(<NursePage />, ['/nurse?encounterId=enc-nurse-2']);
    const finishBtn = await screen.findByRole('button', { name: /Hoàn tất & Lưu sinh hiệu/i });
    const spo2Input = screen.getByLabelText(/SpO2/i);
    fireEvent.change(spo2Input, { target: { value: '89' } });
    expect(await screen.findByText(/SpO2 thấp/i)).toBeInTheDocument();

    fireEvent.click(finishBtn);
    expect(await screen.findByText(/Vẫn còn chỉ số sinh hiệu để trống/i)).toBeInTheDocument();

    staffApiMock.override('POST', '/api/clinical/staff/nurse/encounters/:encounterId/vitals', () => ({
      status: 409,
      data: { code: 'VERSION_CONFLICT', currentVersion: 5, detail: 'Xung đột' },
    }));
    fireEvent.click(screen.getByRole('button', { name: /^Xác nhận$/i }));
    expect(await screen.findByText(/Xung đột phiên bản dữ liệu \(409\)/i)).toBeInTheDocument();
  });

  it('NU01: lists the nurse room queue read-only without calling numbers', async () => {
    renderStaff(<NurseQueuePage />);
    expect(await screen.findByText(/^\d+ bệnh nhân trong hàng chờ$/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Gọi người tiếp theo/i })).not.toBeInTheDocument();
    expect((await screen.findAllByRole('button', { name: /Đo sinh hiệu/i })).length).toBeGreaterThan(0);
  });

  it('DR01, DR06, DR07, DR10: dual queues with single Call Next buttons, no drug stock UI, 409 conflict dialog', async () => {
    renderStaff(<DoctorPage />);
    expect(await screen.findByText(/Hàng đợi Khám đầu/i)).toBeInTheDocument();
    expect(screen.getByText(/Hàng đợi Quay lại sau CLS/i)).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /Gọi người tiếp theo/i })).toHaveLength(2);
    expect(screen.queryByRole('button', { name: /Mua thuốc/i })).not.toBeInTheDocument();

    staffApiMock.override('PUT', '/api/clinical/staff/doctor/encounters/:encounterId/draft', () => ({
      status: 409,
      data: { code: 'VERSION_CONFLICT', currentVersion: 9, detail: 'Đã sửa nơi khác' },
    }));
    fireEvent.click(await screen.findByRole('button', { name: /Lưu nháp/i }));
    expect(await screen.findByText(/Xung đột phiên bản dữ liệu \(409\)/i)).toBeInTheDocument();
  });

  it('DR02: saves the draft in the backend shape and keeps the loaded record fields', async () => {
    let sent: Record<string, unknown> | undefined;
    staffApiMock.override('PUT', '/api/clinical/staff/doctor/encounters/:encounterId/draft', (_url, body) => {
      sent = typeof body === 'string' ? JSON.parse(body) : (body as Record<string, unknown>);
      return { status: 200, data: {} };
    });
    renderStaff(<DoctorPage />);
    fireEvent.click(await screen.findByRole('button', { name: /Lưu nháp/i }));
    await waitFor(() => expect(sent).toBeDefined());
    expect(sent).toMatchObject({
      version: 'AAAAAAAAB9E=',
      symptoms: 'Đau đầu nhẹ vùng chẩm, hồi hộp buổi sáng.',
      clinicalExaminationNotes: 'Nhịp tim đều, không tiếng thổi bệnh lý.',
    });
    expect(sent).toHaveProperty('chiefComplaint');
    expect((sent!.diagnoses as { icd10Code: string; diagnosisType: string }[])[0]).toMatchObject({ diagnosisType: 'Initial' });
    expect(sent).not.toHaveProperty('prescriptionItems');
  });

  it('CL04: locks viewing after CLS result is Published', async () => {
    staffApiMock.override('GET', '/api/clinical/staff/room/results/:id', () => ({
      status: 200,
      data: {
        orderItemId: 'cls-ord-1',
        ticketNumber: 'CLS-021',
        patientName: 'Nguyễn Minh Tâm',
        serviceCode: 'XN-CTM',
        serviceName: 'Tổng phân tích tế bào máu',
        resultStatus: 'Published',
        summaryText: '',
        technicalConclusion: '',
        readingDoctorName: 'BS.CKII Đỗ Văn Hùng',
        availableShiftDoctors: ['BS.CKII Đỗ Văn Hùng'],
        version: 3,
        draftFiles: [],
      },
    }));
    renderStaff(<RoomResultUploadPage />, ['/room/upload?orderItemId=cls-ord-1']);
    await waitFor(() => {
      expect(screen.getByText(/Kết quả đã phát hành — Đã khóa xem tại phòng thực hiện/i)).toBeInTheDocument();
    });
  });

  it('DOC-W3: ResultFileUploader shows the ticket to match, allows file selection and lists draft files', async () => {
    staffApiMock.override('GET', '/api/clinical/staff/room/results/:id', () => ({
      status: 200,
      data: {
        resultId: 'res-1',
        orderItemId: 'cls-ord-draft',
        queueTicketId: 'qt-sa-1',
        resultStatus: 'Draft',
        summaryText: '',
        technicalConclusion: '',
        version: 'AAAAAAAAB9E=',
        readingDoctorId: 'doc-1',
        readingDoctors: [{ doctorId: 'doc-1', fullName: 'BS.CKI Trần Quốc Khánh' }],
      },
    }));
    staffApiMock.override('GET', '/api/documents/staff/room/results/:id/files', () => ({
      status: 200,
      data: [
        {
          id: 'file-sa-1',
          clsResultId: 'cls-ord-draft',
          queueTicketId: 'cls-ord-draft',
          encounterId: 'enc-1',
          patientId: 'p-1',
          fileName: 'SieuAm-OB-TranThiMai.png',
          contentType: 'image/png',
          sizeBytes: 1048576,
          sha256Hash: 'a1b2c3d4e5f6',
          attachmentVersion: 1,
          uploadStatus: 'Uploaded',
          isPublished: false,
          uploadedAtUtc: '2026-11-16T10:00:00Z',
        },
      ],
    }));

    renderStaff(<RoomResultUploadPage />, ['/room/upload?orderItemId=cls-ord-draft']);
    expect(await screen.findByText(/Mã vé: CLS-088/i)).toBeInTheDocument();
    expect(await screen.findByText(/SieuAm-OB-TranThiMai\.png/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Chọn tệp từ máy/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Xóa/i })).toBeInTheDocument();
  });

  it('CL-MENU: RoomAccount menu has "Danh sách kết quả đã chẩn đoán" and not "Nhập & Upload kết quả"', () => {
    const titles = ROLE_MENUS.RoomAccount.map((m) => m.title);
    expect(titles).toContain('Danh sách kết quả đã chẩn đoán');
    expect(titles).not.toContain('Nhập & Upload kết quả');
  });

  it('CL-RESULTS: RoomResultsPage renders diagnosed results list', async () => {
    renderStaff(<RoomResultsPage />, ['/room/results']);
    expect(await screen.findByText('Danh sách kết quả đã chẩn đoán')).toBeInTheDocument();
    expect(screen.getByPlaceholderText(/Tìm theo mã vé, tên bệnh nhân/i)).toBeInTheDocument();
  });
});
