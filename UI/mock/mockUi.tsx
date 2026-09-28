import { cloneElement, ReactElement, ReactNode } from 'react';

/** Browser stand-in for the game's Tooltip: shows the text as a title. */
export const Tooltip = ({ tooltip, children }: {
  tooltip: ReactNode;
  children: ReactElement;
}) => typeof tooltip === 'string'
  ? cloneElement(children, { title: tooltip })
  : children;
