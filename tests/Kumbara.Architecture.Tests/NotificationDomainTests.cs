using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Kumbara.Notification.Application.Commands.SendNotification;
using Kumbara.Notification.Domain.Entities;
using Kumbara.Notification.Domain.Enums;
using Kumbara.Notification.Domain.Exceptions;
using Kumbara.Notification.Infrastructure.Persistence;
using Kumbara.Notification.Infrastructure.Repositories;
using Kumbara.Notification.Infrastructure.Services;
using Xunit;

namespace Kumbara.Architecture.Tests;

public class NotificationDomainTests
{
    private static NotificationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new NotificationDbContext(options);
    }

    [Fact]
    public void NotificationRecord_Creation_WithValidData_Succeeds()
    {
        var transferId = Guid.NewGuid();
        var record = new NotificationRecord(
            transferId,
            "test@kumbara.local",
            "Transfer succeeded",
            NotificationChannel.Email,
            NotificationStatus.Sent);

        Assert.NotEqual(Guid.Empty, record.Id);
        Assert.Equal(transferId, record.TransferId);
        Assert.Equal("test@kumbara.local", record.Recipient);
        Assert.Equal("Transfer succeeded", record.Message);
        Assert.Equal(NotificationChannel.Email, record.Channel);
        Assert.Equal(NotificationStatus.Sent, record.Status);
    }

    [Fact]
    public void NotificationRecord_EmptyTransferId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new NotificationRecord(
            Guid.Empty,
            "test@kumbara.local",
            "Message",
            NotificationChannel.Email));
    }

    [Fact]
    public void NotificationRecord_EmptyRecipient_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new NotificationRecord(
            Guid.NewGuid(),
            "   ",
            "Message",
            NotificationChannel.Email));
    }

    [Fact]
    public async Task NotificationRepository_AddAsync_EnforcesIdempotencyPerChannel()
    {
        using var db = CreateInMemoryDbContext();
        var repository = new NotificationRepository(db);
        var transferId = Guid.NewGuid();

        var record1 = new NotificationRecord(
            transferId,
            "user@kumbara.local",
            "Transfer 1",
            NotificationChannel.Email);

        await repository.AddAsync(record1);

        // Same transfer + channel -> DuplicateNotificationException
        var record2 = new NotificationRecord(
            transferId,
            "user@kumbara.local",
            "Transfer 2",
            NotificationChannel.Email);

        await Assert.ThrowsAsync<DuplicateNotificationException>(() => repository.AddAsync(record2));

        // Different channel for same transfer -> Should succeed
        var recordSms = new NotificationRecord(
            transferId,
            "+905551234567",
            "Transfer 1 SMS",
            NotificationChannel.Sms);

        await repository.AddAsync(recordSms);

        var notifications = await repository.GetByTransferIdAsync(transferId);
        Assert.Equal(2, notifications.Count);
    }

    [Fact]
    public async Task SendNotificationCommandHandler_DuplicateNotification_ReturnsFailure()
    {
        using var db = CreateInMemoryDbContext();
        var repository = new NotificationRepository(db);
        var senderService = new SimulatedNotificationSenderService(NullLogger<SimulatedNotificationSenderService>.Instance);
        var handler = new SendNotificationCommandHandler(repository, senderService, NullLogger<SendNotificationCommandHandler>.Instance);

        var transferId = Guid.NewGuid();
        var command = new SendNotificationCommand(
            transferId,
            "user@kumbara.local",
            "Your transfer has been processed.",
            NotificationChannel.Email);

        var result1 = await handler.Handle(command, CancellationToken.None);
        Assert.True(result1.IsSuccess);
        Assert.NotNull(result1.Value);

        // Duplicate attempt
        var result2 = await handler.Handle(command, CancellationToken.None);
        Assert.False(result2.IsSuccess);
        Assert.Contains("already been notified", result2.Error);
    }
}
