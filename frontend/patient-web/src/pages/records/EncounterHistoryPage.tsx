import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { FileText, Pill, Receipt } from 'lucide-react';
import { clinicalApi } from '../../api/clinicalApi';
import { patientGet } from '../../shared/lib/http';
import {
  Banner,
  Button,
  Card,
  DateTimeText,
  EmptyState,
  ErrorState,
  FormField,
  PaginationBar,
  Select,
  Skeleton,
  StatusPill,
} from '../../shared/ui';
import { extractPatientError } from '../../shared/lib/errors';

interface ProfileOption {
  id: string;
  fullName: string;
  relationship: string;
  verificationStatus?: 'Pending' | 'Verified' | 'Rejected';
}

/**
 * P20 — Lịch sử hồ sơ khám bệnh: chỉ cho phép xem đầy đủ khi hồ sơ đã được xác minh (Verified).
 */
export const EncounterHistoryPage: React.FC = () => {
  const [selectedProfileId, setSelectedProfileId] = useState<string>('');
  const [page, setPage] = useState(1);

  const profilesQuery = useQuery({
    queryKey: ['patient', 'profiles'],
    queryFn: () => patientGet<ProfileOption[]>('/api/identity/patient/profiles'),
  });

  const profiles = profilesQuery.data ?? [];
  const currentProfile = profiles.find((p) => p.id === selectedProfileId) ?? profiles[0];
  const activeId = currentProfile?.id ?? '';
  const isPendingProfile = currentProfile?.verificationStatus === 'Pending';

  const encountersQuery = useQuery({
    queryKey: ['patient', 'encounters', activeId, page],
    queryFn: () => clinicalApi.listEncounters({ patientProfileId: activeId || undefined, page, pageSize: 10 }),
    enabled: Boolean(activeId) && !isPendingProfile,
    retry: false,
  });

  const apiErr = encountersQuery.isError ? extractPatientError(encountersQuery.error) : null;

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-semibold text-[#0f1f18]">Kết quả khám & Hồ sơ bệnh án</h1>
        <p className="mt-1 text-sm text-[#3e4941]">
          Chỉ hồ sơ đã xác minh định danh tại quầy mới có thể xem lịch sử khám và kết quả xét nghiệm chi tiết.
        </p>
      </div>

      <Card title="Chọn hồ sơ">
        <FormField label="Hồ sơ người khám">
          {(props) => (
            <Select
              {...props}
              value={activeId}
              onChange={(e) => {
                setSelectedProfileId(e.target.value);
                setPage(1);
              }}
              options={profiles.map((p) => ({
                value: p.id,
                label: `${p.fullName} (${p.relationship}) — ${p.verificationStatus === 'Verified' ? 'Đã xác minh' : 'Chờ xác minh'}`,
              }))}
            />
          )}
        </FormField>
      </Card>

      {isPendingProfile || apiErr?.isPendingVerification ? (
        <Banner
          tone="warning"
          title="Hồ sơ đang chờ xác minh định danh"
          action={
            <Link to="/status/pending-verification">
              <Button size="sm" variant="warning">
                Xem hướng dẫn xác minh
              </Button>
            </Link>
          }
        >
          Hồ sơ này đang ở trạng thái Chờ xác minh nên hệ thống khóa xem hồ sơ y khoa để bảo vệ quyền riêng tư. Vui lòng mang CCCD/giấy tờ gốc đến quầy tiếp đón khi đi khám để được xác minh.
        </Banner>
      ) : encountersQuery.isLoading ? (
        <Card><Skeleton rows={4} /></Card>
      ) : apiErr ? (
        <ErrorState message={apiErr.message} onRetry={() => encountersQuery.refetch()} />
      ) : (encountersQuery.data?.items ?? []).length === 0 ? (
        <EmptyState title="Chưa có lượt khám nào cho hồ sơ này" />
      ) : (
        <div className="space-y-4">
          {encountersQuery.data!.items.map((enc) => (
            <Card key={enc.id}>
              <div className="flex flex-wrap items-start justify-between gap-4">
                <div className="space-y-1.5">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-mono text-xs font-semibold text-[#006a41]">{enc.encounterCode}</span>
                    <StatusPill status={enc.status} />
                  </div>
                  <h3 className="text-lg font-semibold text-[#0f1f18]">
                    {enc.primaryDiagnosis || 'Đang chờ bác sĩ kết luận & phát hành'}
                  </h3>
                  <p className="text-sm text-[#3e4941]">
                    {enc.doctorName} • {enc.specialtyName} • Ngày <DateTimeText value={enc.serviceDate} mode="date" />
                  </p>
                </div>

                <div className="flex flex-wrap gap-2">
                  <Link to={`/records/${enc.id}`}>
                    <Button variant="primary" size="sm" leftIcon={<FileText className="h-4 w-4" />}>
                      Xem chi tiết
                    </Button>
                  </Link>
                  {enc.hasPrescription ? (
                    <Link to={`/records/${enc.id}/prescription`}>
                      <Button variant="secondary" size="sm" leftIcon={<Pill className="h-4 w-4" />}>
                        Đơn thuốc
                      </Button>
                    </Link>
                  ) : null}
                  <Link to={`/encounters/${enc.id}/cost`}>
                    <Button variant="outline" size="sm" leftIcon={<Receipt className="h-4 w-4" />}>
                      Chi phí
                    </Button>
                  </Link>
                </div>
              </div>
            </Card>
          ))}
          <PaginationBar page={page} hasMore={Boolean(encountersQuery.data?.hasMore)} onPageChange={setPage} />
        </div>
      )}
    </div>
  );
};
