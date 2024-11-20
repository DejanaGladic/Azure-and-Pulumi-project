using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace HttpTrigger
{
    public static class HttpTrigger
    {
        [FunctionName("HttpTrigger")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "HttpTrigger")] HttpRequest req,
            ILogger log)
        {
            try
            {
                log.LogInformation("C# HTTP trigger function processed a request.");

                //return new OkObjectResult("Dejana has created AzureFunction!");

                //connect to VM??
                // Prijem komande iz HTTP zahteva
                var customScriptCommand = "#!/bin/bash\n" +
                           "echo 'Dejana and VM'";

                // Parametri za SSH pristup
                string vmIpAddress = "10.0.0.100";  // IP adresa VM-a
                string privateKeyPath = @"C:\Users\user\.ssh\id_rsa"; // Putanja do privatnog ključa
                string adminUsername = "dejanagladic";  // Administratorsko korisničko ime

                // Kreiranje SSH klijenta
                var privateKey = new PrivateKeyFile(privateKeyPath);
                var privateKeyAuthMethod = new PrivateKeyAuthenticationMethod(adminUsername, new PrivateKeyFile(privateKeyPath));
                var connectionInfo = new Renci.SshNet.ConnectionInfo(vmIpAddress, adminUsername, privateKeyAuthMethod);

                using (var client = new SshClient(connectionInfo))
                {
                    try
                    {
                        client.Connect();  // Povezivanje na VM putem SSH
                        log.LogInformation("Connected to the VM.");

                        // Pokretanje komande koja je prosleđena iz HTTP zahteva
                        var cmd = client.RunCommand(customScriptCommand);
                        string result = cmd.Result;

                        log.LogInformation($"Command executed successfully: {result}");

                        client.Disconnect();  // Zatvaranje SSH konekcije
                    }
                    catch (Exception ex)
                    {
                        log.LogError($"Error: {ex.Message}");
                        return new BadRequestObjectResult("Some exception...");
                    }
                }

                // Odgovaranje na HTTP zahtev
                return new OkObjectResult("Dejana has created AzureFunction! and connect it to VM");
            }
            catch (Exception ex)
            {
                log.LogError($"Error occurred: {ex.Message}", ex);
                return new StatusCodeResult(500);  // Internal server error
            }
        }
    }
}
