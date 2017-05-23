# Sign-in with Azure account credentials

Login-AzureRmAccount

# Select Azure Subscription

$subscriptionId = 
    (Get-AzureRmSubscription |
     Out-GridView `
        -Title "Select an Azure Subscription …" `
        -PassThru).SubscriptionId

Select-AzureRmSubscription `
    -SubscriptionId $subscriptionId

# Select Azure Resource Group

$rgName =
    (Get-AzureRmResourceGroup |
     Out-GridView `
        -Title "Select an Azure Resource Group …" `
        -PassThru).ResourceGroupName


# Download current list of Azure Public IP ranges

$downloadUri = 
    "https://www.microsoft.com/en-in/download/confirmation.aspx?id=41653"

$downloadPage = 
    Invoke-WebRequest -Uri $downloadUri

$xmlFileUri = 
    ($downloadPage.RawContent.Split('"') -like "https://*PublicIps*")[0]

$response = 
    Invoke-WebRequest -Uri $xmlFileUri

# Get list of regions & public IP ranges

[xml]$xmlResponse = 
    [System.Text.Encoding]::UTF8.GetString($response.Content)

$regions = 
    $xmlResponse.AzurePublicIpAddresses.Region


# Select Azure regions for which to define NSG rules

$selectedRegions =
    $regions.Name |
    Out-GridView `
        -Title "Select Azure Datacenter Regions …" `
        -PassThru

$ipRange = 
    ( $regions | 
      where-object Name -In $selectedRegions ).IpRange

# Build NSG rules

$rules = @()

$rulePriority = 400

ForEach ($subnet in $ipRange.Subnet) {

    $ruleName = "hdirule" + $subnet.Replace("/","-")
    
    $rules += 
        New-AzureRmNetworkSecurityRuleConfig `
            -Name $ruleName `
            -Description "Allow inbound 443 to Azure $subnet" `
            -Protocol * `
            -SourcePortRange * `
            -DestinationPortRange "443" `
            -SourceAddressPrefix "$subnet" `
            -DestinationAddressPrefix VirtualNetwork `
            -Access Allow `
            -Priority $rulePriority `
            -Direction Inbound        
            
    $rulePriority++

}


# Set Azure region in which to create NSG

$location = "westus"

# Create Network Security Group

$nsgName = "VNetHDI-nsg"

$nsg = 
    New-AzureRmNetworkSecurityGroup `
       -Name "$nsgName" `
       -ResourceGroupName $rgName `
       -Location $location `
       -SecurityRules $rules



# Select VNET

$vnetName = 
    (Get-AzureRmVirtualNetwork `
        -ResourceGroupName $rgName).Name |
     Out-GridView `
        -Title "Select an Azure VNET …" `
        -PassThru

$vnet = Get-AzureRmVirtualNetwork `
    -ResourceGroupName $rgName `
    -Name $vnetName

# Select Subnet

$subnetName = 
    $vnet.Subnets.Name |
    Out-GridView `
        -Title "Select an Azure Subnet …" `
        -PassThru

$subnet = $vnet.Subnets | 
    Where-Object Name -eq $subnetName

# Select nsg (for fun...no need for this case)

#$nsgName = 
#    (Get-AzureRmNetworkSecurityGroup `
#        -ResourceGroupName $rgName).Name |
#     Out-GridView `
#        -Title "Select an NSG …" `
#        -PassThru

#$nsg = Get-AzureRmNetworkSecurityGroup `
#    -ResourceGroupName $rgName `
#    -Name $nsgName

# Associate NSG to selected Subnet

Set-AzureRmVirtualNetworkSubnetConfig `
    -VirtualNetwork $vnet `
    -Name $subnetName `
    -AddressPrefix $subnet.AddressPrefix `
    -NetworkSecurityGroup $nsg |
Set-AzureRmVirtualNetwork