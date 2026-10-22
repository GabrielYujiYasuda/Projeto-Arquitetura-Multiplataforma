using Messaging.Contracts.Payments;
using Messaging.Outbox;
using PaymentService.Domain;
using PaymentService.Domain.Ports;

namespace PaymentService.Infrastructure.Messaging;

/// <summary>
/// Grava no outbox os eventos do tópico <c>payments</c>. O OutboxPublisherService da lib Messaging
/// publica no Kafka depois do SaveChanges. Status sem evento no contrato (Pending, Processing,
/// Cancelled) não geram mensagem.
/// </summary>
public sealed class OutboxPaymentEventPublisher(IOutbox outbox) : IPaymentEventPublisher
{
    public const string DefaultFailureReason = "Motivo não informado.";

    public Task PublishAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        switch (payment.Status)
        {
            case PaymentStatus.Paid:
                outbox.Enqueue(new PaymentApproved(
                    payment.Id,
                    payment.AppointmentId,
                    ToCents(payment.Amount),
                    (payment.ProcessedAt ?? payment.UpdatedAt).UtcDateTime));
                break;

            case PaymentStatus.Failed:
                outbox.Enqueue(new PaymentFailed(
                    payment.Id,
                    payment.AppointmentId,
                    payment.FailureReason ?? DefaultFailureReason));
                break;
        }

        return Task.CompletedTask;
    }

    // O contrato usa centavos (inteiro); o domínio guarda reais com duas casas.
    private static int ToCents(decimal amount) =>
        (int)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
}
