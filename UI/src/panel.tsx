import { useEffect, useRef, useState } from "react";
import { useValue } from "cs2/api";
import {
  assetOptionsJson$, buildPark, clearPolygon,
  decorationBuildBusy$, decorationBuildPresent$, decorationEnabledMask$,
  decorationPlanReady$, decorationSummary$,
  entranceCount$, finishPark, generateDecorations, generatePaths,
  furnitureDensity$, locale$, panelOpen$, pathBuildBusy$,
  pathBuildPresent$, pathBuildStatus$, pathBuildSummary$, pathPlanReady$,
  pathType$, plannerMode$, pointCount$, lakeEnabled$, setLakeEnabled,
  plazaArrangementPlacement$, plazaArrangementSpacing$,
  plazaCenterOptionsJson$, plazaCenterSelected$, plazaCenterPlacement$,
  plazaCenterpieceSpacing$, plazaFenceEnabled$, selectPlazaCenter,
  setPlazaArrangementPlacement, setPlazaArrangementSpacing,
  setPlazaCenterPlacement, setPlazaCenterpieceSpacing, setPlazaFenceEnabled,
  polygonArea$, polygonClosed$, polygonValid$, removeBuiltPaths, status$,
  selectAsset, setPathType, setSiteType, setPlannerMode, siteType$, toggleTool,
  vegetationDensity$,
} from "./bindings";
import { parseAssetChoices } from "./assetChoices";
import { AssetCatalog } from "./components/AssetCatalog";
import { PathSettings } from "./components/PathSettings";
import { PlazaFenceSelector } from "./components/PlazaFenceSelector";
import { PlazaArrangementEditor } from "./components/PlazaArrangementEditor";
import { NO_PLAZA_CENTER, parsePlazaCenterOptions,
  PlazaCenterSelector } from "./components/PlazaCenterSelector";
import { AssetTileGroup } from "./components/AssetTileGroup";
import { SnapControls, StatePill } from "./components/WorkflowParts";
import { getTexts } from "./i18n";
import arrowRightIcon from "./assets/arrow-right.svg";
import brandLogo from "./assets/park-manager.svg";
import styles from "./panel.module.less";
import { deriveWorkflowModel } from "./workflow";
import { formatUiText, parseUiText } from "./uiText";

const stop = (event: any) => event.stopPropagation();

/**
 * Four-step workflow shell. Detailed snapping and asset-selection concerns
 * live in dedicated components so this file only coordinates workflow state.
 */
