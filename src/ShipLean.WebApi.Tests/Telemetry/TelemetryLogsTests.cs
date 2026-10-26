using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
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
    private Running? _running;
    private string? _previousEndpoint;
    private string? _previousProtocol;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.AddControllers().AddApplicationPart(typeof(TelemetryLogsTests).Assembly);

    protected override async Task BeforeHostStartAsync()
    {
        var receiver = await Receiver.StartAsync();

        // The server only starts an Activity per request when something listens; the OTel tracing pipeline is empty here.
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        _running = new Running(receiver, listener);

        _previousEndpoint = Environment.GetEnvironmentVariable(EndpointKey);
        _previousProtocol = Environment.GetEnvironmentVariable(ProtocolKey);
        Environment.SetEnvironmentVariable(EndpointKey, receiver.Address);
        Environment.SetEnvironmentVariable(ProtocolKey, "http/protobuf");
    }

    protected override async Task AfterHostStopAsync()
    {
        Environment.SetEnvironmentVariable(EndpointKey, _previousEndpoint);
        Environment.SetEnvironmentVariable(ProtocolKey, _previousProtocol);
        if (_running is not null)
        {
            await _running.StopAsync();
        }
    }

    [Test]
    public async Task Log_WhenWrittenInsideRequest_ShouldReachOtlpReceiverWithRequestTraceId()
    {
        var response = await Client.GetAsync("/api/test-controller/log-probe");
        var traceId = (await response.Content.ReadAsStringAsync()).Trim('"');
        var expected = Convert.FromHexString(traceId);

        // The OTLP log record carries the trace id as 16 raw bytes.
        var batch = await _running!.Receiver.WaitForBatchAsync(body => body.AsSpan().IndexOf(expected) >= 0);
        batch.ShouldNotBeNull();
    }

    [Test]
    public async Task AddOtlpSink_WhenEndpointSet_ShouldPostLogsToTheLogsPath()
    {
        await using var receiver = await Receiver.StartAsync();
        var set = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [EndpointKey] = receiver.Address,
            [ProtocolKey] = "http/protobuf",
        }).Build();

        using (var logger = new LoggerConfiguration().AddOtlpSink(set).CreateLogger())
        {
            logger.Warning("somebody home");
        }

        (await receiver.WaitForBatchAsync(body => body.AsSpan().IndexOf("somebody home"u8) >= 0)).ShouldNotBeNull();
    }

    [Test]
    public async Task AddOtlpSink_WhenEndpointUnset_ShouldNotAddASink()
    {
        await using var receiver = await Receiver.StartAsync();
        var unset = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ProtocolKey] = "http/protobuf",
            ["OTEL_EXPORTER_OTLP_HEADERS"] = "x-key=abc",
        }).Build();

        using (var logger = new LoggerConfiguration().AddOtlpSink(unset).CreateLogger())
        {
            logger.Warning("nothing listens");
        }

        receiver.Count.ShouldBe(0);
    }

    // Not IDisposable on purpose: it is stopped from AfterHostStopAsync, after the host's last log flush.
    private sealed class Running(Receiver receiver, ActivityListener listener)
    {
        public Receiver Receiver => receiver;

        public async Task StopAsync()
        {
            listener.Dispose();
            await receiver.DisposeAsync();
        }
    }

    // An in-process OTLP/HTTP receiver: keeps the body of every POST to /v1/logs.
    private sealed class Receiver : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly ConcurrentQueue<byte[]> _bodies = new();

        private Receiver(WebApplication app) => _app = app;

        public string Address => _app.Urls.First();

        public int Count => _bodies.Count;

        public static async Task<Receiver> StartAsync()
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var app = builder.Build();
            var receiver = new Receiver(app);
            app.MapPost("/v1/logs", async (HttpRequest request) =>
            {
                using var body = new MemoryStream();
                await request.Body.CopyToAsync(body);
                receiver._bodies.Enqueue(body.ToArray());
                return Results.Ok();
            });
            await app.StartAsync();
            return receiver;
        }

        // Waits for the sink's periodic batch (a couple of seconds) to deliver a matching body.
        public async Task<byte[]?> WaitForBatchAsync(Func<byte[], bool> matches)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
            try
            {
                while (await timer.WaitForNextTickAsync(timeout.Token))
                {
                    var found = _bodies.FirstOrDefault(matches);
                    if (found is not null)
                    {
                        return found;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }

            return null;
        }

        public ValueTask DisposeAsync() => _app.DisposeAsync();
    }
}
