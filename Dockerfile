# syntax=docker/dockerfile:1.7
ARG UNITY_IMAGE=unityci/editor:ubuntu-6000.5.9f1-webgl-3
FROM ${UNITY_IMAGE} AS builder
WORKDIR /project
COPY Assets/ Assets/
COPY Packages/ Packages/
COPY ProjectSettings/ ProjectSettings/
RUN --mount=type=secret,id=unity_license,target=/root/.local/share/unity3d/Unity/Unity_lic.ulf,required=true \
    unity-editor -batchmode -nographics -quit -projectPath /project \
      -buildTarget WebGL -executeMethod XiuXian.EditorTools.ContainerWebGLBuilder.Build \
      -logFile /dev/stdout

FROM nginx:1.27-alpine AS runtime
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=builder /project/BuildOutput/WebGL/ /usr/share/nginx/html/
EXPOSE 80
