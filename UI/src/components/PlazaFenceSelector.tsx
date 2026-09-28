import { AssetChoiceOption } from "../assetChoices";
import { Texts } from "../i18n";
import styles from "../panel.module.less";
import { AssetTileGroup } from "./AssetTileGroup";

type Props = {
  t: Texts;
  options: AssetChoiceOption[];
  selected: string;
  enabled: boolean;
  disabled: boolean;
  onSelect: (name: string | null) => void;
  onTooltip: (event: React.MouseEvent<HTMLButtonElement>, name: string) => void;
  onTooltipClose: () => void;
};

/** Optional plaza fence; the "—" tile disables it. */
export const PlazaFenceSelector = ({ t, options, selected, enabled, disabled,
  onSelect, onTooltip, onTooltipClose }: Props) =>
  <AssetTileGroup title={t.plazaFence}
    testIds={{ group: "plaza-fence-group", header: "plaza-fence-group-header",
      choices: "plaza-fence-choices" }}
    choicesClassName={styles.plazaCenterChoices}
    tileClassName={styles.plazaCenterChoice}
    activeClassName={styles.plazaCenterChoiceActive}
    options={options} isActive={(name) => enabled && selected === name}
    onSelect={onSelect}
    none={{ label: t.plazaNoFence, active: !enabled, onSelect: () => onSelect(null) }}
    disabled={disabled} onTooltip={onTooltip} onTooltipClose={onTooltipClose} />;
