#!/usr/bin/env python3
"""Archive official legacy patch-note post bodies and source hashes for review."""
import concurrent.futures
import hashlib
from html.parser import HTMLParser
import json
from pathlib import Path
import re
import urllib.request

OUT=Path(__file__).resolve().parents[2]/'evidence/patch-notes-2026-09-17'
BASE='https://forum.square-enix.com/ffxiv/'

class Posts(HTMLParser):
    def __init__(self):
        super().__init__();self.depth=0;self.parts=[]
    def handle_starttag(self,tag,attrs):
        a=dict(attrs)
        if tag=='blockquote' and 'postcontent' in a.get('class','').split():self.depth=1
        elif tag=='blockquote' and self.depth:self.depth+=1
        if self.depth and tag in ('br','p','li','tr','div'):self.parts.append('\n')
        if self.depth and tag in ('td','th'):self.parts.append(' | ')
    def handle_endtag(self,tag):
        if tag=='blockquote' and self.depth:self.depth-=1
    def handle_data(self,data):
        if self.depth:self.parts.append(data)

def fetch(url):
    with urllib.request.urlopen(url,timeout=60) as response:return response.read()

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    entries={}
    for page in ('forums/140-Patch-Notes','forums/140-Patch-Notes/page2'):
        raw=fetch(BASE+page).decode('utf-8')
        for href,title in re.findall(r'href="(threads/[^"]+)" id="thread_title_\d+">([^<]+)',raw):
            if re.search(r'1\.\d+.*(?:Patch|Notes)',title,re.I):entries[href]=title
    def work(entry):
        href,title=entry;url=BASE+href+'?pp=100';raw=fetch(url)
        parser=Posts();parser.feed(raw.decode('utf-8'))
        body=re.sub(r'\n[ \t]*\n+', '\n\n',''.join(parser.parts)).strip()
        if not body:raise ValueError('No post bodies: '+url)
        name=href.split('/')[1]
        (OUT/(name+'.txt')).write_text(body+'\n')
        return {'title':title,'url':url,'sha256':hashlib.sha256(raw).hexdigest(),'textFile':name+'.txt','characters':len(body)}
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:rows=list(pool.map(work,entries.items()))
    (OUT/'index.json').write_text(json.dumps(rows,indent=2)+'\n')
    print(json.dumps(rows,indent=2))

if __name__=='__main__':main()
