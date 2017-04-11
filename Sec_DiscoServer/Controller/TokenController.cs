namespace Sec_DiscoServer.Controller
{
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using PCLCrypto;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web.Http;

    public class Token
    {
        public string Hostname { get; set; }
        public string SAS { get; set; }
    }

    public class TokenController : ApiController
    {
        [HttpGet]
        [Route("IoTHub/GetToken/{deviceId}")]
        public async Task<Token> Get(string deviceId)
        {
            // Device Id - IoTHub Connection Information
            // string deviceId = "<<Replace with ID of your Device. - E.g.: Device01;>>";
            string ioTHubConnectionString = Environment.GetEnvironmentVariable("AZURE_IOT_HUB_OWNER_KEY"); // "<<Replace with IoT Hub Connection String from Azure Portal - E.g.:HostName=robertsiothub.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=BZ1...Q=";
            var hub = ioTHubConnectionString.ParseAzureIoTHubConnectionString();
            string ioTHubName = hub.HostName; //"<<Replace with Name of your IoT Hub instance>>.azure-devices.net - E.g.: robertsiothub.azure-devices.net";

            //Get Device Symmetric Key
            RegistryManager registryManager = RegistryManager.CreateFromConnectionString(ioTHubConnectionString);

            Device device;
            try
            {
                device = await registryManager.AddDeviceAsync(new Device(id: deviceId));
            }
            catch (DeviceAlreadyExistsException)
            {
                device = await registryManager.GetDeviceAsync(deviceId: deviceId);
            }

            string deviceSymmetricKey = device.Authentication.SymmetricKey.PrimaryKey;

            //Create Token with fixed Endtime - 31st of this year
            DateTime _epochTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            DateTime endLiveTime = new DateTime(DateTime.UtcNow.Year, 12, 31);
            
            TimeSpan secondsEpochTime = endLiveTime.Subtract(_epochTime);
            long seconds = Convert.ToInt64(secondsEpochTime.TotalSeconds, CultureInfo.InvariantCulture);
            string expiresOnSeconds = Convert.ToString(seconds, CultureInfo.InvariantCulture);

            string url = WebUtility.UrlEncode("{0}/devices/{1}".FormatInvariant(ioTHubName, deviceId));
            string signature = Sign($"{url}\n{expiresOnSeconds}" , deviceSymmetricKey);

            StringBuilder sharedAccessSignature = new StringBuilder();
            sharedAccessSignature.AppendFormat(CultureInfo.InvariantCulture, "{0} {1}={2}&{3}={4}&{5}={6}",
                "SharedAccessSignature",
                "sr", url,
                "sig", WebUtility.UrlEncode(signature),
                "se", WebUtility.UrlEncode(expiresOnSeconds));

            return new Token { Hostname = ioTHubName, SAS = sharedAccessSignature.ToString() };
        }

        static string Sign(string requestString, string key)
        {
            var algorithm = WinRTCrypto.MacAlgorithmProvider.OpenAlgorithm(MacAlgorithm.HmacSha256);
            var hash = algorithm.CreateHash(Convert.FromBase64String(key));
            hash.Append(Encoding.UTF8.GetBytes(requestString));
            var mac = hash.GetValueAndReset();
            return Convert.ToBase64String(mac);
        }
    }

    public class IoTHubConnectionStringParams
    {
        public string HostName { get; set; }
        public string SharedAccessKeyName { get; set; }
        public byte[] SharedAccessKey { get; set; }
    }

    public static class MyExtensions
    {
        public static Dictionary<string, string> ParseConnectionString(this string connectionString)
        {
            return connectionString.Split(';')
                .ToDictionary(
                    _ => _.Substring(0, _.IndexOf('=')),
                    _ => _.Substring(_.IndexOf('=') + 1));
        }

        public static IoTHubConnectionStringParams ParseAzureIoTHubConnectionString(this string connectionString)
        {
            var pairs = connectionString.ParseConnectionString();

            return new IoTHubConnectionStringParams
            {
                HostName = pairs["HostName"],
                SharedAccessKeyName = pairs["SharedAccessKeyName"],
                SharedAccessKey = Convert.FromBase64String(pairs["SharedAccessKey"])
            };
        }

        public static string GetSASToken(this IoTHubConnectionStringParams hub, string deviceId, TimeSpan duration)
        {
            string policyName = "device";

            var sr = WebUtility.UrlEncode($"{hub.HostName}/devices/{deviceId}".ToLower());

            TimeSpan fromEpochStart = DateTime.UtcNow - new DateTime(1970, 1, 1);
            var se = Convert.ToString((long)(fromEpochStart.TotalSeconds + duration.TotalSeconds));

            var stringToSign = $"{sr}\n{se}";
            var hmac = new HMACSHA256(hub.SharedAccessKey);
            var sigBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
            var sig = WebUtility.UrlEncode(Convert.ToBase64String(sigBytes));

            return $"SharedAccessSignature sr={sr}&sig={sig}&se={se}"; // + $"&skn={policyName}";
        }
    }
}