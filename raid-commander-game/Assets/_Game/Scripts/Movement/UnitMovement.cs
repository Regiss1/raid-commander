using System;
using UnityEngine;
using UnityEngine.AI;

namespace RaidCommander.Movement
{
    public enum MovementSource { Autonomous, Manual }
    public enum MovementState { Idle, Moving, Completed, Interrupted, Failed, Cancelled }
    public enum MoveResult { Accepted, ManualOverrideActive, AgentUnavailable, InvalidDestination, Unreachable }

    /// <summary>Executes world-space orders independently of selection and command producers.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(NavMeshAgent))]
    public sealed class UnitMovement : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float destinationSnapDistance = 0.5f;
        [SerializeField, Min(0.5f)] private float stalledTimeout = 6f;

        private NavMeshAgent agent;
        private Vector3 progressPosition;
        private float stalledTime;

        public MovementState State { get; private set; } = MovementState.Idle;
        public MovementSource Source { get; private set; }
        public Vector3 Destination { get; private set; }
        public int CommandId { get; private set; }
        public bool HasManualOverride => State == MovementState.Moving && Source == MovementSource.Manual;
        public event Action<int, MovementSource, MovementState> CommandEnded;

        private bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

        private void Awake() => agent = GetComponent<NavMeshAgent>();

        public MoveResult TryMove(Vector3 worldDestination, MovementSource source)
        {
            if (!isActiveAndEnabled || !AgentReady)
                return MoveResult.AgentUnavailable;
            if (source == MovementSource.Autonomous && HasManualOverride)
                return MoveResult.ManualOverrideActive;
            if (!IsFinite(worldDestination))
                return MoveResult.InvalidDestination;

            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(worldDestination, out NavMeshHit hit, destinationSnapDistance, filter))
                return MoveResult.InvalidDestination;

            // Calculate before replacing the active path: partial paths must never become orders.
            var path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                return MoveResult.Unreachable;

            if (!agent.SetPath(path))
            {
                if (State == MovementState.Moving)
                    Finish(MovementState.Failed);
                return MoveResult.AgentUnavailable;
            }

            bool interrupted = State == MovementState.Moving;
            int previousId = CommandId;
            MovementSource previousSource = Source;
            CommandId++;
            Source = source;
            Destination = hit.position;
            State = MovementState.Moving;
            progressPosition = transform.position;
            stalledTime = 0f;
            agent.isStopped = false;

            if (interrupted)
                CommandEnded?.Invoke(previousId, previousSource, MovementState.Interrupted);
            return MoveResult.Accepted;
        }

        private void Update()
        {
            if (State != MovementState.Moving)
                return;
            if (!AgentReady)
            {
                Finish(MovementState.Failed);
                return;
            }

            stalledTime += Time.deltaTime;
            if ((transform.position - progressPosition).sqrMagnitude >= 0.01f)
            {
                progressPosition = transform.position;
                stalledTime = 0f;
            }

            if (!agent.pathPending)
            {
                // The placeholders have centered pivots. baseOffset keeps their feet on the mesh.
                Vector3 feet = transform.position - Vector3.up * agent.baseOffset;
                float arrivalDistance = agent.stoppingDistance + 0.05f;
                bool atDestination = Vector3.Distance(feet, Destination) <= arrivalDistance;
                bool pathArrived = agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete
                    && agent.remainingDistance <= arrivalDistance;

                if ((atDestination || pathArrived) && agent.velocity.sqrMagnitude <= 0.01f)
                {
                    Finish(MovementState.Completed);
                    return;
                }
                if (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete)
                {
                    Finish(MovementState.Failed);
                    return;
                }
            }

            if (stalledTime >= stalledTimeout)
                Finish(MovementState.Failed);
        }

        public void Cancel()
        {
            if (State == MovementState.Moving)
                Finish(MovementState.Cancelled);
        }

        private void OnDisable() => Cancel();

        private void Finish(MovementState outcome)
        {
            if (AgentReady)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            State = outcome;
            CommandEnded?.Invoke(CommandId, Source, outcome);
        }

        private static bool IsFinite(Vector3 point) =>
            !float.IsNaN(point.x) && !float.IsInfinity(point.x)
            && !float.IsNaN(point.y) && !float.IsInfinity(point.y)
            && !float.IsNaN(point.z) && !float.IsInfinity(point.z);
    }
}
