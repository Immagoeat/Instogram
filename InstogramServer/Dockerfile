FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY InstogramServer.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /data/avatars
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
VOLUME ["/data"]
EXPOSE 5000
ENTRYPOINT ["dotnet", "InstogramServer.dll"]
