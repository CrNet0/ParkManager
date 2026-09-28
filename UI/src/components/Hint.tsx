import { ReactElement, ReactNode } from "react";
import * as CS2UI from "cs2/ui";

/*
 * Cohtml has no browser chrome, so `title` attributes never show anything in
 * the game. CS2 ships its own Tooltip component; it is read through the
 * namespace because a missing export must cost only the tooltip, not the UI.
 */
const GameTooltip: any = (CS2UI as any).Tooltip;
const usable = typeof GameTooltip === "function"
  || (typeof GameTooltip === "object" && GameTooltip !== null
    && "$$typeof" in GameTooltip);

/** Wraps one ref-capable element (e.g. a button) with the game tooltip. */
export const Hint = ({ text, children }: {
  text: ReactNode;
  children: ReactElement;
}) => usable && text ? <GameTooltip tooltip={text}>{children}</GameTooltip> : children;
