using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Rest;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Azure.Management.DataFactory;
using Microsoft.Azure.Management.DataFactory.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace ADFv2OnDemandSpark
{
    class CreateandRun
    {
        static void Main(string[] args)
        {
            // Set variables
         
            string tenantID = ""; //tenant ID
            string applicationId = ""; //ID of the Service Principal who's contributor role of your sub
            string authenticationKey = ""; //Service Principal Key

            string subscriptionId = ""; //Subscription ID
            string resourceGroup = ""; //Resource Group Name

            // Note that the data stores (Azure Storage, Azure SQL Database, etc.) and computes (HDInsight, etc.) used by a data factory can be in other regions.
            string region = "East US"; //currently ADF support East US and East US 2, it's for metadata store only. We can orchestrate resources in way more regions. 
            string dataFactoryName = "test001"; //Data Factory Name

            // Specify the source Azure Blob information
            string storageAccount = ""; //Account of the Storage used as Spark's primary storage and as the Spark script store
            string storageKey = ""; //Storage key
            
            string storageLinkedServiceName = "myAzureStorageLinkedService";
            string sparkLinkedServiceName = "mySparkOnDemandPipeline";
            
            string pipelineName = "mySparkPiepline";


            // Authenticate and create a data factory management client
            var context = new AuthenticationContext("https://login.windows.net/" + tenantID);
            ClientCredential cc = new ClientCredential(applicationId, authenticationKey);
            AuthenticationResult result = context.AcquireTokenAsync("https://management.azure.com/", cc).Result;
            ServiceClientCredentials cred = new TokenCredentials(result.AccessToken);
            var client = new DataFactoryManagementClient(cred) { SubscriptionId = subscriptionId };

            // Create a data factory
            Console.WriteLine("Creating a data factory " + dataFactoryName + "...");
            Factory dataFactory = new Factory
            {
                Location = region,
                Identity = new FactoryIdentity()

            };
            client.Factories.CreateOrUpdate(resourceGroup, dataFactoryName, dataFactory);
            Console.WriteLine(SafeJsonConvert.SerializeObject(dataFactory, client.SerializationSettings));

            while (client.Factories.Get(resourceGroup, dataFactoryName).ProvisioningState == "PendingCreation")
            {
                System.Threading.Thread.Sleep(1000);
            }

            // Create an Azure Storage linked service
            Console.WriteLine("Creating linked service " + storageLinkedServiceName + "...");

            LinkedServiceResource storageLinkedService = new LinkedServiceResource(
                new AzureStorageLinkedService
                {
                    ConnectionString = new SecureString("DefaultEndpointsProtocol=https;AccountName=" + storageAccount + ";AccountKey=" + storageKey)
                }
            );
            client.LinkedServices.CreateOrUpdate(resourceGroup, dataFactoryName, storageLinkedServiceName, storageLinkedService);
            Console.WriteLine(SafeJsonConvert.SerializeObject(storageLinkedService, client.SerializationSettings));

            Console.WriteLine("Creating linked service " + sparkLinkedServiceName + "...");

            LinkedServiceResource sparkLinkedService = new LinkedServiceResource(
                new HDInsightOnDemandLinkedService
                {
                    ClusterSize = 1,
                    ClusterType = "Spark",
                    TimeToLive = "00:15:00", //time for the cluster to be alive before being deleted after all jobs are completed. 
                    ClusterNamePrefix = "ByADF_", //identifier for you to tell this is auto created by ADF.
                    HostSubscriptionId = subscriptionId,
                    ServicePrincipalId = applicationId,
                    ServicePrincipalKey = new SecureString(authenticationKey),
                    Tenant = tenantID,
                    ClusterResourceGroup = resourceGroup,
                    Version = "3.6",
                    LinkedServiceName = new LinkedServiceReference(storageLinkedServiceName)
                }
            );
            client.LinkedServices.CreateOrUpdate(resourceGroup, dataFactoryName, sparkLinkedServiceName, sparkLinkedService);
            Console.WriteLine(SafeJsonConvert.SerializeObject(sparkLinkedService, client.SerializationSettings));


            // Create a pipeline with copy activity
            Console.WriteLine("Creating pipeline " + pipelineName + "...");
            PipelineResource pipeline = new PipelineResource
            {
                Activities = new List<Activity>
                {
                    new HDInsightSparkActivity
                    {
                        Name ="mySparkActivity",
                        LinkedServiceName = new LinkedServiceReference(sparkLinkedServiceName),
                        RootPath = "adftutorial/spark", //path to spark root foler
                        EntryFilePath = "script/WordCount_Spark.py", //path to python or scala file
                        GetDebugInfo = "Failure",
                        SparkJobLinkedService = new LinkedServiceReference(storageLinkedServiceName)

                    }
                }
            };
            client.Pipelines.CreateOrUpdate(resourceGroup, dataFactoryName, pipelineName, pipeline);
            Console.WriteLine(SafeJsonConvert.SerializeObject(pipeline, client.SerializationSettings));

            // Create a pipeline run
            Console.WriteLine("Creating pipeline run...");
            CreateRunResponse runResponse = client.Pipelines.CreateRunWithHttpMessagesAsync(resourceGroup, dataFactoryName, pipelineName).Result.Body;
            Console.WriteLine("Pipeline run ID: " + runResponse.RunId);

            // Monitor the pipeline run, On-Demand cluster creation takes about 20 mins. You can see the cluster on Azure Portal
            Console.WriteLine("Checking pipeline run status...");
            PipelineRun pipelineRun;
            while (true)
            {
                pipelineRun = client.PipelineRuns.Get(resourceGroup, dataFactoryName, runResponse.RunId);
                Console.WriteLine("Status: " + pipelineRun.Status);
                if (pipelineRun.Status == "InProgress")
                    System.Threading.Thread.Sleep(15000);
                else
                    break;
            }

            // Check the copy activity run details
            Console.WriteLine("Checking copy activity run details...");

            List<ActivityRun> activityRuns = client.ActivityRuns.ListByPipelineRun(
            resourceGroup, dataFactoryName, runResponse.RunId, DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(10)).ToList();
            if (pipelineRun.Status == "Succeeded")
            {
                Console.WriteLine(activityRuns.First().Output);
            }
            else
                Console.WriteLine(activityRuns.First().Error);

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();

        }
    }
}
