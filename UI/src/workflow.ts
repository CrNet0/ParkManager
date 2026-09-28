/** The one next step the main button offers. */
export type NextAction =
  | "drawOutline"      // outline open or invalid: nothing to press yet
  | "placeEntrances"   // outline ready: switch the map to entrance mode
  | "markEntrance"     // entrance mode without entrances: nothing to press yet
  | "plan"             // entrances set: plan paths (and furnishings) together
  | "chooseSurface"    // plan exists but no ground surface is selected
  | "planFurnishings"  // paths planned, furnishings not (yet or anymore)
  | "build"            // both previews ready, or a failed furnishing build
  | "finish"           // everything built: detach and start the next park
  | "busy";            // a build is running

type WorkflowFacts = {
  polygonValid: boolean;
  plannerMode: boolean;
  entranceCount: number;
  pathsPlanned: boolean;
  surfaceSelected: boolean;
  decorationsPlanned: boolean;
  pathsBuilt: boolean;
  decorationsBuilt: boolean;
  busy: boolean;
};

/** Derives the main action from the durable workflow facts. */
export const deriveNextAction = (facts: WorkflowFacts): NextAction => {
  if (facts.busy) return "busy";
  if (facts.pathsBuilt) return facts.decorationsBuilt ? "finish" : "build";
  if (!facts.polygonValid) return "drawOutline";
  if (!facts.plannerMode) return "placeEntrances";
  if (facts.entranceCount === 0) return "markEntrance";
  if (!facts.pathsPlanned) return "plan";
  if (!facts.surfaceSelected) return "chooseSurface";
  if (!facts.decorationsPlanned) return "planFurnishings";
  return "build";
};

/** Actions that only describe what the player has to do on the map. */
export const isHintAction = (action: NextAction) => action === "drawOutline"
  || action === "markEntrance" || action === "chooseSurface" || action === "busy";
