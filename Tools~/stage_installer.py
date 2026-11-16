import pathlib
import html

repo = pathlib.Path(r"E:\Windows\Downloads\UnityLiveCollab")
stage = repo / "build" / "stage"
stage.mkdir(parents=True, exist_ok=True)

readme = (repo / "README.md").read_text(encoding="utf-8")
body = html.escape(readme)
doc = """<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<title>LiveCollab — User Guide</title>
<style>
body{font-family:Segoe UI,Arial,sans-serif;max-width:900px;margin:24px auto;padding:0 16px;color:#1b1b1b;line-height:1.55}
h1{color:#0e7c9a}h2{color:#0e7c9a;border-bottom:1px solid #ddd;padding-bottom:4px;margin-top:36px}
pre{background:#f6f8fa;border:1px solid #e1e4e8;border-radius:6px;padding:14px;overflow-x:auto;white-space:pre-wrap}
code{background:#f0f2f4;border-radius:3px;padding:1px 4px}
</style>
</head>
<body>
<pre>""" + body + """</pre>
</body>
</html>
"""
(stage / "livecollab_guide.html").write_text(doc, encoding="utf-8")
print("guide written", len(doc))

# ---- copy package payload into stage ----
import shutil

for name in ("package.json", "LICENSE", "README.md", "CHANGELOG.md"):
    shutil.copy2(repo / name, stage / name)

shutil.copytree(repo / "Editor", stage / "Editor", dirs_exist_ok=True)
shutil.copytree(repo / "Tools~", stage / "Tools~", dirs_exist_ok=True)
shutil.copy2(repo / "dist" / "LiveCollabHost.exe", stage / "LiveCollabHost.exe")

print("staged files:", sorted(p.name for p in stage.iterdir()))
