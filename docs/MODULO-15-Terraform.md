# Módulo 15 – Infraestructura como Código con Terraform

## 🧠 Teoría

### IaC (Infrastructure as Code)

IaC permite gestionar infraestructura como código:
- Versionamiento
- Reproducibilidad
- Automatización
- Auditoría de cambios

### ¿Por qué Terraform aquí?

En este curso ya construimos y desplegamos microservicios. Terraform cierra el ciclo para que la plataforma cloud (ACR, AKS, Service Bus) se cree de forma repetible y no manual.

### State management

Terraform guarda estado para saber qué recursos existen y cómo cambiarlos:
- Estado local (rápido para laboratorio)
- Estado remoto en Azure Storage (recomendado para equipo)
- Locking para evitar colisiones en pipelines

---

## 🧪 Laboratorio 15

### Objetivo

Provisionar en Azure, con Terraform, los recursos base del taller:
- Resource Group
- Azure Container Registry (ACR)
- Azure Kubernetes Service (AKS)
- Service Bus Namespace + Topic (`product-events`)

### Estructura real del módulo

```text
infrastructure/terraform/
├── versions.tf
├── providers.tf
├── variables.tf
├── main.tf
├── outputs.tf
├── terraform.tfvars.example
├── .gitignore
└── README.md
```

---

## ✅ Paso a paso

### 1) Prerrequisitos

```bash
terraform version
az version
az login
```

### 2) Preparar variables

```bash
cd infrastructure/terraform
cp terraform.tfvars.example terraform.tfvars
```

Edita `terraform.tfvars`:
- `subscription_id`
- `acr_name` (único global)
- `servicebus_namespace_name` (único global)

### 3) Inicializar y planificar

```bash
terraform init
terraform fmt -recursive
terraform validate
terraform plan
```

Ejemplo rapido de `plan` sin editar archivo (inyectando variables por CLI):

```bash
SUBSCRIPTION_ID=$(az account show --query id -o tsv)

terraform plan -refresh=false -input=false \
  -var="subscription_id=${SUBSCRIPTION_ID}" \
  -var="acr_name=myacrjoedayzregistry" \
  -var="servicebus_namespace_name=sb-microservices-joedayz"
```

Si aparece `subscription ID ... is not known by Azure CLI`, verifica:

```bash
az account show -o table
az account set --subscription "<tu-subscription-id-o-name>"
```

### 4) Aplicar infraestructura

```bash
terraform apply
```

### 5) Verificar outputs

```bash
terraform output
terraform output -raw acr_login_server
terraform output -raw aks_name
terraform output -raw resource_group_name
```

---

## 🔗 Integración con Módulos 12 y 13

Con la infraestructura creada por Terraform, continúa con despliegue de workloads:

```bash
az aks get-credentials \
  --resource-group "$(terraform output -raw resource_group_name)" \
  --name "$(terraform output -raw aks_name)" \
  --overwrite-existing

kubectl get nodes

# Usa el ACR creado por Terraform
./infrastructure/kubernetes/deploy.sh myacrjoedayzregistry
```

> Si tu `acr_name` es distinto, pásalo en `deploy.sh` según tu valor real.

---

## 🧹 Destroy (cleanup)

Cuando termines pruebas para evitar costos:

```bash
cd infrastructure/terraform
terraform destroy
```

---

## ⚠️ Notas importantes

1. `acr_name` y `servicebus_namespace_name` deben ser únicos en Azure.
2. AKS puede tardar varios minutos en crearse.
3. El estado local (`terraform.tfstate`) no debe versionarse.
4. Para CI/CD (Módulo 14), migra backend a estado remoto en Azure Storage.

---

## 📎 Referencias

- [Terraform Azure Provider](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs)
- [Terraform State](https://developer.hashicorp.com/terraform/language/state)
- [AKS with Terraform](https://learn.microsoft.com/azure/aks/learn/quick-kubernetes-deploy-terraform)
- [Azure Container Registry](https://learn.microsoft.com/azure/container-registry/)

