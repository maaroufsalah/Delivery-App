'use client';

import { ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { Button } from '@/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

const PAGE_SIZES = [10, 25, 50, 100];

interface PaginatedTable {
  /** v9 expose l’état via `state`, il n’y a plus de `getState()`. */
  state: { pagination: { pageIndex: number; pageSize: number } };
  getRowCount: () => number;
  getCanPreviousPage: () => boolean;
  getCanNextPage: () => boolean;
  firstPage: () => void;
  previousPage: () => void;
  nextPage: () => void;
  lastPage: () => void;
  setPageSize: (size: number) => void;
}

/**
 * Pagination compacte : nombre de lignes, plage affichée, 4 boutons.
 * Sur mobile, seuls précédent / suivant restent visibles.
 */
export function DataTablePagination({ table }: { table: PaginatedTable }) {
  const t = useTranslations('table');
  const { pageIndex, pageSize } = table.state.pagination;
  const total = table.getRowCount();
  const from = total === 0 ? 0 : pageIndex * pageSize + 1;
  const to = Math.min((pageIndex + 1) * pageSize, total);

  return (
    <div className="bg-card dense:py-2 flex flex-wrap items-center justify-between gap-3 border-t px-4 py-3">
      <div className="flex items-center gap-2">
        <span className="text-muted-foreground text-xs">{t('rowsPerPage')}</span>
        <Select
          value={String(pageSize)}
          onValueChange={(value) => table.setPageSize(Number(value))}
        >
          <SelectTrigger size="sm" className="h-8 w-[4.5rem]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {PAGE_SIZES.map((size) => (
              <SelectItem key={size} value={String(size)}>
                {size}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <div className="flex items-center gap-3">
        <span className="text-muted-foreground text-xs tabular-nums">
          {t('showing', { from, to, total })}
        </span>

        <div className="flex items-center gap-1">
          <Button
            variant="outline"
            size="icon"
            className="hidden size-8 sm:inline-flex"
            onClick={() => table.firstPage()}
            disabled={!table.getCanPreviousPage()}
            aria-label={t('first')}
          >
            <ChevronsLeft className="flip-rtl size-4" />
          </Button>
          <Button
            variant="outline"
            size="icon"
            className="size-8"
            onClick={() => table.previousPage()}
            disabled={!table.getCanPreviousPage()}
            aria-label={t('previous')}
          >
            <ChevronLeft className="flip-rtl size-4" />
          </Button>
          <Button
            variant="outline"
            size="icon"
            className="size-8"
            onClick={() => table.nextPage()}
            disabled={!table.getCanNextPage()}
            aria-label={t('next')}
          >
            <ChevronRight className="flip-rtl size-4" />
          </Button>
          <Button
            variant="outline"
            size="icon"
            className="hidden size-8 sm:inline-flex"
            onClick={() => table.lastPage()}
            disabled={!table.getCanNextPage()}
            aria-label={t('last')}
          >
            <ChevronsRight className="flip-rtl size-4" />
          </Button>
        </div>
      </div>
    </div>
  );
}
