import React, { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Printer } from 'lucide-react';
import { receptionApi } from '../../api/receptionApi';
import {
  Banner,
  Button,
  Card,
  DatePicker,
  FormField,
  Input,
  MoneyText,
  Skeleton,
  Stepper,
  useToast,
} from '../../shared/ui';
import { getVNDateISO } from '../../shared/lib/datetime';
import { createIdempotencyKey } from '../../shared/lib/idempotency';
import { extractFriendlyError } from '../../shared/lib/errors';

export const WalkInRegisterPage: React.FC = () => {
  const { pushToast } = useToast();
  const [step, setStep] = useState(0);
  const [fullName, setFullName] = useState('');
  const [phone, setPhone] = useState('');
  const [dateOfBirth, setDateOfBirth] = useState('1990-01-01');
  const [cccd, setCccd] = useState('');
  const [selectedDoctorId, setSelectedDoctorId] = useState('');
  const [issuedTicket, setIssuedTicket] = useState<{ appointmentCode: string; ticketNumber: string; queueNumber: number } | null>(null);
  const [idemKey] = useState(() => createIdempotencyKey('walkin'));

  const today = getVNDateISO();
  const doctorsQuery = useQuery({
    queryKey: ['reception', 'walkin-doctors', today],
    queryFn: () => receptionApi.listWalkInDoctors(today),
  });

  const registerMutation = useMutation({
    mutationFn: () =>
      receptionApi.registerWalkIn(
        {
          fullName: fullName.trim(),
          phone: phone.trim(),
          dateOfBirth,
          cccd: cccd.trim(),
          doctorId: selectedDoctorId,
          serviceDate: today,
          collectCashNow: true,
        },
        idemKey,
      ),
    onSuccess: (res) => {
      setIssuedTicket(res);
      setStep(2);
      pushToast(`Đăng ký khám trực tiếp thành công. STT ${res.queueNumber}`, 'success');
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  const chosenDoc = (doctorsQuery.data ?? []).find((d) => d.doctorId === selectedDoctorId);

  return (
    <div className="space-y-5">
      <div>
        <h1 className="font-headline-md text-2xl font-extrabold tracking-tight text-on-surface">
          Đăng ký khám trực tiếp tại quầy — Walk-in (RC02)
        </h1>
        <p className="text-sm text-on-surface-variant">Bệnh nhân tại quầy dùng chung 30 suất/ngày với bệnh nhân đặt trực tuyến.</p>
      </div>

      <Stepper
        currentStepIndex={step}
        steps={[
          { id: 'info', label: '1. Thông tin bệnh nhân' },
          { id: 'doc', label: '2. Chọn bác sĩ còn suất' },
          { id: 'done', label: '3. Thu phí & In vé STT' },
        ]}
      />

      {step === 0 ? (
        <Card title="Bước 1: Đối chiếu & nhập thông tin người khám">
          <div className="grid gap-4 sm:grid-cols-2">
            <FormField label="Họ và tên" required>
              {(props) => <Input {...props} value={fullName} onChange={(e) => setFullName(e.target.value)} />}
            </FormField>
            <FormField label="Số điện thoại" required>
              {(props) => <Input {...props} value={phone} onChange={(e) => setPhone(e.target.value)} />}
            </FormField>
            <FormField label="Ngày sinh" required>
              {(props) => <DatePicker {...props} value={dateOfBirth} onChange={(e) => setDateOfBirth(e.target.value)} />}
            </FormField>
            <FormField label="Số CCCD / Giấy tờ tùy thân" required>
              {(props) => <Input {...props} value={cccd} onChange={(e) => setCccd(e.target.value)} />}
            </FormField>
          </div>
          <div className="mt-4 flex justify-end">
            <Button
              variant="primary"
              disabled={!fullName.trim() || !phone.trim() || !cccd.trim()}
              onClick={() => setStep(1)}
            >
              Tiếp theo: Chọn bác sĩ
            </Button>
          </div>
        </Card>
      ) : null}

      {step === 1 ? (
        <Card title="Bước 2: Chọn bác sĩ đang mở suất hôm nay">
          {doctorsQuery.isLoading ? (
            <Skeleton rows={3} />
          ) : (
            <div className="space-y-3">
              {(doctorsQuery.data ?? []).map((doc) => {
                const selected = doc.doctorId === selectedDoctorId;
                return (
                  <div
                    key={doc.doctorId}
                    className={`flex flex-wrap items-center justify-between gap-3 rounded-2xl p-4 transition-all ${
                      selected
                        ? 'bg-primary-container/60 ring-2 ring-primary'
                        : 'bg-surface-container-low/65 hover:bg-surface-container-low'
                    }`}
                  >
                    <div>
                      <p className="font-semibold text-on-surface">{doc.doctorName}</p>
                      <p className="text-xs text-on-surface-variant">
                        {doc.specialtyName} • {doc.roomName} • Còn <strong className="font-data-tabular text-primary">{doc.remainingSlots}</strong>/30 suất
                      </p>
                    </div>
                    <div className="flex items-center gap-3">
                      <MoneyText amount={doc.consultationFee} />
                      <Button
                        size="sm"
                        variant={selected ? 'primary' : 'outline'}
                        disabled={doc.remainingSlots <= 0}
                        onClick={() => setSelectedDoctorId(doc.doctorId)}
                      >
                        {selected ? 'Đã chọn' : 'Chọn bác sĩ'}
                      </Button>
                    </div>
                  </div>
                );
              })}

              <div className="flex justify-between pt-2">
                <Button variant="outline" onClick={() => setStep(0)}>Quay lại</Button>
                <Button
                  variant="primary"
                  disabled={!selectedDoctorId}
                  loading={registerMutation.isPending}
                  onClick={() => registerMutation.mutate()}
                >
                  Xác nhận & Thu tiền mặt (<MoneyText amount={chosenDoc?.consultationFee} />)
                </Button>
              </div>
            </div>
          )}
        </Card>
      ) : null}

      {step === 2 && issuedTicket ? (
        <Card title="Bước 3: Đã cấp vé & số thứ tự">
          <Banner tone="success" title="Đăng ký walk-in hoàn tất">
            Mã lịch: <strong>{issuedTicket.appointmentCode}</strong> • Số vé: <strong>{issuedTicket.ticketNumber}</strong> • Số thứ tự: <strong>{issuedTicket.queueNumber}</strong>
          </Banner>
          <div className="mt-4 flex gap-2">
            <Button variant="outline" leftIcon={<Printer className="h-4 w-4" />} onClick={() => window.print()}>
              In phiếu thứ tự
            </Button>
            <Button
              variant="primary"
              onClick={() => {
                setStep(0);
                setFullName('');
                setPhone('');
                setCccd('');
                setIssuedTicket(null);
              }}
            >
              Đăng ký bệnh nhân tiếp theo
            </Button>
          </div>
        </Card>
      ) : null}
    </div>
  );
};
