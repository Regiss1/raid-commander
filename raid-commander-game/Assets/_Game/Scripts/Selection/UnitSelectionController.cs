using System;
using UnityEngine;
using UnityEngine.InputSystem;
using RaidCommander.UI;

namespace RaidCommander.Selection
{
    public sealed class UnitSelectionController : MonoBehaviour
    {
        [SerializeField] private Camera selectionCamera;
        [SerializeField] private LayerMask raycastMask = ~0;
        [SerializeField] private SelectableUnit tank;
        [SerializeField] private SelectableUnit healer;
        [SerializeField] private SelectableUnit dps;
        [SerializeField] private SelectableUnit rangedDps;
        [SerializeField] private SelectionRing selectionRing;
        [SerializeField, Min(1f)] private float raycastDistance = 200f;

        private InputAction selectAction;
        private InputAction tankAction;
        private InputAction healerAction;
        private InputAction dpsAction;
        private InputAction rangedDpsAction;

        public SelectableUnit SelectedUnit { get; private set; }
        public event Action<SelectableUnit> SelectionChanged;

        private void Awake()
        {
            if (selectionCamera == null)
                selectionCamera = Camera.main;

            selectAction = new InputAction("Select Unit", InputActionType.Button, "<Mouse>/leftButton");
            tankAction = new InputAction("Select Tank", InputActionType.Button, "<Keyboard>/1");
            healerAction = new InputAction("Select Healer", InputActionType.Button, "<Keyboard>/2");
            dpsAction = new InputAction("Select DPS", InputActionType.Button, "<Keyboard>/3");
            rangedDpsAction = new InputAction("Select Ranged DPS", InputActionType.Button, "<Keyboard>/4");
        }

        private void OnEnable()
        {
            selectAction?.Enable();
            tankAction?.Enable();
            healerAction?.Enable();
            dpsAction?.Enable();
            rangedDpsAction?.Enable();
        }

        private void OnDisable()
        {
            selectAction?.Disable();
            tankAction?.Disable();
            healerAction?.Disable();
            dpsAction?.Disable();
            rangedDpsAction?.Disable();
        }

        private void OnDestroy()
        {
            selectAction?.Dispose();
            tankAction?.Dispose();
            healerAction?.Dispose();
            dpsAction?.Dispose();
            rangedDpsAction?.Dispose();
        }

        private void Update()
        {
            if (selectAction.WasPressedThisFrame())
                SelectAtPointer();

            if (tankAction.WasPressedThisFrame())
                Select(tank);
            else if (healerAction.WasPressedThisFrame())
                Select(healer);
            else if (dpsAction.WasPressedThisFrame())
                Select(dps);
            else if (rangedDpsAction.WasPressedThisFrame())
                Select(rangedDps);
        }

        public void Select(SelectableUnit unit)
        {
            if (unit != null && !unit.isActiveAndEnabled)
                unit = null;

            if (SelectedUnit == unit)
                return;

            SelectedUnit = unit;
            if (selectionRing != null)
                selectionRing.SetTarget(SelectedUnit);

            SelectionChanged?.Invoke(SelectedUnit);
        }

        private void SelectAtPointer()
        {
            if (Mouse.current != null && UiPointerBlocker.Contains(Mouse.current.position.ReadValue()))
                return;
            if (selectionCamera == null || Mouse.current == null)
            {
                Select(null);
                return;
            }

            Vector2 pointerPosition = Mouse.current.position.ReadValue();
            Ray ray = selectionCamera.ScreenPointToRay(pointerPosition);

            if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, raycastMask, QueryTriggerInteraction.Ignore)
                && hit.collider.TryGetComponent(out SelectableUnit clickedUnit)
                && clickedUnit.gameObject.layer == LayerMask.NameToLayer("FriendlyUnit"))
            {
                Select(clickedUnit);
                return;
            }

            Select(null);
        }
    }
}
