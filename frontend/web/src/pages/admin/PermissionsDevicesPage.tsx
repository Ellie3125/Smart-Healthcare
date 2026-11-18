import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { adminOpsApi } from '../../api/adminOpsApi';
import { Card, DataTable, ErrorState, Skeleton } from '../../shared/ui';
import { extractFriendlyError } from '../../shared/lib/errors';

export const PermissionsDevicesPage: React.FC = () => {
  const matrixQuery = useQuery({
    queryKey: ['admin', 'permission-matrix'],
    queryFn: () => adminOpsApi.getPermissionMatrix(),
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-headline-md text-2xl font-extrabold tracking-tight text-on-surface">
          Phân quyền theo vai trò (AD10)
        </h1>
        <p className="text-sm text-on-surface-variant">Ranh giới vai trò lấy từ policy và endpoint thực tế của hệ thống.</p>
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

    </div>
  );
};
