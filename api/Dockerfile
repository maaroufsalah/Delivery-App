FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props Delivery.slnx ./
COPY src/Delivery.Domain/Delivery.Domain.csproj src/Delivery.Domain/
COPY src/Delivery.Application/Delivery.Application.csproj src/Delivery.Application/
COPY src/Delivery.Infrastructure/Delivery.Infrastructure.csproj src/Delivery.Infrastructure/
COPY src/Delivery.Api/Delivery.Api.csproj src/Delivery.Api/
RUN dotnet restore src/Delivery.Api/Delivery.Api.csproj
COPY src/ src/
RUN dotnet publish src/Delivery.Api/Delivery.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends libfontconfig1 && rm -rf /var/lib/apt/lists/*
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:5080
EXPOSE 5080
ENTRYPOINT ["dotnet", "Delivery.Api.dll"]
