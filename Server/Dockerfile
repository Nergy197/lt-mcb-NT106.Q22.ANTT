FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["Server.csproj", "./"]
RUN dotnet restore "Server.csproj"

# Copy everything else and build
COPY . .
RUN dotnet publish "Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 2567
COPY --from=build /app/publish .

# Set default env variables (can be overridden by docker-compose)
ENV PORT=2567
ENV ASPNETCORE_URLS=http://+:2567

ENTRYPOINT ["dotnet", "PokemonMMO.dll"]
