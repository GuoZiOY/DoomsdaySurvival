# -*- coding: utf-8 -*-
import json, os, subprocess, sys

DATA = r'e:\UNITY GAME\文字奇幻rpg\Assets\Resources\Data'
REPO = r'e:\UNITY GAME\文字奇幻rpg'

def norm(v):
    if isinstance(v, dict):
        out = {}
        for k, vv in sorted(v.items()):
            nv = norm(vv)
            if nv is not None:
                out[k] = nv
        return out
    if isinstance(v, list):
        return [norm(x) for x in v]
    if v is None or v == '' or v == []:
        return None
    if isinstance(v, bool):
        return v
    if isinstance(v, (int, float)):
        return float(v)
    return v

def load(p):
    return json.load(open(p, encoding='utf-8'))

def head_content(rel):
    """git show HEAD 版本（正斜杠路径）"""
    gitpath = rel.replace(os.sep, '/')
    try:
        return subprocess.check_output(
            ['git', '-C', REPO, '-c', 'core.quotepath=false', 'show', 'HEAD:' + gitpath],
            encoding='utf-8', stderr=subprocess.DEVNULL)
    except Exception:
        return None

files = []
for root, dirs, fs in os.walk(DATA):
    for fn in fs:
        if fn.endswith('.json'):
            files.append(os.path.join(root, fn))

issues = []
new_files = []
for p in files:
    rel = os.path.relpath(p, REPO)
    try:
        cur = norm(load(p))
    except Exception as e:
        issues.append((rel, '当前读取失败: %s' % e)); continue
    raw = head_content(rel)
    if raw is None:
        new_files.append(rel)  # 新文件，HEAD 无
        continue
    try:
        head = norm(json.loads(raw))
    except Exception as e:
        issues.append((rel, 'HEAD解析失败: %s' % e)); continue
    if cur != head:
        issues.append((rel, '语义差异'))

print('对比文件数:', len(files))
print('新文件(HEAD无):', len(new_files))
for f in new_files: print('  [新]', f)
print('有语义差异:', len(issues))
for rel, msg in issues:
    print('  !!!', rel, '->', msg)