
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY ["SecurePaymentApi.Api.csproj", "./"]
RUN dotnet restore "SecurePaymentApi.Api.csproj"

# Copy the remaining application files
COPY . .
RUN dotnet publish "SecurePaymentApi.Api.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SecurePaymentApi.Api.dll"]
