import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { clinicalStaffApi } from '../../api/clinicalStaffApi';
import { Card, DataTable, DateTimeText, ErrorState, FormField, Input, Skeleton, StatusPill } from '../../shared/ui';
import { extractFriendlyError } from '../../shared/lib/errors';

/**
 * DR09 — Lịch sử hồ sơ bệnh án chỉ đọc phía Bác sĩ.
 */
export const DoctorRecordsPage: React.FC = () => {
  const [keyword, setKeyword] = useState('');

  const query = useQuery({
    queryKey: ['staff', 'doctor-records', keyword],
    queryFn: () => clinicalStaffApi.listDoctorRecords(keyword || undefined),
  });

  return (
    <div className="space-y-5">
      <div>
        <h1 className="font-headline-md text-2xl font-extrabold tracking-tight text-on-surface">
          Lịch sử khám (Chỉ đọc — DR09)
        </h1>
      </div>

      <Card>
        <FormField label="Tìm theo tên bệnh nhân hoặc mã hồ sơ">
          {(props) => <Input {...props} value={keyword} onChange={(e) => setKeyword(e.target.value)} placeholder="Mã lượt khám, tên bệnh nhân hoặc chẩn đoán" />}
        </FormField>
      </Card>

      {query.isLoading ? (
        <Card><Skeleton rows={4} /></Card>
      ) : query.isError ? (
        <ErrorState message={extractFriendlyError(query.error).message} onRetry={() => query.refetch()} />
      ) : (
        <DataTable
          rowKey={(r) => r.id}
          rows={query.data ?? []}
          columns={[
            { key: 'code', header: 'Mã hồ sơ', render: (r) => <span className="font-mono font-semibold">{r.encounterCode}</span> },
            { key: 'patient', header: 'Bệnh nhân', render: (r) => r.patientName },
            { key: 'date', header: 'Ngày khám', render: (r) => <DateTimeText value={r.serviceDate} mode="date" /> },
            { key: 'dx', header: 'Chẩn đoán chính', render: (r) => r.diagnosis },
            { key: 'status', header: 'Trạng thái', render: (r) => <StatusPill status={r.status} /> },
          ]}
        />
      )}
    </div>
  );
};
