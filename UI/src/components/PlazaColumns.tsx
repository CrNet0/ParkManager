import { useValue } from "cs2/api";
import { AssetChoiceMap, AssetChoiceOption, NO_PLAZA_CENTER } from "../assetChoices";
import { generateDecorations, generatePaths, plazaArrangementJson$,
  selectAsset, selectPlazaCenter, setFurnitureDensity,
  setPlazaArrangementPlacement, setPlazaArrangementSpacing,
  setPlazaCenterPlacement, setPlazaCenterpieceSpacing,
  setPlazaFenceEnabled } from "../bindings";
import { Texts } from "../i18n";
import { AssetIcon, useIconFailures } from "./AssetIcon";
import { AssetTileGroup } from "./AssetTileGroup";
import { Column, DropdownCaret, HeadAction, Segmented, SliderRow } from "./Bar";
import { Hint } from "./Hint";
import { arrangementKinds, parseArrangement } from "./PlazaArrangementEditor";
import styles from "../panel.module.less";

type PlacementIcon = "center" | "mirrored" | "axis" | "around" | "boundary";

const PlacementIcon = ({ kind }: { kind: PlacementIcon }) => (
  <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
    {kind === "center" ? <>
      <circle cx="12" cy="12" r="8" fill="none" stroke="currentColor" strokeWidth="1.5" />
      <path d="M12 2v4M12 18v4M2 12h4m12 0h4" stroke="currentColor" strokeWidth="1.5" />
      <circle cx="12" cy="12" r="2.5" fill="currentColor" />
    </> : null}
    {kind === "mirrored" ? <>
      <path d="M12 4v16" stroke="currentColor" strokeWidth="1.5" strokeDasharray="2 2" />
      <circle cx="6" cy="12" r="3" fill="currentColor" />
      <circle cx="18" cy="12" r="3" fill="currentColor" />
    </> : null}
    {kind === "axis" ? <>
      <path d="M4 12h16" stroke="currentColor" strokeWidth="1.5" />
      <circle cx="5" cy="12" r="2.5" fill="currentColor" />
      <circle cx="12" cy="12" r="2.5" fill="currentColor" />
      <circle cx="19" cy="12" r="2.5" fill="currentColor" />
    </> : null}
    {kind === "around" ? <>
      <circle cx="12" cy="12" r="7" fill="none" stroke="currentColor" strokeWidth="1.5" />
      <circle cx="12" cy="3.5" r="2" fill="currentColor" />
      <circle cx="20.5" cy="12" r="2" fill="currentColor" />
      <circle cx="12" cy="20.5" r="2" fill="currentColor" />
      <circle cx="3.5" cy="12" r="2" fill="currentColor" />
    </> : null}
    {kind === "boundary" ? <>
      <path d="M5 5h14v14H5z" fill="none" stroke="currentColor" strokeWidth="1.5" />
      <circle cx="5" cy="5" r="2" fill="currentColor" />
      <circle cx="19" cy="5" r="2" fill="currentColor" />
      <circle cx="19" cy="19" r="2" fill="currentColor" />
      <circle cx="5" cy="19" r="2" fill="currentColor" />
    </> : null}
  </svg>
);

type Props = {
  t: Texts;
  choices: AssetChoiceMap;
  centerOptions: AssetChoiceOption[];
  centerSelected: string;
  centerPlacement: number;
  arrangementPlacement: number;
  centerpieceSpacing: number;
  arrangementSpacing: number;
  fenceEnabled: boolean;
  density: number;
  pathPlanReady: boolean;
  decorationPlanReady: boolean;
  structureLocked: boolean;
  furnishingLocked: boolean;
  canPlan: boolean;
  /** Localized "arrangement does not fit" notice, if any. */
  notice: string | null;
  arrangementOpen: boolean;
  onEditArrangement: () => void;
};

