import { ReactNode, useState } from "react";

/**
 * Remembers thumbnails that failed to load. Many valid game prefabs point to
 * missing icons; a failed URL switches every tile using it to its fallback.
 */
export const useIconFailures = () => {
  const [failed, setFailed] = useState<Record<string, boolean>>({});
  return {
    usable: (icon: string | undefined): icon is string => !!icon && !failed[icon],
    markFailed: (icon: string) => setFailed((current) =>
      current[icon] ? current : { ...current, [icon]: true }),
  };
};

type IconFailures = ReturnType<typeof useIconFailures>;

type Props = {
  icon: string | undefined;
  icons: IconFailures;
  fallback: ReactNode;
  className?: string;
};

/** Asset thumbnail that falls back to a placeholder when it cannot load. */
export const AssetIcon = ({ icon, icons, fallback, className }: Props) =>
  icons.usable(icon)
    ? <img className={className} src={icon} alt=""
        onError={() => icons.markFailed(icon)} />
    : <>{fallback}</>;

/** Scrolls a tile grid by two rows. */
export const scrollTileGrid = (grid: HTMLElement | null, direction: number) => {
  if (!grid) return;
  const tile = grid.querySelector("button");
  grid.scrollTop += direction * (tile ? tile.getBoundingClientRect().height : 78) * 2;
};
