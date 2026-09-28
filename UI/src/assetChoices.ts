/** Asset catalog DTOs kept separate from workflow rendering. */
export type AssetChoiceOption = { name: string; icon: string };

type AssetChoiceState = {
  selected: string;
  selectedMany: string[];
  options: AssetChoiceOption[];
};

export type AssetChoiceMap = Record<string, AssetChoiceState>;

export const assetCategories = [
  { key: "tree", payload: "Tree", multi: true, kind: 1 },
  { key: "bush", payload: "Bush", multi: true, kind: 2 },
  { key: "bench", payload: "Bench", multi: false, kind: 3 },
  { key: "lamp", payload: "Lamp", multi: false, kind: 4 },
  { key: "fence", payload: "Fence", multi: false, kind: 5 },
  { key: "trashbin", payload: "TrashBin", multi: false, kind: 6 },
] as const;

/**
 * Normalizes published options. An icon is optional because many valid game
 * prefabs expose no UIObject icon or thumbnail; plain strings are tolerated
 * because an old payload may survive for one frame during a hot reload.
 */
export const parseOptions = (options: unknown[]): AssetChoiceOption[] =>
  options.map((option) => typeof option === "string"
    ? { name: option, icon: "" }
    : {
        name: typeof (option as any)?.name === "string" ? (option as any).name : "",
        icon: typeof (option as any)?.icon === "string" ? (option as any).icon : "",
      }).filter((option) => option.name.trim().length > 0);

/** Parses the version-tolerant JSON bridge. */
export const parseAssetChoices = (json: string): AssetChoiceMap => {
  try {
    const parsed = JSON.parse(json);
    if (!parsed || typeof parsed !== "object") return {};
    const result: AssetChoiceMap = {};
    Object.keys(parsed).forEach((key) => {
      const state = parsed[key];
      if (!state || !Array.isArray(state.options)) return;
      result[key] = {
        selected: typeof state.selected === "string" ? state.selected : "",
        selectedMany: Array.isArray(state.selectedMany)
          ? state.selectedMany.filter((name: unknown): name is string =>
              typeof name === "string")
          : [],
        options: parseOptions(state.options),
      };
    });
    return result;
  } catch {
    return {};
  }
};

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
