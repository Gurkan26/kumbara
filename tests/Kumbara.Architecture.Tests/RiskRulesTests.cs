using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Kumbara.Risk.Application.Commands;
using Kumbara.Risk.Domain.Interfaces;
using Kumbara.Risk.Domain.Models;
using Kumbara.Risk.Domain.Rules;
using Kumbara.Risk.Infrastructure.Services;
using Xunit;

namespace Kumbara.Architecture.Tests;

public class RiskRulesTests
{
    private readonly Guid _sourceAccountId = Guid.NewGuid();
    private readonly Guid _destAccountId = Guid.NewGuid();
    private readonly Guid _transferId = Guid.NewGuid();

    [Fact]
    public async Task SingleTransactionLimitRule_ShouldApprove_WhenAmountWithinLimit()
    {
        var rule = new SingleTransactionLimitRule(maxSingleLimit: 50000m);
        var context = new RiskEvaluationContext(_transferId, _sourceAccountId, _destAccountId, 10000m, "TRY");

        var result = await rule.EvaluateAsync(context);

        Assert.True(result.IsApproved);
        Assert.Null(result.RejectionReason);
    }

    [Fact]
    public async Task SingleTransactionLimitRule_ShouldApprove_WhenAmountEqualsLimit()
    {
        var rule = new SingleTransactionLimitRule(maxSingleLimit: 50000m);
        var context = new RiskEvaluationContext(_transferId, _sourceAccountId, _destAccountId, 50000m, "TRY");

        var result = await rule.EvaluateAsync(context);

        Assert.True(result.IsApproved);
    }

