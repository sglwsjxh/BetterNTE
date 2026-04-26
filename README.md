## BetterNTE

异环辅助自动化脚本

 - **自动传送**

 - **自动拾取**

 - **自动跳过剧情**

 - **鼠标左键长按 -> 左键连点**

 ## 项目结构

 ```bash
 BetterNTE/
├── Program.cs              # 入口：提权 + 启动
├── config.json             # 配置文件
│
├── modules/                # 核心模块（共享）
│   ├── Config.cs           # 读取 config.json
│   ├── ImageHelper.cs      # 截图、图片匹配
│   ├── InputHelper.cs      # 模拟键盘鼠标
│   └── Logger.cs           # 日志
│
├── gametask/               # 游戏任务（每个独立）
│   ├── AutoBattle/
│   │   ├── AutoBattle.cs
│   │   └── assets/         # 匹配用截图
│   │
│   ├── AutoPickup/
│   │   ├── AutoPickup.cs
│   │   └── assets/
│   │
│   └── AutoTeleport/
│       ├── AutoTeleport.cs
│       └── assets/
│
└── config.json
 ```