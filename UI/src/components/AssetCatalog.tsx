import { ReactNode, useEffect, useRef } from "react";
import { AssetChoiceMap, assetCategories } from "../assetChoices";
import { selectAsset, toggleDecorationCategory } from "../bindings";
import { AssetIcon, scrollTileGrid, useIconFailures } from "./AssetIcon";
import { DropdownCaret } from "./Bar";
import { Hint } from "./Hint";
import { ChevronIcon, PlaceholderIcon } from "./Icons";
import { Texts } from "../i18n";
import styles from "../panel.module.less";

export type CategoryKey = typeof assetCategories[number]["key"];

const categoryOf = (key: string) =>
  assetCategories.find((item) => item.key === key) || assetCategories[0];

const selectionCount = (choices: AssetChoiceMap, key: string) => {
  const category = categoryOf(key);
  const choice = choices[key];
  const count = category.multi ? choice?.selectedMany.length ?? 0
    : choice?.selected ? 1 : 0;
  return count === 0 ? "" : String(count);
};

/**
 * One chip per furnishing category: the box includes or excludes the
 * category, the chip itself opens its asset choice below the bar.
 */
export const CategoryChips = ({ t, keys, choices, enabledMask, busy, openKey,
  onOpen, children }: {
  t: Texts;
  keys: CategoryKey[];
  choices: AssetChoiceMap;
  enabledMask: number;
  busy: boolean;
  openKey: string | null;
  onOpen: (key: string) => void;
  children?: ReactNode;
}) => {
  const icons = useIconFailures();
  return <div className={styles.chips}>
    {keys.map((key) => {
      const category = categoryOf(key);
      const choice = choices[key];
      const label = t.categories[key];
      const enabled = (enabledMask & (1 << (category.kind - 1))) !== 0;
      const shown = choice?.options.find((option) =>
        (category.multi ? choice.selectedMany.includes(option.name)
          : option.name === choice.selected) && icons.usable(option.icon));
      return <div key={key} data-testid={`chip-${key}`}
        className={`${styles.chip} ${openKey === key ? styles.chipOpen : ""} ${
          enabled ? "" : styles.chipOff}`}>
        <Hint text={t.toggleCategory(label, enabled)}>
          <button type="button" className={`${styles.chipCheck} ${
            enabled ? styles.chipCheckOn : ""}`} disabled={busy}
            aria-pressed={enabled} aria-label={t.toggleCategory(label, enabled)}
            onClick={() => toggleDecorationCategory(category.kind)}>
            {enabled ? "✓" : ""}</button>
        </Hint>
        <Hint text={t.openAssets(label)}>
          <button type="button" className={styles.chipBody}
            aria-label={t.openAssets(label)} aria-expanded={openKey === key}
            onClick={() => onOpen(key)}>
            <AssetIcon icon={shown?.icon} icons={icons} className={styles.chipIcon}
              fallback={null} />
            <span className={styles.chipLabel}>{label}</span>
            {selectionCount(choices, key)
              ? <span className={styles.chipCount}>{selectionCount(choices, key)}</span>
              : null}
            <DropdownCaret open={openKey === key} />
          </button>
        </Hint>
      </div>;
    })}
    {children}
  </div>;
};

/** Asset grid of one category, shown in the window below the bar. */
export const AssetChooser = ({ t, choices, categoryKey, busy }: {
  t: Texts;
  choices: AssetChoiceMap;
  categoryKey: string;
  busy: boolean;
}) => {
  const icons = useIconFailures();
  const gridRef = useRef<HTMLDivElement | null>(null);
  const category = categoryOf(categoryKey);
  const choice = choices[category.key];

  useEffect(() => {
    if (gridRef.current) gridRef.current.scrollTop = 0;
  }, [categoryKey]);

  if (!choice) return <div className={styles.windowEmpty}>{t.chooseCategory}</div>;
  return <div className={styles.assetChoiceBody} data-testid="asset-chooser">
    <div className={styles.assetChoiceGrid} ref={gridRef}>
      {[{ name: "", icon: "" }, ...choice.options].map((option) => {
        const active = option.name ? (category.multi
          ? choice.selectedMany.includes(option.name)
          : choice.selected === option.name) : (category.multi
            ? choice.selectedMany.length === 0 : !choice.selected);
        const label = option.name || t.automatic;
        return <Hint key={option.name || "__automatic"} text={label}>
          <button type="button"
            className={`${styles.assetChoiceTile} ${active ? styles.assetChoiceTileActive : ""}`}
            disabled={busy} aria-label={label} aria-pressed={active}
            onClick={() => selectAsset(category.payload, option.name, category.multi)}>
            <AssetIcon icon={option.icon} icons={icons}
              className={styles.assetChoiceIcon}
              fallback={<span className={styles.assetChoiceFallback}><PlaceholderIcon /></span>} />
            <span className={styles.assetChoiceName}>{label}</span>
            {active ? <span className={styles.assetChoiceCheck}>✓</span> : null}
          </button>
        </Hint>;
      })}
    </div>
    <div className={styles.assetScrollControls}>
      <button type="button" aria-label={t.scrollAssetsUp}
        onClick={() => scrollTileGrid(gridRef.current, -1)}><ChevronIcon direction="up" /></button>
      <button type="button" aria-label={t.scrollAssetsDown}
        onClick={() => scrollTileGrid(gridRef.current, 1)}><ChevronIcon direction="down" /></button>
    </div>
  </div>;
};
