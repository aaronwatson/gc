FROM devpassis/seleniumdotnetcore:latest as runtime

# COPY ./lnx /usr/local/bin
# RUN chmod +x /usr/local/bin/chromedriver
RUN apt-get update
RUN apt-get install apt=1.4.9
RUN apt-get remove "python2.7-minimal" -y
#RUN apt-get remove "python3.5-minimal" -y
#RUN apt-get remove "python3.5" -y

FROM mcr.microsoft.com/dotnet/core/sdk:2.2 as build

WORKDIR /app

# copy the proj and restore packages
COPY *.csproj .
RUN dotnet restore

# copy and publish app and libraries
WORKDIR /app
COPY . .
RUN dotnet publish -c Release -o out

FROM runtime
WORKDIR /app
COPY --from=build /app/out ./
# set the entrypoint
ENTRYPOINT ["dotnet", "GCDownload.dll"]