import { useEffect, useRef, useState, type ReactNode } from "react";
import clsx from "clsx";

interface Props<T extends string> {
  current: T;
  options: T[];
  onPick: (value: T) => void;
  // The badge (or any clickable trigger) that should open the popover.
  trigger: ReactNode;
  disabled?: boolean;
}

// Click-to-edit popover anchored to its trigger. Used as a drop-in around badges
// so a status column doubles as an inline editor.
export function StatusPicker<T extends string>({ current, options, onPick, trigger, disabled }: Props<T>) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onClick = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") setOpen(false); };
    document.addEventListener("mousedown", onClick);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onClick);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  return (
    <div ref={ref} className="relative inline-block">
      <button
        type="button"
        disabled={disabled || options.length === 0}
        onClick={() => setOpen((v) => !v)}
        title={options.length === 0 ? "No transitions available" : "Change status"}
        className={clsx(
          "rounded-full transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
          (disabled || options.length === 0) ? "cursor-not-allowed" : "cursor-pointer hover:brightness-110",
        )}
      >
        {trigger}
      </button>

      {open && options.length > 0 && (
        <div
          role="menu"
          className="absolute left-0 top-full z-40 mt-1 min-w-[10rem] overflow-hidden rounded-md border bg-popover text-popover-foreground shadow-lg"
        >
          {options.map((opt) => (
            <button
              key={opt}
              role="menuitem"
              onClick={() => { setOpen(false); onPick(opt); }}
              className={clsx(
                "block w-full px-3 py-1.5 text-left text-sm hover:bg-accent hover:text-accent-foreground",
                opt === current && "font-semibold",
              )}
            >
              {opt}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
