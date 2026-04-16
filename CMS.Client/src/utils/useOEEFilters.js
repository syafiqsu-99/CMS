import { ref, watch } from 'vue';

export function useOeeFilters(onFilterChange) {
  const startDate = ref(new Date());
  const endDate = ref(new Date());

  watch([startDate, endDate], ([start, end]) => {
    if (start && end) onFilterChange(start, end);
  });

  return { startDate, endDate };
}
