using Microsoft.Extensions.Logging;
using PaymentService.Domain;

namespace PaymentService.Application.Logging;

/// <summary>Logs de negócio de pagamentos (ver elk/docs/logging-standard.md). Apenas identificadores e valores.</summary>
internal static partial class PaymentLog
{
    [LoggerMessage(EventId = 4001, EventName = "payment.created", Level = LogLevel.Information,
        Message = "Pagamento {PaymentId} criado para o agendamento {AppointmentId}. Valor {Amount}.")]
    public static partial void PaymentCreated(this ILogger logger, Guid paymentId, Guid appointmentId, decimal amount);

    [LoggerMessage(EventId = 4002, EventName = "payment.status_changed", Level = LogLevel.Information,
        Message = "Pagamento {PaymentId} (agendamento {AppointmentId}) mudou de {PreviousStatus} para {NewStatus}.")]
    public static partial void PaymentStatusChanged(this ILogger logger, Guid paymentId, Guid appointmentId, PaymentStatus previousStatus, PaymentStatus newStatus);

    [LoggerMessage(EventId = 4003, EventName = "payment.not_found", Level = LogLevel.Warning,
        Message = "Pagamento {PaymentId} não encontrado.")]
    public static partial void PaymentNotFound(this ILogger logger, Guid paymentId);
}
