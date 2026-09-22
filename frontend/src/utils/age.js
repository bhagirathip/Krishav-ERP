const ageUnitAbbr = { Years: 'Yr', Months: 'Mo', Weeks: 'Wk', Days: 'Day' };

export function ageText(patient) {
  if (!patient || patient.age === null || patient.age === undefined) return '';
  return `${patient.age} ${ageUnitAbbr[patient.ageUnit] || 'Yr'}`;
}
