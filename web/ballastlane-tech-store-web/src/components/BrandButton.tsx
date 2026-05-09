import clsx from "clsx";
import type { ButtonHTMLAttributes, ReactNode } from "react";

// Brand CTA: a 2px orange gradient ring around a themed fill that "wipes" away
// on hover, exposing the orange underneath. Fill + text colour swap by theme so
// idle state stays legible in light (white fill / orange text) and dark
// (near-black fill / white text); both transition to white-on-orange on hover.
interface Props extends ButtonHTMLAttributes<HTMLButtonElement> {
  children: ReactNode;
}

export function BrandButton({ children, className, ...rest }: Props) {
  return (
    <button
      {...rest}
      className={clsx(
        "group relative inline-block h-11 cursor-pointer overflow-hidden whitespace-nowrap rounded-full p-[2px] font-semibold leading-[44px]",
        "bg-gradient-to-r from-[#fd450b] to-[#fd7f0b]",
        "transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2 focus-visible:ring-offset-background",
        "disabled:pointer-events-none disabled:opacity-50",
        className,
      )}
    >
      <span
        aria-hidden="true"
        className="pointer-events-none absolute inset-[2px] flex justify-end overflow-hidden rounded-full"
      >
        <span className="h-full w-full bg-white dark:bg-[#0b0b0b] transition-[width] duration-300 ease-out group-hover:w-0" />
      </span>
      <span className="relative flex h-full items-center justify-center px-5 text-sm transition-colors text-[#fd450b] dark:text-white group-hover:text-white">
        {children}
      </span>
    </button>
  );
}
