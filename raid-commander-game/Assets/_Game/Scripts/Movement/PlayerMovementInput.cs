using RaidCommander.Selection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RaidCommander.Movement
{
    /// <summary>Converts player input into orders for the selected unit's movement executor.</summary>
    public sealed class PlayerMovementInput : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private UnitSelectionController selection;
        [SerializeField] private DestinationMarker destinationMarker;
        [SerializeField] private LayerMask groundMask;
        [SerializeField, Min(1f)] private float rayDistance = 200f;
        private InputAction moveAction;

        private void Awake()
        {
            moveAction = new InputAction("Order Move", InputActionType.Button, "<Mouse>/rightButton");
        }

        private void OnEnable() => moveAction?.Enable();
        private void OnDisable() => moveAction?.Disable();
        private void OnDestroy() => moveAction?.Dispose();

        private void Update()
        {
            if (moveAction.WasPressedThisFrame() && Mouse.current != null)
                TryMoveAtScreenPosition(Mouse.current.position.ReadValue());
        }

        public bool TryMoveAtScreenPosition(Vector2 screenPosition)
        {
            if (worldCamera == null || selection == null || selection.SelectedUnit == null
                || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
                return false;
            if (!selection.SelectedUnit.TryGetComponent(out UnitMovement movement))
                return false;

            Ray ray = worldCamera.ScreenPointToRay(screenPosition);
            int targetingMask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("FriendlyUnit", "HostileUnit");
            if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, targetingMask, QueryTriggerInteraction.Ignore))
            {
                destinationMarker?.Hide();
                return false;
            }

            // Units are passable; solid arena geometry still blocks ground targeting.
            if ((groundMask.value & (1 << hit.collider.gameObject.layer)) == 0)
            {
                if (Physics.Raycast(hit.point + Vector3.up * 0.1f, Vector3.down, out RaycastHit floor,
                    rayDistance, groundMask, QueryTriggerInteraction.Ignore))
                    destinationMarker?.Show(floor.point, false);
                return false;
            }

            MoveResult result = movement.TryMove(hit.point, MovementSource.Manual);
            bool accepted = result == MoveResult.Accepted;
            destinationMarker?.Show(accepted ? movement.Destination : hit.point, accepted);
            return accepted;
        }
    }
}
