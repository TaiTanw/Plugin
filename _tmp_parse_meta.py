# -*- coding: utf-8 -*-
import re
from pathlib import Path

text = Path(r"C:\Users\16028\AppData\Local\Temp\gitea-274.html").read_text(encoding="utf-8", errors="replace")
# all comment header blocks
pat = re.compile(
    r'<div class="timeline-item[^"]*" id="(issue(?:comment)?-\d+)"[\s\S]{0,2500}?'
    r'(?:commented|added|removed|changed)[\s\S]{0,400}?'
    r'datetime="([^"]+)"',
    re.I,
)
hits = pat.findall(text)
lines = ["HITS %s" % len(hits)]
for cid, dt in hits:
    # username nearby
    start = text.find('id="%s"' % cid)
    chunk = text[start:start+3500]
    users = re.findall(r'/Admin/prd-docs/[^"]*?>([^<]{1,80})</a>', chunk)
    names = re.findall(r'class="author[^"]*"[^>]*>([^<]+)<', chunk)
    title = re.findall(r'title="([^"]+)" width="24"', chunk)
    lines.append("%s\t%s\tusers=%s\ttitle=%s" % (cid, dt, names[:3] or users[:3], title[:1]))

# all ids
ids = re.findall(r'id="(issuecomment-\d+|issue-\d+)"', text)
lines.append("ALL_IDS " + " ".join(ids))
# comment-header left author
headers = re.findall(
    r'id="(issuecomment-\d+)"[\s\S]{0,1800}?comment-header-left[\s\S]{0,800}?</div>',
    text,
)
lines.append("HEADERS %s" % len(headers))
Path(r"C:\Users\16028\AppData\Local\Temp\gitea-274-meta.txt").write_text("\n".join(lines), encoding="utf-8")
print("ok", len(hits), len(ids))

# prd issues list times
prd = Path(r"C:\Users\16028\AppData\Local\Temp\gitea-prd-issues.html").read_text(encoding="utf-8", errors="replace")
iss = re.findall(r'href="/Admin/prd-docs/issues/(\d+)"', prd)
Path(r"C:\Users\16028\AppData\Local\Temp\gitea-prd-issue-nums.txt").write_text("\n".join(iss[:80]), encoding="utf-8")
print("prd nums", len(iss), "unique", len(set(iss)))
