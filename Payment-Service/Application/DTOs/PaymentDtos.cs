using System.ComponentModel.DataAnnotations;
using PaymentService.Domain;

namespace PaymentService.Application.DTOs;

/// <summary>Dados necessários para criar uma solicitação de pagamento.</summary>
public sealed record CreatePaymentRequest
{
    /// <summary>Identificador do agendamento odontológico associado ao pagamento.</summary>
    [Required]
    public required Guid AppointmentId { get; init; }

    /// <summary>Valor do pagamento em reais.</summary>
    [Range(0.01, 999999.99)]
    public required decimal Amount { get; init; }
}

/// <summary>Dados para simular uma mudança no processamento do pagamento.</summary>
public sealed record UpdatePaymentStatusRequest
{
    /// <summary>Novo status do pagamento.</summary>
    [EnumDataType(typeof(PaymentStatus))]
    public required PaymentStatus? Status { get; init; }

    /// <summary>Motivo da recusa. Usado apenas quando o status é <c>Failed</c> e vai no evento <c>PaymentFailed</c>.</summary>
    [MaxLength(Payment.FailureReasonMaxLength)]
    public string? Reason { get; init; }
}

/// <summary>Representação pública de um pagamento.</summary>
public sealed record PaymentResponse(
    Guid Id,
    Guid AppointmentId,
    decimal Amount,
    PaymentStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ProcessedAt,
    string? FailureReason);
