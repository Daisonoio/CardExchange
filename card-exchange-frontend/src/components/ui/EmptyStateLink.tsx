import { Link } from 'react-router-dom';

/** Pulsante per gli stati vuoti: dice cosa fare dopo e porta lì. */
export default function EmptyStateLink({ to, label }: { to: string; label: string }) {
  return (
    <Link to={to} className="inline-flex items-center justify-center gap-1.5 px-4 py-2 rounded-xl bg-primary text-white text-sm font-semibold hover:bg-primary-light">
      {label}
    </Link>
  );
}
