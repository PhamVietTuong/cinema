using Cinema.Business.Contracts;
using Cinema.Business.Contracts.Payments;
using Cinema.Business.DTO.Invoices;
using Cinema.Service.WebApiHost.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Cinema.Service.WebApiHost.Tests;

/// <summary>Covers the per-provider acknowledgement shape of <see cref="PaymentController.PaymentCallback"/>:
/// VNPay must always get VNPay's {RspCode, Message} body (including on an unhandled exception), while every
/// other provider keeps the legacy Ok/BadRequest shape unchanged.</summary>
public class PaymentControllerCallbackTests
{
    private readonly Mock<IBookingManager> _bookingManagerMock = new();
    private readonly Mock<IPaymentGatewayResolver> _gatewaysMock = new();

    private PaymentController Controller(string resolvedGatewayName)
    {
        var gateway = new Mock<IPaymentGateway>();
        gateway.Setup(g => g.Name).Returns(resolvedGatewayName);
        _gatewaysMock.Setup(r => r.Resolve(It.IsAny<string?>())).Returns(gateway.Object);

        return new PaymentController(_bookingManagerMock.Object, null!, null!, _gatewaysMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public async Task PaymentCallback_VnPay_InvalidAmount_ReturnsRspCode04()
    {
        _bookingManagerMock.Setup(m => m.HandlePaymentCallbackAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>()))
            .ReturnsAsync(new PaymentCallbackHandlingResult("VNPay", PaymentCallbackOutcome.InvalidAmount));

        var result = await Controller("VNPay").PaymentCallback("VNPay");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<VnPayIpnResponseDTO>().Subject;
        body.RspCode.Should().Be("04");
    }

    [Fact]
    public async Task PaymentCallback_VnPay_UnhandledException_ReturnsRspCode99_Not500()
    {
        _bookingManagerMock.Setup(m => m.HandlePaymentCallbackAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await Controller("VNPay").PaymentCallback("VNPay");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<VnPayIpnResponseDTO>().Subject;
        body.RspCode.Should().Be("99");
    }

    [Fact]
    public async Task PaymentCallback_NonVnPay_Rejected_ReturnsBadRequest_LegacyShape()
    {
        _bookingManagerMock.Setup(m => m.HandlePaymentCallbackAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>()))
            .ReturnsAsync(new PaymentCallbackHandlingResult("MoMo", PaymentCallbackOutcome.Rejected));

        var result = await Controller("MoMo").PaymentCallback("MoMo");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PaymentCallback_NonVnPay_Confirmed_ReturnsOk_LegacyShape()
    {
        _bookingManagerMock.Setup(m => m.HandlePaymentCallbackAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>()))
            .ReturnsAsync(new PaymentCallbackHandlingResult("MoMo", PaymentCallbackOutcome.Confirmed));

        var result = await Controller("MoMo").PaymentCallback("MoMo");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeOfType<VnPayIpnResponseDTO>();
    }

    [Fact]
    public async Task PaymentCallback_NonVnPay_UnhandledException_FallsBackToHandleException()
    {
        _bookingManagerMock.Setup(m => m.HandlePaymentCallbackAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await Controller("MoMo").PaymentCallback("MoMo");

        // HandleException maps InvalidOperationException to 400 (see ApiControllerBase).
        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
