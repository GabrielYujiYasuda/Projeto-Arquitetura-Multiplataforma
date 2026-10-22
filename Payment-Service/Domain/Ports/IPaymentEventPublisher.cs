using PaymentService.Domain;

namespace PaymentService.Domain.Ports;

public interface IPaymentEventPublisher
{
    /// <summary>
    /// Registra o evento correspondente ao status atual do pagamento. Chame antes de persistir o
    /// pagamento: o evento é gravado no outbox e precisa entrar no mesmo SaveChanges.
    /// </summary>
    Task PublishAsync(Payment payment, CancellationToken cancellationToken = default);
}
