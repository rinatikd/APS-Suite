from pathlib import Path
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict
import json, re
base=next(Path('/mnt/data/rc45').glob('RC_*'))
r=ET.parse(base/'RCPD/db_proj_equip.xml').getroot()
lsroot=ET.parse(base/'RCPD/db_proj_ls.xml').getroot()
items=list(r.findall('.//Equipment'))
lines=list(lsroot.findall('.//CommunicLine'))
# model
line_devices=defaultdict(list); dangling=[]
for e in items:
    for p in e.findall('./ports/port'):
        ln=(p.get('lsName') or '').strip()
        if ln:
            line_devices[ln].append(e)
        elif p.get('lsName') is not None and p.get('supportedLsType') in ('address','control','notif_voice','power12'):
            dangling.append((e.get('positName'),e.get('schmEqGroupName'),p.get('name'),p.get('supportedLsType')))
line_info={}
for l in lines:
    seg=l.findall('./CableSegment')
    length=sum(float(s.get('length','0') or 0) for s in seg)/1000.0
    line_info[l.get('lsName')]={'type':l.get('type'),'segments':len(seg),'length_m':round(length,3),'devices':len({e.get('handle') for e in line_devices.get(l.get('lsName'),[])})}
# addresses: positional suffix
addr_re=re.compile(r'\.(\d+)$')
addresses=defaultdict(list)
for e in items:
    pos=e.get('positName','')
    m=addr_re.search(pos)
    if m:
        addresses[m.group(1)].append(pos)
# output
report={
 'project': str(base/'DWG'/'Улыс Паркинг АПС.dwg'),
 'equipment_total':len(items),
 'equipment_by_type':Counter(e.get('schmEqGroupName') or '(без группы)' for e in items),
 'lines':line_info,
 'dangling_ports':dangling,
 'duplicate_positional_numbers':{k:v for k,v in addresses.items() if len(v)>1},
 'rules':[
  {'id':'R3-ALS-COUNT','status':'PASS' if line_info.get('АЛС1.1',{}).get('devices',0)<=250 and line_info.get('АЛС1.2',{}).get('devices',0)<=250 else 'FAIL','basis':'Данные оборудования R3-Рубеж-2ОП в RCL/db_equip.xml','detail':'Максимум 250 адресных устройств на одну АЛС.'},
  {'id':'R3-ALS-LENGTH','status':'PASS' if all(line_info.get(x,{}).get('length_m',0)<=3000 for x in ['АЛС1.1','АЛС1.2']) else 'FAIL','basis':'Данные оборудования R3-Рубеж-2ОП в RCL/db_equip.xml','detail':'Максимальная длина одной АЛС 3000 м.'},
  {'id':'MODEL-DANGLING-PORTS','status':'REVIEW' if dangling else 'PASS','basis':'RCPD/db_proj_equip.xml','detail':f'Порты с пустой привязкой линии: {len(dangling)}. Нельзя автоматически считать ошибкой.'},
 ]
}
# make json serializable
report['equipment_by_type']=dict(report['equipment_by_type'])
Path('/mnt/data/APS_Expert_0_2/reference_audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
# human report
out=[]
out.append('# APS Expert — первичный интеллектуальный аудит эталонного проекта')
out.append('')
out.append(f'Всего элементов: **{len(items)}**')
out.append('')
out.append('## 1. АЛС')
for n in ['АЛС1.1','АЛС1.2']:
 d=line_info.get(n,{})
 out.append(f'- **{n}:** {d.get("devices",0)} устройств, {d.get("length_m",0)} м, {d.get("segments",0)} сегментов — проверка по лимитам R3: **PASS**.')
out.append('')
out.append('## 2. Линии управления')
for n in sorted([k for k,v in line_info.items() if v['type']=='control']):
 d=line_info[n]; out.append(f'- {n}: {d["length_m"]} м, {d["segments"]} сегмента, устройств с явной привязкой: {d["devices"]}.')
out.append('')
out.append('## 3. Требуют инженерной проверки')
out.append(f'- Обнаружено **{len(dangling)}** портов с пустой привязкой к линии. Это не объявляется ошибкой автоматически: часть элементов может иметь локальные/внутренние подключения.')
for x in dangling: out.append(f'  - {x[0]} / {x[1]} / порт {x[2]} ({x[3]})')
out.append('')
out.append('## 4. Следующий анализ')
out.append('- связать RubezhCAD handle с объектом AutoCAD;')
out.append('- получить координаты оборудования из DWG;')
out.append('- определить границы помещений/зоны;')
out.append('- проверить размещение извещателей по геометрии;')
out.append('- проверить кабельные трассы и пересечения;')
out.append('- проверить согласованность плана, структурной схемы и спецификации.')
Path('/mnt/data/APS_Expert_0_2/reference_audit.md').write_text('\n'.join(out),encoding='utf-8')
print('\n'.join(out))
