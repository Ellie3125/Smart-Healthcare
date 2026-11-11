import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
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
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold text-[#0f1f18]">Lịch sử khám (Chỉ đọc — DR09)</h1>
          <p className="text-xs text-[#44544c]">Tra cứu nhanh các đợt khám đã thực hiện của bác sĩ.</p>
        </div>
        <Link to="/doctor" className="inline-flex items-center gap-1.5 text-sm font-medium text-[#006a41] hover:underline">
          <ArrowLeft className="h-4 w-4" aria-hidden="true" />
          Về bàn khám
        </Link>
      </div>

      <Card>
        <FormField label="Tìm theo tên bệnh nhân hoặc mã hồ sơ">
          {(props) => <Input {...props} value={keyword} onChange={(e) => setKeyword(e.target.value)} placeholder="VD: HS-2026..." />}
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
