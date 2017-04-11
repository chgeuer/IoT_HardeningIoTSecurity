namespace Sec_DeviceEmulator
{
    using Newtonsoft.Json;
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;

    public class Token
    {
        public string Hostname { get; set; }
        public string SAS { get; set; }
    }

    class Program
    {
        static void Main(string[] args) { MainAsync(args).Wait(); }

        public static async Task MainAsync(string[] args)
        {
            Console.Title = "Client";
            Console.Write("Press <return>");
            Console.ReadLine();

            string deviceId = "foo"; // "<<Replace with ID of your Device. - E.g.: Device01;>>";

            Func<string, Task<Token>> getSasTokenAsync = async (deviceIdentifier) =>
            {
                //Get Token from Discovery Service
                //Url from Discovery Service - Sync with Url from Sec_DiscoServer/Program.cs
                using (var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:8080/") })
                {
                    var msg = await httpClient.GetAsync($"IoTHub/GetToken/{deviceIdentifier}");
                    var readTokenTask = await msg.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<Token>(readTokenTask);
                }
            };

            //Ingest to IoT Hub - http Post - Use Token from Discovery Service
            var token = await getSasTokenAsync(deviceId);
            Uri targetUri = new Uri($"https://{token.Hostname}/devices/{deviceId}/messages/events?api-version=2015-08-15-preview");
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
                httpClient.DefaultRequestHeaders.Add("Authorization", token.SAS);

                var response = await httpClient.PostAsync(targetUri, new StringContent("Payload"));
                if (response.IsSuccessStatusCode)
                    Console.WriteLine("Successful Telemetry Ingest");
                else
                    Console.WriteLine($"Http Status Code: {response.StatusCode}");
            }

            Console.WriteLine("Press any key ...");
            Console.ReadLine();
        }
    }
}