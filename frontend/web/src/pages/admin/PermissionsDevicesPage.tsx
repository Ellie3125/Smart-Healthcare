import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminOpsApi } from '../../api/adminOpsApi';
import {
  Card,
  DataTable,
  DatePicker,
  ErrorState,
  FormField,
  Input,
  Skeleton,
  StatusPill,
  useToast,
} from '../../shared/ui';
import { getVNDateISO } from '../../shared/lib/datetime';
import { extractFriendlyError } from '../../shared/lib/errors';

export const PermissionsDevicesPage: React.FC = () => {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [date, setDate] = useState(getVNDateISO());

  const matrixQuery = useQuery({
    queryKey: ['admin', 'permission-matrix'],
    queryFn: () => adminOpsApi.getPermissionMatrix(),
  });

  const devicesQuery = useQuery({
    queryKey: ['admin', 'devices', date],
    queryFn: () => adminOpsApi.listDevices(date),
  });

  const assignMutation = useMutation({
    mutationFn: ({ deviceId, accountUsername }: { deviceId: string; accountUsername: string }) =>
      adminOpsApi.assignDevice(deviceId, accountUsername),
    onSuccess: () => {
      pushToast('Đã cập nhật gán tài khoản thiết bị.', 'success');
      queryClient.invalidateQueries({ queryKey: ['admin', 'devices', date] });
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold text-[#0f1f18]">Phân quyền & Thiết bị bệnh viện (AD10)</h1>
        <p className="text-xs text-[#44544c]">Kiểm soát ranh giới vai trò và thiết bị gán phòng theo ngày.</p>
      </div>

      <Card title="Ma trận phân quyền theo vai trò">
        {matrixQuery.isLoading ? (
          <Skeleton rows={4} />
        ) : matrixQuery.isError ? (
          <ErrorState message={extractFriendlyError(matrixQuery.error).message} onRetry={() => matrixQuery.refetch()} />
        ) : (
          <DataTable
            rowKey={(r) => r.role}
            rows={matrixQuery.data ?? []}
            columns={[
              { key: 'role', header: 'Vai trò', render: (r) => <strong>{r.role}</strong> },
              {
                key: 'cap',
                header: 'Phạm vi được phép',
                render: (r) => (
                  <ul className="list-disc pl-4 text-xs">
                    {r.capabilities.map((c) => (
                      <li key={c}>{c}</li>
                    ))}
                  </ul>
                ),
              },
              { key: 'res', header: 'Giới hạn cứng', render: (r) => <span className="text-xs text-[#ba1a1a]">{r.restrictedNote}</span> },
            ]}
          />
        )}
      </Card>

      <Card
        title="Thiết bị & Máy phòng theo ngày"
        actions={
          <div className="w-44">
            <FormField label="Ngày áp dụng">
              {(props) => <DatePicker {...props} value={date} onChange={(e) => setDate(e.target.value)} />}
            </FormField>
          </div>
        }
      >
        {devicesQuery.isLoading ? (
          <Skeleton rows={3} />
        ) : (
          <DataTable
            rowKey={(r) => r.id}
            rows={devicesQuery.data ?? []}
            columns={[
              { key: 'code', header: 'Mã máy', render: (r) => <span className="font-mono font-semibold">{r.deviceCode}</span> },
              { key: 'name', header: 'Thiết bị', render: (r) => r.deviceName },
              { key: 'room', header: 'Phòng / Trạm', render: (r) => r.roomOrStationName },
              {
                key: 'acc',
                header: 'Tài khoản phòng gán',
                render: (r) => (
                  <Input
                    aria-label={`Tài khoản cho ${r.deviceCode}`}
                    defaultValue={r.accountUsername || ''}
                    onBlur={(e) => {
                      if (e.target.value !== (r.accountUsername || '')) {
                        assignMutation.mutate({ deviceId: r.id, accountUsername: e.target.value });
                      }
                    }}
                  />
                ),
              },
              { key: 'status', header: 'Trạng thái', render: (r) => <StatusPill status={r.status} /> },
            ]}
          />
        )}
      </Card>
    </div>
  );
};
