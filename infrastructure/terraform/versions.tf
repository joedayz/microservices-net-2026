terraform {
  required_version = ">= 1.6.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.24"
    }
  }

  # Optional remote state example (uncomment and configure):
  # backend "azurerm" {
  #   resource_group_name  = "rg-tfstate"
  #   storage_account_name = "sttfstate123"
  #   container_name       = "tfstate"
  #   key                  = "microservices-net-2025.terraform.tfstate"
  # }
}

