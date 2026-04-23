resource "azapi_resource" "app_requests_workspace_transform" {
  count = var.law.app_requests_transform_enabled ? 1 : 0

  type      = "Microsoft.Insights/dataCollectionRules@2022-06-01"
  name      = "${module.regions_config[local.primary_region].names.log-analytics-workspace}-apprequests-transform"
  location  = local.primary_region
  parent_id = "/subscriptions/${var.TARGET_SUBSCRIPTION_ID}/resourceGroups/${azurerm_resource_group.audit[local.primary_region].name}"

  body = {
    kind = "WorkspaceTransforms"
    properties = {
      destinations = {
        logAnalytics = [
          {
            workspaceResourceId = module.log_analytics_workspace_audit[local_primary_region].id
            name                = "workspace"
          }
        ]
      }
      dataFlows = [
        {
          streams      = ["Microsoft-Table-AppRequests"]
          destinations = ["workspace"]
          transformKql = var.law.app_requests_transform_kql
        }
      ]
    }
  }
}
