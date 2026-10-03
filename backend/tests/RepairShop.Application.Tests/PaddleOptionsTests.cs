using FluentAssertions;
using RepairShop.Application.Billing;

namespace RepairShop.Application.Tests;

public sealed class PaddleOptionsTests
{
    [Theory]
    [InlineData("sandbox", "pdl_sdbx_apikey_x", "test_x")]
    [InlineData("production", "pdl_live_apikey_x", "live_x")]
    [InlineData("sandbox", "legacy-key-without-prefix", "token")]
    public void Keys_from_the_configured_environment_are_accepted(string env, string apiKey, string token)
        => new PaddleBillingOptions { Environment = env, ApiKey = apiKey, ClientToken = token }.EnvironmentMismatch().Should().BeNull();

    [Theory]
    [InlineData("sandbox", "pdl_live_apikey_x", "test_x", "PADDLE_API_KEY")]
    [InlineData("sandbox", "pdl_sdbx_apikey_x", "live_x", "PADDLE_CLIENT_TOKEN")]
    [InlineData("production", "pdl_sdbx_apikey_x", "live_x", "PADDLE_API_KEY")]
    [InlineData("production", "pdl_live_apikey_x", "test_x", "PADDLE_CLIENT_TOKEN")]
    public void Keys_from_the_other_environment_are_reported(string env, string apiKey, string token, string culprit)
        => new PaddleBillingOptions { Environment = env, ApiKey = apiKey, ClientToken = token }.EnvironmentMismatch().Should().StartWith(culprit);
}
