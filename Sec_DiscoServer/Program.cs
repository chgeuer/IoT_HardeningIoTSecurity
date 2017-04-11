namespace Sec_DiscoServer
{
    using Microsoft.Owin.Hosting;
    using System;

    class Program
    {
        static void Main(string[] args)
        {
            //**************************************************
            //* Please modify/change IoT Hub - Device Settings 
            //* in Sec_DiscoServer/Controller/TokenController.cs
            //**************************************************
            
            //Self-Hosted REST API; Local Port No
            string baseAddress = "http://localhost:8080/";

            using (WebApp.Start<Startup>(baseAddress))
            {
                Console.ReadLine();
            }

        }
    }
}
