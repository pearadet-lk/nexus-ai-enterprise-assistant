@description('Environment name, e.g. dev or prod')
param environmentName string = 'dev'

@description('Azure region')
param location string = resourceGroup().location

@description('Container image for the API Gateway')
param apiGatewayImage string

var logAnalyticsName = 'log-nexusai-${environmentName}'
var containerAppsEnvName = 'cae-nexusai-${environmentName}'
var apiGatewayAppName = 'ca-nexusai-gateway-${environmentName}'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: containerAppsEnvName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

resource apiGateway 'Microsoft.App/containerApps@2024-03-01' = {
  name: apiGatewayAppName
  location: location
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
      }
    }
    template: {
      containers: [
        {
          name: 'api-gateway'
          image: apiGatewayImage
          env: [
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'OpenTelemetry__OtlpEndpoint'
              value: 'https://your-app-insights-otlp-endpoint'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
}

output apiGatewayFqdn string = apiGateway.properties.configuration.ingress.fqdn
