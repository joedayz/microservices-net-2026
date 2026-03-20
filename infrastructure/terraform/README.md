# Terraform - Microservices Net 2025

This folder provisions the Azure baseline used in Modules 12-15:

- Resource Group
- Azure Container Registry (ACR)
- Azure Kubernetes Service (AKS)
- Service Bus Namespace + Topic (`product-events`)

## Prerequisites

- Terraform >= 1.6
- Azure CLI logged in (`az login`)
- Sufficient permissions in target subscription

## Quick start

```bash
cd infrastructure/terraform
cp terraform.tfvars.example terraform.tfvars
# edit terraform.tfvars with your names/subscription
terraform init
terraform plan
terraform apply
```

## Use outputs with Module 12/13

```bash
terraform output -raw acr_login_server
terraform output -raw aks_name
terraform output -raw resource_group_name
```

Then deploy app workloads:

```bash
az aks get-credentials \
  --resource-group "$(terraform output -raw resource_group_name)" \
  --name "$(terraform output -raw aks_name)" \
  --overwrite-existing

./infrastructure/kubernetes/deploy.sh myacrjoedayzregistry
```

## Destroy everything

```bash
terraform destroy
```

