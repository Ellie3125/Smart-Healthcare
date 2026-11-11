import React, { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { bookingApi } from '../../api/bookingApi';
import {
  Banner,
  Button,
  Card,
  ConfirmDialog,
  DatePicker,
  EmptyState,
  FormField,
  MoneyText,
  Skeleton,
  StatusPill,
  useToast,
} from '../../shared/ui';
import { getVNDateISO, isBefore0500VN } from '../../shared/lib/datetime';
import { createIdempotencyKey } from '../../shared/lib/money';
import { extractPatientError } from '../../shared/lib/errors';
import type { RescheduleOption } from '../../types/booking';

/**
 * P14 — Đổi lịch khám: chỉ hỗ trợ đổi cùng mức giá và trước 05:00 sáng ngày khám.
 */
export const RescheduleAppointmentPage: React.FC = () => {
  const { id = '' } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { pushToast } = useToast();
  const [targetDate, setTargetDate] = useState<string>(() => getVNDateISO(new Date(Date.now() + 86400000)));
  const [selectedDoctor, setSelectedDoctor] = useState<RescheduleOption | null>(null);
  const [idemKey] = useState(() => createIdempotencyKey('resched'));

  const aptQuery = useQuery({
    queryKey: ['patient', 'appointments', 'detail-for-reschedule', id],
    queryFn: async () => {
      const list = await bookingApi.listMyAppointments();
      return list.items.find((a) => a.id === id) ?? null;
    },
    enabled: Boolean(id),
  });

  const optionsQuery = useQuery({
    queryKey: ['patient', 'reschedule-options', id, targetDate],
    queryFn: () => bookingApi.listRescheduleOptions(id, targetDate),
    enabled: Boolean(id && targetDate),
  });

  const mutate = useMutation({
    mutationFn: () =>
      bookingApi.rescheduleAppointment(
        id,
        { targetDoctorId: selectedDoctor!.doctorId, targetServiceDate: targetDate },
        idemKey,
      ),
    onSuccess: () => {
      pushToast('Đổi lịch khám thành công.', 'success');
      navigate('/appointments');
    },
    onError: (err) => {
      pushToast(extractPatientError(err).message, 'danger');
    },
  });

  if (aptQuery.isLoading) {
    return (
      <Card title="Đang kiểm tra điều kiện đổi lịch">
        <Skeleton rows={4} />
      </Card>
    );
  }

  const apt = aptQuery.data;
  if (!apt) {
    return <EmptyState title="Không tìm thấy lịch khám" />;
  }

  const before0500 = isBefore0500VN(apt.serviceDate);
  const canProceed = apt.canReschedule && before0500;

  return (
    <div className="space-y-5">
      <Link to="/appointments" className="inline-flex items-center gap-1.5 text-sm font-medium text-[#006a41] hover:underline">
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        Quay lại lịch khám của tôi
      </Link>

      <Card
        title={`Đổi lịch khám ${apt.appointmentCode}`}
        subtitle={`Hiện tại: ${apt.doctorName} • Phí gốc ${apt.consultationFee.toLocaleString('vi-VN')} ₫`}
      >
        {!canProceed ? (
          <Banner tone="warning" title="Không thể đổi lịch trực tuyến">
            {apt.rescheduleBlockedReason ||
              'Hệ thống chỉ cho phép đổi lịch trực tuyến trước 05:00 sáng ngày khám và lịch chưa check-in.'}
          </Banner>
        ) : (
          <div className="space-y-4">
            <Banner tone="info">
              Quy tắc đổi lịch: chỉ áp dụng cho bác sĩ/ngày khám có <strong>cùng mức phí</strong> (<MoneyText amount={apt.consultationFee} />) và trước 05:00 ngày khám cũ.
            </Banner>

            <FormField label="Chọn ngày khám muốn chuyển sang" required>
              {(props) => (
                <DatePicker
                  {...props}
                  min={getVNDateISO()}
                  value={targetDate}
                  onChange={(e) => setTargetDate(e.target.value)}
                />
              )}
            </FormField>

            {optionsQuery.isLoading ? (
              <Skeleton rows={3} />
            ) : (
              <div className="space-y-3">
                {(optionsQuery.data ?? []).map((opt) => (
                  <div
                    key={opt.doctorId}
                    className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-[#bdcabf] p-4"
                  >
                    <div>
                      <div className="flex items-center gap-2">
                        <h3 className="font-semibold text-[#0f1f18]">{opt.doctorName}</h3>
                        <StatusPill
                          status={opt.sameFeeEligible ? 'Confirmed' : 'Rejected'}
                          labelOverride={opt.sameFeeEligible ? 'Đủ điều kiện cùng giá' : 'Khác mức giá'}
                        />
                      </div>
                      <p className="mt-1 text-sm text-[#3e4941]">
                        {opt.specialtyName} • {opt.roomName} • Còn {opt.remainingSlots} suất • Phí:{' '}
                        <MoneyText amount={opt.consultationFee} />
                      </p>
                    </div>
                    <Button
                      variant={opt.sameFeeEligible ? 'primary' : 'outline'}
                      disabled={!opt.sameFeeEligible}
                      onClick={() => setSelectedDoctor(opt)}
                    >
                      Chọn bác sĩ này
                    </Button>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
      </Card>

      <ConfirmDialog
        open={Boolean(selectedDoctor)}
        onClose={() => setSelectedDoctor(null)}
        onConfirm={() => mutate.mutate()}
        loading={mutate.isPending}
        title="Xác nhận đổi lịch khám"
        message={
          selectedDoctor ? (
            <p>
              Bạn chắc chắn muốn chuyển lịch sang <strong>{selectedDoctor.doctorName}</strong> vào ngày{' '}
              <strong>{targetDate}</strong> với mức phí giữ nguyên{' '}
              <MoneyText amount={selectedDoctor.consultationFee} />?
            </p>
          ) : null
        }
      />
    </div>
  );
};
