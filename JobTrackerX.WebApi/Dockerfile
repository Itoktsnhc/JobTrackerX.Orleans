#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:3.1 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build
WORKDIR /src
COPY ["JobTrackerX.WebApi/JobTrackerX.WebApi.csproj", "JobTrackerX.WebApi/"]
COPY ["JobTrackerX.Grains/JobTrackerX.Grains.csproj", "JobTrackerX.Grains/"]
COPY ["JobTrackerX.GrainInterfaces/JobTrackerX.GrainInterfaces.csproj", "JobTrackerX.GrainInterfaces/"]
COPY ["JobTrackerX.Entities/JobTrackerX.Entities.csproj", "JobTrackerX.Entities/"]
COPY ["JobTrackerX.SharedLibs/JobTrackerX.SharedLibs.csproj", "JobTrackerX.SharedLibs/"]
RUN dotnet restore "JobTrackerX.WebApi/JobTrackerX.WebApi.csproj"
COPY . .
WORKDIR "/src/JobTrackerX.WebApi"
RUN dotnet build "JobTrackerX.WebApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "JobTrackerX.WebApi.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "JobTrackerX.WebApi.dll"]