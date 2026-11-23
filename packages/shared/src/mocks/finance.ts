import type { Bank, BankAccount } from '../types/finance';

/**
 * Référentiel bancaire mocké. Banques marocaines réelles, comptes inventés :
 * aucun RIB de production n'entre dans le dépôt.
 *
 * Le legacy avait 14 banques et 2 comptes (DOMAIN.md §1.16). On garde les
 * mêmes ordres de grandeur : une liste qu'un administrateur reconnaît, et
 * assez de comptes pour que l'écran de règlement ait un choix à offrir.
 */

export const mockBanks: Bank[] = [
  { id: 'bank-awb', name: 'Attijariwafa Bank', code: 'AWB', isActive: true },
  { id: 'bank-bp', name: 'Banque Populaire', code: 'BP', isActive: true },
  { id: 'bank-boa', name: 'Bank of Africa', code: 'BOA', isActive: true },
  { id: 'bank-bmci', name: 'BMCI', code: 'BMCI', isActive: true },
  { id: 'bank-sg', name: 'Société Générale Maroc', code: 'SGMA', isActive: true },
  { id: 'bank-cih', name: 'CIH Bank', code: 'CIH', isActive: true },
  { id: 'bank-cam', name: 'Crédit Agricole du Maroc', code: 'CAM', isActive: true },
  { id: 'bank-cdm', name: 'Crédit du Maroc', code: 'CDM', isActive: true },
  { id: 'bank-cfg', name: 'CFG Bank', code: 'CFG', isActive: true },
  { id: 'bank-albarid', name: 'Al Barid Bank', code: 'ABB', isActive: true },
  { id: 'bank-umnia', name: 'Umnia Bank', code: 'UMN', isActive: true },
  { id: 'bank-bti', name: 'Bank Al Yousr', code: 'BAY', isActive: false },
];

export const mockBankAccounts: BankAccount[] = [
  {
    id: 'account-1',
    bankId: 'bank-awb',
    bankName: 'Attijariwafa Bank',
    label: 'Compte courant, siège',
    rib: '007780000123456789012345',
    holder: 'Delivery Demo SARL',
    isActive: true,
    isDefault: true,
  },
  {
    id: 'account-2',
    bankId: 'bank-cih',
    bankName: 'CIH Bank',
    label: 'Compte encaissement COD',
    rib: '230780000987654321098765',
    holder: 'Delivery Demo SARL',
    isActive: true,
    isDefault: false,
  },
];
