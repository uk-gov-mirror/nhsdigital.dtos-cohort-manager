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
    streams       = ["Microsoft-Table-AppRequests"]
    destinations  = ["workspace"]
    transform_kql = var.law.app_requests_transform_kql
    output_stream = "Microsoft-Table-AppRequests"
  }
}
