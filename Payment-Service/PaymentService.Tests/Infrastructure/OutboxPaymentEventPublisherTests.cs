using FluentAssertions;
using Messaging.Contracts.Payments;
using Messaging.Outbox;
using Moq;
using PaymentService.Domain;
using PaymentService.Infrastructure.Messaging;

namespace PaymentService.Tests.Infrastructure;

public class OutboxPaymentEventPublisherTests
{
    private readonly Mock<IOutbox> _outbox = new();
    private readonly OutboxPaymentEventPublisher _publisher;

    public OutboxPaymentEventPublisherTests()
    {
        _publisher = new OutboxPaymentEventPublisher(_outbox.Object);
    }

    [Fact]
    public async Task PublishAsync_QuandoPago_DeveEnfileirarPaymentApprovedEmCentavosComChaveDoAgendamento()
    {
        var payment = CreatePayment(150.75m);
        payment.UpdateStatus(PaymentStatus.Paid, new DateTimeOffset(2026, 10, 20, 13, 0, 0, TimeSpan.FromHours(-3)));
        PaymentApproved? captured = null;
        _outbox
            .Setup(o => o.Enqueue(It.IsAny<PaymentApproved>(), It.IsAny<string?>()))
            .Callback<PaymentApproved, string?>((e, _) => captured = e);

        await _publisher.PublishAsync(payment);

        captured.Should().Be(new PaymentApproved(
            payment.Id,
            payment.AppointmentId,
            15075,
            new DateTime(2026, 10, 20, 16, 0, 0, DateTimeKind.Utc)));
        captured!.ApprovedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        captured.Key.Should().Be(payment.AppointmentId.ToString());
    }

    [Fact]
    public async Task PublishAsync_QuandoFalhouComMotivo_DeveEnfileirarPaymentFailedComOMotivo()
    {
        var payment = CreatePayment(100m);
        payment.UpdateStatus(PaymentStatus.Failed, DateTimeOffset.UtcNow, "Cartão recusado");

        await _publisher.PublishAsync(payment);

        _outbox.Verify(o => o.Enqueue(
            new PaymentFailed(payment.Id, payment.AppointmentId, "Cartão recusado"),
            null), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_QuandoFalhouSemMotivo_DeveUsarMotivoPadrao()
    {
        var payment = CreatePayment(100m);
        payment.UpdateStatus(PaymentStatus.Failed, DateTimeOffset.UtcNow);

        await _publisher.PublishAsync(payment);

        _outbox.Verify(o => o.Enqueue(
            It.Is<PaymentFailed>(e => e.Reason == OutboxPaymentEventPublisher.DefaultFailureReason),
            null), Times.Once);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Processing)]
    [InlineData(PaymentStatus.Cancelled)]
    public async Task PublishAsync_QuandoStatusNaoTemEventoNoContrato_NaoDeveEnfileirarNada(PaymentStatus status)
    {
        var payment = CreatePayment(100m);
        payment.UpdateStatus(status, DateTimeOffset.UtcNow);

        await _publisher.PublishAsync(payment);

        _outbox.Invocations.Should().BeEmpty();
    }

    private static Payment CreatePayment(decimal amount) =>
        new(Guid.NewGuid(), Guid.NewGuid(), amount, DateTimeOffset.UtcNow);
}
