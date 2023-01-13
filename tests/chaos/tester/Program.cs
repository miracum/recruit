using System.CommandLine;
using System.Net.Http.Json;
using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Polly;

var rootCommand = new RootCommand("recruIT chaos testing verifier");

var mailHogApiBaseUrlOption = new Option<Uri>(
    name: "--mailhog-api-base-url",
    description: "The base URL of the MailHog api endpoint."
);
mailHogApiBaseUrlOption.SetDefaultValue(new Uri("http://localhost:8025/api/"));

// delete messages command
var deleteMessagesCommand = new Command("delete-messages", "Delete all messages stored in MailHog")
{
    mailHogApiBaseUrlOption,
};
deleteMessagesCommand.SetHandler(RunDeleteMessages, mailHogApiBaseUrlOption);
rootCommand.AddCommand(deleteMessagesCommand);

// test command
var fileOption = new Option<FileInfo?>(
            name: "--fhir-resource-bundle",
            description: "The file containing the bundle of resources to send to the FHIR server.")
{
    IsRequired = true,
};

var sendCountOption = new Option<int>(
            name: "--send-count",
            description: "The number of times the bundle should be sent to to FHIR server.");
sendCountOption.SetDefaultValue(5);

var fhirServerUrlOption = new Option<Uri>(
            name: "--fhir-server-base-url",
            description: "The base URL of the FHIR server.");
fhirServerUrlOption.SetDefaultValue(new Uri("http://localhost:8082/fhir"));

var durationOption = new Option<TimeSpan>(
    name: "--total-duration",
    description: "The total duration this test should run."
);
durationOption.SetDefaultValue(TimeSpan.FromSeconds(30));

var testCommand = new Command("test", "Run the test by submitting the FHIR resource bundle to the server.")
{
    fileOption,
    fhirServerUrlOption,
    durationOption,
    sendCountOption,
};
testCommand.SetHandler((file, fhirServerUrl, totalDuration, sendCount) => RunTest(file!, fhirServerUrl, totalDuration, sendCount),
    fileOption, fhirServerUrlOption, durationOption, sendCountOption);
rootCommand.AddCommand(testCommand);

// assert command
var expectedMessageCountOption = new Option<int>(
            name: "--expected-number-of-messages",
            description: "The expected number of messages.");
expectedMessageCountOption.SetDefaultValue(5);

var retriesOption = new Option<int>(
            name: "--retries",
            description: "The number of times to re-assert the number of messages present compared to the expected");
retriesOption.SetDefaultValue(10);

var assertCommand = new Command("assert", "Verify that the expected number of mails were received.")
{
    mailHogApiBaseUrlOption,
    expectedMessageCountOption,
    retriesOption,
};
assertCommand.SetHandler(RunAssert, mailHogApiBaseUrlOption, expectedMessageCountOption, retriesOption);
rootCommand.AddCommand(assertCommand);

return await rootCommand.InvokeAsync(args);

static async System.Threading.Tasks.Task RunTest(FileInfo fhirResourceBundle, Uri fhirServerUrl, TimeSpan totalDuration, int sendCount)
{
    Console.WriteLine($"Sending FHIR bundle from file {fhirResourceBundle.FullName} " +
        $"to server {fhirServerUrl} {sendCount} times over a duration of {totalDuration} ({totalDuration.TotalSeconds}s)");

    var fhirSerializerOptions = new JsonSerializerOptions().ForFhir(typeof(Bundle).Assembly);

    var bundleJson = File.ReadAllText(fhirResourceBundle.FullName);
    var bundle = JsonSerializer.Deserialize<Bundle>(bundleJson, fhirSerializerOptions);

    var client = new FhirClient(fhirServerUrl);

    var sleepBetween = totalDuration.TotalSeconds / sendCount;

    for (int i = 0; i < sendCount; i++)
    {
        Console.WriteLine($"Sending for the {i}. time");

        var policy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetry(3, retryAttempt =>
            {
                Console.WriteLine($"Failed to send the bundle. Attempt: {retryAttempt}");
                return TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
            });

        var response = policy.Execute(() => client.Transaction(bundle));

        Console.WriteLine($"Sleeping for {sleepBetween} s");
        await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(sleepBetween));
    }
}

static async System.Threading.Tasks.Task RunDeleteMessages(Uri mailHogServerBaseUrl)
{
    Console.WriteLine("Deleting all previous messages");

    var mailHogClient = new HttpClient
    {
        BaseAddress = mailHogServerBaseUrl,
    };

    var messagesResponse = await mailHogClient.GetFromJsonAsync<MailHogMessages>("v2/messages") ??
        throw new Exception("Getting messages from MailHog failed");

    Console.WriteLine($"A total of {messagesResponse.Total} messages already on the server.");

    var deleteResponse = await mailHogClient.DeleteAsync("v1/messages");
    deleteResponse.EnsureSuccessStatusCode();

    Console.WriteLine("Done.");
}

static async System.Threading.Tasks.Task RunAssert(Uri mailHogServerBaseUrl, int expectedMessageCount, int retries)
{
    Console.WriteLine($"Using MailHog base URL: {mailHogServerBaseUrl}");

    var client = new HttpClient
    {
        BaseAddress = mailHogServerBaseUrl,
    };

    for (int i = 0; i < retries; i++)
    {
        MailHogMessages response;
        try
        {
            response = await client.GetFromJsonAsync<MailHogMessages>("v2/messages") ??
                throw new Exception("Getting messages from MailHog failed");
        }
        catch (Exception exc)
        {
            Console.WriteLine($"Failed to get response from MailHog: {exc.Message}. Attempt: {i + 1}");
            if (i == retries - 1)
            {
                throw;
            }

            await System.Threading.Tasks.Task.Delay(TimeSpan.FromMinutes(1));
            continue;
        }

        Console.WriteLine($"Expected message count is {expectedMessageCount}. Actual: {response.Total}. Attempt: {i + 1}");

        if (expectedMessageCount == response.Total)
        {
            Console.WriteLine("✅ Found expected message count!");
            break;
        }

        if (i == retries - 1)
        {
            throw new Exception($"response.Total ({response.Total}) is not the expected {expectedMessageCount} after {i + 1} attempts.");
        }

        await System.Threading.Tasks.Task.Delay(TimeSpan.FromMinutes(1));
    }
}

record MailHogMessages(int Total);
