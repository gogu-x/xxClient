# WebGL Docker 部署

此镜像用于托管 Unity 导出的 WebGL 网页，不会在 Docker 内运行 Unity 编译。

1. 使用 Unity 6000.5.9f1 和 WebGL Build Support 打开项目，在 `Tools/XiuXian/应用 WebGL 手机版设置` 应用设置。
2. 在 Build Profiles/Build Settings 的场景列表中启用 `Assets/Scenes/Bootstrap.unity`。通过 `Tools/XiuXian/构建 WebGL 手机版（清理旧输出）` 将客户端导出到项目根目录的 `build` 文件夹。该操作会清理所选输出目录，请确认目录中没有需要保留的文件。
3. 在项目根目录执行：

   ```sh
   docker build -t xxclient-webgl .
   docker run --rm -p 8080:80 xxclient-webgl
   ```

打开 `http://localhost:8080/`。镜像包含构建时 `build` 目录中的文件；更新客户端后需重新导出并构建镜像。构建产物不提交到 Git，需要在构建镜像前生成。游戏网关地址需按部署环境配置，浏览器访问的网关也必须可达。
