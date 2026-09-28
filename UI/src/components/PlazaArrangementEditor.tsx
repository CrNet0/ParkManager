import { useEffect, useRef, useState } from "react";
import { useValue } from "cs2/api";
import { AssetChoiceMap } from "../assetChoices";
import { editPlazaArrangement, plazaArrangementJson$ } from "../bindings";
import { AssetIcon, scrollTileGrid, useIconFailures } from "./AssetIcon";
import { Hint } from "./Hint";
import { ChevronIcon, PlaceholderIcon } from "./Icons";
import { Texts } from "../i18n";
import styles from "../panel.module.less";

type Item = { kind: number; name: string };
export const arrangementKinds = [
  { kind: 0, key: "bench" },
  { kind: 1, key: "lamp" },
  { kind: 2, key: "trashbin" },
  { kind: 3, key: "tree" },
  { kind: 4, key: "plazaplanter" },
] as const;

export const parseArrangement = (json: string): Item[] => {
  try {
    const value = JSON.parse(json);
    return Array.isArray(value) ? value.slice(0, 5).filter((item) =>
      Number.isInteger(item?.kind) && item.kind >= 0 && item.kind <= 4
      && typeof item?.name === "string") : [];
  } catch { return []; }
};

type Props = {
  t: Texts;
  choices: AssetChoiceMap;
  busy: boolean;
};

/** Five ordered slots; every asset choice applies to exactly one slot. */
export const PlazaArrangementEditor = ({ t, choices, busy }: Props) => {
  const arrangement = parseArrangement(useValue(plazaArrangementJson$));
  const [selected, setSelected] = useState(0);
  const icons = useIconFailures();
  const gridRef = useRef<HTMLDivElement | null>(null);
  const current = arrangement[Math.min(selected, arrangement.length - 1)];
  const selectedIndex = Math.min(selected, arrangement.length - 1);
  const category = arrangementKinds.find((kind) => kind.kind === current?.kind)
    || arrangementKinds[0];
  const options = choices[category.key]?.options || [];
  const selectedOption = options.find((option) => option.name === current?.name);

  useEffect(() => { if (gridRef.current) gridRef.current.scrollTop = 0; },
    [selectedIndex, current?.kind]);
  const send = (action: string, index: number, value?: string | number) =>
    editPlazaArrangement(`${action}\n${index}${value === undefined ? "" : `\n${value}`}`);
  return <div className={styles.plazaArrangementWorkspace}
    data-testid="plaza-arrangement-workspace">
    <div className={styles.plazaArrangementSlots} data-testid="plaza-arrangement-slots">
      <div className={styles.assetChooserHeader}>
        <strong>{t.plazaArrangement}</strong>
        <span>{arrangement.length} / 5</span>
      </div>
      <div className={styles.plazaArrangementSlotRow}>
        {arrangement.map((item, index) => {
          const kind = arrangementKinds.find((candidate) => candidate.kind === item.kind)
            || arrangementKinds[0];
          const asset = choices[kind.key]?.options.find((option) =>
            option.name === item.name);
          return <button key={index} type="button"
            className={`${styles.plazaArrangementSlot} ${index === selectedIndex
              ? styles.plazaArrangementSlotActive : ""}`}
            aria-pressed={index === selectedIndex}
            onClick={() => setSelected(index)}>
            <span>{index + 1}</span>
            <AssetIcon icon={asset?.icon} icons={icons}
              fallback={<b>{t.categories[kind.key].charAt(0)}</b>} />
            <small>{item.name || t.categories[kind.key]}</small>
          </button>;
        })}
        {arrangement.length < 5 ? <Hint text={t.plazaArrangementAdd}>
          <button type="button" className={styles.plazaArrangementAdd}
            disabled={busy} aria-label={t.plazaArrangementAdd}
            onClick={() => {
              editPlazaArrangement("add");
              setSelected(arrangement.length);
            }}>+</button>
        </Hint> : null}
      </div>
      <div className={styles.plazaArrangementActions}>
        <button type="button" disabled={busy || selectedIndex <= 0}
          onClick={() => { send("move", selectedIndex, selectedIndex - 1);
            setSelected(selectedIndex - 1); }}><ChevronIcon direction="left" /></button>
        <button type="button" disabled={busy || selectedIndex >= arrangement.length - 1}
          onClick={() => { send("move", selectedIndex, selectedIndex + 1);
            setSelected(selectedIndex + 1); }}><ChevronIcon direction="right" /></button>
        <button type="button" disabled={busy || arrangement.length <= 1}
          onClick={() => { send("remove", selectedIndex);
            setSelected(Math.max(0, selectedIndex - 1)); }}>
          {t.plazaArrangementRemove}</button>
      </div>
    </div>
    <div className={styles.plazaArrangementPicker} data-testid="plaza-arrangement-picker">
      <div className={styles.plazaArrangementKinds}>
        {arrangementKinds.map((kind) => <button key={kind.kind} type="button"
          className={kind.kind === current?.kind ? styles.segmentActive : ""}
          disabled={busy || !current}
          onClick={() => send("kind", selectedIndex, kind.kind)}>
          {t.categories[kind.key]}</button>)}
      </div>
      <div className={styles.assetChoiceBody}>
        <div className={styles.assetChoiceGrid} ref={gridRef}>
          {options.map((option) => <Hint key={option.name} text={option.name}>
            <button type="button"
              className={`${styles.assetChoiceTile} ${option.name === current?.name
                ? styles.assetChoiceTileActive : ""}`}
              disabled={busy} aria-label={option.name}
              aria-pressed={option.name === current?.name}
              onClick={() => send("asset", selectedIndex, option.name)}>
              <AssetIcon icon={option.icon} icons={icons}
                className={styles.assetChoiceIcon}
                fallback={<span className={styles.assetChoiceFallback}><PlaceholderIcon /></span>} />
              <span className={styles.assetChoiceName}>{option.name}</span>
              {option.name === current?.name
                ? <span className={styles.assetChoiceCheck}>✓</span> : null}
            </button>
          </Hint>)}
        </div>
        <div className={styles.assetScrollControls}>
          <button type="button" aria-label={t.scrollAssetsUp}
            onClick={() => scrollTileGrid(gridRef.current, -1)}><ChevronIcon direction="up" /></button>
          <button type="button" aria-label={t.scrollAssetsDown}
            onClick={() => scrollTileGrid(gridRef.current, 1)}><ChevronIcon direction="down" /></button>
        </div>
      </div>
      <div className={styles.plazaArrangementSelection}>
        {selectedOption?.name || t.plazaArrangementChooseAsset}
      </div>
    </div>
  </div>;
};
