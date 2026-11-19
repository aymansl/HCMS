using HCMS4.Shared.Common;

namespace HCMS4.Shared.Workflow
{
    /// <summary>
    /// An order or case whose status follows a <see cref="StateMachine{TStatus}"/>
    /// (lab request, imaging request/session, emergency case).
    /// </summary>
    public interface IHasWorkflowStatus<TStatus> where TStatus : struct, Enum
    {
        TStatus Status { get; set; }
        /// <summary>When the status last changed (Damascus time).</summary>
        DateTime StatusChangedAt { get; set; }
        /// <summary>The user id that made the last status change.</summary>
        string? StatusChangedBy { get; set; }
    }

    /// <summary>One status change. Modules can store these to measure turnaround time (S4).</summary>
    public sealed record StatusTransition<TStatus>(TStatus From, TStatus To, DateTime ChangedAt, string ChangedBy)
        where TStatus : struct, Enum;

    /// <summary>
    /// The allowed status transitions of one kind of order. Build it once per order type:
    /// <code>
    /// var machine = new StateMachine&lt;LabStatus&gt;()
    ///     .Allow(LabStatus.Requested, LabStatus.Received, LabStatus.Canceled)
    ///     .Allow(LabStatus.Received, LabStatus.Completed);
    /// </code>
    /// A status with no outgoing transition is terminal.
    /// </summary>
    public sealed class StateMachine<TStatus> where TStatus : struct, Enum
    {
        private readonly Dictionary<TStatus, HashSet<TStatus>> _allowed = new();

        public StateMachine<TStatus> Allow(TStatus from, params TStatus[] to)
        {
            if (to.Length == 0)
                throw new ArgumentException("At least one target status is required.", nameof(to));

            if (to.Contains(from))
                throw new ArgumentException($"A status cannot transition to itself ({from}).", nameof(to));

            if (!_allowed.TryGetValue(from, out var targets))
            {
                targets = new HashSet<TStatus>();
                _allowed[from] = targets;
            }

            targets.UnionWith(to);
            return this;
        }

        public bool CanTransition(TStatus from, TStatus to) =>
            _allowed.TryGetValue(from, out var targets) && targets.Contains(to);

        public IReadOnlyCollection<TStatus> NextStatuses(TStatus from) =>
            _allowed.TryGetValue(from, out var targets) ? targets.ToList() : Array.Empty<TStatus>();

        public bool IsTerminal(TStatus status) => !_allowed.ContainsKey(status);

        /// <summary>
        /// Moves <paramref name="item"/> to <paramref name="to"/> when the transition is allowed, and records
        /// when and by whom. Nothing changes when it is not allowed.
        /// </summary>
        public ServiceResult<StatusTransition<TStatus>> Transition(IHasWorkflowStatus<TStatus> item, TStatus to,
            string changedBy, IClinicClock clock)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(clock);

            if (string.IsNullOrWhiteSpace(changedBy))
                return ServiceResult<StatusTransition<TStatus>>.Fail("The user making the change is required.");

            var from = item.Status;
            if (!CanTransition(from, to))
                return ServiceResult<StatusTransition<TStatus>>.Fail($"Cannot change the status from {from} to {to}.");

            var now = clock.Now;
            item.Status = to;
            item.StatusChangedAt = now;
            item.StatusChangedBy = changedBy;

            return ServiceResult<StatusTransition<TStatus>>.Ok(new StatusTransition<TStatus>(from, to, now, changedBy));
        }
    }
}
