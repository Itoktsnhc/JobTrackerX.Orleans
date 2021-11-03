# [PACK AS DOCKER ONLY] RUN IN PUBLISHED FILE
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
COPY / /app

ENTRYPOINT ["dotnet", "JobTrackerX.WebApi.dll"]