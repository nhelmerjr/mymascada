'use client';

import React, { useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { FilterCategory } from '@/hooks/use-analytics-category-filter';
import {
  AdjustmentsHorizontalIcon,
  ChevronRightIcon,
  MagnifyingGlassIcon,
  XMarkIcon,
} from '@heroicons/react/24/outline';
import { CheckIcon, MinusIcon } from '@heroicons/react/24/solid';
import { useTranslations } from 'next-intl';

interface CategoryFilterProps {
  categories: FilterCategory[];
  selectedSet: Set<number>;
  childrenByParent: Map<number, number[]>;
  isAllSelected: boolean;
  selectedCount: number;
  totalCount: number;
  onToggle: (id: number) => void;
  onSelectAll: () => void;
  onClear: () => void;
}

type CheckState = 'all' | 'partial' | 'none';

export function CategoryFilter({
  categories,
  selectedSet,
  childrenByParent,
  isAllSelected,
  selectedCount,
  totalCount,
  onToggle,
  onSelectAll,
  onClear,
}: CategoryFilterProps) {
  const t = useTranslations('analytics.categoryFilter');
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');
  const [expanded, setExpanded] = useState<Set<number>>(new Set());

  const parents = useMemo(
    () =>
      categories
        .filter((c) => !c.parentCategoryId)
        .sort((a, b) => a.name.localeCompare(b.name)),
    [categories],
  );

  const childrenOf = useMemo(() => {
    const map = new Map<number, FilterCategory[]>();
    for (const c of categories) {
      if (c.parentCategoryId) {
        const arr = map.get(c.parentCategoryId) ?? [];
        arr.push(c);
        map.set(c.parentCategoryId, arr);
      }
    }
    for (const arr of map.values()) arr.sort((a, b) => a.name.localeCompare(b.name));
    return map;
  }, [categories]);

  const term = search.trim().toLowerCase();

  const parentState = (parentId: number): CheckState => {
    const childIds = childrenByParent.get(parentId) ?? [];
    const total = 1 + childIds.length;
    let checked = selectedSet.has(parentId) ? 1 : 0;
    for (const id of childIds) if (selectedSet.has(id)) checked += 1;
    if (checked === 0) return 'none';
    if (checked === total) return 'all';
    return 'partial';
  };

  const visibleParents = useMemo(() => {
    if (!term) return parents;
    return parents.filter((p) => {
      if (p.name.toLowerCase().includes(term)) return true;
      const kids = childrenOf.get(p.id) ?? [];
      return kids.some((c) => c.name.toLowerCase().includes(term));
    });
  }, [parents, childrenOf, term]);

  const isExpanded = (parentId: number) => expanded.has(parentId) || term.length > 0;

  const toggleExpanded = (parentId: number) => {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(parentId)) next.delete(parentId);
      else next.add(parentId);
      return next;
    });
  };

  const Checkbox = ({ state }: { state: CheckState }) => (
    <span
      className={cn(
        'flex h-4 w-4 shrink-0 items-center justify-center rounded border transition-colors',
        state === 'all' && 'border-primary-500 bg-primary-500 text-white',
        state === 'partial' && 'border-primary-500 bg-primary-100 text-primary-700',
        state === 'none' && 'border-ink-300 bg-white',
      )}
    >
      {state === 'all' && <CheckIcon className="h-3 w-3" />}
      {state === 'partial' && <MinusIcon className="h-3 w-3" />}
    </span>
  );

  const triggerLabel = isAllSelected
    ? t('allCategories')
    : t('nSelected', { count: selectedCount, total: totalCount });

  return (
    <div className="relative">
      <Button
        variant="outline"
        className="gap-2"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
      >
        <AdjustmentsHorizontalIcon className="h-4 w-4" />
        {triggerLabel}
      </Button>

      {open && (
        <>
          {/* Click-away backdrop */}
          <div className="fixed inset-0 z-30" onClick={() => setOpen(false)} aria-hidden="true" />

          <div className="absolute right-0 z-40 mt-2 w-[320px] rounded-2xl border border-ink-200 bg-white/98 p-4 shadow-[0_24px_50px_-30px_rgba(47,129,112,0.30)] backdrop-blur-xs">
            <div className="mb-3 flex items-center justify-between">
              <h3 className="font-[var(--font-dash-sans)] text-sm font-semibold text-ink-900">
                {t('title')}
              </h3>
              <span className="text-xs text-ink-500">
                {t('nOfTotal', { count: selectedCount, total: totalCount })}
              </span>
            </div>

            {/* Search */}
            <div className="relative mb-3">
              <MagnifyingGlassIcon className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-ink-400" />
              <input
                type="text"
                placeholder={t('search')}
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className="w-full rounded-xl border border-ink-200 bg-white pl-9 pr-8 py-2 text-sm text-ink-700 transition-colors placeholder:text-ink-400 focus:border-primary-300 focus:outline-hidden focus:ring-2 focus:ring-primary-200"
              />
              {search && (
                <button
                  type="button"
                  onClick={() => setSearch('')}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-ink-400 hover:text-ink-600"
                >
                  <XMarkIcon className="h-4 w-4" />
                </button>
              )}
            </div>

            {/* Quick actions */}
            <div className="mb-3 flex gap-2">
              <Button
                variant="secondary"
                size="sm"
                onClick={onSelectAll}
                className="h-8 rounded-lg px-3 py-1.5 text-[11px] uppercase tracking-[0.08em]"
              >
                {t('selectAll')}
              </Button>
              <Button
                variant="secondary"
                size="sm"
                onClick={onClear}
                className="h-8 rounded-lg px-3 py-1.5 text-[11px] uppercase tracking-[0.08em]"
              >
                {t('clear')}
              </Button>
            </div>

            {/* Tree */}
            <div className="max-h-[340px] space-y-0.5 overflow-y-auto pr-1">
              {visibleParents.map((parent) => {
                const kids = childrenOf.get(parent.id) ?? [];
                const state = parentState(parent.id);
                const hasKids = kids.length > 0;

                return (
                  <div key={parent.id}>
                    <div className="flex items-center gap-1">
                      {hasKids ? (
                        <button
                          type="button"
                          onClick={() => toggleExpanded(parent.id)}
                          className="flex h-6 w-6 items-center justify-center rounded text-ink-400 hover:text-ink-600"
                          aria-label="toggle"
                        >
                          <ChevronRightIcon
                            className={cn('h-4 w-4 transition-transform', isExpanded(parent.id) && 'rotate-90')}
                          />
                        </button>
                      ) : (
                        <span className="w-6" />
                      )}
                      <button
                        type="button"
                        onClick={() => onToggle(parent.id)}
                        className="flex flex-1 items-center gap-2.5 rounded-xl px-2 py-1.5 text-left hover:bg-primary-50/50"
                      >
                        <Checkbox state={state} />
                        <span
                          className="h-3 w-3 shrink-0 rounded-full"
                          style={{ backgroundColor: parent.color || '#2f8170' }}
                        />
                        <span className="truncate text-sm font-medium text-ink-800">{parent.name}</span>
                      </button>
                    </div>

                    {hasKids && isExpanded(parent.id) && (
                      <div className="ml-7 space-y-0.5 border-l border-ink-100 pl-2">
                        {kids
                          .filter((c) => !term || c.name.toLowerCase().includes(term) || parent.name.toLowerCase().includes(term))
                          .map((child) => (
                            <button
                              key={child.id}
                              type="button"
                              onClick={() => onToggle(child.id)}
                              className="flex w-full items-center gap-2.5 rounded-xl px-2 py-1.5 text-left hover:bg-primary-50/50"
                            >
                              <Checkbox state={selectedSet.has(child.id) ? 'all' : 'none'} />
                              <span
                                className="h-2.5 w-2.5 shrink-0 rounded-full"
                                style={{ backgroundColor: child.color || '#94a3b8' }}
                              />
                              <span className="truncate text-sm text-ink-700">{child.name}</span>
                            </button>
                          ))}
                      </div>
                    )}
                  </div>
                );
              })}

              {visibleParents.length === 0 && (
                <p className="py-4 text-center text-sm text-ink-500">{t('noMatches')}</p>
              )}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
