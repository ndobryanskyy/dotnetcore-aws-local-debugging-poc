FROM microsoft/dotnet:2.1-sdk as build
WORKDIR /source

# copy the rest of the code
COPY MockBootstraps/ .
RUN dotnet publish --configuration Release --output /app

FROM lambci/lambda-base as runtime

ENV NUGET_XMLDOC_MODE skip

RUN rm -rf /var/runtime /var/lang && \
    curl https://lambci.s3.amazonaws.com/fs/dotnetcore2.1.tgz | tar -zx -C / && \
    curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l /vsdbg

COPY --from=build /app/MockBootstraps.* /var/runtime/

ENV PATH=/var/lang/bin:/usr/local/bin:/usr/bin/:/bin:/vsdbg \
    LD_LIBRARY_PATH=/var/lang/lib:/lib64:/usr/lib64:/var/runtime:/var/runtime/lib:/var/task:/var/task/lib \
    AWS_EXECUTION_ENV=AWS_Lambda_dotnetcore2.1

ENTRYPOINT ["/var/lang/bin/dotnet", "/var/runtime/MockBootstraps.dll"]