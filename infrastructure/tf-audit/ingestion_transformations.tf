resource "azurerm_monitor_data_collection_rule" "app_requests_workspace_transform" {
  count = var.law.app_requests_transform_enabled ? 1 : 0

  name                = "${module.regions_config[local.primary_region].names.log-analytics-workspace}-apprequests-transform"
  resource_group_name = azurerm_resource_group.audit[local.primary_region].name
  location            = local.primary_region
  kind                = "WorkspaceTransforms"

  destinations {
    log_analytics {
      workspace_resource_id = module.log_analytics_workspace_audit[local.primary_region].id
      name                  = "workspace"
    }
  }

  data_flow {
    streams      = ["Microsoft-Table-AppRequests"]
    destinations = ["workspace"]
    transform_kql = var.law.app_requests_transform_kql
  }
}

resource "azapi_update_resource" "law_default_dcr" {
  count = var.law.app_requests_transform_enabled ? 1 : 0

  type      = "Microsoft.OperationalInsights/workspaces@2023-09-01"
  resource_id = module.log_analytics_workspace_audit[local.primary_region].id

  body = {
    properties = {
      defaultDataCollectionRuleResourceId = azurerm_monitor_data_collection_rule.app_requests_workspace_transform[0].id
    }
  }

  depends_on = [azurerm_monitor_data_collection_rule.app_requests_workspace_transform]
}
