// Legge i permessi dal JWT salvato (claim "Permission"). Serve solo a mostrare o nascondere
// parti dell'interfaccia: il controllo vero resta sul backend.
function tokenPayload(): Record<string, unknown> | null {
  try {
    const token = localStorage.getItem('token');
    if (!token) return null;
    return JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
  } catch {
    return null;
  }
}

function claimIncludes(claim: unknown, value: string): boolean {
  return Array.isArray(claim) ? claim.includes(value) : claim === value;
}

export function hasPermission(permission: string): boolean {
  const payload = tokenPayload();
  return !!payload && claimIncludes(payload.Permission ?? payload.permission, permission);
}

const ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

export function hasRole(...roles: string[]): boolean {
  const payload = tokenPayload();
  if (!payload) return false;
  const claim = payload[ROLE_CLAIM] ?? payload.role;
  return roles.some((r) => claimIncludes(claim, r));
}
