import { AssetChoiceMap } from "../assetChoices";
import { generateDecorations, generatePaths, selectAsset, setFurnitureDensity,
  setLakeEnabled, setPathType, setVegetationDensity } from "../bindings";
import { Texts } from "../i18n";
import { AssetTileGroup } from "./AssetTileGroup";
import { CategoryChips } from "./AssetCatalog";
import { Column, HeadAction, Segmented, SliderRow, ToggleChip } from "./Bar";

type Props = {
  t: Texts;
  choices: AssetChoiceMap;
  pathType: number;
  lakeEnabled: boolean;
  vegetationDensity: number;
  furnitureDensity: number;
  enabledMask: number;
  pathPlanReady: boolean;
  decorationPlanReady: boolean;
  /** Paths and surface can no longer change (building or built). */
  structureLocked: boolean;
  /** Furnishings can no longer change (building or built). */
  furnishingLocked: boolean;
  canPlan: boolean;
  openKey: string | null;
  onOpen: (key: string) => void;
};

/** Park settings: paths and surface, planting, furnishing. */
export const ParkColumns = ({ t, choices, pathType, lakeEnabled,
  vegetationDensity, furnitureDensity, enabledMask, pathPlanReady,
  decorationPlanReady, structureLocked, furnishingLocked, canPlan,
  openKey, onOpen }: Props) => {
  const surface = choices.surface;
  return <>
    <Column title={t.colPaths} tone="toneBlue" testId="column-paths"
      action={pathPlanReady ? <HeadAction text={t.variant} hint={t.variantHint}
        disabled={structureLocked || !canPlan} onClick={generatePaths}
        testId="paths-variant" /> : null}>
      <Segmented label={t.pathType} value={pathType} disabled={structureLocked}
        testId="path-width-selector" onChange={setPathType}
        options={[{ value: 0, text: t.narrow, hint: t.pathNarrow },
          { value: 1, text: t.wide, hint: t.pathWide }]} />
      <AssetTileGroup testId="park-surface-choices" label={t.background}
        options={surface?.options ?? []}
        isActive={(name) => surface?.selected === name}
        onSelect={(name) => selectAsset("Surface", name)}
        disabled={structureLocked} />
    </Column>
    <Column title={t.colPlanting} tone="toneGreen" testId="column-planting">
      <SliderRow label={t.density} value={vegetationDensity} minimum={25}
        maximum={200} step={25} unit="%" disabled={furnishingLocked}
        testId="plant-density" onChange={setVegetationDensity} />
      <CategoryChips t={t} keys={["tree", "bush"]} choices={choices}
        enabledMask={enabledMask} busy={furnishingLocked} openKey={openKey}
        onOpen={onOpen}>
        <ToggleChip label={t.lake} hint={t.lakeHint} value={lakeEnabled}
          disabled={furnishingLocked} testId="lake-selector"
          onChange={setLakeEnabled} />
      </CategoryChips>
    </Column>
    <Column title={t.colFurnishing} tone="toneAmber" testId="column-furnishing"
      action={decorationPlanReady ? <HeadAction text={t.rearrange}
        hint={t.rearrangeHint} disabled={furnishingLocked || !pathPlanReady}
        onClick={generateDecorations} testId="furnishing-variant" /> : null}>
      <SliderRow label={t.density} value={furnitureDensity} minimum={25}
        maximum={200} step={25} unit="%" disabled={furnishingLocked}
        testId="furniture-density" onChange={setFurnitureDensity} />
      <CategoryChips t={t} keys={["bench", "lamp", "trashbin", "fence"]}
        choices={choices} enabledMask={enabledMask} busy={furnishingLocked}
        openKey={openKey} onOpen={onOpen} />
    </Column>
  </>;
};
