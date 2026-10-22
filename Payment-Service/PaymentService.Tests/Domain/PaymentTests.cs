using FluentAssertions;
using PaymentService.Domain;

namespace PaymentService.Tests.Domain;

public class PaymentTests
{
    [Fact]
    public void UpdateStatus_QuandoFalhaComMotivo_DeveGuardarMotivoSemEspacosExtras()
    {
        var payment = CreatePayment();

        payment.UpdateStatus(PaymentStatus.Failed, DateTimeOffset.UtcNow, "  Saldo insuficiente  ");

        payment.FailureReason.Should().Be("Saldo insuficiente");
    }

    [Fact]
    public void UpdateStatus_QuandoSaiDeFailed_DeveLimparMotivo()
    {
        var payment = CreatePayment();
        payment.UpdateStatus(PaymentStatus.Failed, DateTimeOffset.UtcNow, "Saldo insuficiente");

        payment.UpdateStatus(PaymentStatus.Paid, DateTimeOffset.UtcNow, "ignorado");

        payment.FailureReason.Should().BeNull();
    }

    private static Payment CreatePayment() =>
        new(Guid.NewGuid(), Guid.NewGuid(), 100m, DateTimeOffset.UtcNow);
}
