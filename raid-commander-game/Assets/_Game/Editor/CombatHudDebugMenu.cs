using UnityEditor;

namespace RaidCommander.UI
{
    /// <summary>Session-only Editor switch; the runtime HUD reads it while Play is active.</summary>
    public static class CombatHudDebugMenu
    {
        private const string Key = "RaidCommander.ShowCombatDebug";
        private const string MenuPath = "Raid Commander/UI/Show Combat Debug";
        public static bool Visible => SessionState.GetBool(Key, false);

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            SessionState.SetBool(Key, !Visible);
            Menu.SetChecked(MenuPath, Visible);
        }

        [MenuItem(MenuPath, true)]
        private static bool Validate()
        {
            Menu.SetChecked(MenuPath, Visible);
            return true;
        }
    }
}
