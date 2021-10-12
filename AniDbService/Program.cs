using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration =
        builder.Configuration["Redis"] ?? "localhost:6379"; //localhost:6379,password=redispwexample
    options.InstanceName = "AniDbService_";
});
builder.Services.AddHttpClient("anidb", c => { c.BaseAddress = new Uri("http://api.anidb.net:9001/"); })
    .ConfigurePrimaryHttpMessageHandler(x => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    });
var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<AniDbService.Services.AniDbService>();
app.MapGet("/",
    () =>
        "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();