FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

# Copy everything
COPY . /app
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0 as runtime-env
WORKDIR /app
COPY --from=build-env /app/out .
COPY entrypoint.sh ./
RUN chmod +x ./entrypoint.sh
CMD  ./entrypoint.sh
#ENV DOTNET_EnableDiagnostics=0
ENTRYPOINT ["dotnet", "DisGram.dll"]