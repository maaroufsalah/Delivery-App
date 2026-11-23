/**
 * Réexports du client HTTP.
 *
 * Ce fichier était le point de bascule mocks → API annoncé depuis l'étape 1 ;
 * la bascule a eu lieu. Il ne reste que les réexports, pour que les écrans qui
 * attrapent `ApiError` n'aient pas à connaître `lib/http`.
 */
export { ApiError, apiFetch, publicFetch, type ApiErrorCode } from '@/lib/http';
