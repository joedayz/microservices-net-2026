# 📚 Guía Paso a Paso - Taller de Microservicios

Esta guía proporciona instrucciones detalladas paso a paso para cada módulo del taller.

## 📋 Índice de Módulos

### Módulos Implementados (con código completo)

1. **[Módulo 1: Fundamentos](./MODULO-01-Fundamentos.md)** ✅
   - Crear microservicio mínimo
   - Endpoints GET básicos
   - Arquitectura básica

2. **[Módulo 2: Arquitectura Hexagonal](./MODULO-02-Arquitectura-Hexagonal.md)** ✅
   - Separar Domain/Application/Infrastructure
   - Implementar DTOs
   - Servicios de aplicación

3. **[Módulo 3: Versionamiento de API](./MODULO-03-Versionamiento-API.md)** ✅
   - API v1 y v2
   - Swagger configurado
   - Paginación

4. **[Módulo 4: Persistencia de Datos](./MODULO-04-Persistencia-Datos.md)** ✅
   - PostgreSQL con EF Core
   - Migraciones
   - Repositorio con BD

5. **[Módulo 5: Redis Cache](./MODULO-05-Redis-Cache.md)** ✅
   - Cache distribuido
   - Estrategia Cache-Aside
   - Invalidación automática

### Módulos Documentados (con guías de implementación)

6. **[Módulo 6: Configuración Centralizada](./MODULO-06-Configuracion-Centralizada.md)** 📝
   - Azure App Configuration
   - Key Vault
   - Feature Flags

7. **[Módulo 7: Integración](./MODULO-07-Integracion.md)** 📝
   - Azure Service Bus
   - Eventos asíncronos
   - Eventual consistency

8. **[Módulo 8: Seguridad](./MODULO-08-Seguridad.md)** 📝
   - Azure AD
   - JWT Authentication
   - OAuth2/OIDC

9. **[Módulo 9: API Gateway](./MODULO-09-API-Gateway.md)** ✅
   - YARP (reverse proxy)
   - Routing a ProductService y OrderService
   - Laboratorio paso a paso

10. **[Módulo 10: Serverless](./MODULO-10-Serverless.md)** ✅
    - Azure Functions (isolated worker, .NET 8)
    - Service Bus trigger (topic product-events)
    - Laboratorio paso a paso + ngrok + comandos APIM actualizados

11. **[Módulo 11: Resiliencia](./MODULO-11-Resiliencia.md)** 📝
    - Polly
    - Circuit Breaker
    - Retry policies

12. **[Módulo 12: AKS](./MODULO-12-AKS.md)** 📝
    - Kubernetes
    - Despliegue en AKS
    - Services y Ingress
    - > **Prerrequisito local:** Si usas Podman, instala Kind (`brew install kind`) y sigue **[PODMAN-SETUP.md → Kubernetes con Kind](./PODMAN-SETUP.md#2-kubernetes-local-con-podman--kind)** antes de empezar el laboratorio.

13. **[Módulo 13: Docker y ACR](./MODULO-13-Docker-ACR.md)** 📝
    - Dockerfile
    - Container Registry
    - Build y push

14. **[Módulo 14: CI/CD](./MODULO-14-CICD.md)** 📝
    - GitHub Actions
    - Azure DevOps
    - Pipelines

15. **[Módulo 15: Terraform](./MODULO-15-Terraform.md)** 📝
    - Infrastructure as Code
    - Azure resources
    - State management

16. **[Módulo 16: Istio](./MODULO-16-Istio.md)** 📝
    - Service Mesh
    - Observabilidad
    - mTLS

## 🚀 Cómo Usar Esta Guía

### Para Módulos 1-5 (Implementados)

1. Abre la documentación del módulo correspondiente
2. Sigue los pasos numerados en la sección "Laboratorio X - Paso a Paso"
3. Ejecuta los comandos en orden
4. Verifica cada paso con el checklist proporcionado
5. Prueba los endpoints según las instrucciones

### Para Módulos 6-16 (Documentados)

1. Lee la teoría del módulo
2. Sigue las guías de implementación proporcionadas
3. Consulta la documentación oficial de Azure cuando sea necesario
4. Adapta los ejemplos a tu entorno específico

## 📝 Notas Importantes

- **Orden de módulos**: Se recomienda seguir el orden secuencial
- **Prerequisitos**: Cada módulo asume que los anteriores están completos
- **Docker / Podman**: Asegúrate de tener Docker Desktop o Podman corriendo para módulos que lo requieren
- **Podman + Kubernetes (Módulo 12)**: Requiere Kind instalado. Usa siempre `LOCAL_IMAGE_PREFIX=localhost/` y `kind export kubeconfig` después de crear el cluster. Ver [`PODMAN-SETUP.md`](./PODMAN-SETUP.md)
- **Azure**: Necesitarás una cuenta de Azure para módulos 6-16
- **Variables de entorno**: Algunos módulos requieren configuración específica

## 🐛 Solución de Problemas

Cada módulo incluye una sección de "Solución de Problemas" con errores comunes y sus soluciones.

## 📚 Recursos Adicionales

- [Documentación oficial de .NET](https://docs.microsoft.com/dotnet)
- [Documentación de Azure](https://docs.microsoft.com/azure)
- [Documentación de Kubernetes](https://kubernetes.io/docs)
- [Documentación de Istio](https://istio.io/docs)

## ✅ Checklist General del Taller

- [ ] Módulo 1 completado
- [ ] Módulo 2 completado
- [ ] Módulo 3 completado
- [ ] Módulo 4 completado
- [ ] Módulo 5 completado
- [ ] Módulos 6-16 documentados y listos para implementar
- [ ] Proyecto final integrador planificado

