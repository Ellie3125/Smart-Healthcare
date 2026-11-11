import React, { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { ArrowLeft, Download, Eye, Pill } from 'lucide-react';
import { clinicalApi } from '../../api/clinicalApi';
import {
  Banner,
  Button,
  Card,
  DateTimeText,
  Dialog,
  ErrorState,
  Skeleton,
  StatusPill,
  useToast,
} from '../../shared/ui';
import { extractPatientError } from '../../shared/lib/errors';

/**
 * P21 & P22 — Chi tiết hồ sơ khám & Xem kết quả CLS.
 * Ẩn hoàn toàn nội dung chuyên môn nếu hồ sơ chưa `Published`.
 * Tệp đính kèm CLS được tải qua Backend stream/blob và chỉ mở qua Blob URL nội bộ trình duyệt.
 */
export const EncounterDetailPage: React.FC = () => {
  const { encounterId = '' } = useParams<{ encounterId: string }>();
  const { pushToast } = useToast();
  const [previewBlobUrl, setPreviewBlobUrl] = useState<string | null>(null);
  const [previewTitle, setPreviewTitle] = useState<string>('');
  const [previewMime, setPreviewMime] = useState<string>('');
  const [downloadingFileId, setDownloadingFileId] = useState<string | null>(null);

  useEffect(() => {
    return () => {
      if (previewBlobUrl) {
        URL.revokeObjectURL(previewBlobUrl);
      }
    };
  }, [previewBlobUrl]);

  const query = useQuery({
    queryKey: ['patient', 'encounter-detail', encounterId],
    queryFn: () => clinicalApi.getEncounterDetail(encounterId),
    enabled: Boolean(encounterId),
  });

  const handleFileAction = async (
    resultId: string,
    fileId: string,
    fileName: string,
    mode: 'preview' | 'download',
  ) => {
    try {
      setDownloadingFileId(fileId);
      const { blob, contentType } = await clinicalApi.downloadClsAttachment(resultId, fileId);
      const objectUrl = URL.createObjectURL(blob);
      if (mode === 'download') {
        const a = document.createElement('a');
        a.href = objectUrl;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        a.remove();
        URL.revokeObjectURL(objectUrl);
      } else {
        if (previewBlobUrl) URL.revokeObjectURL(previewBlobUrl);
        setPreviewBlobUrl(objectUrl);
        setPreviewTitle(fileName);
        setPreviewMime(contentType);
      }
    } catch (err) {
      pushToast(extractPatientError(err).message, 'danger');
    } finally {
      setDownloadingFileId(null);
    }
  };

  if (query.isLoading) {
    return (
      <Card title="Đang tải chi tiết hồ sơ khám">
        <Skeleton rows={5} />
      </Card>
    );
  }

  if (query.isError || !query.data) {
    return <ErrorState message={extractPatientError(query.error).message} onRetry={() => query.refetch()} />;
  }

  const d = query.data;

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <Link to="/records" className="inline-flex items-center gap-1.5 text-sm font-medium text-[#006a41] hover:underline">
          <ArrowLeft className="h-4 w-4" aria-hidden="true" />
          Quay lại lịch sử khám
        </Link>
        {d.isPublished && d.hasPrescription ? (
          <Link to={`/records/${d.id}/prescription`}>
            <Button variant="secondary" size="sm" leftIcon={<Pill className="h-4 w-4" />}>
              Xem đơn thuốc
            </Button>
          </Link>
        ) : null}
      </div>

      <Card
        title={`Hồ sơ khám ${d.encounterCode}`}
        subtitle={`${d.doctorName} • ${d.specialtyName} • Ngày ${d.serviceDate}`}
        actions={<StatusPill status={d.status} />}
      >
        {!d.isPublished ? (
          <Banner tone="info" title="Hồ sơ đang trong quá trình khám và hoàn thiện">
            Nội dung chẩn đoán, kết luận bác sĩ và kết quả cận lâm sàng chỉ được hiển thị sau khi bác sĩ hoàn tất và phát hành chính thức.
          </Banner>
        ) : (
          <div className="space-y-5">
            {d.amendedNote ? (
              <Banner tone="warning" title="Bản ghi có bổ sung sau phát hành">
                {d.amendedNote}
              </Banner>
            ) : null}

            {d.vitals ? (
              <div className="grid grid-cols-2 gap-3 rounded-2xl bg-[#e5f8ed] p-4 sm:grid-cols-4">
                <div>
                  <p className="text-xs text-[#3e4941]">Huyết áp</p>
                  <p className="font-semibold tabular-nums text-[#0f1f18]">
                    {d.vitals.systolicBp}/{d.vitals.diastolicBp} mmHg
                  </p>
                </div>
                <div>
                  <p className="text-xs text-[#3e4941]">Mạch</p>
                  <p className="font-semibold tabular-nums text-[#0f1f18]">{d.vitals.heartRate} l/p</p>
                </div>
                <div>
                  <p className="text-xs text-[#3e4941]">Nhiệt độ</p>
                  <p className="font-semibold tabular-nums text-[#0f1f18]">{d.vitals.temperatureC} °C</p>
                </div>
                <div>
                  <p className="text-xs text-[#3e4941]">SpO2</p>
                  <p className="font-semibold tabular-nums text-[#0f1f18]">{d.vitals.spo2}%</p>
                </div>
              </div>
            ) : null}

            <div className="space-y-3 text-sm">
              <div>
                <h3 className="font-semibold text-[#0f1f18]">Triệu chứng & Khám lâm sàng</h3>
                <p className="mt-1 text-[#3e4941]">{d.symptoms || '—'}</p>
                {d.clinicalNotes ? <p className="mt-1 text-[#3e4941]">{d.clinicalNotes}</p> : null}
              </div>

              <div>
                <h3 className="font-semibold text-[#0f1f18]">Chẩn đoán ICD-10</h3>
                <ul className="mt-1 space-y-1">
                  {(d.diagnoses ?? []).map((dx) => (
                    <li key={dx.icdCode} className="text-[#0f1f18]">
                      <strong>{dx.icdCode}</strong> — {dx.description}
                      {dx.isPrimary ? ' (Chẩn đoán chính)' : ''}
                    </li>
                  ))}
                </ul>
              </div>

              <div>
                <h3 className="font-semibold text-[#0f1f18]">Kết luận & Hướng dẫn theo dõi</h3>
                <p className="mt-1 text-[#0f1f18]">{d.conclusion}</p>
                {d.followUpInstruction ? (
                  <p className="mt-1 text-[#3e4941]">{d.followUpInstruction}</p>
                ) : null}
              </div>

              {d.publishedAt ? (
                <p className="text-xs text-[#3e4941]">
                  Phát hành lúc <DateTimeText value={d.publishedAt} /> (Phiên bản v{d.version})
                </p>
              ) : null}
            </div>
          </div>
        )}
      </Card>

      {d.isPublished && d.clsResults.length > 0 ? (
        <Card title="Kết quả Cận lâm sàng đã phát hành">
          <div className="space-y-4">
            {d.clsResults.map((res) => (
              <div key={res.id} className="rounded-xl border border-[#bdcabf] p-4">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <h3 className="font-semibold text-[#0f1f18]">
                    {res.serviceName} ({res.serviceCode})
                  </h3>
                  <StatusPill status={res.status} />
                </div>
                {res.summaryText ? <p className="mt-2 text-sm text-[#0f1f18]">{res.summaryText}</p> : null}
                {res.technicalConclusion ? (
                  <p className="mt-1 text-sm text-[#3e4941]">Kết luận CLS: {res.technicalConclusion}</p>
                ) : null}
                {res.attachments.length > 0 ? (
                  <div className="mt-3 flex flex-wrap gap-2">
                    {res.attachments.map((att) => (
                      <div key={att.fileId} className="flex flex-wrap items-center gap-2 rounded-lg bg-[#e5f8ed] px-3 py-2 text-xs">
                        <span className="font-medium text-[#0f1f18]">{att.fileName}</span>
                        <Button
                          size="sm"
                          variant="outline"
                          loading={downloadingFileId === att.fileId}
                          leftIcon={<Eye className="h-3.5 w-3.5" />}
                          onClick={() => handleFileAction(res.id, att.fileId, att.fileName, 'preview')}
                        >
                          Xem tệp
                        </Button>
                        <Button
                          size="sm"
                          variant="secondary"
                          loading={downloadingFileId === att.fileId}
                          leftIcon={<Download className="h-3.5 w-3.5" />}
                          onClick={() => handleFileAction(res.id, att.fileId, att.fileName, 'download')}
                        >
                          Tải về
                        </Button>
                      </div>
                    ))}
                  </div>
                ) : null}
              </div>
            ))}
          </div>
        </Card>
      ) : null}

      <Dialog
        open={Boolean(previewBlobUrl)}
        onClose={() => {
          if (previewBlobUrl) URL.revokeObjectURL(previewBlobUrl);
          setPreviewBlobUrl(null);
        }}
        title={previewTitle || 'Xem kết quả cận lâm sàng'}
      >
        {previewBlobUrl ? (
          previewMime.startsWith('image/') ? (
            <img src={previewBlobUrl} alt={previewTitle} className="max-h-[65vh] w-full rounded-lg object-contain" />
          ) : (
            <iframe
              src={previewBlobUrl}
              title={previewTitle}
              className="h-[60vh] w-full rounded-lg border border-[#bdcabf]"
            />
          )
        ) : null}
      </Dialog>
    </div>
  );
};
