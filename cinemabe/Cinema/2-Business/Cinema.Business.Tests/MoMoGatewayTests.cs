using System.Net;
using System.Text.Json;
using Cinema.Business.Contracts.Payments;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class MoMoGatewayTests
{
    [Fact]
    public void IsConfigured_AllKeysPresent_True()
    {
        var gateway = MoMoTestHelper.Gateway();

        gateway.IsConfigured.Should().BeTrue();
    }

    [Theory]
    [InlineData("Payments:MoMo:PartnerCode")]
    [InlineData("Payments:MoMo:AccessKey")]
    [InlineData("Payments:MoMo:SecretKey")]
    public void IsConfigured_MissingKey_False(string key)
    {
        var gateway = MoMoTestHelper.Gateway(overrides: new Dictionary<string, string?> { [key] = "" });

        gateway.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyPayment_AlwaysFails_EvenWhenConfigured()
    {
        var gateway = MoMoTestHelper.Gateway();

        var verification = await gateway.VerifyPaymentAsync("4088878653", 100_000);

        verification.Success.Should().BeFalse();
        verification.FailureReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreatePayment_NotConfigured_Throws()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, """{"resultCode":0,"payUrl":"https://pay.example/x"}""");
        var gateway = MoMoTestHelper.Gateway(HttpFactoryOf(handler), new Dictionary<string, string?> { ["Payments:MoMo:SecretKey"] = "" });

        var act = () => gateway.CreatePaymentAsync(Guid.NewGuid(), 100_000, null, null);

        await act.Should().ThrowAsync<InvalidOperationException>();
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task CreatePayment_Success_ReturnsPayUrlAndOrderReference()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, """{"resultCode":0,"payUrl":"https://pay.example/x"}""");
        var gateway = MoMoTestHelper.Gateway(HttpFactoryOf(handler));
        var invoiceId = Guid.NewGuid();

        var initiation = await gateway.CreatePaymentAsync(invoiceId, 100_000, null, null);

        initiation.PaymentReference.Should().Be(invoiceId.ToString("N"));
        initiation.RedirectUrl.Should().Be("https://pay.example/x");
        handler.LastRequestUri.Should().Be(new Uri(MoMoTestHelper.Endpoint));
    }

    [Fact]
    public async Task CreatePayment_SignsRequestBody()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, """{"resultCode":0,"payUrl":"https://pay.example/x"}""");
        var gateway = MoMoTestHelper.Gateway(HttpFactoryOf(handler));

        await gateway.CreatePaymentAsync(Guid.NewGuid(), 100_000.6, null, null);

        using var doc = JsonDocument.Parse(handler.LastRequestBody!);
        var root = doc.RootElement;
        root.GetProperty("amount").GetString().Should().Be("100001");
        root.GetProperty("requestType").GetString().Should().Be("captureWallet");

        var raw = $"accessKey={MoMoTestHelper.AccessKey}&amount={root.GetProperty("amount").GetString()}" +
                  $"&extraData={root.GetProperty("extraData").GetString()}&ipnUrl={root.GetProperty("ipnUrl").GetString()}" +
                  $"&orderId={root.GetProperty("orderId").GetString()}&orderInfo={root.GetProperty("orderInfo").GetString()}" +
                  $"&partnerCode={root.GetProperty("partnerCode").GetString()}&redirectUrl={root.GetProperty("redirectUrl").GetString()}" +
                  $"&requestId={root.GetProperty("requestId").GetString()}&requestType={root.GetProperty("requestType").GetString()}";
        root.GetProperty("signature").GetString().Should().Be(MoMoTestHelper.Sign(raw));
    }

    [Fact]
    public async Task CreatePayment_NullReturnUrl_UsesConfiguredReturnUrl()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, """{"resultCode":0,"payUrl":"https://pay.example/x"}""");
        var gateway = MoMoTestHelper.Gateway(HttpFactoryOf(handler));

        await gateway.CreatePaymentAsync(Guid.NewGuid(), 100_000, null, null);

        using var doc = JsonDocument.Parse(handler.LastRequestBody!);
        doc.RootElement.GetProperty("redirectUrl").GetString().Should().Be("https://cinema.example/payment-return");
    }

    [Fact]
    public async Task CreatePayment_NonZeroResultCode_Throws()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, """{"resultCode":1006,"message":"Transaction denied."}""");
        var gateway = MoMoTestHelper.Gateway(HttpFactoryOf(handler));

        var act = () => gateway.CreatePaymentAsync(Guid.NewGuid(), 100_000, null, null);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*1006*");
    }

    [Fact]
    public void ParseCallback_AcceptsValidlySignedIpn()
    {
        var gateway = MoMoTestHelper.Gateway();
        var invoiceId = Guid.NewGuid();

        var result = gateway.ParseCallback(MoMoTestHelper.SignedIpn(invoiceId, 100_000));

        result.Success.Should().BeTrue(result.FailureReason);
        result.InvoiceId.Should().Be(invoiceId);
        result.PaymentReference.Should().Be("4088878653");
        result.Status.Should().Be(PaymentCallbackStatus.Paid);
    }

    [Fact]
    public void ParseCallback_TamperedSignature_StatusInvalidSignature()
    {
        var gateway = MoMoTestHelper.Gateway();
        var callbackData = MoMoTestHelper.SignedIpn(Guid.NewGuid(), 100_000);
        callbackData["signature"] = "not-a-valid-hash";

        var result = gateway.ParseCallback(callbackData);

        result.Success.Should().BeFalse();
        result.InvoiceId.Should().Be(Guid.Empty);
        result.Status.Should().Be(PaymentCallbackStatus.InvalidSignature);
    }

    [Fact]
    public void ParseCallback_AmountAlteredAfterSigning_Rejected()
    {
        var gateway = MoMoTestHelper.Gateway();
        var callbackData = MoMoTestHelper.SignedIpn(Guid.NewGuid(), 100_000);
        callbackData["amount"] = "1";

        var result = gateway.ParseCallback(callbackData);

        result.Success.Should().BeFalse();
        result.Status.Should().Be(PaymentCallbackStatus.InvalidSignature);
    }

    [Fact]
    public void ParseCallback_UppercaseSignature_Accepted()
    {
        var gateway = MoMoTestHelper.Gateway();
        var callbackData = MoMoTestHelper.SignedIpn(Guid.NewGuid(), 100_000);
        callbackData["signature"] = callbackData["signature"].ToUpperInvariant();

        var result = gateway.ParseCallback(callbackData);

        result.Success.Should().BeTrue(result.FailureReason);
    }

    [Fact]
    public void ParseCallback_MissingSignature_Rejected()
    {
        var gateway = MoMoTestHelper.Gateway();
        var callbackData = MoMoTestHelper.SignedIpn(Guid.NewGuid(), 100_000);
        callbackData.Remove("signature");

        var result = gateway.ParseCallback(callbackData);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public void ParseCallback_NonZeroResultCode_StatusPaymentFailed()
    {
        var gateway = MoMoTestHelper.Gateway();
        var invoiceId = Guid.NewGuid();

        var result = gateway.ParseCallback(MoMoTestHelper.SignedIpn(invoiceId, 100_000, resultCode: "1006"));

        result.Success.Should().BeFalse();
        result.InvoiceId.Should().Be(invoiceId);
        result.Status.Should().Be(PaymentCallbackStatus.PaymentFailed);
        result.FailureReason.Should().Contain("1006");
    }

    [Fact]
    public void ParseCallback_InvalidOrderId_StatusInvalidOrderReference()
    {
        var gateway = MoMoTestHelper.Gateway();

        var result = gateway.ParseCallback(MoMoTestHelper.SignedIpn(Guid.NewGuid(), 100_000, orderIdOverride: "not-a-guid"));

        result.Success.Should().BeFalse();
        result.InvoiceId.Should().Be(Guid.Empty);
        result.Status.Should().Be(PaymentCallbackStatus.InvalidOrderReference);
    }

    [Fact]
    public void ParseCallback_NotConfigured_Rejected()
    {
        var gateway = MoMoTestHelper.Gateway(overrides: new Dictionary<string, string?> { ["Payments:MoMo:SecretKey"] = "" });

        var result = gateway.ParseCallback(MoMoTestHelper.SignedIpn(Guid.NewGuid(), 100_000));

        result.Success.Should().BeFalse();
        result.Status.Should().Be(PaymentCallbackStatus.Rejected);
    }

    [Fact]
    public void ParseCallback_ValidIpn_ExposesReceivedAmount()
    {
        var gateway = MoMoTestHelper.Gateway();

        var result = gateway.ParseCallback(MoMoTestHelper.SignedIpn(Guid.NewGuid(), 100_000));

        result.ReceivedAmount.Should().Be(100_000m);
    }

    [Fact]
    public void ParseCallback_MissingAmount_StatusInvalidAmount()
    {
        var gateway = MoMoTestHelper.Gateway();

        var result = gateway.ParseCallback(MoMoTestHelper.SignedIpn(Guid.NewGuid(), 100_000, includeAmount: false));

        result.Status.Should().Be(PaymentCallbackStatus.InvalidAmount);
    }

    [Fact]
    public void ParseCallback_FailedResult_StillExposesReceivedAmount()
    {
        var gateway = MoMoTestHelper.Gateway();

        var result = gateway.ParseCallback(MoMoTestHelper.SignedIpn(Guid.NewGuid(), 100_000, resultCode: "1006"));

        result.ReceivedAmount.Should().Be(100_000m);
    }

    private static IHttpClientFactory HttpFactoryOf(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
        return factoryMock.Object;
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public int CallCount { get; private set; }
        public Uri? LastRequestUri { get; private set; }
        public string? LastRequestBody { get; private set; }

        public CapturingHandler(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequestUri = request.RequestUri;
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return new HttpResponseMessage(_statusCode) { Content = new StringContent(_responseBody) };
        }
    }
}
