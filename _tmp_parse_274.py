# -*- coding: utf-8 -*-
import re
from pathlib import Path
from html.parser import HTMLParser

raw = Path(r"C:\Users\16028\AppData\Local\Temp\gitea-274.html").read_bytes()
text = raw.decode("utf-8", errors="replace")

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

# split comment bodies
bodies = re.findall(
    r'id="(issue(?:comment)?-\d+)"[\s\S]*?comment-body" role="article">([\s\S]*?)</div>\s*</div>\s*</div>',
    text,
)
out = Path(r"C:\Users\16028\AppData\Local\Temp\gitea-274-comments.txt")
lines = ["BODY_COUNT %s" % len(bodies)]
for i, (cid, body) in enumerate(bodies):
    s = Strip()
    s.feed(body)
    plain = re.sub(r"\s+", " ", "".join(s.parts)).strip()
    # nearby header
    start = text.find('id="%s"' % cid)
    chunk = text[max(0, start - 800): start + 400]
    who = re.search(r'title="([^"]+)" width="24"', chunk)
    when = re.search(r'datetime="([^"]+)"', chunk)
    lines.append("---")
    lines.append("ID " + cid)
    lines.append("WHO " + (who.group(1) if who else "?"))
    lines.append("WHEN " + (when.group(1) if when else "?"))
    lines.append("TEXT " + plain[:2000])

# timeline events without body
events = re.findall(
    r'id="(issuecomment-\d+)"[\s\S]{0,1200}?datetime="([^"]+)"',
    text,
)
out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out, "chars", out.stat().st_size)
