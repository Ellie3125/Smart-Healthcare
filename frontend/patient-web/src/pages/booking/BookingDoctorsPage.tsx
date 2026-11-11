import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { Calendar, Search, Stethoscope, Clock } from 'lucide-react';
import { bookingApi } from '../../api/bookingApi';
import {
  Banner,
  Button,
  Card,
  DatePicker,
  DateTimeText,
  EmptyState,
  ErrorState,
  FormField,
  MoneyText,
  PaginationBar,
  Select,
  Skeleton,
  StatusPill,
} from '../../shared/ui';
import { extractPatientError } from '../../shared/lib/errors';
import { getVNDateISO } from '../../shared/lib/datetime';

export const BookingDoctorsPage: React.FC = () => {
  const [serviceDate, setServiceDate] = useState<string>(getVNDateISO());
  const [specialtyId, setSpecialtyId] = useState<string>('');
  const [doctorRankId, setDoctorRankId] = useState<string>('');
  const [page, setPage] = useState<number>(1);

  const specialtiesQuery = useQuery({
    queryKey: ['patient', 'specialties'],
    queryFn: () => bookingApi.listSpecialties(),
  });

  const ranksQuery = useQuery({
    queryKey: ['patient', 'doctor-ranks'],
    queryFn: () => bookingApi.listDoctorRanks(),
  });

  const recoverableQuery = useQuery({
    queryKey: ['patient', 'recoverable-attempts'],
    queryFn: () => bookingApi.listRecoverableAttempts(),
  });

  const doctorsQuery = useQuery({
    queryKey: ['patient', 'doctors', { serviceDate, specialtyId, doctorRankId, page }],
    queryFn: () =>
      bookingApi.searchDoctors({
        serviceDate,
        specialtyId: specialtyId || undefined,
        doctorRankId: doctorRankId || undefined,
        page,
        pageSize: 12,
      }),
  });

  const doctors = doctorsQuery.data?.items ?? [];
  const recoverable = recoverableQuery.data ?? [];

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-[#0f1f18]">Đặt lịch khám bệnh</h1>
          <p className="mt-1 text-sm text-[#3e4941]">
            Chọn ngày khám, chuyên khoa và bác sĩ phù hợp. Mỗi bác sĩ phục vụ 30 lượt khám mỗi ngày.
          </p>
        </div>
        <Link to="/appointments">
          <Button variant="outline" size="sm">
            Xem lịch của tôi
          </Button>
        </Link>
      </div>

      {recoverable.length > 0 ? (
        <Banner
          tone="warning"
          title="Bạn đang có giữ chỗ chưa hoàn tất thanh toán"
          action={
            <Link to={`/booking/attempt/${recoverable[0].bookingAttemptId}`}>
              <Button size="sm" variant="warning">
                Tiếp tục thanh toán
              </Button>
            </Link>
          }
        >
          Phiên giữ chỗ cho bác sĩ <strong>{recoverable[0].doctorName}</strong> ngày{' '}
          <DateTimeText value={recoverable[0].serviceDate} mode="date" /> đang chờ hoàn tất.
        </Banner>
      ) : null}

      <Card title="Bộ lọc tìm bác sĩ">
        <div className="grid gap-4 sm:grid-cols-3">
          <FormField label="Ngày khám" required>
            {(props) => (
              <DatePicker
                {...props}
                min={getVNDateISO()}
                value={serviceDate}
                onChange={(e) => {
                  setServiceDate(e.target.value);
                  setPage(1);
                }}
              />
            )}
          </FormField>

          <FormField label="Chuyên khoa">
            {(props) => (
              <Select
                {...props}
                placeholder="Tất cả chuyên khoa"
                value={specialtyId}
                onChange={(e) => {
                  setSpecialtyId(e.target.value);
                  setPage(1);
                }}
                options={(specialtiesQuery.data ?? []).map((s) => ({
                  value: s.id,
                  label: s.name,
                }))}
              />
            )}
          </FormField>

          <FormField label="Học hàm / Học vị">
            {(props) => (
              <Select
                {...props}
                placeholder="Tất cả mức phí"
                value={doctorRankId}
                onChange={(e) => {
                  setDoctorRankId(e.target.value);
                  setPage(1);
                }}
                options={(ranksQuery.data ?? []).map((r) => ({
                  value: r.id,
                  label: `${r.name} (${r.consultationFee.toLocaleString('vi-VN')} ₫)`,
                }))}
              />
            )}
          </FormField>
        </div>
      </Card>

      {doctorsQuery.isLoading ? (
        <div className="grid gap-4 md:grid-cols-2">
          <Card><Skeleton rows={4} /></Card>
          <Card><Skeleton rows={4} /></Card>
        </div>
      ) : doctorsQuery.isError ? (
        <ErrorState
          message={extractPatientError(doctorsQuery.error).message}
          onRetry={() => doctorsQuery.refetch()}
        />
      ) : doctors.length === 0 ? (
        <EmptyState
          title="Không có bác sĩ mở lịch trong ngày đã chọn"
          description="Bạn có thể thử chọn ngày khám kế tiếp hoặc bỏ bớt điều kiện lọc chuyên khoa."
          icon={<Search className="h-6 w-6" />}
        />
      ) : (
        <>
          <div className="grid gap-4 md:grid-cols-2">
            {doctors.map((doc) => {
              const disabled = doc.status !== 'Open' || doc.remainingSlots <= 0;
              return (
                <Card key={`${doc.doctorId}-${doc.serviceDate}`}>
                  <div className="flex flex-col justify-between gap-4">
                    <div className="space-y-2">
                      <div className="flex items-start justify-between gap-3">
                        <div>
                          <span className="inline-flex items-center gap-1 rounded-full bg-[#e5f8ed] px-2.5 py-0.5 text-xs font-medium text-[#005231]">
                            <Stethoscope className="h-3.5 w-3.5" aria-hidden="true" />
                            {doc.specialtyName}
                          </span>
                          <h3 className="mt-1.5 text-lg font-semibold text-[#0f1f18]">{doc.fullName}</h3>
                          <p className="text-sm text-[#3e4941]">{doc.doctorRankName}</p>
                        </div>
                        <StatusPill
                          status={doc.remainingSlots > 0 && doc.status === 'Open' ? 'Open' : 'Closed'}
                          labelOverride={doc.remainingSlots > 0 ? `Còn ${doc.remainingSlots}/${doc.totalSlots} suất` : 'Đã hết suất'}
                          toneOverride={doc.remainingSlots > 0 ? 'success' : 'danger'}
                        />
                      </div>

                      {doc.bio ? <p className="text-sm text-[#3e4941]">{doc.bio}</p> : null}

                      <div className="flex flex-wrap items-center gap-4 pt-1 text-sm text-[#3e4941]">
                        <span className="inline-flex items-center gap-1.5">
                          <Calendar className="h-4 w-4 text-[#006a41]" aria-hidden="true" />
                          <DateTimeText value={doc.serviceDate} mode="date" />
                        </span>
                        <span className="inline-flex items-center gap-1.5">
                          <Clock className="h-4 w-4 text-[#006a41]" aria-hidden="true" />
                          {doc.roomName}
                        </span>
                      </div>
                    </div>

                    <div className="flex items-center justify-between border-t border-[#e5f8ed] pt-3">
                      <div>
                        <p className="text-xs text-[#3e4941]">Phí khám dự kiến</p>
                        <MoneyText amount={doc.consultationFee} className="text-lg text-[#006a41]" />
                      </div>
                      {disabled ? (
                        <Button variant="outline" disabled>
                          Hết suất khám
                        </Button>
                      ) : (
                        <Link to={`/booking/doctors/${doc.doctorId}?date=${doc.serviceDate}`}>
                          <Button variant="primary">Xem suất & Chọn lịch</Button>
                        </Link>
                      )}
                    </div>
                  </div>
                </Card>
              );
            })}
          </div>

          <PaginationBar
            page={page}
            hasMore={Boolean(doctorsQuery.data?.hasMore)}
            onPageChange={setPage}
            loading={doctorsQuery.isFetching}
          />
        </>
      )}
    </div>
  );
};
