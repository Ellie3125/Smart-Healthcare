import React, { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Printer } from 'lucide-react';
import { receptionApi } from '../../api/receptionApi';
import {
  Banner,
  Button,
  Card,
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
  // Backend chỉ đăng ký walk-in cho hồ sơ đã có: tra theo SĐT/CCCD khớp chính xác rồi chọn hồ sơ.
  const [keyword, setKeyword] = useState('');
  const [patient, setPatient] = useState<{ id: string; patientName: string } | null>(null);
  const [selectedDoctorId, setSelectedDoctorId] = useState('');
  const [issuedTicket, setIssuedTicket] = useState<{ appointmentCode: string; ticketNumber: string; queueNumber: number } | null>(null);
  const [idemKey] = useState(() => createIdempotencyKey('walkin'));

  const today = getVNDateISO();
  const doctorsQuery = useQuery({
    queryKey: ['reception', 'walkin-doctors', today],
    queryFn: () => receptionApi.listWalkInDoctors(today),
  });

  const patientsQuery = useQuery({
    queryKey: ['reception', 'walkin-patients', keyword],
    queryFn: () => receptionApi.searchPatients(keyword.replace(/\s/g, '')),
    enabled: /^\d{9,20}$/.test(keyword.replace(/\s/g, '')),
  });
  const patients = patientsQuery.data ?? [];

  const registerMutation = useMutation({
    mutationFn: () =>
      receptionApi.registerWalkIn(
        { patientId: patient!.id, doctorId: selectedDoctorId },
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
        <Card title="Bước 1: Tra hồ sơ người khám">
          <FormField label="Số điện thoại hoặc CCCD của người khám" required>
            {(props) => (
              <Input {...props} value={keyword} onChange={(e) => setKeyword(e.target.value)} placeholder="VD: 0901234567" />
            )}
          </FormField>
          <div className="mt-3 space-y-2">
            {patientsQuery.isFetching ? <Skeleton rows={2} /> : null}
            {patientsQuery.isSuccess && patients.length === 0 ? (
              <Banner tone="warning" title="Không tìm thấy hồ sơ">
                Người khám cần có hồ sơ trên web bệnh nhân trước khi đăng ký tại quầy.
              </Banner>
            ) : null}
            {patients.map((p) => {
              const selected = patient?.id === p.patientId;
              return (
                <label key={p.patientId} className="flex items-center gap-3 rounded-xl bg-surface-container-low/65 p-3 text-sm">
                  <input
                    type="radio"
                    name="walkin-patient"
                    checked={selected}
                    onChange={() => setPatient({ id: p.patientId, patientName: p.fullName })}
                    className="h-4 w-4 accent-[#006a41]"
                  />
                  <span className="font-semibold">{p.fullName}</span>
                  <span className="text-xs text-on-surface-variant">
                    {p.phone} • CCCD {p.cccdMasked || '—'} • {p.dateOfBirth}
                  </span>
                </label>
              );
            })}
          </div>
          <div className="mt-4 flex justify-end">
            <Button variant="primary" disabled={!patient} onClick={() => setStep(1)}>
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
                        {doc.specialtyName} • Còn <strong className="font-data-tabular text-primary">{doc.remainingSlots}</strong>/{doc.totalSlots} suất
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
                setKeyword('');
                setPatient(null);
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
