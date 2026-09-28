import { AssetChoiceOption } from "../assetChoices";
import { AssetIcon, useIconFailures } from "./AssetIcon";
import { Hint } from "./Hint";
import styles from "../panel.module.less";

type Props = {
  testId: string;
  label?: string;
  options: AssetChoiceOption[];
  isActive: (name: string) => boolean;
  onSelect: (name: string) => void;
  /** Optional leading "—" tile, e.g. "no centerpiece" or "no fence". */
  none?: { label: string; active: boolean; onSelect: () => void };
  disabled: boolean;
};

/** Compact row of icon tiles used for surfaces, centerpieces and fences. */
export const AssetTileGroup = ({ testId, label, options, isActive, onSelect,
  none, disabled }: Props) => {
  const icons = useIconFailures();
  const tileClass = (active: boolean) =>
    `${styles.tile} ${active ? styles.tileActive : ""}`;

  return <div className={styles.tileRow}>
    {label ? <span className={styles.settingLabel}>{label}</span> : null}
    <div className={styles.tiles} data-testid={testId}>
      {none ? <Hint text={none.label}>
        <button type="button" aria-label={none.label}
          className={tileClass(none.active)} disabled={disabled}
          aria-pressed={none.active} onClick={none.onSelect}>—</button>
      </Hint> : null}
      {options.map((option) => {
        const active = isActive(option.name);
        return <Hint key={option.name} text={option.name}>
          <button type="button" aria-label={option.name}
            aria-pressed={active} className={tileClass(active)} disabled={disabled}
            onClick={() => onSelect(option.name)}>
            <AssetIcon icon={option.icon} icons={icons}
              fallback={<span className={styles.tileFallback} aria-hidden="true">
                {option.name.charAt(0).toUpperCase()}
              </span>} />
          </button>
        </Hint>;
      })}
    </div>
  </div>;
};
