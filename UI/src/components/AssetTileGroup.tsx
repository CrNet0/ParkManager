import { AssetChoiceOption } from "../assetChoices";
import { AssetIcon, useIconFailures } from "./AssetIcon";
import styles from "../panel.module.less";

type Props = {
  testIds: { group: string; header: string; choices: string };
  title: string;
  groupClassName?: string;
  choicesClassName: string;
  tileClassName: string;
  activeClassName: string;
  options: AssetChoiceOption[];
  isActive: (name: string) => boolean;
  onSelect: (name: string) => void;
  /** Optional leading "—" tile, e.g. "no centerpiece" or "no fence". */
  none?: { label: string; active: boolean; onSelect: () => void };
  disabled: boolean;
  onTooltip: (event: React.MouseEvent<HTMLButtonElement>, name: string) => void;
  onTooltipClose: () => void;
};

/** Compact picker of icon tiles used for surfaces, centerpieces and fences. */
export const AssetTileGroup = ({ testIds, title, groupClassName,
  choicesClassName, tileClassName, activeClassName, options, isActive,
  onSelect, none, disabled, onTooltip, onTooltipClose }: Props) => {
  const icons = useIconFailures();
  const tileClass = (active: boolean) =>
    `${tileClassName} ${active ? activeClassName : ""}`.trim();

  return <section className={`${styles.plazaAssetGroup} ${groupClassName ?? ""}`.trim()}
    data-testid={testIds.group}>
    <div className={styles.plazaAssetGroupHeader} data-testid={testIds.header}>
      <strong>{title}</strong>
    </div>
    <div className={choicesClassName} data-testid={testIds.choices}>
      {none ? <button type="button" title={none.label} aria-label={none.label}
        className={tileClass(none.active)} disabled={disabled}
        aria-pressed={none.active} onClick={none.onSelect}>—</button> : null}
      {options.map((option) => {
        const active = isActive(option.name);
        return <button key={option.name} type="button" aria-label={option.name}
          aria-pressed={active} className={tileClass(active)} disabled={disabled}
          onMouseEnter={(event) => onTooltip(event, option.name)}
          onMouseLeave={onTooltipClose}
          onClick={() => onSelect(option.name)}>
          <AssetIcon icon={option.icon} icons={icons}
            fallback={<span className={styles.plazaCenterFallback} aria-hidden="true">
              {option.name.charAt(0).toUpperCase()}
            </span>} />
        </button>;
      })}
    </div>
  </section>;
};