/** Plaza settings: centerpiece, arrangement, surface and fence. */
export const PlazaColumns = ({ t, choices, centerOptions, centerSelected,
  centerPlacement, arrangementPlacement, centerpieceSpacing, arrangementSpacing,
  fenceEnabled, density, pathPlanReady, decorationPlanReady, structureLocked,
  furnishingLocked, canPlan, notice, arrangementOpen, onEditArrangement }: Props) => {
  const icons = useIconFailures();
  const arrangement = parseArrangement(useValue(plazaArrangementJson$));
  const surface = choices.surface;
  const fence = choices.fence;
  const noCenter = centerSelected === NO_PLAZA_CENTER;

  return <>
    <Column title={t.colCenter} tone="toneBlue" testId="column-center"
      action={pathPlanReady ? <HeadAction text={t.variant} hint={t.variantHint}
        disabled={structureLocked || !canPlan} onClick={generatePaths}
        testId="paths-variant" /> : null}>
      <AssetTileGroup testId="plaza-center-choices" options={centerOptions}
        isActive={(name) => name === centerSelected} onSelect={selectPlazaCenter}
        none={{ label: t.plazaNoCenter, active: noCenter,
          onSelect: () => selectPlazaCenter(NO_PLAZA_CENTER) }}
        disabled={structureLocked} />
      <Segmented label={t.plazaCenterPlacementShort} value={centerPlacement}
        disabled={structureLocked} testId="plaza-center-placement"
        onChange={setPlazaCenterPlacement} options={[
          { value: 0, icon: <PlacementIcon kind="center" />, hint: t.plazaCenterSingle },
          { value: 1, icon: <PlacementIcon kind="mirrored" />, hint: t.plazaCenterMirrored },
          { value: 2, icon: <PlacementIcon kind="axis" />, hint: t.plazaCenterAxis },
        ]} />
      <SliderRow label={t.spacing} value={centerpieceSpacing} minimum={5}
        maximum={60} unit=" m" testId="plaza-centerpiece-spacing"
        disabled={structureLocked || noCenter || centerPlacement === 0}
        onChange={setPlazaCenterpieceSpacing} />
    </Column>
    <Column title={t.colArrangement} tone="toneGreen" testId="column-arrangement"
      action={decorationPlanReady ? <HeadAction text={t.rearrange}
        hint={t.rearrangeHint} disabled={furnishingLocked || !pathPlanReady}
        onClick={generateDecorations} testId="furnishing-variant" /> : null}>
      <Segmented label={t.plazaArrangementPlacementShort}
        value={arrangementPlacement} disabled={structureLocked}
        testId="plaza-arrangement-placement"
        onChange={setPlazaArrangementPlacement} options={[
          { value: 0, icon: <PlacementIcon kind="around" />, hint: t.plazaAroundCenter },
          { value: 1, icon: <PlacementIcon kind="boundary" />, hint: t.plazaAlongBoundary },
        ]} />
      <SliderRow label={arrangementPlacement === 0 ? t.toCenter : t.toEdge}
        value={arrangementSpacing} minimum={0} maximum={20} unit=" m"
        disabled={structureLocked} testId="plaza-arrangement-spacing"
        onChange={setPlazaArrangementSpacing} />
      <SliderRow label={t.density} value={density} minimum={25} maximum={200}
        step={25} unit="%" disabled={furnishingLocked}
        testId="plaza-density" onChange={setFurnitureDensity} />
      <div className={styles.settingRow}>
        <span className={styles.settingLabel}>{t.plazaArrangement}</span>
        <Hint text={t.plazaArrangementHelp}>
          <button type="button" data-testid="plaza-arrangement-edit"
            aria-expanded={arrangementOpen}
            className={`${styles.arrangementPreview} ${
              arrangementOpen ? styles.chipOpen : ""}`}
            onClick={onEditArrangement}>
            {arrangement.map((item, index) => {
              const kind = arrangementKinds.find((candidate) =>
                candidate.kind === item.kind) || arrangementKinds[0];
              const asset = choices[kind.key]?.options.find((option) =>
                option.name === item.name);
              return <AssetIcon key={index} icon={asset?.icon} icons={icons}
                className={styles.chipIcon}
                fallback={<b className={styles.chipIcon}>
                  {t.categories[kind.key].charAt(0)}</b>} />;
            })}
            <span className={styles.chipLabel}>{t.editArrangement}</span>
            <DropdownCaret open={arrangementOpen} />
          </button>
        </Hint>
      </div>
      {notice ? <div className={styles.columnNotice} role="alert">{notice}</div> : null}
    </Column>
    <Column title={t.colSurfaceFence} tone="toneAmber" testId="column-surface">
      <AssetTileGroup testId="plaza-surface-choices" label={t.background}
        options={surface?.options ?? []}
        isActive={(name) => surface?.selected === name}
        onSelect={(name) => selectAsset("Surface", name)}
        disabled={structureLocked} />
      <AssetTileGroup testId="plaza-fence-choices" label={t.plazaFence}
        options={fence?.options ?? []}
        isActive={(name) => fenceEnabled && fence?.selected === name}
        onSelect={(name) => { selectAsset("Fence", name); setPlazaFenceEnabled(true); }}
        none={{ label: t.plazaNoFence, active: !fenceEnabled,
          onSelect: () => setPlazaFenceEnabled(false) }}
        disabled={structureLocked} />
    </Column>
  </>;
};
