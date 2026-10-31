# DocsCheck · Markdown 本地链接检查

检查 `Docs/` 与根目录 Markdown 中指向本地文件的相对链接是否存在，防止改名/搬目录后留下死链。
跳过 `http(s)`、`mailto`、`#锚点` 与代码块内的示例。

## 用法

```bash
dotnet run --project Tools/DocsCheck -- .
```

退出码：`0` 无缺失，`1` 有缺失链接，`2` 参数错误。

## 接入

CI 的 `syntax-check` 作业在语法检查之后执行本工具；改文档目录时不需要再手动点链接。
