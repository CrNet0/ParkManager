import styles from "../panel.module.less";

/*
 * Cohtml's game font lacks many symbol glyphs (↻, arrows, ✦), which then
 * render as nothing. Inline SVG icons draw the same everywhere.
 */

const stroke = {
  fill: "none", stroke: "currentColor", strokeWidth: 1.8,
  strokeLinecap: "round" as const, strokeLinejoin: "round" as const,
};

/** Circular arrow for "roll again" actions. */
export const ReloadIcon = () => (
  <svg className={styles.icon} viewBox="0 0 16 16" aria-hidden="true" focusable="false">
    <path d="M13 8a5 5 0 1 1-1.5-3.6" {...stroke} />
    <path d="M12 1.8v3h-3" {...stroke} />
  </svg>
);

export type Direction = "up" | "down" | "left" | "right";
const rotation: Record<Direction, number> = { down: 0, left: 90, up: 180, right: 270 };

/** Chevron pointing in one direction; "down" is the default drawing. */
export const ChevronIcon = ({ direction }: { direction: Direction }) => (
  <svg className={styles.icon} viewBox="0 0 12 12" aria-hidden="true" focusable="false"
    style={{ transform: `rotate(${rotation[direction]}deg)` }}>
    <path d="M2.5 4.5 6 8l3.5-3.5" {...stroke} />
  </svg>
);

/** Placeholder for an asset without a usable thumbnail. */
export const PlaceholderIcon = () => (
  <svg className={styles.placeholderIcon} viewBox="0 0 16 16" aria-hidden="true"
    focusable="false">
    <path d="M8 1.5 9.6 6.4 14.5 8 9.6 9.6 8 14.5 6.4 9.6 1.5 8 6.4 6.4Z"
      fill="currentColor" />
  </svg>
);
