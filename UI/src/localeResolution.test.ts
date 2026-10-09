import { de } from "./locales/de";
import { en } from "./locales/en";
import { getTexts } from "./i18n";
import { formatUiText } from "./uiText";

const failures: string[] = [];
const check = (condition: boolean, message: string) => {
  if (!condition) failures.push(message);
};

const placeholders = (template: string): string =>
  (template.match(/\{(\d+)(?::(\d+))?\}/g) || []).slice().sort().join(",");

check(getTexts("de") === de, "de");
check(getTexts("de-DE") === de, "de-DE");
check(getTexts("de-AT") === de, "de-AT");
check(getTexts("de_CH") === de, "de_CH");
check(getTexts("  de-DE  ") === de, "padded de-DE");
check(getTexts("en") === en, "en");
check(getTexts("en-US") === en, "en-US");
check(getTexts("en-GB") === en, "en-GB");
check(getTexts("DE-DE") === en, "DE-DE stays English");
check(getTexts("debug") === en, "debug is not German");
check(getTexts("den-DE") === en, "den-DE is not German");
check(getTexts("fr-FR") === en, "fr-FR");
check(getTexts("pt-BR") === en, "pt-BR falls back until a translation is registered");
check(getTexts("zh-HANS") === en, "zh-HANS");
check(getTexts("") === en, "empty");
check(getTexts("   ") === en, "blank");
check(getTexts(undefined as unknown as string) === en, "undefined");
check(getTexts("de").close === "ParkManager schließen", "German close");
check(getTexts("en-US").close === "Close ParkManager", "English close");

const messageKeys = Object.keys(en.messages) as (keyof typeof en.messages)[];
check(messageKeys.length === Object.keys(de.messages).length, "message key count");
messageKeys.forEach((key) => {
  check(placeholders(en.messages[key]) === placeholders(de.messages[key]),
    "placeholders " + key);
});

const warning = JSON.stringify({
  k: "preflight.warning",
  a: [{ k: "preflight.pathSteep", a: [] }, 12.4, -0.2],
});
check(formatUiText(warning, getTexts("de-DE").messages)
  === "Hinweis: Weg stark geneigt bei X 12, Z 0. Bau wird trotzdem versucht.",
  "German warning");
check(formatUiText(warning, getTexts("en-US").messages)
  === "Note: steep path at X 12, Z 0. Building anyway.",
  "English warning");
check(formatUiText("Validation failed: selected area is blocked.", en.messages)
  === "Validation failed: selected area is blocked.",
  "plain text");

if (failures.length > 0) {
  throw new Error(failures.join("\n"));
}
