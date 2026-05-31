'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { apiClient } from '@/lib/api-client';

export interface FilterCategory {
  id: number;
  name: string;
  color?: string;
  type: number;
  parentCategoryId?: number;
}

const STORAGE_KEY = 'mymascada.analytics.categoryFilter';

function readStoredIds(): number[] | null {
  if (typeof window === 'undefined') return null;
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw);
    if (Array.isArray(parsed) && parsed.every((x) => typeof x === 'number')) {
      return parsed as number[];
    }
    return null;
  } catch {
    return null;
  }
}

/**
 * Shared category filter for the analytics screens.
 *
 * - Loads the full category tree (parents + subcategories, income + expense).
 * - Persists the latest selection in localStorage (per browser).
 * - Default when nothing is stored: ALL categories selected.
 * - `categoryIdsParam` is what gets sent to the reports API:
 *     - `undefined` when all are selected (no filter → backend returns everything)
 *     - the comma-joined ids for a subset
 *     - `'0'` when nothing is selected (matches no category → empty results)
 */
export function useAnalyticsCategoryFilter() {
  const [categories, setCategories] = useState<FilterCategory[]>([]);
  const [selectedIds, setSelectedIds] = useState<number[]>([]);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const data = (await apiClient.getCategories({
          includeSystemCategories: true,
          includeInactive: false,
          includeHierarchy: false,
        })) as FilterCategory[];

        if (cancelled) return;

        const cats = Array.isArray(data) ? data : [];
        setCategories(cats);

        const allIds = cats.map((c) => c.id);
        const stored = readStoredIds();
        // Reconcile stored ids with current categories; default = all when nothing valid is stored.
        const reconciled = stored ? stored.filter((id) => allIds.includes(id)) : null;
        setSelectedIds(reconciled && reconciled.length > 0 ? reconciled : allIds);
      } catch {
        if (!cancelled) setCategories([]);
      } finally {
        if (!cancelled) setLoaded(true);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  // Persist whenever the selection changes (after the initial load).
  useEffect(() => {
    if (!loaded || typeof window === 'undefined') return;
    try {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify(selectedIds));
    } catch {
      // Ignore quota / serialization errors — persistence is best-effort.
    }
  }, [selectedIds, loaded]);

  const allIds = useMemo(() => categories.map((c) => c.id), [categories]);

  const childrenByParent = useMemo(() => {
    const map = new Map<number, number[]>();
    for (const c of categories) {
      if (c.parentCategoryId) {
        const arr = map.get(c.parentCategoryId) ?? [];
        arr.push(c.id);
        map.set(c.parentCategoryId, arr);
      }
    }
    return map;
  }, [categories]);

  const selectedSet = useMemo(() => new Set(selectedIds), [selectedIds]);

  const isAllSelected = loaded && allIds.length > 0 && selectedIds.length === allIds.length;

  /**
   * Toggle a category. Selecting/deselecting a parent cascades to all of its subcategories;
   * a subcategory can still be toggled on its own.
   */
  const toggleCategory = useCallback(
    (id: number) => {
      setSelectedIds((prev) => {
        const set = new Set(prev);
        const affected = [id, ...(childrenByParent.get(id) ?? [])];
        const willSelect = !set.has(id);
        if (willSelect) affected.forEach((x) => set.add(x));
        else affected.forEach((x) => set.delete(x));
        return Array.from(set);
      });
    },
    [childrenByParent],
  );

  const selectAll = useCallback(() => setSelectedIds(allIds), [allIds]);
  const clear = useCallback(() => setSelectedIds([]), []);

  const categoryIdsParam = useMemo(() => {
    if (!loaded || allIds.length === 0) return undefined;
    if (selectedIds.length === allIds.length) return undefined; // all selected → no filter
    if (selectedIds.length === 0) return '0'; // none selected → match nothing
    return selectedIds.join(',');
  }, [loaded, allIds, selectedIds]);

  return {
    categories,
    selectedIds,
    selectedSet,
    childrenByParent,
    isAllSelected,
    loaded,
    toggleCategory,
    selectAll,
    clear,
    categoryIdsParam,
  };
}
