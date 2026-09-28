import { AssetChoiceOption, parseOptions } from "../assetChoices";
import { Texts } from "../i18n";
import styles from "../panel.module.less";
import { AssetTileGroup } from "./AssetTileGroup";

/** Sentinel the C# tool uses for "plan without a centerpiece". */
export const NO_PLAZA_CENTER = "__none__";

/**
 * Parses the published centerpiece list. Entries without an icon are kept:
 * a missing or broken thumbnail is a display concern and must not make an
 * asset unselectable or block planning.
 */
export const parsePlazaCenterOptions = (json: string): AssetChoiceOption[] => {
  try {
    const parsed = JSON.parse(json);
    return Array.isArray(parsed) ? parseOptions(parsed) : [];
  } catch {
    return [];
  }
};

type Props = {
  t: Texts;
  options: AssetChoiceOption[];
  selected: string;
  disabled: boolean;
  onSelect: (name: string) => void;
  onTooltip: (event: React.MouseEvent<HTMLButtonElement>, name: string) => void;
  onTooltipClose: () => void;
};

/** Centerpiece choice for plazas, including the "no centerpiece" option. */
export const PlazaCenterSelector = ({ t, options, selected, disabled,
  onSelect, onTooltip, onTooltipClose }: Props) =>
  <AssetTileGroup title={t.plazaCenter} groupClassName={styles.plazaCenterGroup}
    testIds={{ group: "plaza-center-group", header: "plaza-center-header",
      choices: "plaza-center-choices" }}
    choicesClassName={styles.plazaCenterChoices}
    tileClassName={styles.plazaCenterChoice}
    activeClassName={styles.plazaCenterChoiceActive}
    options={options} isActive={(name) => name === selected}
    onSelect={onSelect}
    none={{ label: t.plazaNoCenter, active: selected === NO_PLAZA_CENTER,
      onSelect: () => onSelect(NO_PLAZA_CENTER) }}
    disabled={disabled} onTooltip={onTooltip} onTooltipClose={onTooltipClose} />;
