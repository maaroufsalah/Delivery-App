import { notFound } from 'next/navigation';
import { ExchangeDetailScreen } from '@/features/parcels/exchange-detail';
import { getExchange } from '@/lib/api/exchanges';
import { ApiError } from '@/lib/http';

export default async function ExchangePage({ params }: { params: Promise<{ code: string }> }) {
  const { code } = await params;

  const exchange = await getExchange(decodeURIComponent(code)).catch((error: unknown) => {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  });

  return <ExchangeDetailScreen exchange={exchange} />;
}
