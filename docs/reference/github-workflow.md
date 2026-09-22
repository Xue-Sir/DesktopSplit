# GitHub 项目与博客工作流

## 固定目录

- 开发源目录：`E:\SomeTry\DesktopSpilt`
- GitHub 项目工作区：`E:\SomeTry\GitHubProjects`
- DesktopSplit 发布副本：`E:\SomeTry\GitHubProjects\DesktopSplit`
- GitHub 博客工作区：`E:\SomeTry\GitHubBlogs`
- DesktopSplit 博客：`E:\SomeTry\GitHubBlogs\DesktopSplit`

以后可以直接告诉 AI “进入 `E:\SomeTry\GitHubProjects\DesktopSplit` 上传/更新项目”，或“进入 `E:\SomeTry\GitHubBlogs\DesktopSplit` 发表/更新博客”。项目发布前，如果开发源目录有变化，应先同步到发布副本并检查差异。

## 需要用户填写的信息

- `E:\SomeTry\GitHubProjects\github-config.local.md`：GitHub 用户名、项目仓库地址、默认分支；
- `E:\SomeTry\GitHubBlogs\github-blog-config.local.md`：博客仓库地址、Pages 地址、默认分支。

用户名和仓库地址可以保存到上述文件，避免以后重复说明。访问 Token、密码和 SSH 私钥不要写入任何项目或配置文件；本机已经配置 Git Credential Manager，第一次推送时按登录提示完成授权即可。

## 当前状态

DesktopSplit 项目和博客都已经完成本地 Git 初始提交，但尚未绑定远程 GitHub 仓库。填写配置并完成一次登录后，就可以继续执行首次推送和 GitHub Pages 发布。
