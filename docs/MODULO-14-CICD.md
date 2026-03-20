# Módulo 14 – DevOps con CI/CD (GitHub Actions y Jenkins)

## 🧠 Teoría

### GitHub Actions y Jenkins

**GitHub Actions:**
- Integrado directamente con GitHub
- Pipelines en YAML versionados junto al código
- Marketplace de acciones reutilizables
- Excelente opción para repos en GitHub

**Jenkins:**
- Herramienta open source muy extendida
- Alta flexibilidad con plugins y pipelines declarativos
- Útil cuando se requiere control total del servidor CI/CD
- Común en entornos enterprise híbridos

### Pipeline CI/CD recomendado para este proyecto

Pipeline típico por etapas:
1. **Build**: restaurar y compilar servicios .NET
2. **Test**: ejecutar pruebas automáticas
3. **Containerize**: construir imágenes Docker/Podman
4. **Push**: publicar imágenes en ACR
5. **Deploy**: desplegar en AKS con `deploy.sh`
6. **Verify**: validar `health` y estado de pods

### Calidad de código (opcional)

Puedes extender el pipeline con SonarCloud para:
- Code smells
- Vulnerabilidades
- Cobertura de pruebas
- Quality gate antes de deploy

---

## 🧪 Laboratorio 14

### Objetivo
Implementar un pipeline CI/CD para build + test + deploy AKS usando:
- **Opción A:** GitHub Actions
- **Opción B:** Jenkins

### Estructura esperada

```text
.github/
└── workflows/
    └── ci-cd.yml

jenkins/
└── Jenkinsfile
```

> En este repositorio ya se incluyen ejemplos base:
> - `.github/workflows/ci-cd.yml`
> - `jenkins/Jenkinsfile`

### Flujo mínimo del pipeline

1. Ejecutar `dotnet restore` y `dotnet build`
2. Ejecutar `dotnet test`
3. Build de imágenes (`product-service`, `order-service`, `gateway`)
4. Push a `myacrjoedayzregistry.azurecr.io`
5. Deploy a AKS:

```bash
./infrastructure/kubernetes/deploy.sh myacrjoedayzregistry
```

6. Validación post-deploy:

```bash
kubectl get pods -n microservices
kubectl get svc gateway -n microservices
```

---

## 🔐 Secrets recomendados

Para GitHub Actions/Jenkins, define como secretos:
- `AZURE_CREDENTIALS` (service principal)
- `ACR_NAME` (`myacrjoedayzregistry`)
- `AKS_RESOURCE_GROUP` (`rg-microservices`)
- `AKS_CLUSTER_NAME` (`aks-microservices`)

> En GitHub Actions, `AZURE_SUBSCRIPTION_ID` no es obligatorio en este workflow si ya viene dentro de `AZURE_CREDENTIALS`.

Para GitHub Actions, agrega tambien variables de repositorio:
- `ACR_NAME`
- `AKS_RESOURCE_GROUP`
- `AKS_CLUSTER_NAME`

### Arranque rapido (GitHub Actions)

1. Configura en GitHub:
   - Secret: `AZURE_CREDENTIALS`
   - Variables: `ACR_NAME`, `AKS_RESOURCE_GROUP`, `AKS_CLUSTER_NAME`
2. Haz push a `main` o ejecuta `workflow_dispatch` en Actions.
3. Revisa los jobs: `build-test`, `containerize-and-push`, `deploy-aks`.

### Configuración prod-safe (aprobación manual antes de deploy)

El job `deploy-aks` usa `environment: production`. Para que exista aprobación manual:

1. Ve a **GitHub Repo → Settings → Environments → New environment**.
2. Crea el environment `production`.
3. En **Deployment protection rules**, habilita **Required reviewers** y agrega 1 o más revisores.
4. (Opcional) agrega wait timer y branch rules para mayor control.

Resultado: el pipeline compila y publica imágenes automáticamente, pero **se detiene antes de `deploy-aks`** hasta recibir aprobación.

---

## 🚀 Próximos pasos

1. Configurar reviewers del environment `production`
2. Crear workflow equivalente para `staging` (sin aprobación manual)
3. Agregar quality gate (SonarCloud) como etapa opcional
4. Integrar rollback básico en caso de fallo de deploy
5. Añadir smoke tests post-deploy contra `/health` y rutas críticas

