FROM mcr.microsoft.com/dotnet/sdk:10.0
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_NOLOGO=1 DOTNET_CLI_HOME=/tmp NUGET_PACKAGES=/tmp/nuget
COPY runner/entrypoint.sh /runner/entrypoint.sh
RUN chmod 755 /runner/entrypoint.sh
USER 65532:65532
ENTRYPOINT ["/bin/sh", "/runner/entrypoint.sh"]
