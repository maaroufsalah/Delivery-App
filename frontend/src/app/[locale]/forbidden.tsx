import { AccessDenied } from '@/components/layout/access-denied';

/** Refus hors du back-office (espace livreur, console) : 403, plein écran — lot W1 (E2E-009). */
export default function Forbidden() {
  return <AccessDenied fullScreen />;
}
