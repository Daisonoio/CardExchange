// Legge i permessi dal JWT salvato (claim "Permission"). Serve solo a mostrare o nascondere
// parti dell'interfaccia: il controllo vero resta sul backend.
export function hasPermission(permission: string): boolean {
  try {
    const token = localStorage.getItem('token');
    if (!token) return false;
    const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
    const claim = payload.Permission ?? payload.permission;
    return Array.isArray(claim) ? claim.includes(permission) : claim === permission;
  } catch {
    return false;
  }
}
