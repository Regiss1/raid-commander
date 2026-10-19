using UnityEngine;

namespace RaidCommander.Selection
{
    public enum PartyRole
    {
        Tank,
        Healer,
        Dps
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class SelectableUnit : MonoBehaviour
    {
        [SerializeField] private PartyRole role;

        public PartyRole Role => role;
        public Collider SelectionCollider { get; private set; }

        private void Awake()
        {
            SelectionCollider = GetComponent<Collider>();
        }

        private void OnValidate()
        {
            if (SelectionCollider == null)
                SelectionCollider = GetComponent<Collider>();
        }
    }
}
