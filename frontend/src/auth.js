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

// The login response stores the user's AppRole.Name under the (confusingly
// named, but functionally correct) "designation" key - this just gives it a
// clearer name for callers that actually care about the role, like the Bulk
// Discount gating on the Bill screen (Manager/Administrator/General Manager).
export function role() {
  return sessionStorage.getItem('designation') || '';
}
