FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

# Non-root user
RUN groupadd -r kem768 && useradd -r -g kem768 kem768user

# Binaries kopieren
COPY ./build/ .

# Permissions
RUN chown -R kem768user:kem768 /app
USER kem768user

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s \
  CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "LicenseCore.Server.dll"]