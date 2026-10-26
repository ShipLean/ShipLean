using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Debugging;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Infrastructure.Telemetry;

namespace ShipLean.WebApi.Tests.Telemetry;

// Not parallel: the endpoint and protocol go in through the process environment, which every host starting at the
// same time would also see.
[TestFixture]
[NonParallelizable]
public class TelemetryLogsTests : ApiFixture
{
    private const string EndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string ProtocolKey = "OTEL_EXPORTER_OTLP_PROTOCOL";
    private readonly TaskCompletionSource<byte[]> _traceSeen = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private WebApplication? _receiver;
    private ActivityListener? _listener;
    private byte[] _expectedTraceId = [];
    private string? _previousEndpoint;
    private string? _previousProtocol;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.AddControllers().AddApplicationPart(typeof(TelemetryLogsTests).Assembly);

    protected override async Task BeforeHostStartAsync()
    {
        // The receiver speaks only HTTP, so any POST to /v1/logs is a Serilog OTLP batch.
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _receiver = builder.Build();
        _receiver.MapPost("/v1/logs", async (HttpRequest request) =>
        {
            using var body = new MemoryStream();
            await request.Body.CopyToAsync(body);
            var bytes = body.ToArray();
            if (_expectedTraceId.Length > 0 && bytes.AsSpan().IndexOf(_expectedTraceId) >= 0)
            {
                _traceSeen.TrySetResult(bytes);
            }

            return Results.Ok();
        });
        await _receiver.StartAsync();
        var address = _receiver.Urls.First();

        // The server only starts an Activity per request when something listens; the OTel tracing pipeline is empty here.
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);

        _previousEndpoint = Environment.GetEnvironmentVariable(EndpointKey);
        _previousProtocol = Environment.GetEnvironmentVariable(ProtocolKey);
        Environment.SetEnvironmentVariable(EndpointKey, address);
        Environment.SetEnvironmentVariable(ProtocolKey, "http/protobuf");
    }

    protected override async Task AfterHostStopAsync()
    {
        Environment.SetEnvironmentVariable(EndpointKey, _previousEndpoint);
        Environment.SetEnvironmentVariable(ProtocolKey, _previousProtocol);
        await Task.CompletedTask;
    }

    [OneTimeTearDown]
    public async Task DisposeReceiverAsync()
    {
        _listener?.Dispose();
        if (_receiver is not null)
        {
            await _receiver.DisposeAsync();
        }
    }

    [Test]
    public async Task Log_WhenWrittenInsideRequest_ShouldReachOtlpReceiverWithRequestTraceId()
    {
        var traceId = ActivityTraceId.CreateRandom();
        _expectedTraceId = Convert.FromHexString(traceId.ToHexString());
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/test-controller/log-probe");
        request.Headers.Add("traceparent", $"00-{traceId.ToHexString()}-{ActivitySpanId.CreateRandom().ToHexString()}-01");

        (await Client.SendAsync(request)).EnsureSuccessStatusCode();
        var finished = await Task.WhenAny(_traceSeen.Task, Task.Delay(TimeSpan.FromSeconds(30)));

        finished.ShouldBe(_traceSeen.Task);
        (await _traceSeen.Task).ShouldNotBeEmpty();
    }

    [Test]
    public void AddOtlpSink_WhenEndpointSetToDeadAddress_ShouldCreateASinkThatReportsFailure()
    {
        var failures = new StringWriter();
        SelfLog.Enable(failures);
        try
        {
            var set = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                [EndpointKey] = "http://127.0.0.1:1",
                [ProtocolKey] = "http/protobuf",
            }).Build();
            using (var logger = new LoggerConfiguration().AddOtlpSink(set).CreateLogger())
            {
                logger.Warning("nobody home");
            }

            failures.ToString().ShouldNotBeEmpty();
        }
        finally
        {
            SelfLog.Disable();
        }
    }

    [Test]
    public void AddOtlpSink_WhenEndpointUnset_ShouldNotCreateASink()
    {
        var failures = new StringWriter();
        SelfLog.Enable(failures);
        try
        {
            var unset = new ConfigurationBuilder().AddInMemoryCollection().Build();
            using (var logger = new LoggerConfiguration().AddOtlpSink(unset).CreateLogger())
            {
                logger.Warning("nothing listens");
            }

            failures.ToString().ShouldBeEmpty();
        }
        finally
        {
            SelfLog.Disable();
        }
    }

    [Route("api/test-controller/log-probe")]
    public sealed class LogProbeTestController(ILogger<LogProbeTestController> logger) : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            logger.LogWarning("probe inside request");
            return Ok(Activity.Current!.TraceId.ToHexString());
        }
    }
}
