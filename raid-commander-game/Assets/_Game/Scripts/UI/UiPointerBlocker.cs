using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RaidCommander.UI
{
    /// <summary>Queries current uGUI geometry before world input, independent of Update order.</summary>
    public static class UiPointerBlocker
    {
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>(16);

        public static bool Contains(Vector2 screenPosition)
        {
            EventSystem system = EventSystem.current;
            if (system == null) return false;
            var pointer = new PointerEventData(system) { position = screenPosition };
            Hits.Clear();
            system.RaycastAll(pointer, Hits);
            bool blocked = Hits.Count > 0;
            Hits.Clear();
            return blocked;
        }
    }
}
