using System.Text;
using System.Security.Cryptography;
using System.Net.Http.Headers;
using System.Text.Json;


// Build the API signature
static string BuildSignature(string message, string secret)
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
static async Task PostDataAsync(string signature, string date, string json, string customerId, string LogName)
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


var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.UseHttpsRedirection();

string? sharedKey = builder.Configuration["WorkspaceSharedKey"];
string? customerId = builder.Configuration["WorkspaceCustomerId"];
string? logTableName = builder.Configuration["WorkspaceTableName"];
string? clientAuthenticationToken = builder.Configuration["ClientAuthenticationToken"];

app.MapPost("DeviceDataEndpoint", async (dynamic data, HttpContext context) =>
{

    // verify request headers has valid DeviceId header with guid
    if (!Guid.TryParse(context.Request.Headers["DeviceId"], out Guid deviceId))
        return Results.Unauthorized();

    if (context.Request.Headers["Authorization"] != clientAuthenticationToken)
        return Results.Unauthorized();

    var json = JsonSerializer.Serialize(data);

    var datestring = DateTime.UtcNow.ToString("r");
    var jsonBytes = Encoding.UTF8.GetBytes(json);
    string stringToHash = "POST\n" + jsonBytes.Length + "\napplication/json\n" + "x-ms-date:" + datestring + "\n/api/logs";
    string hashedString = BuildSignature(stringToHash, sharedKey);
    string signature = "SharedKey " + customerId + ":" + hashedString;

    await PostDataAsync(signature, datestring, json, customerId, logTableName);

    return Results.Ok();
});

app.Run();
