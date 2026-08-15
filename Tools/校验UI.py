# -*- coding: utf-8 -*-
"""校验控制器节点名依赖与 UXML 定义一致。"""
import re, glob

uxml_names = set()
for f in glob.glob(r'E:\UNITY GAME\文字奇幻rpg\Assets\Resources\UI\**\*.uxml', recursive=True):
    with open(f, encoding='utf-8') as fp:
        uxml_names.update(re.findall(r'name="([^"]+)"', fp.read()))

deps = set()
for f in glob.glob(r'E:\UNITY GAME\文字奇幻rpg\Assets\Scripts\**\*.cs', recursive=True):
    if 'MCPForUnity' in f or 'Plugins' in f:
        continue
    with open(f, encoding='utf-8') as fp:
        for line in fp:
            s = line.strip()
            if s.startswith('//'):
                continue
            deps.update(re.findall(r'Q<[^>]+>\("([^"]+)"\)', line))

print(f'控制器引用 {len(deps)} 个节点名')
missing = sorted(d for d in deps if d not in uxml_names)
print('缺失:' if missing else '全部匹配 ✓')
for d in missing:
    print('  ✗', d)
print(f'UXML 定义 {len(uxml_names)} 个 name: {sorted(uxml_names)}')
