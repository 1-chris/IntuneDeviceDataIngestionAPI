using System.Text;
using System.Security.Cryptography;
using System.Net.Http.Headers;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<LogAnalyticsBackgroundService>();
builder.Services.AddHostedService(p => p.GetRequiredService<LogAnalyticsBackgroundService>());

var app = builder.Build();
//app.UseHttpsRedirection();

string? clientAuthenticationToken = builder.Configuration["ClientAuthenticationToken"];

app.MapPost("DeviceDataEndpoint", (dynamic data, HttpContext context, IServiceProvider serviceProvider) =>
{
    var loganalytics = serviceProvider.GetRequiredService<LogAnalyticsBackgroundService>();

    // verify request headers has valid DeviceId header with guid
    if (!Guid.TryParse(context.Request.Headers["DeviceId"], out Guid deviceId))
        return Results.Unauthorized();

    if (context.Request.Headers["Authorization"] != clientAuthenticationToken)
        return Results.Unauthorized();

    loganalytics.AddSysinfoItem(data);

    return Results.Ok();
});

app.MapPost("DeviceDataEndpointProcess", (dynamic data, HttpContext context, IServiceProvider serviceProvider) =>
{
    var loganalytics = serviceProvider.GetRequiredService<LogAnalyticsBackgroundService>();
    // verify request headers has valid DeviceId header with guid
    if (!Guid.TryParse(context.Request.Headers["DeviceId"], out Guid deviceId))
        return Results.Unauthorized();

    if (context.Request.Headers["Authorization"] != clientAuthenticationToken)
        return Results.Unauthorized();

    loganalytics.AddProcessItem(data);

    return Results.Ok();
});

app.Run();


public class LogAnalyticsBackgroundService : BackgroundService
{
    readonly ILogger<LogAnalyticsBackgroundService> _logger;
    private readonly IConfiguration _configuration;
    public List<object> ProcessItems = new();
    public List<object> SysinfoItems = new();
    public string CustomerId { get; set; } = "";
    public string SharedKey { get; set; } = "";

    public LogAnalyticsBackgroundService(ILogger<LogAnalyticsBackgroundService> logger, IConfiguration configuration)
    {
        System.Console.WriteLine("setting up service");
        _logger = logger;
        _configuration = configuration;

        CustomerId = _configuration.GetValue<string>("WorkspaceCustomerId");
        SharedKey = _configuration.GetValue<string>("WorkspaceSharedKey");

        System.Console.WriteLine($"{CustomerId} - {SharedKey}");
    }

    public void AddSysinfoItem(object item)
    {
        SysinfoItems.Add(item);
    }

    public void AddProcessItem(object item)
    {
        ProcessItems.Add(item);
    }


    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        System.Console.WriteLine("Running ExecuteAsync");
        while (!stoppingToken.IsCancellationRequested)
        {
            if (SysinfoItems.Count > 0)
            {
                _logger.LogInformation($"Posting {SysinfoItems.Count} basic items to log analytics at {DateTime.UtcNow}");

                var json = JsonSerializer.Serialize(SysinfoItems);
                var datestring = DateTime.UtcNow.ToString("r");
                var jsonBytes = Encoding.UTF8.GetBytes(json);
                string stringToHash = "POST\n" + jsonBytes.Length + "\napplication/json\n" + "x-ms-date:" + datestring + "\n/api/logs";
                string hashedString = BuildSignature(stringToHash, SharedKey);
                string signature = "SharedKey " + CustomerId + ":" + hashedString;
            
                _ = PostDataAsync(signature, datestring, json, CustomerId, "DeviceTelemetryBasic");
                SysinfoItems.Clear();
            }

            if (ProcessItems.Count > 0)
            {
                _logger.LogInformation($"Posting {ProcessItems.Count} process items to log analytics at {DateTime.UtcNow}");

                var json = JsonSerializer.Serialize(ProcessItems);
                var datestring = DateTime.UtcNow.ToString("r");
                var jsonBytes = Encoding.UTF8.GetBytes(json);
                string stringToHash = "POST\n" + jsonBytes.Length + "\napplication/json\n" + "x-ms-date:" + datestring + "\n/api/logs";
                string hashedString = BuildSignature(stringToHash, SharedKey);
                string signature = "SharedKey " + CustomerId + ":" + hashedString;
            
                _ = PostDataAsync(signature, datestring, json, CustomerId, "DeviceTelemetryProcess");
                ProcessItems.Clear();
            }

            await Task.Delay(3000, stoppingToken);
        }
    }

    // Build the API signature
    string BuildSignature(string message, string secret)
    {
        var encoding = new System.Text.ASCIIEncoding();
        byte[] keyByte = Convert.FromBase64String(secret);
        byte[] messageBytes = encoding.GetBytes(message);
        using (var hmacsha256 = new HMACSHA256(keyByte))
        {
            byte[] hash = hmacsha256.ComputeHash(messageBytes);
            return Convert.ToBase64String(hash);
        }
    }

    // PostData async
    async Task PostDataAsync(string signature, string date, string json, string customerId, string LogName)
    {
        try
        {
            string TimeStampField = "";
            string url = "https://" + customerId + ".ods.opinsights.azure.com/api/logs?api-version=2016-04-01";

            System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("Log-Type", LogName);
            client.DefaultRequestHeaders.Add("Authorization", signature);
            client.DefaultRequestHeaders.Add("x-ms-date", date);
            client.DefaultRequestHeaders.Add("time-generated-field", TimeStampField);

            // If charset=utf-8 is part of the content-type header, the API call may return forbidden.
            System.Net.Http.HttpContent httpContent = new StringContent(json, Encoding.UTF8);
            httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            Task<System.Net.Http.HttpResponseMessage> response = client.PostAsync(new Uri(url), httpContent);

            System.Net.Http.HttpContent responseContent = response.Result.Content;
            string result = await responseContent.ReadAsStringAsync();
        }
        catch (Exception excep)
        {
            Console.WriteLine("API Post Exception: " + excep.Message);
        }
    }

}
