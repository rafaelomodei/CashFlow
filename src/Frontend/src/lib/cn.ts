/** Joins class names, dropping the falsy ones produced by conditionals. */
export function cn(...classes: Array<string | false | null | undefined>): string {
  return classes.filter(Boolean).join(' ');
}
