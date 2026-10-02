using Colossal.Logging;
using Game;
using Game.Modding;
using ParkManager.Tools;
using ParkManager.Assets;

namespace ParkManager
{
    /// <summary>
    /// Entry point registered with the Cities: Skylines II mod loader.
    /// It owns no gameplay state; its only responsibility is scheduling the
    /// ParkManager UI, world tool and runtime asset catalog systems.
    /// </summary>
    public sealed class Mod : IMod
    {
        internal static string Version => typeof(Mod).Assembly.GetName().Version.ToString(3);

        public static readonly ILog Log = LogManager
            .GetLogger($"{nameof(ParkManager)}.{nameof(Mod)}")
            .SetShowsErrorsInUI(false);

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info($"ParkManager {Version} loaded.");
            updateSystem.UpdateAt<ParkManagerUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<ParkToolSystem>(SystemUpdatePhase.ToolUpdate);
            // Not GameSimulation: that phase does not tick while the game is
            // paused, so a paused city would never receive its asset lists.
            updateSystem.UpdateAt<ParkAssetCatalogSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<ParkBundleNetworkCleanupSystem>(
                SystemUpdatePhase.Modification2);
            updateSystem.UpdateAt<ParkBundleCleanupSystem>(
                SystemUpdatePhase.Modification3);
        }

        public void OnDispose()
        {
            Log.Info("ParkManager bootstrap disposed.");
        }
    }
}
