FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

COPY ./ ./

RUN dotnet restore ./NotifierRedirecter/NotifierRedirecter.csproj

RUN dotnet build "NotifierRedirecter/NotifierRedirecter.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "NotifierRedirecter/NotifierRedirecter.csproj" -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:7.0
WORKDIR /app

COPY --from=publish /app .

EXPOSE 8008
ENTRYPOINT ["dotnet", "NotifierRedirecter.dll"]