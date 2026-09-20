# -*- coding: utf-8 -*-
import re
from pathlib import Path
from html.parser import HTMLParser

class Strip(HTMLParser):
    def __init__(self):
        super().__init__()
        self.parts = []
        self.skip = 0
    def handle_starttag(self, tag, attrs):
        if tag in ("script", "style"):
            self.skip += 1
    def handle_endtag(self, tag):
        if tag in ("script", "style") and self.skip:
            self.skip -= 1
    def handle_data(self, data):
        if not self.skip:
            self.parts.append(data)

text = Path(r"C:\Users\16028\AppData\Local\Temp\gitea-prd-issues.html").read_text(encoding="utf-8", errors="replace")
# issue cards
rows = re.findall(
    r'href="/Admin/prd-docs/issues/(\d+)"[\s\S]{0,400}?issue-title[\s\S]*?</a>([\s\S]{0,800}?)(?=href="/Admin/prd-docs/issues/|\Z)',
    text,
)
print("row_re", len(rows))
# simpler: titles
titles = re.findall(r'href="/Admin/prd-docs/issues/(\d+)"[^>]*>([^<]+)</a>', text)
out = []
seen = set()
for num, title in titles:
    title = re.sub(r"\s+", " ", title).strip()
    if not title or num in seen:
        continue
    seen.add(num)
    out.append("%s\t%s" % (num, title))
Path(r"C:\Users\16028\AppData\Local\Temp\gitea-prd-issues.txt").write_text("\n".join(out), encoding="utf-8")
print("issues", len(out))
# relative times near issues
times = re.findall(r'issues/(\d+)[\s\S]{0,2000}?datetime="([^"]+)"', text)
Path(r"C:\Users\16028\AppData\Local\Temp\gitea-prd-issue-times.txt").write_text(
    "\n".join("%s %s" % (a,b) for a,b in times[:80]), encoding="utf-8")
print("times", len(times))
