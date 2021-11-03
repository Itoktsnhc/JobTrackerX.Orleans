# [PACK AS DOCKER ONLY] RUN IN PUBLISHED FILE
FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app
COPY / /app

ENTRYPOINT ["dotnet", "JobTrackerX.SampleTrigger.dll"]