export const ParkManagerPanel = () => {
  // Hooks stay unconditional: conditional hooks caused React #310 in Cohtml.
  const [activeStage, setActiveStage] = useState(0);
  const rawBuildMessage = useValue(status$);
  const [assetTooltip, setAssetTooltip] = useState<{
    name: string; x: number; y: number;
  } | null>(null);
  const panelRef = useRef<HTMLDivElement | null>(null);
  const open = useValue(panelOpen$);
  const t = getTexts(useValue(locale$));
  const pointCount = useValue(pointCount$);
  const polygonArea = useValue(polygonArea$);
  const closed = useValue(polygonClosed$);
  const valid = useValue(polygonValid$);
  const plannerMode = useValue(plannerMode$);
  const entranceCount = useValue(entranceCount$);
  const pathPlanReady = useValue(pathPlanReady$);
  const pathBuildBusy = useValue(pathBuildBusy$);
  const pathBuildPresent = useValue(pathBuildPresent$);
  const rawPathBuildSummary = useValue(pathBuildSummary$);
  const pathBuildStatus = useValue(pathBuildStatus$);
  const pathType = useValue(pathType$);
  const lakeEnabled = useValue(lakeEnabled$);
  const siteType = useValue(siteType$);
  const plazaCenterPlacement = useValue(plazaCenterPlacement$);
  const plazaArrangementPlacement = useValue(plazaArrangementPlacement$);
  const plazaCenterpieceSpacing = useValue(plazaCenterpieceSpacing$);
  const plazaArrangementSpacing = useValue(plazaArrangementSpacing$);
  const plazaFenceEnabled = useValue(plazaFenceEnabled$);
  const plazaCenterOptions = parsePlazaCenterOptions(
    useValue(plazaCenterOptionsJson$));
  const plazaCenterSelected = useValue(plazaCenterSelected$);
  const vegetationDensity = useValue(vegetationDensity$);
  const furnitureDensity = useValue(furnitureDensity$);
  const decorationEnabledMask = useValue(decorationEnabledMask$);
  const decorationPlanReady = useValue(decorationPlanReady$);
  const rawDecorationSummary = useValue(decorationSummary$);
  const decorationBuildBusy = useValue(decorationBuildBusy$);
  const decorationBuildPresent = useValue(decorationBuildPresent$);
  const assetChoices = parseAssetChoices(useValue(assetOptionsJson$));
  const surfaceChoice = assetChoices.surface;
  const visibleSurfaces = surfaceChoice?.options ?? [];
  const hasSelectedSurface = !!surfaceChoice?.selected
    && visibleSurfaces.some((option) => option.name === surfaceChoice.selected);
  const fenceChoice = assetChoices.fence;

  // The C# systems publish message keys; resolve them for the game language.
  const buildMessage = formatUiText(rawBuildMessage, t.messages);
  const pathBuildSummary = formatUiText(rawPathBuildSummary, t.messages);
  const decorationSummary = formatUiText(rawDecorationSummary, t.messages);
  const parsedDecorationSummary = parseUiText(rawDecorationSummary);
  const arrangementDoesNotFit = typeof parsedDecorationSummary !== "string"
    && parsedDecorationSummary.k === "plaza.arrangementDoesNotFit";

  const busy = pathBuildBusy || decorationBuildBusy;
  const workflow = deriveWorkflowModel({ plannerMode, polygonValid: valid,
    pathsPlanned: pathPlanReady && hasSelectedSurface,
    decorationsPlanned: decorationPlanReady,
    pathsBuilt: pathBuildPresent, decorationsBuilt: decorationBuildPresent });
  const outlineState = pointCount === 0 ? t.outlineEmpty
    : !closed ? t.outlineOpen(pointCount)
      : !valid ? t.outlineInvalid(pointCount) : t.outlineReady(pointCount);
  const pathBuildNotice = pathBuildStatus !== "ok";
  const isPlaza = siteType === 1;
  const workflowSteps = isPlaza ? t.plazaSteps : t.steps;
  const canPlanPlaza = plazaCenterSelected === NO_PLAZA_CENTER
    || plazaCenterOptions.some((option) => option.name === plazaCenterSelected);

  const selectPlazaFence = (name: string | null) => {
    if (name === null) {
      setPlazaFenceEnabled(false);
      return;
    }
    selectAsset("Fence", name);
    setPlazaFenceEnabled(true);
  };

  const showAssetTooltip = (event: any, name: string) => {
    const rect = panelRef.current?.getBoundingClientRect();
    if (!rect) return;
    setAssetTooltip({ name,
      x: Math.max(8, Math.min(event.clientX - rect.left + 10,
        rect.width - 220)),
      y: Math.max(62, event.clientY - rect.top - 39) });
  };

  const renderSurfaceSelector = () => {
    const prefix = isPlaza ? "plaza" : "park";
    return <AssetTileGroup title={t.background}
      groupClassName={styles.plazaSurfaceGroup}
      testIds={{ group: `${prefix}-surface-group`,
        header: `${prefix}-surface-group-header`,
        choices: `${prefix}-surface-choices` }}
      choicesClassName={styles.surfaceChoices} tileClassName=""
      activeClassName={styles.surfaceChoiceActive}
      options={visibleSurfaces}
      isActive={(name) => surfaceChoice?.selected === name}
      onSelect={(name) => selectAsset("Surface", name)}
      disabled={busy || pathBuildPresent} onTooltip={showAssetTooltip}
      onTooltipClose={() => setAssetTooltip(null)} />;
  };

  const renderPlazaAssetSelectors = () => (
    <div className={styles.plazaAssetSelectors} data-testid="plaza-asset-selectors">
      <PlazaCenterSelector t={t} options={plazaCenterOptions}
        selected={plazaCenterSelected} disabled={busy || pathBuildPresent}
        onSelect={selectPlazaCenter} onTooltip={showAssetTooltip}
        onTooltipClose={() => setAssetTooltip(null)} />
      <PlazaFenceSelector t={t} options={fenceChoice?.options ?? []}
        selected={fenceChoice?.selected ?? ""} enabled={plazaFenceEnabled}
        disabled={busy || pathBuildPresent} onSelect={selectPlazaFence}
        onTooltip={showAssetTooltip}
        onTooltipClose={() => setAssetTooltip(null)} />
    </div>
  );

  useEffect(() => {
    if (open) setActiveStage(workflow.progressStage);
  }, [open, workflow.progressStage, siteType]);

  useEffect(() => {
    if (!valid) setActiveStage(0);
    else if (!pathBuildPresent && activeStage > 1 && (!pathPlanReady || !hasSelectedSurface))
      setActiveStage(1);
    else if (!pathBuildPresent && activeStage === 3 && !decorationPlanReady)
      setActiveStage(2);
  }, [valid, pathPlanReady, hasSelectedSurface, decorationPlanReady, pathBuildPresent, activeStage]);

  useEffect(() => setAssetTooltip(null), [open, activeStage]);

  if (!open) return null;

  const openStage = (index: number) => {
    if (busy || !workflow.stageAvailability[index as 0 | 1 | 2 | 3]) return;
    setActiveStage(index);
    setPlannerMode(index > 0);
  };

  const renderOutline = () => (
      <div className={styles.stageColumn}>
        <div className={styles.stageCopy}>
          <p>{t.outlineText}</p>
          <div className={styles.stateRow}>
            <StatePill success={valid}>{outlineState}</StatePill>
            {pointCount >= 3
              ? <StatePill>{t.outlineArea(Math.round(polygonArea).toLocaleString())}</StatePill>
              : null}
          </div>
        </div>
      </div>
  );

  const renderPaths = () => (
      <div className={`${styles.stageColumn} ${styles.pathStageColumn}`}>
        <div className={styles.stageCopy} data-testid="path-stage-copy">
          <div className={styles.pathStageIntro} data-testid="path-stage-intro">
            <p>{pathPlanReady ? (isPlaza ? t.plazaPathsPreview : t.pathsPreview)
              : isPlaza ? (entranceCount > 0 ? t.plazaPathsNeedPlan : t.plazaPathsNoGate)
                : entranceCount > 0 ? t.pathsGates(entranceCount) : t.pathsNoGate}</p>
            {pathBuildNotice ? <p className={styles.buildWarning} role="alert"
              data-status={pathBuildStatus}>
              {pathBuildSummary}
            </p> : null}
            <div className={styles.stateRow} data-testid="path-stage-state">
              <StatePill success={entranceCount > 0}>{isPlaza
                ? t.plazaAccesses(entranceCount) : t.pathsGates(entranceCount)}</StatePill>
              {pathPlanReady ? <StatePill success>{workflowSteps[1]}</StatePill> : null}
            </div>
            {!isPlaza ? <div className={`${styles.compactSetting} ${styles.lakeSetting}`}
              data-testid="lake-selector">
              <span>{t.lake}</span>
              <div className={styles.segmentedControl}>
                <button className={!lakeEnabled ? styles.segmentActive : ""}
                  disabled={busy || decorationBuildPresent} aria-pressed={!lakeEnabled}
                  onClick={() => setLakeEnabled(false)}>{t.lakeOff}</button>
                <button className={lakeEnabled ? styles.segmentActive : ""}
                  disabled={busy || decorationBuildPresent} aria-pressed={lakeEnabled}
                  onClick={() => setLakeEnabled(true)}>{t.lakeOn}</button>
              </div>
            </div> : null}
          </div>
          {isPlaza ? renderPlazaAssetSelectors() : null}
        </div>
        <PathSettings t={t} isPlaza={isPlaza} busy={busy}
          pathBuildPresent={pathBuildPresent} siteType={siteType} pathType={pathType}
          centerSelected={plazaCenterSelected}
          centerPlacement={plazaCenterPlacement}
          arrangementPlacement={plazaArrangementPlacement}
          centerpieceSpacing={plazaCenterpieceSpacing}
          arrangementSpacing={plazaArrangementSpacing}
          surfaceSelector={renderSurfaceSelector()}
          onSiteType={setSiteType} onPathType={setPathType}
          onCenterPlacement={setPlazaCenterPlacement}
          onArrangementPlacement={setPlazaArrangementPlacement}
          onCenterpieceSpacing={setPlazaCenterpieceSpacing}
          onArrangementSpacing={setPlazaArrangementSpacing}
        />
      </div>
  );

  const renderAssets = () => (
    <div className={styles.assetStage}>
      {isPlaza ? <PlazaArrangementEditor t={t} choices={assetChoices}
        busy={busy || decorationBuildPresent}
        density={furnitureDensity} decorationPlanReady={decorationPlanReady}
        notice={arrangementDoesNotFit ? decorationSummary : null} />
        : <AssetCatalog t={t} choices={assetChoices}
        busy={busy || decorationBuildPresent}
        vegetationDensity={vegetationDensity}
        furnitureDensity={furnitureDensity}
        enabledMask={decorationEnabledMask}
        decorationBuildPresent={decorationBuildPresent}
        decorationPlanReady={decorationPlanReady} />}
    </div>
  );

  const renderComplete = () => (
      <div className={styles.stageColumn}>
        <div className={styles.stageCopy}>
          <p>{decorationBuildPresent
            ? (isPlaza ? t.plazaCompleteText : t.completeText) : t.finalPreview}</p>
          <div className={styles.stateRow}>
            <StatePill success>{pathBuildPresent
              ? (isPlaza ? t.plazaSurfaceBuilt : t.pathsBuilt) : t.surfacePlanned}</StatePill>
            <StatePill success>{decorationBuildPresent ? t.decorationsBuilt : t.furnishingsPlanned}</StatePill>
          </div>
          {busy ? <p role="status">{t.busy}</p> : null}
          {buildMessage ? <p role="status">{buildMessage}</p> : null}
          {pathBuildStatus === "error" ? <p role="alert">{pathBuildSummary}</p> : null}
        </div>
      </div>
  );

  const renderFooter = () => (
    <div className={styles.panelFooter} data-testid="panel-footer">
      <div className={styles.footerLeft}>
        {activeStage > 0 ? <button className={styles.backButton}
          disabled={busy || (activeStage !== 2 && pathBuildPresent)}
          onClick={() => openStage(activeStage - 1)}>
          <img className={`${styles.buttonIcon} ${styles.backButtonIcon}`}
            src={arrowRightIcon} alt="" />
          {activeStage === 1 ? t.editOutline
            : activeStage === 2 ? (isPlaza ? t.backToStructure : t.backToSurface)
              : isPlaza ? t.backToDetails : t.backToFurnishings}</button> : null}
      </div>
      <div className={styles.footerRight}>
        {activeStage === 0 ? <>
          <button className={styles.secondaryButton} disabled={pointCount === 0}
            onClick={clearPolygon}>{t.reset}</button>
          <button className={styles.primaryButton} disabled={!valid}
            onClick={() => openStage(1)}>
            {t.continuePaths}<img className={styles.buttonIcon} src={arrowRightIcon} alt="" />
          </button>
        </> : null}
        {activeStage === 1 ? <>
          <button className={styles.secondaryButton}
            disabled={busy || pathBuildPresent || entranceCount === 0
              || (isPlaza && !canPlanPlaza)}
            onClick={generatePaths}>
            {pathPlanReady
              ? (isPlaza ? t.plazaRecalculate : t.recalculatePaths)
              : t.createVariant}</button>
          <button className={styles.primaryButton}
            disabled={busy || pathBuildPresent || !pathPlanReady
              || !hasSelectedSurface || entranceCount === 0
              || (isPlaza && !canPlanPlaza)}
            onClick={() => openStage(2)}>
            {pathBuildBusy ? t.busy : isPlaza
              ? t.continuePlazaDetails : t.continueFurnishings}
            <img className={styles.buttonIcon} src={arrowRightIcon} alt="" />
          </button>
        </> : null}
        {activeStage === 2 ? <>
          <button className={styles.secondaryButton}
            disabled={busy || decorationBuildPresent || !pathPlanReady}
            onClick={generateDecorations}>
            {decorationPlanReady ? t.replanDecorations : t.generateDecorations}</button>
          <button className={styles.primaryButton}
            disabled={busy || !decorationPlanReady || !pathPlanReady || !hasSelectedSurface}
            onClick={() => openStage(3)}>
            {t.continueFinal}
            <img className={styles.buttonIcon} src={arrowRightIcon} alt="" />
          </button>
        </> : null}
        {activeStage === 3 ? <>
          {pathBuildPresent ? <button className={styles.dangerButton} disabled={busy}
            onClick={removeBuiltPaths}>{isPlaza ? t.removePlaza : t.removePark}</button> : null}
          <button className={styles.successButton} disabled={busy}
            onClick={decorationBuildPresent ? finishPark : buildPark}>
            {busy ? t.busy : decorationBuildPresent
              ? (isPlaza ? t.finishPlaza : t.finishPark)
              : (isPlaza ? t.buildPlaza : t.buildPark)}</button>
        </> : null}
      </div>
    </div>
  );

  return (
    <div ref={panelRef} className={styles.panel} data-testid="park-panel"
      onMouseDown={stop} onMouseUp={stop}
      onClick={stop} onContextMenu={stop}>
      <div className={styles.panelHeader} data-testid="panel-header">
        <div className={styles.brand}>
          <span className={styles.brandMark}>
            <img src={brandLogo} alt="" data-testid="brand-logo" />
          </span><span>ParkManager</span>
        </div>
        <div className={styles.progress} data-testid="workflow-progress" role="list">
          {workflowSteps.map((label, index) => (
            <div key={label} role="listitem" data-testid="workflow-step"
              className={`${styles.progressStep} ${
                index === activeStage ? styles.progressStepActive : ""} ${
                index < activeStage ? styles.progressStepDone : ""}`}
              aria-current={index === activeStage ? "step" : undefined}>
              <span className={styles.progressNumber}>
                {index < activeStage ? "✓" : index + 1}
              </span>
              <span>{label}</span>
            </div>
          ))}
        </div>
        <div className={styles.toolSlot}>
          {activeStage === 0 ? <SnapControls t={t} /> : null}
        </div>
        <button className={styles.closeButton} title={t.close}
          onClick={toggleTool}>×</button>
      </div>

      <div data-testid="panel-body" data-stage={activeStage}
        data-paths-built={pathBuildPresent} data-decorations-built={decorationBuildPresent}
        className={`${styles.panelBody} ${
        activeStage <= 1 || activeStage === 3 ? styles.compactBody : ""} ${
        activeStage === 1 && isPlaza ? styles.plazaBody : ""} ${
        activeStage === 2 ? styles.assetBody : ""}`}>
        {activeStage === 0 ? renderOutline()
          : activeStage === 1 ? renderPaths()
            : activeStage === 2 ? renderAssets() : renderComplete()}
      </div>
      {renderFooter()}
      {assetTooltip ? <div className={styles.assetTooltip}
        style={{ left: `${assetTooltip.x}px`, top: `${assetTooltip.y}px` }}>
        {assetTooltip.name}
      </div> : null}
    </div>
  );
};
