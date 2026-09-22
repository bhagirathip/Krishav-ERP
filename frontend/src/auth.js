export function permissions() {
  try {
    return JSON.parse(
      sessionStorage.getItem('permissions') || '[]'
    );
  } catch {
    return [];
  }
}

export function can(module, action = 'view') {
  const permission = permissions()
    .find(x => x.module === module);

  if (!permission) {
    return false;
  }

  const map = {
    view: 'canView',
    add: 'canAdd',
    edit: 'canEdit',
    delete: 'canDelete'
  };

  return !!permission[map[action]];
}
