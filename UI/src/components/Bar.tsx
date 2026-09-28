import { ReactNode } from "react";
import { Hint } from "./Hint";
import { ReloadIcon } from "./Icons";
import { RangeSlider } from "./RangeSlider";
import styles from "../panel.module.less";

export type Tone = "toneBlue" | "toneGreen" | "toneAmber";

/** One titled group of the bar; the tone ties it to its map preview. */
export const Column = ({ title, tone, action, testId, children }: {
  title: string;
  tone: Tone;
  action?: ReactNode;
  testId: string;
  children: ReactNode;
}) => (
  <section className={`${styles.column} ${styles[tone]}`} data-testid={testId}>
    <header className={styles.columnHead}>
      <span className={styles.columnTitle}>{title}</span>
      {action}
    </header>
    <div className={styles.columnBody}>{children}</div>
  </section>
);

export type SegmentOption<T> = {
  value: T;
  text?: string;
  icon?: ReactNode;
  hint?: string;
};

/** Labelled row with mutually exclusive buttons. */
export function Segmented<T>({ label, options, value, disabled = false,
  onChange, testId }: {
  label?: string;
  options: SegmentOption<T>[];
  value: T;
  disabled?: boolean;
  onChange: (value: T) => void;
  testId?: string;
}) {
  return <div className={styles.settingRow} data-testid={testId}>
    {label ? <span className={styles.settingLabel}>{label}</span> : null}
    <div className={styles.segmented}>
      {options.map((option) => {
        const active = option.value === value;
        return <Hint key={String(option.value)} text={option.hint}>
          <button type="button" disabled={disabled} aria-pressed={active}
            aria-label={option.text ? undefined : option.hint}
            className={active ? styles.segmentActive : ""}
            onClick={() => onChange(option.value)}>
            {option.icon}{option.text ? <span>{option.text}</span> : null}
          </button>
        </Hint>;
      })}
    </div>
  </div>;
}

/** Labelled slider with its current value on the right. */
export const SliderRow = ({ label, value, minimum, maximum, step = 1, unit,
  disabled = false, onChange, testId }: {
  label: string;
  value: number;
  minimum: number;
  maximum: number;
  step?: number;
  unit: string;
  disabled?: boolean;
  onChange: (value: number) => void;
  testId?: string;
}) => (
  <div className={styles.settingRow} data-testid={testId}>
    <span className={styles.settingLabel}>{label}</span>
    <RangeSlider label={label} value={value} minimum={minimum} maximum={maximum}
      step={step} disabled={disabled}
      formatValue={(next) => `${next}${unit}`} onChange={onChange} />
    <strong className={styles.settingValue}>{`${value}${unit}`}</strong>
  </div>
);

/** Small text button in a column header, e.g. "New variant". */
export const HeadAction = ({ text, hint, disabled = false, onClick, testId }: {
  text: string;
  hint: string;
  disabled?: boolean;
  onClick: () => void;
  testId?: string;
}) => (
  <Hint text={hint}>
    <button type="button" className={styles.headAction} disabled={disabled}
      data-testid={testId} onClick={onClick}><ReloadIcon /><span>{text}</span></button>
  </Hint>
);

/** Chevron marking a control that opens the window below the bar. */
export const DropdownCaret = ({ open }: { open: boolean }) => (
  <svg className={`${styles.caret} ${open ? styles.caretOpen : ""}`}
    viewBox="0 0 12 12" aria-hidden="true" focusable="false">
    <path d="M2.5 4.5 6 8l3.5-3.5" fill="none" stroke="currentColor"
      strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
  </svg>
);

/** Chip-shaped on/off switch, styled like the asset category chips. */
export const ToggleChip = ({ label, hint, value, disabled = false, onChange,
  testId }: {
  label: string;
  hint: string;
  value: boolean;
  disabled?: boolean;
  onChange: (value: boolean) => void;
  testId?: string;
}) => (
  <Hint text={hint}>
    <button type="button" data-testid={testId} disabled={disabled}
      aria-pressed={value}
      className={`${styles.chip} ${styles.toggleChip} ${value ? "" : styles.chipOff}`}
      onClick={() => onChange(!value)}>
      <span className={`${styles.chipCheck} ${value ? styles.chipCheckOn : ""}`}
        aria-hidden="true">{value ? "✓" : ""}</span>
      <span className={styles.chipLabel}>{label}</span>
    </button>
  </Hint>
);
