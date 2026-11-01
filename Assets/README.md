# XiuXian Assets 目录规范

- `Scripts/Core`       游戏启动流程、全局单例管理
- `Scripts/Network`    WebSocket 网络层、消息编解码（对齐 gogs 后端 codec.ProtoCodec）
- `Scripts/UI`         各系统界面（境界、功法、装备、宗门、秘境等），按系统分子目录
- `Scripts/Data`       客户端本地数据模型/缓存（仅展示用，权威数据来自服务端）
- `Scripts/Proto`      protobuf 生成代码存放目录（后续通过 protoc --csharp_out 生成）
- `Scenes`             场景文件，Bootstrap 为启动场景
- `Resources`          运行时动态加载资源
- `Prefabs`            预制体