    [Fact]
    public async Task SingleTransactionLimitRule_ShouldReject_WhenAmountExceedsLimit()
    {
        var rule = new SingleTransactionLimitRule(maxSingleLimit: 50000m);
        var context = new RiskEvaluationContext(_transferId, _sourceAccountId, _destAccountId, 50000.01m, "TRY");

        var result = await rule.EvaluateAsync(context);

        Assert.False(result.IsApproved);
        Assert.Contains("exceeds the single transaction limit", result.RejectionReason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task SingleTransactionLimitRule_ShouldReject_WhenAmountIsZeroOrNegative(decimal invalidAmount)
    {
        var rule = new SingleTransactionLimitRule();
        var context = new RiskEvaluationContext(_transferId, _sourceAccountId, _destAccountId, invalidAmount, "TRY");

        var result = await rule.EvaluateAsync(context);

        Assert.False(result.IsApproved);
        Assert.Contains("strictly greater than zero", result.RejectionReason);
    }

    [Fact]
    public async Task DailyVelocityRule_ShouldApprove_WhenWithinDailyLimit()
    {
        var mockVelocity = new FakeVelocityService(dailyResult: true, freqResult: true);
        var rule = new DailyVelocityRule(mockVelocity, dailyLimit: 100000m);
        var context = new RiskEvaluationContext(_transferId, _sourceAccountId, _destAccountId, 25000m, "TRY");

        var result = await rule.EvaluateAsync(context);

        Assert.True(result.IsApproved);
    }

    [Fact]
    public async Task DailyVelocityRule_ShouldReject_WhenDailyLimitExceeded()
    {
        var mockVelocity = new FakeVelocityService(dailyResult: false, freqResult: true);
        var rule = new DailyVelocityRule(mockVelocity, dailyLimit: 100000m);
        var context = new RiskEvaluationContext(_transferId, _sourceAccountId, _destAccountId, 25000m, "TRY");

        var result = await rule.EvaluateAsync(context);

        Assert.False(result.IsApproved);
        Assert.Contains("Daily velocity limit", result.RejectionReason);
    }

    [Fact]
    public async Task SuspiciousVelocityFrequencyRule_ShouldApprove_WhenWithinFrequencyLimit()
    {
        var mockVelocity = new FakeVelocityService(dailyResult: true, freqResult: true);
        var rule = new SuspiciousVelocityFrequencyRule(mockVelocity, maxTransfers: 5);
        var context = new RiskEvaluationContext(_transferId, _sourceAccountId, _destAccountId, 1000m, "TRY");

        var result = await rule.EvaluateAsync(context);

        Assert.True(result.IsApproved);
    }

    [Fact]
    public async Task SuspiciousVelocityFrequencyRule_ShouldReject_WhenFrequencyLimitExceeded()
    {
        var mockVelocity = new FakeVelocityService(dailyResult: true, freqResult: false);
        var rule = new SuspiciousVelocityFrequencyRule(mockVelocity, maxTransfers: 5);
        var context = new RiskEvaluationContext(_transferId, _sourceAccountId, _destAccountId, 1000m, "TRY");

        var result = await rule.EvaluateAsync(context);

        Assert.False(result.IsApproved);
        Assert.Contains("Suspicious transaction velocity frequency", result.RejectionReason);
    }

    [Fact]
    public async Task RedisVelocityService_InMemoryFallback_ShouldTrackDailyLimitAccurately()
    {
        var config = new ConfigurationBuilder().Build();
        var service = new RedisVelocityService(config, NullLogger<RedisVelocityService>.Instance);
        var accountId = Guid.NewGuid();

        // 3 transfers of 30,000 -> 90,000 total (limit 100,000)
        Assert.True(await service.IncrementAndCheckDailyLimitAsync(accountId, 30000m, 100000m));
        Assert.True(await service.IncrementAndCheckDailyLimitAsync(accountId, 30000m, 100000m));
        Assert.True(await service.IncrementAndCheckDailyLimitAsync(accountId, 30000m, 100000m));

        // 4th transfer of 20,000 -> 90,000 + 20,000 = 110,000 > 100,000 -> rejected
        Assert.False(await service.IncrementAndCheckDailyLimitAsync(accountId, 20000m, 100000m));

        // 5th transfer of 10,000 -> 90,000 + 10,000 = 100,000 -> approved
        Assert.True(await service.IncrementAndCheckDailyLimitAsync(accountId, 10000m, 100000m));
    }

    [Fact]
    public async Task RedisVelocityService_InMemoryFallback_ShouldTrackFrequencyAccurately()
    {
        var config = new ConfigurationBuilder().Build();
        var service = new RedisVelocityService(config, NullLogger<RedisVelocityService>.Instance);
        var accountId = Guid.NewGuid();

        // 5 transfers within 5 minutes allowed
        for (int i = 0; i < 5; i++)
        {
            Assert.True(await service.IncrementAndCheckFrequencyAsync(accountId, maxAllowedCount: 5, window: TimeSpan.FromMinutes(5)));
        }

        // 6th transfer should be rejected
        Assert.False(await service.IncrementAndCheckFrequencyAsync(accountId, maxAllowedCount: 5, window: TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public async Task EvaluateRiskCommandHandler_ShouldStopOnFirstRuleRejection()
    {
        var mockVelocity = new FakeVelocityService(dailyResult: true, freqResult: true);
        var rules = new IRiskRule[]
        {
            new SingleTransactionLimitRule(maxSingleLimit: 50000m),
            new DailyVelocityRule(mockVelocity, dailyLimit: 100000m),
            new SuspiciousVelocityFrequencyRule(mockVelocity, maxTransfers: 5)
        };

        var handler = new EvaluateRiskCommandHandler(rules, NullLogger<EvaluateRiskCommandHandler>.Instance);

        // Transaction exceeding single limit (60,000 TRY)
        var command = new EvaluateRiskCommand(Guid.NewGuid(), _sourceAccountId, _destAccountId, 60000m, "TRY");
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsApproved);
        Assert.Contains("single transaction limit", result.RejectionReason);
        Assert.Equal(0, mockVelocity.DailyCallCount); // Short-circuited before calling velocity service
    }

    [Fact]
    public async Task EvaluateRiskCommandHandler_ShouldApprove_WhenAllRulesPass()
    {
        var mockVelocity = new FakeVelocityService(dailyResult: true, freqResult: true);
        var rules = new IRiskRule[]
        {
            new SingleTransactionLimitRule(maxSingleLimit: 50000m),
            new DailyVelocityRule(mockVelocity, dailyLimit: 100000m),
            new SuspiciousVelocityFrequencyRule(mockVelocity, maxTransfers: 5)
        };

        var handler = new EvaluateRiskCommandHandler(rules, NullLogger<EvaluateRiskCommandHandler>.Instance);

        var command = new EvaluateRiskCommand(Guid.NewGuid(), _sourceAccountId, _destAccountId, 15000m, "TRY");
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsApproved);
        Assert.Null(result.RejectionReason);
        Assert.Equal(1, mockVelocity.DailyCallCount);
        Assert.Equal(1, mockVelocity.FreqCallCount);
    }

    private class FakeVelocityService : IVelocityService
    {
        private readonly bool _dailyResult;
        private readonly bool _freqResult;
        public int DailyCallCount { get; private set; }
        public int FreqCallCount { get; private set; }

        public FakeVelocityService(bool dailyResult, bool freqResult)
        {
            _dailyResult = dailyResult;
            _freqResult = freqResult;
        }

        public Task<bool> IncrementAndCheckDailyLimitAsync(Guid accountId, decimal amount, decimal dailyLimit = 100000m, CancellationToken cancellationToken = default)
        {
            DailyCallCount++;
            return Task.FromResult(_dailyResult);
        }

        public Task<bool> IncrementAndCheckFrequencyAsync(Guid accountId, int maxAllowedCount = 5, TimeSpan? window = null, CancellationToken cancellationToken = default)
        {
            FreqCallCount++;
            return Task.FromResult(_freqResult);
        }
    }
}
