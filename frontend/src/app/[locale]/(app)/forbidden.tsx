import { AccessDenied } from '@/components/layout/access-denied';

/** Une page du back-office refusée par l'API : 403, dans la coque — lot W1 (E2E-009). */
export default function Forbidden() {
  return <AccessDenied />;
}
