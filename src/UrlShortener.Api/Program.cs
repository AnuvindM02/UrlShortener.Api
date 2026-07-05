using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StackExchange.Redis;
using UrlShortener.Api.Options;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Options Pattern — binds config sections to strongly-typed classes.
// System Design Lesson: Externalised configuration is how real systems
// swap behavior across environments (dev/staging/prod) without recompiling.
// Docker overrides these via environment variables (MongoDb__ConnectionString).
// ---------------------------------------------------------------------------
builder.Services.Configure<MongoDbOptions>(builder.Configuration.GetSection(MongoDbOptions.SectionName));
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));
builder.Services.Configure<ShortUrlOptions>(builder.Configuration.GetSection(ShortUrlOptions.SectionName));

// ---------------------------------------------------------------------------
// MongoDB — registered as Singleton.
// System Design Lesson: MongoClient manages an internal connection pool.
// Creating one per request would exhaust sockets. Singleton = one pool for
// the app lifetime, which is the documented best practice.
// ---------------------------------------------------------------------------
// 1. Tell .NET to globally bind the config to the Options Pattern ONCE.
builder.Services.Configure<MongoDbOptions>(
    builder.Configuration.GetSection(MongoDbOptions.SectionName));

// 2. Register the Client
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    // Resolve the globally configured options from DI using 'sp'
    var options = sp.GetRequiredService<IOptions<MongoDbOptions>>().Value;
    return new MongoClient(options.ConnectionString);
});

// 3. Register the Database
builder.Services.AddSingleton(sp =>
{
    // Resolve BOTH the client and the options from DI
    var client = sp.GetRequiredService<IMongoClient>();
    var options = sp.GetRequiredService<IOptions<MongoDbOptions>>().Value;
    return client.GetDatabase(options.DatabaseName);
});

// ---------------------------------------------------------------------------
// Redis — registered as Singleton.
// System Design Lesson: ConnectionMultiplexer is thread-safe and multiplexes
// commands over a small number of TCP connections. One instance handles
// thousands of concurrent operations. Disposing/recreating it per request
// would kill throughput.
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = builder.Configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>()!;
    return ConnectionMultiplexer.Connect(config.ConnectionString);
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Health check — smoke-tests that the API process is alive.
// System Design Lesson: Load balancers and container orchestrators (Docker,
// K8s) poll /health to decide whether to route traffic to this instance.
// ---------------------------------------------------------------------------
app.MapGet("/health", () => Results.Ok("OK"));

app.Run();
