using HCMS4.Shared.Common;
using HCMS4.Shared.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace HCMS4.Tests
{
    public class StateMachineTests
    {
        public enum SampleStatus { Requested, Received, InProgress, Completed, Canceled }

        private sealed class SampleOrder : IHasWorkflowStatus<SampleStatus>
        {
            public SampleStatus Status { get; set; } = SampleStatus.Requested;
            public DateTime StatusChangedAt { get; set; }
            public string? StatusChangedBy { get; set; }
        }

        // 2026-11-18 09:00 UTC = 12:00 Damascus.
        private readonly ClinicClock _clock = new(
            new FakeTimeProvider(new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero)), ClinicTime.TimeZone);

        private static StateMachine<SampleStatus> Machine() => new StateMachine<SampleStatus>()
            .Allow(SampleStatus.Requested, SampleStatus.Received, SampleStatus.Canceled)
            .Allow(SampleStatus.Received, SampleStatus.InProgress, SampleStatus.Canceled)
            .Allow(SampleStatus.InProgress, SampleStatus.Completed);

        [Fact]
        public void AllowedTransition_ChangesStatus_AndRecordsTimeAndUser()
        {
            var order = new SampleOrder();

            var result = Machine().Transition(order, SampleStatus.Received, "tech-1", _clock);

            Assert.True(result.Success);
            Assert.Equal(SampleStatus.Received, order.Status);
            Assert.Equal(new DateTime(2026, 11, 18, 12, 0, 0), order.StatusChangedAt);
            Assert.Equal("tech-1", order.StatusChangedBy);
            Assert.Equal(new StatusTransition<SampleStatus>(SampleStatus.Requested, SampleStatus.Received,
                new DateTime(2026, 11, 18, 12, 0, 0), "tech-1"), result.Data);
        }

        [Theory]
        [InlineData(SampleStatus.Requested, SampleStatus.Completed)]   // skipping steps
        [InlineData(SampleStatus.InProgress, SampleStatus.Received)]   // going back
        [InlineData(SampleStatus.Completed, SampleStatus.Canceled)]    // from a terminal status
        [InlineData(SampleStatus.Canceled, SampleStatus.Requested)]
        public void DisallowedTransition_IsRejected_AndLeavesTheOrderUnchanged(SampleStatus from, SampleStatus to)
        {
            var order = new SampleOrder { Status = from, StatusChangedBy = "earlier" };

            var result = Machine().Transition(order, to, "tech-1", _clock);

            Assert.False(result.Success);
            Assert.Contains(from.ToString(), result.Message);
            Assert.Equal(from, order.Status);
            Assert.Equal("earlier", order.StatusChangedBy);
            Assert.Equal(default, order.StatusChangedAt);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void Transition_WithoutUser_IsRejected(string? user)
        {
            var order = new SampleOrder();

            Assert.False(Machine().Transition(order, SampleStatus.Received, user!, _clock).Success);
            Assert.Equal(SampleStatus.Requested, order.Status);
        }

        [Fact]
        public void FullPath_RecordsEveryStep()
        {
            var machine = Machine();
            var order = new SampleOrder();

            var steps = new[] { SampleStatus.Received, SampleStatus.InProgress, SampleStatus.Completed }
                .Select(next => machine.Transition(order, next, "tech-1", _clock))
                .ToList();

            Assert.All(steps, s => Assert.True(s.Success));
            Assert.Equal(new[] { SampleStatus.Requested, SampleStatus.Received, SampleStatus.InProgress },
                steps.Select(s => s.Data.From));
            Assert.True(machine.IsTerminal(order.Status));
        }

        [Fact]
        public void Queries_DescribeTheMachine()
        {
            var machine = Machine();

            Assert.True(machine.CanTransition(SampleStatus.Requested, SampleStatus.Canceled));
            Assert.False(machine.CanTransition(SampleStatus.Requested, SampleStatus.InProgress));
            Assert.Equal(new[] { SampleStatus.Canceled, SampleStatus.Received },
                machine.NextStatuses(SampleStatus.Requested).OrderBy(s => s.ToString()));
            Assert.Empty(machine.NextStatuses(SampleStatus.Completed));
            Assert.False(machine.IsTerminal(SampleStatus.Requested));
            Assert.True(machine.IsTerminal(SampleStatus.Canceled));
        }

        [Fact]
        public void Allow_RejectsSelfTransitions_AndEmptyTargets()
        {
            var machine = new StateMachine<SampleStatus>();

            Assert.Throws<ArgumentException>(() => machine.Allow(SampleStatus.Requested, SampleStatus.Requested));
            Assert.Throws<ArgumentException>(() => machine.Allow(SampleStatus.Requested));
        }

        [Fact]
        public void Allow_CanBeCalledTwiceForTheSameStatus()
        {
            var machine = new StateMachine<SampleStatus>()
                .Allow(SampleStatus.Requested, SampleStatus.Received)
                .Allow(SampleStatus.Requested, SampleStatus.Canceled);

            Assert.Equal(2, machine.NextStatuses(SampleStatus.Requested).Count);
        }
    }
}
