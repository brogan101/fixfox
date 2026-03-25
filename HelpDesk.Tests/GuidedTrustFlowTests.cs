using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using HelpDesk.Infrastructure.Services;
using Xunit;

namespace HelpDesk.Tests;

public sealed class GuidedTrustFlowTests
{
    [Fact]
    public async Task GuidedRepairExecutionService_FailedStep_StaysFailedAndRecordsReceipt()
    {
        var scripts = new FakeScriptService { InlineSuccess = false };
        var state = new FakeStatePersistenceService();
        var history = new FakeRepairHistoryService();
        var service = new GuidedRepairExecutionService(scripts, state, history);
        var fix = new FixItem
        {
            Id = "guided-network-fix",
            Title = "Guided Network Fix",
            Type = FixType.Guided,
            Steps =
            [
                new FixStep { Id = "step-1", Title = "Reset stack", Instruction = "Run reset", Script = "netsh winsock reset" },
                new FixStep { Id = "step-2", Title = "Retry", Instruction = "Retry the connection" }
            ]
        };

        var result = await service.AdvanceAsync(fix, 0, "internet broken");

        Assert.Equal(ExecutionOutcome.Failed, result.Outcome);
        Assert.Equal("step-1", result.FailedStepId);
        Assert.Equal("Reset stack", result.FailedStepTitle);
        Assert.NotNull(state.Load());
        Assert.Equal(ExecutionOutcome.Failed, state.Load()!.Outcome);
        Assert.Contains(history.Entries, entry => entry.FixId == fix.Id && entry.Outcome == ExecutionOutcome.Failed);
    }

    [Fact]
    public void GuidedRepairExecutionService_BuildResumeState_UsesPersistedStep()
    {
        var scripts = new FakeScriptService();
        var state = new FakeStatePersistenceService();
        var history = new FakeRepairHistoryService();
        var service = new GuidedRepairExecutionService(scripts, state, history);
        var fix = new FixItem
        {
            Id = "guided-audio-fix",
            Title = "Guided Audio Fix",
            Type = FixType.Guided,
            Steps =
            [
                new FixStep { Id = "step-1", Title = "Check service", Instruction = "Check Windows Audio" },
                new FixStep { Id = "step-2", Title = "Restart app", Instruction = "Restart the meeting app" }
            ]
        };

        state.Save(new InterruptedOperationState
        {
            OperationType = "guided",
            OperationTargetId = fix.Id,
            CurrentStepId = "step-2",
            DisplayTitle = fix.Title,
            Outcome = ExecutionOutcome.Resumable,
            Summary = "Resume on step 2.",
            CanResume = true
        });

        var result = service.BuildResumeState(fix, state.Load());

        Assert.NotNull(result);
        Assert.Equal(1, result!.CurrentStepIndex);
        Assert.Equal("step-2", result.CurrentStepId);
        Assert.True(result.CanResume);
    }
}
