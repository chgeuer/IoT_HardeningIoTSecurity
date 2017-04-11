namespace Sec_DiscoServer.Controller
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web.Http;
    using PCLCrypto;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Common.Exceptions;

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
            string ioTHubName = ioTHubConnectionString.ParseConnectionString()["HostName"]; //"<<Replace with Name of your IoT Hub instance>>.azure-devices.net - E.g.: robertsiothub.azure-devices.net";

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

            var sas = MyExtensions.GetSASTokenPCL(
                hubHostname: ioTHubName, 
                deviceId: deviceId,
                deviceKey: Convert.FromBase64String(deviceSymmetricKey),
                duration: secondsEpochTime);

            return new Token { Hostname = ioTHubName, SAS = sas };
        }
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

        public static string GetSASToken(string hubHostname, string deviceId, byte[] deviceKey, TimeSpan duration)
        {
            var sr = WebUtility.UrlEncode($"{hubHostname}/devices/{deviceId}".ToLower());

            TimeSpan fromEpochStart = DateTime.UtcNow - new DateTime(1970, 1, 1);
            var se = Convert.ToString((long)(fromEpochStart.TotalSeconds + duration.TotalSeconds));

            var stringToSign = $"{sr}\n{se}";
            var hmac = new HMACSHA256(deviceKey);
            var sigBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
            var sig = WebUtility.UrlEncode(Convert.ToBase64String(sigBytes));

            return $"SharedAccessSignature sr={sr}&sig={sig}&se={se}"; 
        }

        public static string GetSASTokenPCL(string hubHostname, string deviceId, byte[] deviceKey, TimeSpan duration)
        {
            var sr = WebUtility.UrlEncode($"{hubHostname}/devices/{deviceId}".ToLower());

            TimeSpan fromEpochStart = DateTime.UtcNow - new DateTime(1970, 1, 1);
            var se = Convert.ToString((long)(fromEpochStart.TotalSeconds + duration.TotalSeconds));

            var stringToSign = $"{sr}\n{se}";
            var algo = WinRTCrypto.MacAlgorithmProvider.OpenAlgorithm(MacAlgorithm.HmacSha256);
            var hmac = algo.CreateHash(deviceKey);
            hmac.Append(Encoding.UTF8.GetBytes(stringToSign));
            var sigBytes = hmac.GetValueAndReset();
            var sig = WebUtility.UrlEncode(Convert.ToBase64String(sigBytes));

            return $"SharedAccessSignature sr={sr}&sig={sig}&se={se}";
        }
    }
}