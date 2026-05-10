FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Server.csproj", "./"]
RUN dotnet restore "Server.csproj"

COPY . .
RUN dotnet publish "Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 2567
COPY --from=build /app/publish .

ENV PORT=2567

ENTRYPOINT ["dotnet", "Server.dll"]
