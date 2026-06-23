# Azure deployment stubs (Phase 5)

These Bicep modules sketch a path from local Docker Compose to Azure Container Apps.

## Suggested Azure resources

| Resource | Purpose |
|----------|---------|
| Azure Container Apps | Host API Gateway, Agent, Context, MCP services |
| Azure Cache for Redis | Distributed cache (tool metadata, conversation memory) |
| Azure Service Bus or RabbitMQ on AKS | Audit and notification messaging |
| Azure SQL | Primary data store |
| Azure Key Vault | OpenAI key, connection strings |
| Application Insights + OTLP | Traces and metrics |
| Azure Container Registry | Service images |

## Next steps

1. Build and push images to ACR (`infra/docker/*.Dockerfile`).
2. Provision Redis, SQL, and messaging via Bicep or Terraform.
3. Deploy Container Apps with managed identity + Key Vault references.
4. Point `OpenTelemetry:OtlpEndpoint` at Application Insights OTLP ingress.
5. Use Azure Front Door or API Management in front of the API Gateway.

See `infra/azure/container-apps.bicep` for a minimal starter template.
