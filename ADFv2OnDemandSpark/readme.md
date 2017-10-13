This sample code shows how you can create Data Factory, Linked Services and Pipelines to run a Spark job against HDInsight Spark cluster created on-demand(auto create and delete for the job) using Azure Data Factory V2. This can be used to operationalize your Spark load and save costs. 

You need following packages: 

Install-Package Microsoft.Azure.Management.DataFactory -Prerelease
Install-Package Microsoft.Azure.Management.ResourceManager -Prerelease
Install-Package Microsoft.IdentityModel.Clients.ActiveDirectory