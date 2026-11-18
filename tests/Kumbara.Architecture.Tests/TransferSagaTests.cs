using System.Text;
using System.Text.Json;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Kumbara.BuildingBlocks.Contracts.Saga;
using Kumbara.BuildingBlocks.Idempotency;
using Kumbara.Transfer.Api.Middleware;
using Kumbara.Transfer.Application.Sagas;
using Kumbara.Transfer.Domain.Entities;
using Kumbara.Transfer.Infrastructure.Persistence;
using Xunit;

namespace Kumbara.Architecture.Tests;

public class TransferSagaTests
{
    private static async Task<(ServiceProvider Provider, ITestHarness Harness, ISagaStateMachineTestHarness<TransferStateMachine, TransferState> SagaHarness)> CreateTestHarnessAsync()
    {
        var services = new ServiceCollection();
        services.AddMassTransitTestHarness(x =>
        {
            x.AddSagaStateMachine<TransferStateMachine, TransferState>()
                .InMemoryRepository();
        });

        var provider = services.BuildServiceProvider(true);
        var harness = await provider.StartTestHarness();
        var sagaHarness = harness.GetSagaStateMachineHarness<TransferStateMachine, TransferState>();

        return (provider, harness, sagaHarness);
    }

    [Fact]
    public async Task TransferSaga_HappyPath_CompletesSuccessfully()
    {
        var (provider, harness, sagaHarness) = await CreateTestHarnessAsync();
        try
        {
            var transferId = Guid.NewGuid();
            var src = Guid.NewGuid();
            var dst = Guid.NewGuid();

            // 1. Start transfer
            await harness.Bus.Publish(new TransferStartedEvent(transferId, src, dst, 1000m, "TRY"));

            Assert.True(await sagaHarness.Consumed.Any<TransferStartedEvent>());
            Assert.True(await harness.Published.Any<CheckTransferRiskCommand>(x => x.Context.Message.TransferId == transferId));
            Assert.NotNull(await sagaHarness.Exists(transferId, x => x.AwaitingRisk));

            // 2. Risk Approves
            await harness.Bus.Publish(new RiskApprovedEvent(transferId));

            Assert.True(await sagaHarness.Consumed.Any<RiskApprovedEvent>());
            Assert.True(await harness.Published.Any<HoldLedgerFundsCommand>(x => x.Context.Message.TransferId == transferId));
            Assert.NotNull(await sagaHarness.Exists(transferId, x => x.AwaitingHold));

            // 3. Funds Held
            await harness.Bus.Publish(new FundsHeldEvent(transferId, src, 1000m));

            Assert.True(await sagaHarness.Consumed.Any<FundsHeldEvent>());
            Assert.True(await harness.Published.Any<CommitLedgerHoldCommand>(x => x.Context.Message.TransferId == transferId));
            Assert.NotNull(await sagaHarness.Exists(transferId, x => x.AwaitingCommit));

            // 4. Hold Committed -> Completed
            await harness.Bus.Publish(new HoldCommittedEvent(transferId));

            Assert.True(await sagaHarness.Consumed.Any<HoldCommittedEvent>());
            Assert.True(await harness.Published.Any<TransferCompletedEvent>(x => x.Context.Message.TransferId == transferId));
            Assert.NotNull(await sagaHarness.Exists(transferId, x => x.Completed));
        }
        finally
        {
            await harness.Stop();
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task TransferSaga_RiskRejected_FailsWithoutTouchingLedger()
    {
        var (provider, harness, sagaHarness) = await CreateTestHarnessAsync();
        try
        {
            var transferId = Guid.NewGuid();
            var src = Guid.NewGuid();
            var dst = Guid.NewGuid();
            const string rejectionReason = "RiskRejected: Cumulative 24h transfer amount exceeds limit.";

            // 1. Start transfer
            await harness.Bus.Publish(new TransferStartedEvent(transferId, src, dst, 75000m, "TRY"));

            Assert.True(await sagaHarness.Consumed.Any<TransferStartedEvent>());
            Assert.True(await harness.Published.Any<CheckTransferRiskCommand>(x => x.Context.Message.TransferId == transferId));
            Assert.NotNull(await sagaHarness.Exists(transferId, x => x.AwaitingRisk));

            // 2. Risk Rejects
            await harness.Bus.Publish(new RiskRejectedEvent(transferId, rejectionReason));

            Assert.True(await sagaHarness.Consumed.Any<RiskRejectedEvent>());

            // Verify final state is Failed
            Assert.NotNull(await sagaHarness.Exists(transferId, x => x.Failed));

            // Verify TransferFailedEvent published with Reason
            Assert.True(await harness.Published.Any<TransferFailedEvent>(x =>
                x.Context.Message.TransferId == transferId && x.Context.Message.Reason == rejectionReason));

            // Verify Ledger was never touched: NO HoldLedgerFundsCommand sent
            Assert.False(await harness.Published.Any<HoldLedgerFundsCommand>(x => x.Context.Message.TransferId == transferId));
            Assert.False(await harness.Published.Any<CommitLedgerHoldCommand>(x => x.Context.Message.TransferId == transferId));
        }
        finally
        {
            await harness.Stop();
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task TransferSaga_InsufficientFunds_FailsGracefully()
    {
        var (provider, harness, sagaHarness) = await CreateTestHarnessAsync();
        try
        {
            var transferId = Guid.NewGuid();
            var src = Guid.NewGuid();
            var dst = Guid.NewGuid();
            const string holdFailureReason = "InsufficientFunds: Available balance is insufficient for transfer.";

            // 1. Start transfer
            await harness.Bus.Publish(new TransferStartedEvent(transferId, src, dst, 5000m, "TRY"));
            Assert.True(await sagaHarness.Consumed.Any<TransferStartedEvent>());

            // 2. Risk Approves
            await harness.Bus.Publish(new RiskApprovedEvent(transferId));
            Assert.True(await sagaHarness.Consumed.Any<RiskApprovedEvent>());
            Assert.NotNull(await sagaHarness.Exists(transferId, x => x.AwaitingHold));

            // 3. Funds Hold Fails (e.g. InsufficientFunds)
            await harness.Bus.Publish(new FundsHoldFailedEvent(transferId, holdFailureReason));
            Assert.True(await sagaHarness.Consumed.Any<FundsHoldFailedEvent>());

            // Verify final state is Failed
            Assert.NotNull(await sagaHarness.Exists(transferId, x => x.Failed));

            // Verify TransferFailedEvent published with failure reason
            Assert.True(await harness.Published.Any<TransferFailedEvent>(x =>
                x.Context.Message.TransferId == transferId && x.Context.Message.Reason == holdFailureReason));

            // Verify Commit was never issued
            Assert.False(await harness.Published.Any<CommitLedgerHoldCommand>(x => x.Context.Message.TransferId == transferId));
        }
        finally
        {
            await harness.Stop();
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task TransferSaga_CommitFails_TriggersCompensationAndReleasesHold()
    {
        var (provider, harness, sagaHarness) = await CreateTestHarnessAsync();
        try
        {
            var transferId = Guid.NewGuid();
            var src = Guid.NewGuid();
            var dst = Guid.NewGuid();
            const string commitFailureReason = "CommitFailed: Optimistic concurrency collision on destination account.";

            // 1. Start transfer
            await harness.Bus.Publish(new TransferStartedEvent(transferId, src, dst, 2000m, "TRY"));
            Assert.True(await sagaHarness.Consumed.Any<TransferStartedEvent>());

            // 2. Risk Approves
            await harness.Bus.Publish(new RiskApprovedEvent(transferId));
            Assert.True(await sagaHarness.Consumed.Any<RiskApprovedEvent>());

            // 3. Funds Held
            await harness.Bus.Publish(new FundsHeldEvent(transferId, src, 2000m));
            Assert.True(await sagaHarness.Consumed.Any<FundsHeldEvent>());
            Assert.NotNull(await sagaHarness.Exists(transferId, x => x.AwaitingCommit));

            // 4. Commit Fails -> Triggers Compensation
            await harness.Bus.Publish(new HoldCommitFailedEvent(transferId, commitFailureReason));
            Assert.True(await sagaHarness.Consumed.Any<HoldCommitFailedEvent>());

            // Verify ReleaseLedgerHoldCommand published for compensation
            Assert.True(await harness.Published.Any<ReleaseLedgerHoldCommand>(x => x.Context.Message.TransferId == transferId));
            Assert.NotNull(await sagaHarness.Exists(transferId, x => x.AwaitingCompensation));

            // 5. Hold Released (Compensation executed)
            await harness.Bus.Publish(new HoldReleasedEvent(transferId));
            Assert.True(await sagaHarness.Consumed.Any<HoldReleasedEvent>());

            // Verify final state is Failed (Compensated)
            Assert.NotNull(await sagaHarness.Exists(transferId, x => x.Failed));
            Assert.True(await harness.Published.Any<TransferFailedEvent>(x =>
                x.Context.Message.TransferId == transferId && x.Context.Message.Reason == commitFailureReason));
        }
        finally
        {
            await harness.Stop();
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task Idempotency_SameKey_ReturnsCachedResponse()
    {
        // Setup in-memory TransferDbContext
        var options = new DbContextOptionsBuilder<TransferDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var services = new ServiceCollection();
        services.AddScoped(_ => new TransferDbContext(options));
        var serviceProvider = services.BuildServiceProvider();

        int downstreamExecutionCount = 0;
        RequestDelegate next = async (ctx) =>
        {
            downstreamExecutionCount++;
            ctx.Response.StatusCode = StatusCodes.Status202Accepted;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                transferId = Guid.NewGuid(),
                status = "Initiated",
                amount = 500m
            }));
        };

        var middleware = new IdempotencyFilter(next, NullLogger<IdempotencyFilter>.Instance);
        var idempotencyKey = Guid.NewGuid().ToString("N");
        var requestBodyJson = JsonSerializer.Serialize(new
        {
            sourceAccountId = Guid.NewGuid(),
            destinationAccountId = Guid.NewGuid(),
            amount = 500m,
            currency = "TRY"
        });

        // First Request: executes downstream and saves record
        var context1 = new DefaultHttpContext();
        context1.Request.Method = "POST";
        context1.Request.Path = "/api/transfers";
        context1.Request.Headers["Idempotency-Key"] = idempotencyKey;
        context1.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(requestBodyJson));
        context1.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context1, serviceProvider);

        Assert.Equal(StatusCodes.Status202Accepted, context1.Response.StatusCode);
        Assert.Equal(1, downstreamExecutionCount);

        context1.Response.Body.Position = 0;
        var firstResponseBody = await new StreamReader(context1.Response.Body).ReadToEndAsync();
        Assert.Contains("Initiated", firstResponseBody);

        // Second Request with identical Idempotency-Key: must return cached response without executing downstream
        var context2 = new DefaultHttpContext();
        context2.Request.Method = "POST";
        context2.Request.Path = "/api/transfers";
        context2.Request.Headers["Idempotency-Key"] = idempotencyKey;
        context2.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(requestBodyJson));
        context2.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context2, serviceProvider);

        Assert.Equal(StatusCodes.Status202Accepted, context2.Response.StatusCode);
        Assert.Equal(1, downstreamExecutionCount); // Downstream handler NOT executed second time!

        context2.Response.Body.Position = 0;
        var secondResponseBody = await new StreamReader(context2.Response.Body).ReadToEndAsync();
        Assert.Equal(firstResponseBody, secondResponseBody); // Returns exact cached response body
    }
}
