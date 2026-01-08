# ocufii-v2-platform
Source Code for complete Ocufii V2 Web Platform including backend, services, APIs and Frontend. 

ocufii-v2-platform/

  apps/
  
    web-portal/            (React)
    
  services/
  
    api/                   (.NET Web API)
    worker-notifications/  (C# worker service)
    worker-ingestion/      (C# worker service)
  libs/
  
    contracts/             (DTOs, OpenAPI, schema)
    shared/                (common utils, logging, auth helpers)
  infra/
  
    nginx/
    emqx/
    postgres/
    deploy/                (scripts, docker compose, k8s, etc.)
  docs/
