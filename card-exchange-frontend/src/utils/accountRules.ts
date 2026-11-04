/** Regole di username e password: le stesse del server (AuthValidators), per avvisare prima dell'invio. */

/** Almeno 8 caratteri, una maiuscola, una minuscola, un numero e un simbolo qualsiasi (! # . - @ € …). */
export const PASSWORD_RULE = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d\s]).{8,}$/;

/** Lettere, numeri, punto, trattino e underscore; da 3 a 50 caratteri. */
export const USERNAME_RULE = /^[a-zA-Z0-9._-]{3,50}$/;

export type PasswordProblem = 'length' | 'upper' | 'lower' | 'digit' | 'symbol';

/** Il primo requisito mancante, per un messaggio preciso (null se la password va bene). */
export function passwordProblem(password: string): PasswordProblem | null {
  if (password.length < 8) return 'length';
  if (!/[A-Z]/.test(password)) return 'upper';
  if (!/[a-z]/.test(password)) return 'lower';
  if (!/\d/.test(password)) return 'digit';
  if (!/[^A-Za-z\d\s]/.test(password)) return 'symbol';
  return null;
}
