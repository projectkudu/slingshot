# make sure the script stops if one of the swaps failed
$ErrorActionPreference = "Stop"

Select-AzureRmSubscription -SubscriptionName "Websites migration"

$ParametersObject = @{
	targetSlot = "staging"
}

Write-Host swapping bay site ....
Invoke-AzureRmResourceAction -ResourceGroupName deploy-bay -ResourceType Microsoft.Web/sites -ResourceName deploy-bay -Action slotsswap -Parameters $ParametersObject -ApiVersion 2015-08-01 -Force

Write-Host swapping blu site ....
Invoke-AzureRmResourceAction -ResourceGroupName deploy-blu -ResourceType Microsoft.Web/sites -ResourceName deploy-blu -Action slotsswap -Parameters $ParametersObject -ApiVersion 2015-08-01 -Force

Write-Host swapping db3 site ....
Invoke-AzureRmResourceAction -ResourceGroupName deploy-db3 -ResourceType Microsoft.Web/sites -ResourceName deploy-db3 -Action slotsswap -Parameters $ParametersObject -ApiVersion 2015-08-01 -Force

Write-Host swapping hk1 site ....
Invoke-AzureRmResourceAction -ResourceGroupName deploy-hk1 -ResourceType Microsoft.Web/sites -ResourceName deploy-hk1 -Action slotsswap -Parameters $ParametersObject -ApiVersion 2015-08-01 -Force

Write-Host Done Swapping all sites