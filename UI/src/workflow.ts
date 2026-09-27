export type WorkflowStage = 0 | 1 | 2 | 3;

export type WorkflowModel = {
  progressStage: WorkflowStage;
  stageAvailability: readonly [boolean, boolean, boolean, boolean];
};

type WorkflowFacts = {
  plannerMode: boolean;
  polygonValid: boolean;
  pathsBuilt: boolean;
  decorationsBuilt: boolean;
  pathsPlanned: boolean;
  decorationsPlanned: boolean;
};

/** Derives navigation progress and reachability from the durable workflow facts. */
export const deriveWorkflowModel = ({ plannerMode, polygonValid,
  pathsBuilt, decorationsBuilt, pathsPlanned, decorationsPlanned }: WorkflowFacts): WorkflowModel => ({
  progressStage: (pathsBuilt || decorationsBuilt ? 3 : plannerMode ? 1 : 0) as WorkflowStage,
  stageAvailability: [!pathsBuilt, polygonValid && !pathsBuilt,
    polygonValid && pathsPlanned && !pathsBuilt,
    pathsBuilt || (polygonValid && pathsPlanned && decorationsPlanned)],
});
