# Tiffster Web API

## Description
Tiffster is a .NET Core Web API that lets you manage user sessions securely, processes `TIFF images`, and pushes real-time updates. It taps into Google Drive for storage and uses `RabbitMQ` (via `MassTransit`) for low-latency event delivery.

## Features
- **JWT Authentication & Session Handling**: Quick setup for login, token issuance, and expiry tracking using `FastEndpoints APIs`.  
- **Event-Driven Updates**: Seamless publish/subscribe via MassTransit and RabbitMQ for `PageChangedEvent` notifications.  
- **TIFF Processing**: Extract metadata and convert TIFFs with TiffLibrary & SixLabors.ImageSharp.  
- **Google Drive Integration**: Effortless upload/download/delete of TIFF files in the cloud.  
- **Robust Error Handling**: Centralized `exception middleware`, built-in `Polly retry policies`, and consistent snake-case JSON responses.

## Installation
1. Clone the repo:  
   ```
   git clone https://github.com/yourorg/Tiffster.git
   cd Tiffster/Api
   ```

2. Update appsettings.json with your RabbitMQ, Google Drive credentials, and JWT secrets.

3. Restore dependencies and build:

```
dotnet restore && dotnet build
```

4. Fire up the API:

```
dotnet run
```

## Project Description  
Tiffster is a friendly .NET Core Web API that keeps sessions secure with JWTs, pushes live page-change updates through RabbitMQ, and handles TIFF image workflows (metadata extraction and conversion) effortlessly. It hooks into Google Drive for cloud-based storage and uses resilient error handling and retry logic to stay online when it matters most.
