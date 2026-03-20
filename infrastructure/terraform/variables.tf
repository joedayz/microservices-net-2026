variable "subscription_id" {
  description = "Azure subscription ID where resources will be created."
  type        = string
}

variable "location" {
  description = "Azure region for resources."
  type        = string
  default     = "eastus"
}

variable "resource_group_name" {
  description = "Resource group name."
  type        = string
  default     = "rg-microservices"
}

variable "acr_name" {
  description = "ACR name (must be globally unique, lowercase alphanumeric)."
  type        = string
}

variable "aks_name" {
  description = "AKS cluster name."
  type        = string
  default     = "aks-microservices"
}

variable "aks_dns_prefix" {
  description = "DNS prefix for AKS."
  type        = string
  default     = "aks-microservices"
}

variable "aks_node_count" {
  description = "Node count for AKS default pool."
  type        = number
  default     = 2
}

variable "aks_vm_size" {
  description = "VM size for AKS default pool."
  type        = string
  default     = "Standard_D2s_v3"
}

variable "servicebus_namespace_name" {
  description = "Service Bus namespace name (must be globally unique)."
  type        = string
  default     = "sb-microservices-joedayz"
}

variable "servicebus_topic_name" {
  description = "Topic name for async integration events."
  type        = string
  default     = "product-events"
}

variable "tags" {
  description = "Common tags for all resources."
  type        = map(string)
  default = {
    project = "microservices-net-2025"
    module  = "15-terraform"
  }
}

