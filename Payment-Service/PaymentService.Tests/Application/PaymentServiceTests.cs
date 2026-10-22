using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using PaymentService.Application.DTOs;
using PaymentService.Domain;
using PaymentService.Domain.Ports;
using PaymentService.Infrastructure.Telemetry;
using PaymentServiceImpl = PaymentService.Application.Services.PaymentService;

namespace PaymentService.Tests.Application;

public class PaymentServiceTests
{
    private readonly Mock<IPaymentRepository> _repository = new();
    private readonly Mock<IPaymentEventPublisher> _publisher = new();
    private readonly FakeLogger<PaymentServiceImpl> _logger = new();
    private readonly PaymentServiceImpl _service;

    public PaymentServiceTests()
    {
        _service = new PaymentServiceImpl(_repository.Object, _publisher.Object, new PaymentMetrics(), _logger);
    }

    [Fact]
    public async Task CreateAsync_QuandoRequisicaoValida_DeveCriarPagamentoPendenteELogarCriacao()
    {
        var appointmentId = Guid.NewGuid();

        var response = await _service.CreateAsync(new CreatePaymentRequest { AppointmentId = appointmentId, Amount = 150m });

        response.Status.Should().Be(PaymentStatus.Pending);
        _repository.Verify(r => r.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Once);
        _publisher.Verify(p => p.PublishAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Once);
        var log = SingleLog("payment.created");
        log.Level.Should().Be(LogLevel.Information);
        log.StructuredState!.Should().Contain(p => p.Key == "PaymentId" && p.Value == response.Id.ToString());
        log.StructuredState!.Should().Contain(p => p.Key == "AppointmentId" && p.Value == appointmentId.ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task CreateAsync_QuandoValorInvalido_DeveLancarExcecaoSemLogarCriacao(decimal amount)
    {
        var act = () => _service.CreateAsync(new CreatePaymentRequest { AppointmentId = Guid.NewGuid(), Amount = amount });

        await act.Should().ThrowAsync<ArgumentException>();
        _logger.Collector.GetSnapshot().Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_QuandoAgendamentoVazio_DeveLancarExcecao()
    {
        var act = () => _service.CreateAsync(new CreatePaymentRequest { AppointmentId = Guid.Empty, Amount = 10m });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateStatusAsync_QuandoPagamentoExiste_DeveAtualizarELogarStatusAnteriorENovo()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m, DateTimeOffset.UtcNow);
        _repository.Setup(r => r.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);

        var response = await _service.UpdateStatusAsync(payment.Id, new UpdatePaymentStatusRequest { Status = PaymentStatus.Paid });

        response!.Status.Should().Be(PaymentStatus.Paid);
        _repository.Verify(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()), Times.Once);
        var log = SingleLog("payment.status_changed");
        log.StructuredState!.Should().Contain(p => p.Key == "PreviousStatus" && p.Value == "Pending");
        log.StructuredState!.Should().Contain(p => p.Key == "NewStatus" && p.Value == "Paid");
    }

    [Fact]
    public async Task CreateAsync_QuandoRequisicaoValida_DeveEnfileirarEventoAntesDeSalvar()
    {
        var calls = new List<string>();
        _publisher.Setup(p => p.PublishAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("publish")).Returns(Task.CompletedTask);
        _repository.Setup(r => r.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("save")).Returns(Task.CompletedTask);

        await _service.CreateAsync(new CreatePaymentRequest { AppointmentId = Guid.NewGuid(), Amount = 150m });

        calls.Should().Equal("publish", "save");
    }

    [Fact]
    public async Task UpdateStatusAsync_QuandoStatusMuda_DeveEnfileirarEventoAntesDeSalvar()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m, DateTimeOffset.UtcNow);
        _repository.Setup(r => r.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        var calls = new List<string>();
        _publisher.Setup(p => p.PublishAsync(payment, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("publish")).Returns(Task.CompletedTask);
        _repository.Setup(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("save")).Returns(Task.CompletedTask);

        await _service.UpdateStatusAsync(payment.Id, new UpdatePaymentStatusRequest { Status = PaymentStatus.Paid });

        calls.Should().Equal("publish", "save");
    }

    [Fact]
    public async Task UpdateStatusAsync_QuandoStatusNaoMuda_DeveSalvarSemPublicarDeNovo()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m, DateTimeOffset.UtcNow);
        payment.UpdateStatus(PaymentStatus.Paid, DateTimeOffset.UtcNow);
        _repository.Setup(r => r.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);

        await _service.UpdateStatusAsync(payment.Id, new UpdatePaymentStatusRequest { Status = PaymentStatus.Paid });

        _repository.Verify(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()), Times.Once);
        _publisher.Verify(p => p.PublishAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatusAsync_QuandoFalhaComMotivo_DevePublicarComMotivoERetornarNaResposta()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m, DateTimeOffset.UtcNow);
        _repository.Setup(r => r.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        string? reasonWhenPublished = null;
        _publisher.Setup(p => p.PublishAsync(payment, It.IsAny<CancellationToken>()))
            .Callback<Payment, CancellationToken>((p, _) => reasonWhenPublished = p.FailureReason)
            .Returns(Task.CompletedTask);

        var response = await _service.UpdateStatusAsync(
            payment.Id,
            new UpdatePaymentStatusRequest { Status = PaymentStatus.Failed, Reason = "Cartão recusado" });

        reasonWhenPublished.Should().Be("Cartão recusado");
        response!.FailureReason.Should().Be("Cartão recusado");
    }

    [Fact]
    public async Task UpdateStatusAsync_QuandoPagamentoNaoExiste_DeveRetornarNuloELogarNotFound()
    {
        var id = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Payment?)null);

        var response = await _service.UpdateStatusAsync(id, new UpdatePaymentStatusRequest { Status = PaymentStatus.Paid });

        response.Should().BeNull();
        SingleLog("payment.not_found").Level.Should().Be(LogLevel.Warning);
        _publisher.Verify(p => p.PublishAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_QuandoPagamentoNaoExiste_DeveLogarNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Payment?)null);

        var response = await _service.GetByIdAsync(Guid.NewGuid());

        response.Should().BeNull();
        SingleLog("payment.not_found");
    }

    private FakeLogRecord SingleLog(string eventName) =>
        _logger.Collector.GetSnapshot().Where(l => l.Id.Name == eventName).Should().ContainSingle().Subject;
}
