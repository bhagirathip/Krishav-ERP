import { computed, ref, watch } from 'vue';
import { debounce } from '../utils/debounce';

// Server-side counterpart to useGrid: instead of filtering/sorting/paging a
// fully-loaded array in the browser, this just tracks the page/sort/search
// state and asks `fetchPage(params)` (an async function the screen provides,
// typically an api.get(...) call) for exactly the rows to show. Screens with
// their own extra filters (date range, status tab, etc.) read those directly
// inside their fetchPage closure - this composable only owns the grid state
// that's generic across every screen.
export function useServerGrid(fetchPage, pageSize = 10) {
  const page = ref(1);
  const sortKey = ref('');
  const sortDirection = ref('asc');
  const searchColumn = ref('');
  const searchText = ref('');
  const rows = ref([]);
  const totalCount = ref(0);
  const loading = ref(false);

  const pageCount = computed(() => Math.max(1, Math.ceil(totalCount.value / pageSize)));

  async function reload() {
    loading.value = true;
    try {
      const result = await fetchPage({
        page: page.value,
        pageSize,
        sortBy: sortKey.value,
        sortDirection: sortDirection.value,
        searchColumn: searchColumn.value,
        searchText: searchText.value.trim()
      });
      rows.value = result.rows;
      totalCount.value = result.totalCount;
    } finally {
      loading.value = false;
    }
  }

  // Jumping back to page 1 after a sort/search change should trigger exactly
  // one reload - if we're already on page 1, setting page.value = 1 again
  // wouldn't fire the watcher below, so reload directly in that case instead
  // of double-fetching.
  function resetPageAndReload() {
    if (page.value === 1) reload();
    else page.value = 1;
  }

  function sortBy(key) {
    if (!key) return;
    if (sortKey.value === key) {
      sortDirection.value = sortDirection.value === 'asc' ? 'desc' : 'asc';
    } else {
      sortKey.value = key;
      sortDirection.value = 'asc';
    }
    resetPageAndReload();
  }

  function sortIndicator(key) {
    if (sortKey.value !== key) return '↕';
    return sortDirection.value === 'asc' ? '↑' : '↓';
  }

  watch([searchColumn, searchText], debounce(resetPageAndReload, 350));
  watch(page, reload);

  return {
    page,
    pageCount,
    rows,
    totalCount,
    loading,
    sortKey,
    sortDirection,
    searchColumn,
    searchText,
    sortBy,
    sortIndicator,
    reload
  };
}
