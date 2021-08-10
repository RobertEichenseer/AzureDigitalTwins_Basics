using System;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using System.IO; 
using System.Threading.Tasks;
using System.Collections.Generic;
using Azure;
using System.Text.Json;

namespace AdditiveManufacturing
{
    class Program
    {
        static async Task Main(string[] args)
        {
        
            //Autorisierung und Instanziierung Digital Twins Client
            Uri adtInstanceUri = new Uri("https://am-adt.api.weu.digitaltwins.azure.net");

            DefaultAzureCredentialOptions options = new DefaultAzureCredentialOptions(){
                ExcludeEnvironmentCredential = false,
                ExcludeAzureCliCredential = true,
                ExcludeAzurePowerShellCredential = true,
                ExcludeInteractiveBrowserCredential = true,
                ExcludeManagedIdentityCredential = true,
                ExcludeSharedTokenCacheCredential = true,
                ExcludeVisualStudioCodeCredential = true,
                ExcludeVisualStudioCredential = true
            };
            
            DefaultAzureCredential defaultAzureCredential = new DefaultAzureCredential();
            DigitalTwinsClient digitalTwinsClient = new DigitalTwinsClient(adtInstanceUri, defaultAzureCredential);

            await UploadMOdelDefinitionAsync(digitalTwinsClient);
            await CreateSingleDigitalTwinAsync(digitalTwinsClient);
            await CreateRelationshipAsync(digitalTwinsClient);
            await PatchProperty(digitalTwinsClient);
            await SendTelemetry(digitalTwinsClient);
            await QueryDigitalTwins(digitalTwinsClient);
            

            var x = digitalTwinsClient.GetModels(); 
            foreach(var y in x)
                Console.WriteLine(y.Id); 
        }

        private static async Task SendTelemetry(DigitalTwinsClient digitalTwinsClient)
        {

            string robotId = "Robo02";
            BasicDigitalTwin basicDigitalTwin = new BasicDigitalTwin();
            basicDigitalTwin.Metadata.ModelId = "dtmi:RobEich:AdditiveManufacturing:Robot;1";
            basicDigitalTwin.Id = robotId;

            string telemetryData = "{\"currentEnergyConsumption\": 15}";
            string msgIdentifier = Guid.NewGuid().ToString(); 
            try {
                await digitalTwinsClient.CreateOrReplaceDigitalTwinAsync<BasicDigitalTwin>(basicDigitalTwin.Id, basicDigitalTwin);
                await digitalTwinsClient.PublishTelemetryAsync(robotId, msgIdentifier, telemetryData);
            }
            catch(RequestFailedException ex) {
                Console.WriteLine($"Request failed: {ex.Message}");
            }
        }

        private static async Task QueryDigitalTwins(DigitalTwinsClient digitalTwinsClient)
        {
            string query = "SELECT * FROM digitaltwins";
            AsyncPageable<BasicDigitalTwin> queryResult = digitalTwinsClient.QueryAsync<BasicDigitalTwin>(query);

            await foreach (BasicDigitalTwin twin in queryResult)
            {
                Console.WriteLine(JsonSerializer.Serialize(twin));
            }
        }

        private static async Task PatchProperty(DigitalTwinsClient digitalTwinsClient)
        {

            string robotId = "Robo01";
            BasicDigitalTwin basicDigitalTwin = new BasicDigitalTwin();
            basicDigitalTwin.Metadata.ModelId = "dtmi:RobEich:AdditiveManufacturing:Robot;1";
            basicDigitalTwin.Contents.Add("engergyConsumptionLast5Minutes", "5kw");
            basicDigitalTwin.Contents.Add("robotType", "KW4738");

            basicDigitalTwin.Id = robotId;

            JsonPatchDocument jsonPatchDocument = new JsonPatchDocument(); 
            jsonPatchDocument.AppendReplace("/robotType", "KW4738-New");
            jsonPatchDocument.AppendReplace("/engergyConsumptionLast5Minutes", "10kw");

            try {
                await digitalTwinsClient.CreateOrReplaceDigitalTwinAsync<BasicDigitalTwin>(basicDigitalTwin.Id, basicDigitalTwin);
                await digitalTwinsClient.UpdateDigitalTwinAsync(robotId, jsonPatchDocument); 
            }
            catch(RequestFailedException ex) {
                Console.WriteLine($"Request failed: {ex.Message}");
            }
        }

        private static async Task CreateRelationshipAsync(DigitalTwinsClient digitalTwinsClient)
        {
            //Create Prodcution Order
            string productionOrderId = "Order020101";
            BasicDigitalTwin productionOrderTwin = new BasicDigitalTwin();
            productionOrderTwin.Metadata.ModelId = "dtmi:RobEich:AdditiveManufacturing:ProductionOrder;1";
            productionOrderTwin.Contents.Add("orderId", "020101");
            productionOrderTwin.Id = productionOrderId;

            //Create Production Line
            string productionLineId = "MUC01";
            BasicDigitalTwin productionLineTwin = new BasicDigitalTwin();
            productionLineTwin.Metadata.ModelId = "dtmi:RobEich:AdditiveManufacturing:ProductionLine;1"; 
            productionLineTwin.Id = productionLineId; 

            //Create Relationship
            BasicRelationship basicRelationship = new BasicRelationship {
                TargetId = productionLineId,
                Name = "assignedProductionLine",
                

            };
            string relationshipId = $"{productionOrderId}-assignedProductionLine->{productionLineId}";

            try {
                await digitalTwinsClient.CreateOrReplaceDigitalTwinAsync<BasicDigitalTwin>(productionOrderTwin.Id, productionOrderTwin);
                await digitalTwinsClient.CreateOrReplaceDigitalTwinAsync<BasicDigitalTwin>(productionLineId, productionLineTwin);
                await digitalTwinsClient.CreateOrReplaceRelationshipAsync(productionOrderId, relationshipId, basicRelationship);
            }
            catch(RequestFailedException ex) {
                Console.WriteLine($"Request failed: {ex.Message}");
            }
        }

        private static async Task CreateSingleDigitalTwinAsync(DigitalTwinsClient digitalTwinsClient)
        {
            //Create Single Digital Twin
            string orderId = "Order-01-01-01";
            BasicDigitalTwin basicDigitalTwin = new BasicDigitalTwin();
            basicDigitalTwin.Metadata.ModelId = "dtmi:RobEich:AdditiveManufacturing:ProductionOrder;1";
            basicDigitalTwin.Contents.Add("orderId", "01-01-01");

            basicDigitalTwin.Id = orderId;
            try {
                await digitalTwinsClient.CreateOrReplaceDigitalTwinAsync<BasicDigitalTwin>(basicDigitalTwin.Id, basicDigitalTwin);
            }
            catch(RequestFailedException ex) {
                Console.WriteLine($"Request failed: {ex.Message}");
            }
        }

        private static async Task UploadMOdelDefinitionAsync(DigitalTwinsClient digitalTwinsClient)
        {
            //Upload Model Definitionen
            string[] modelDefinitionFiles = {"ProdAsset.json", "ConveyorBelt.json", "Printer.json", "ProductionLine.json", "ProductionOrder.json", "Robot.json"}; 
            foreach(string file in modelDefinitionFiles)
            {
                List<string>  modelDefinition = new List<string> {(await File.ReadAllTextAsync(Path.Combine("./models/", file)))}; 
                try{
                    await digitalTwinsClient.CreateModelsAsync(modelDefinition);
                }
                catch (RequestFailedException ex) {
                    Console.WriteLine($"Request failed: {ex.Message}"); 
                }
            }
        }
    }
}
