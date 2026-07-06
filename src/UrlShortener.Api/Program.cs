using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StackExchange.Redis;
using UrlShortener.Api.Caching;
using UrlShortener.Api.Endpoints;
using UrlShortener.Api.Options;
using UrlShortener.Api.Repositories;
using UrlShortener.Api.Services;

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
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<MongoDbOptions>>().Value;
    return new MongoClient(options.ConnectionString);
});

builder.Services.AddSingleton(sp =>
{
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

// ---------------------------------------------------------------------------
// Application Services
// System Design Lesson: CounterService is singleton because it's stateless —
// it delegates all state to MongoDB's atomic findOneAndUpdate. The MongoDB
// collection handle it holds is thread-safe.
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<ICounterService, CounterService>();
builder.Services.AddSingleton<IUrlRepository, MongoUrlRepository>();
builder.Services.AddSingleton<ICacheService, RedisCacheService>();
builder.Services.AddSingleton<IUrlService, UrlService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactClient", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("AllowReactClient");

// ---------------------------------------------------------------------------
// Health check — smoke-tests that the API process is alive.
// System Design Lesson: Load balancers and container orchestrators (Docker,
// K8s) poll /health to decide whether to route traffic to this instance.
// ---------------------------------------------------------------------------
app.MapGet("/health", () => Results.Ok("OK"));

app.MapUrlEndpoints();

app.Run();

