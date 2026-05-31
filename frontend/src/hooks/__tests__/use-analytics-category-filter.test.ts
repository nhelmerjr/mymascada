import { renderHook, act, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useAnalyticsCategoryFilter } from '../use-analytics-category-filter';

vi.mock('@/lib/api-client', () => ({
  apiClient: {
    getCategories: vi.fn(),
  },
}));

import { apiClient } from '@/lib/api-client';

const SAMPLE = [
  { id: 1, name: 'Food', type: 2 },
  { id: 2, name: 'Groceries', type: 2, parentCategoryId: 1 },
  { id: 3, name: 'Dining', type: 2, parentCategoryId: 1 },
  { id: 4, name: 'Salary', type: 1 },
];

const STORAGE_KEY = 'mymascada.analytics.categoryFilter';

describe('useAnalyticsCategoryFilter', () => {
  beforeEach(() => {
    window.localStorage.clear();
    (apiClient.getCategories as ReturnType<typeof vi.fn>).mockResolvedValue(SAMPLE);
  });

  it('defaults to all categories selected when nothing is stored', async () => {
    const { result } = renderHook(() => useAnalyticsCategoryFilter());

    await waitFor(() => expect(result.current.loaded).toBe(true));

    expect(result.current.selectedIds.sort()).toEqual([1, 2, 3, 4]);
    expect(result.current.isAllSelected).toBe(true);
    // All selected → no filter is sent to the API.
    expect(result.current.categoryIdsParam).toBeUndefined();
  });

  it('cascades a parent toggle to its subcategories', async () => {
    const { result } = renderHook(() => useAnalyticsCategoryFilter());
    await waitFor(() => expect(result.current.loaded).toBe(true));

    act(() => result.current.toggleCategory(1)); // deselect "Food" (parent)

    // Food (1) + Groceries (2) + Dining (3) all removed; only Salary (4) remains.
    expect(result.current.selectedIds.sort()).toEqual([4]);
    expect(result.current.isAllSelected).toBe(false);
    expect(result.current.categoryIdsParam).toBe('4');
  });

  it('persists the selection to localStorage and restores it on a new instance', async () => {
    const first = renderHook(() => useAnalyticsCategoryFilter());
    await waitFor(() => expect(first.result.current.loaded).toBe(true));

    act(() => first.result.current.toggleCategory(1)); // leaves only [4]

    await waitFor(() =>
      expect(JSON.parse(window.localStorage.getItem(STORAGE_KEY) || '[]').sort()).toEqual([4]),
    );

    // A brand-new instance must restore the stored selection.
    const second = renderHook(() => useAnalyticsCategoryFilter());
    await waitFor(() => expect(second.result.current.loaded).toBe(true));
    expect(second.result.current.selectedIds).toEqual([4]);
    expect(second.result.current.categoryIdsParam).toBe('4');
  });

  it('sends the "0" sentinel when nothing is selected', async () => {
    const { result } = renderHook(() => useAnalyticsCategoryFilter());
    await waitFor(() => expect(result.current.loaded).toBe(true));

    act(() => result.current.clear());

    expect(result.current.selectedIds).toEqual([]);
    expect(result.current.categoryIdsParam).toBe('0');
  });
});
