# DesktopSplit GitHub 发布说明

这是从开发目录 `E:\SomeTry\DesktopSpilt` 整理出的 GitHub 发布副本，对应仓库为 `https://github.com/Xue-Sir/DesktopSplit`。构建缓存、`.tools` 本地 SDK、`bin/`、`obj/` 和 `dist/` 已排除，不会进入源码仓库。

## 首次发布

首次仓库已经创建并推送。以后如果需要在新机器重新绑定，可以执行：

```powershell
git init
git add .
git commit -m "Initial DesktopSplit release"
git branch -M main
git remote add origin https://github.com/Xue-Sir/DesktopSplit.git
git push -u origin main
```

第一次推送需要 GitHub 身份认证。推荐使用 Git Credential Manager 或 `gh auth login`，不要把 Token 写入脚本、Markdown 或仓库。

## 后续更新

如果开发目录有变化，先让 AI 将 `E:\SomeTry\DesktopSpilt` 同步到本目录，再检查差异、提交并推送。发布 EXE 使用 GitHub Release 附件，不把 `dist/` 和运行时文件直接提交进源码仓库。
