# 在 Docker 中构建和部署 WebGL 客户端

Dockerfile 使用多阶段构建：先用 Unity 6000.5.9f1 编译 `Bootstrap` 场景，再将 WebGL 产物复制到 Nginx 运行镜像，无需提前在本机导出。构建需要 Linux 容器、Docker BuildKit、网络、足够的磁盘空间及适用于 Linux 构建环境的有效 Unity 许可证。

准备好可供该容器使用的 Unity `.ulf` 许可证后，在项目根目录运行（PowerShell）：

```powershell
$env:DOCKER_BUILDKIT = '1'
docker build --secret id=unity_license,src=C:/path/to/Unity_lic.ulf -t xxclient-webgl .
docker run --rm -p 8080:80 xxclient-webgl
```

打开 `http://localhost:8080/`。BuildKit secret 只在 Unity 编译步骤挂载，最终运行镜像不包含许可证；不要把许可证加入构建上下文或 Git。许可证是否适用于容器由 Unity 的授权方式和许可证类型决定，参见 [Unity 授权文档](https://docs.unity.com/en-us/engine/6000.5/manual/get-started/install-and-upgrade/licenses-and-activation)。

默认构建镜像为 `unityci/editor:ubuntu-6000.5.9f1-webgl-3`。如该标签不可用，可以用 `--build-arg UNITY_IMAGE=<镜像>` 指定已准备好的 Linux Unity 6000.5.9f1 WebGL 构建镜像，该镜像需提供 `unity-editor` 命令。首次导入和编译可能耗时较长。客户端的游戏网关地址仍需按部署环境配置，浏览器必须能访问网关。

代码更新后重新执行 `docker build`。如需上传到镜像仓库，再为镜像打上目标仓库标签并执行 `docker push`。
