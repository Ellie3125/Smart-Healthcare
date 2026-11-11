import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { clinicalApi } from '../../api/clinicalApi';
import { Banner, Card, DateTimeText, ErrorState, Skeleton } from '../../shared/ui';
import { extractPatientError } from '../../shared/lib/errors';

/**
 * P23 — Đơn thuốc chỉ đọc: có khuyến cáo sử dụng an toàn, KHÔNG nút mua/đặt thuốc hay tồn kho.
 */
export const PrescriptionDetailPage: React.FC = () => {
  const { encounterId = '' } = useParams<{ encounterId: string }>();

  const query = useQuery({
    queryKey: ['patient', 'prescription', encounterId],
    queryFn: () => clinicalApi.getPrescription(encounterId),
    enabled: Boolean(encounterId),
  });

  if (query.isLoading) {
    return (
      <Card title="Đang tải đơn thuốc">
        <Skeleton rows={4} />
      </Card>
    );
  }

  if (query.isError || !query.data) {
    return <ErrorState message={extractPatientError(query.error).message} onRetry={() => query.refetch()} />;
  }

  const rx = query.data;

  return (
    <div className="space-y-5">
      <Link to={`/records/${encounterId}`} className="inline-flex items-center gap-1.5 text-sm font-medium text-[#006a41] hover:underline">
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        Quay lại chi tiết hồ sơ khám
      </Link>

      <Banner tone="info" title="Lưu ý sử dụng đơn thuốc">
        {rx.disclaimer}
      </Banner>

      <Card
        title={`Đơn thuốc ${rx.encounterCode}`}
        subtitle={
          <>
            Bác sĩ kê đơn: {rx.doctorName} • Ngày cấp <DateTimeText value={rx.issuedAt} />
          </>
        }
      >
        <div className="space-y-3">
          {rx.items.map((item, idx) => (
            <div key={item.id} className="rounded-xl border border-[#bdcabf] bg-[#f8fbf9] p-4">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <h3 className="font-semibold text-[#0f1f18]">
                  {idx + 1}. {item.drugName}
                </h3>
                <span className="rounded-full bg-[#e5f8ed] px-3 py-0.5 text-xs font-semibold tabular-nums text-[#005231]">
                  Số lượng: {item.quantity} {item.unit}
                </span>
              </div>
              <p className="mt-1 text-xs text-[#3e4941]">Hoạt chất: {item.activeIngredient}</p>
              <p className="mt-2 text-sm text-[#0f1f18]">
                Liều dùng: <strong>{item.dosage}</strong> • {item.frequency} • Trong {item.durationDays} ngày
              </p>
              <p className="mt-1 text-sm text-[#3e4941]">Hướng dẫn: {item.usageInstruction}</p>
            </div>
          ))}
        </div>

        {rx.notes ? (
          <p className="mt-4 rounded-xl bg-[#e5f8ed] p-3 text-sm text-[#005231]">
            Lời dặn bác sĩ: {rx.notes}
          </p>
        ) : null}
      </Card>
    </div>
  );
};
