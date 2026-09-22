import { computed, ref, watch } from 'vue';

function getValue(object, path) {
  if (!path) return object;
  return String(path)
    .split('.')
    .reduce((value, key) => value == null ? null : value[key], object);
}

function flatten(value) {
  if (value == null) return '';
  if (Array.isArray(value)) return value.map(flatten).join(' ');
  if (typeof value === 'object') return Object.values(value).map(flatten).join(' ');
  return String(value);
}

function compare(a, b) {
  if (a == null && b == null) return 0;
  if (a == null) return 1;
  if (b == null) return -1;

  const an = Number(a);
  const bn = Number(b);
  if (!Number.isNaN(an) && !Number.isNaN(bn) && String(a).trim() !== '' && String(b).trim() !== '') {
    return an - bn;
  }

  const ad = Date.parse(a);
  const bd = Date.parse(b);
  if (!Number.isNaN(ad) && !Number.isNaN(bd) && /[-T:/]/.test(String(a))) {
    return ad - bd;
  }

  return String(a).localeCompare(String(b), undefined, {
    numeric: true,
    sensitivity: 'base'
  });
}

export function useGrid(source, pageSize = 10) {
  const search = ref('');
  const sortKey = ref('');
  const sortDirection = ref('asc');
  const page = ref(1);

  const filteredRows = computed(() => {
    const query = search.value.trim().toLowerCase();
    if (!query) return [...(source.value || [])];
    return (source.value || []).filter(row => flatten(row).toLowerCase().includes(query));
  });

  const sortedRows = computed(() => {
    const rows = [...filteredRows.value];
    if (!sortKey.value) return rows;
    const direction = sortDirection.value === 'desc' ? -1 : 1;
    rows.sort((left, right) => direction * compare(
      getValue(left, sortKey.value),
      getValue(right, sortKey.value)
    ));
    return rows;
  });

  const pageCount = computed(() => Math.max(1, Math.ceil(sortedRows.value.length / pageSize)));
  const pagedRows = computed(() => sortedRows.value.slice((page.value - 1) * pageSize, page.value * pageSize));

  function sortBy(key) {
    if (!key) return;
    if (sortKey.value === key) {
      sortDirection.value = sortDirection.value === 'asc' ? 'desc' : 'asc';
    } else {
      sortKey.value = key;
      sortDirection.value = 'asc';
    }
  }

  function sortIndicator(key) {
    if (sortKey.value !== key) return '↕';
    return sortDirection.value === 'asc' ? '↑' : '↓';
  }

  watch([search, sortKey, sortDirection], () => {
    page.value = 1;
  });

  watch(source, () => {
    if (page.value > pageCount.value) page.value = pageCount.value;
  }, { deep: true });

  return {
    search,
    sortKey,
    sortDirection,
    page,
    filteredRows,
    sortedRows,
    pageCount,
    pagedRows,
    sortBy,
    sortIndicator
  };
}
