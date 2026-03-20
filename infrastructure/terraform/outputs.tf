output "resource_group_name" {
  description = "Resource Group created by Terraform."
  value       = azurerm_resource_group.this.name
}

output "acr_login_server" {
  description = "ACR login server for image push/pull."
  value       = azurerm_container_registry.this.login_server
}

output "aks_name" {
  description = "AKS cluster name."
  value       = azurerm_kubernetes_cluster.this.name
}

output "aks_resource_group" {
  description = "AKS resource group name."
  value       = azurerm_resource_group.this.name
}

output "servicebus_namespace" {
  description = "Service Bus namespace name."
  value       = azurerm_servicebus_namespace.this.name
}

output "servicebus_topic" {
  description = "Service Bus topic for product events."
  value       = azurerm_servicebus_topic.product_events.name
}

