#!/usr/bin/env python3
"""Compare PHB placement hypotheses with independent XYZ samples.

Research only: pitch/roll placements are excluded unless an explicit Euler-order
hypothesis is supplied. Collision flags are not interpreted; no results enable
server travel. Samples are a JSON
array of {name,x,y,z}; y is the independent reference elevation.
"""
import argparse
import collections
import hashlib
import importlib.util
import json
import math
from pathlib import Path

spec = importlib.util.spec_from_file_location('extract_geometry', Path(__file__).with_name('extract-zone-geometry.py'))
g = importlib.util.module_from_spec(spec)
spec.loader.exec_module(g)


def inverse_yaw(point, transform, sign):
    x,y,z = (point[i]-transform['position'][i] for i in range(3))
    sx,sy,sz = transform['scale_raw'][:3]
    if not all(math.isfinite(v) and abs(v)>1e-12 for v in (sx,sy,sz)):
        raise ValueError('Invalid scale')
    angle=sign*transform['rotation_raw'][1]
    c,s=math.cos(angle),math.sin(angle)
    return ((c*x-s*z)/sx,y/sy,(s*x+c*z)/sz)


def inverse_euler(point, transform, sign, order):
    """Inverse of scale, then listed intrinsic-to-fixed-axis rotation operations,
    then translation. Order is a research hypothesis, not a verified client ABI.
    """
    values=[point[i]-transform['position'][i] for i in range(3)]
    for axis in reversed(order):
        angle=-sign*transform['rotation_raw']['XYZ'.index(axis)]
        c,s=math.cos(angle),math.sin(angle)
        x,y,z=values
        if axis=='X': values=[x,c*y-s*z,s*y+c*z]
        elif axis=='Y': values=[c*x+s*z,y,-s*x+c*z]
        else: values=[c*x-s*y,s*x+c*y,z]
    scale=transform['scale_raw'][:3]
    if not all(math.isfinite(v) and abs(v)>1e-12 for v in scale):
        raise ValueError('Invalid scale')
    return tuple(values[i]/scale[i] for i in range(3))


def line_box(origin, direction, bounds):
    lower,upper=-math.inf,math.inf
    for i in range(3):
        lo,hi=bounds[0][i]-1e-4,bounds[1][i]+1e-4
        if abs(direction[i])<1e-12:
            if not lo<=origin[i]<=hi:return False
        else:
            a,b=(lo-origin[i])/direction[i],(hi-origin[i])/direction[i]
            lower=max(lower,min(a,b));upper=min(upper,max(a,b))
            if lower>upper:return False
    return True


def cross(a,b):
    return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])


def dot(a,b):
    return sum(x*y for x,y in zip(a,b))


def line_triangle(origin,direction,a,b,c):
    edge1=tuple(b[i]-a[i] for i in range(3))
    edge2=tuple(c[i]-a[i] for i in range(3))
    h=cross(direction,edge2);det=dot(edge1,h)
    if abs(det)<1e-10:return None
    delta=tuple(origin[i]-a[i] for i in range(3))
    u=dot(delta,h)/det
    if not -1e-6<=u<=1+1e-6:return None
    q=cross(delta,edge1);v=dot(direction,q)/det
    if v< -1e-6 or u+v>1+1e-6:return None
    return dot(edge2,q)/det


def triangle_height(x,z,a,b,c):
    denominator=(b[2]-c[2])*(a[0]-c[0])+(c[0]-b[0])*(a[2]-c[2])
    if abs(denominator)<1e-10:
        return None
    u=((b[2]-c[2])*(x-c[0])+(c[0]-b[0])*(z-c[2]))/denominator
    v=((c[2]-a[2])*(x-c[0])+(a[0]-c[0])*(z-c[2]))/denominator
    if min(u,v,1-u-v)<-1e-6:
        return None
    return u*a[1]+v*b[1]+(1-u-v)*c[1]


def physics_resources(resources):
    result={}
    for resource in resources:
        if resource['type']!=0x706862:
            continue
        prior=result.get(resource['name'])
        if prior and prior['resource_id']!=resource['resource_id']:
            raise ValueError('Ambiguous physics resource name')
        result[resource['name']]=resource
    return result


def probe(root,lid,points,sign,euler_order=None):
    data=g.resource_path(root,lid).read_bytes()
    resources,nodes=g.layout(data)
    by={n['offset']:n for n in nodes}
    # Render and physics resources can share a name; select the physics namespace.
    rs=physics_resources(resources)
    cache={}
    hashes={}
    stats=collections.Counter()
    hits=[dict(sample=p,hits=[]) for p in points]
    def resolve(ref,chain,seen):
        if ref in seen or len(seen)>64:
            stats['cyclic_or_deep_reference']+=1
            return
        node=by.get(ref)
        if node is None:
            stats['missing_node']+=1
            return
        if node['type']=='BaseObjects/Attribute/AttributeBaseObject':
            yield node,chain
        elif 'items' in node:
            for item in node['items']:
                yield from resolve(item['reference'],chain+[item],seen|{ref})
        elif node['type']=='RefObjects/InstanceObject':
            yield from resolve(node['reference'],chain+[node],seen|{ref})
        else:
            stats['non_attribute_leaf']+=1
    for instance in nodes:
        if instance['type']!='RefObjects/InstanceObject':
            continue
        for attribute,chain in resolve(instance['reference'],[instance],set()):
            resource=rs.get(attribute['resource_name_candidate'])
            if not resource or resource['type']!=0x706862:
                stats['unresolved_resource']+=1
                continue
            tilted=any(abs(t['rotation_raw'][0])>1e-5 or abs(t['rotation_raw'][2])>1e-5 for t in chain)
            if tilted and euler_order is None:
                stats['tilted_placements_skipped']+=1
                continue
            if not all(math.isfinite(v) for t in chain for k in ('position','rotation_raw','scale_raw') for v in t[k]):
                stats['nonfinite_transform']+=1
                continue
            rid=resource['resource_id']
            if rid not in cache:
                try:
                    raw=g.resource_path(root,rid).read_bytes()
                    cache[rid]=g.geometry(raw)
                    hashes[f'{rid:08X}']=hashlib.sha256(raw).hexdigest()
                except (ValueError,OSError):
                    cache[rid]=[]
                    stats['unsupported_mesh']+=1
            stats['tilted_hypothesis_placements' if tilted else 'yaw_only_placements']+=1
            for result in hits:
                p=result['sample']
                local=(p['x'],p['y'],p['z'])
                endpoint=(p['x'],p['y']+1,p['z'])
                try:
                    for t in chain:
                        if tilted:
                            local=inverse_euler(local,t,sign,euler_order)
                            endpoint=inverse_euler(endpoint,t,sign,euler_order)
                        else:
                            local=inverse_yaw(local,t,sign)
                except ValueError:
                    stats['invalid_transform_sample']+=1
                    continue
                x,_,z=local
                direction=tuple(endpoint[i]-local[i] for i in range(3)) if tilted else None
                for mesh in cache[rid]:
                    lo,hi=mesh['bounds']
                    if tilted:
                        if not line_box(local,direction,mesh['bounds']):continue
                    elif not lo[0]-1e-4<=x<=hi[0]+1e-4 or not lo[2]-1e-4<=z<=hi[2]+1e-4:
                        continue
                    for index,face in enumerate(mesh['faces']):
                        vertices=tuple(mesh['vertices'][i] for i in face[:3])
                        if tilted:
                            parameter=line_triangle(local,direction,*vertices)
                            if parameter is None:continue
                            height=p['y']+parameter
                        else:
                            height=triangle_height(x,z,*vertices)
                            if height is None:continue
                            for t in reversed(chain):
                                height=height*t['scale_raw'][1]+t['position'][1]
                        result['hits'].append(dict(y=height,error=height-p['y'],resource=f'{rid:08X}',
                            block_offset=mesh['offset'],triangle=index,instance_offset=instance['offset'],
                            attribute_offset=attribute['offset'],face_attribute_raw=face[3],tilted_hypothesis=tilted))
    for row in hits:
        row['hits'].sort(key=lambda h:abs(h['error']))
    errors=sorted(abs(row['hits'][0]['error']) for row in hits if row['hits'])
    summary=dict(samples=len(points),samples_with_hits=len(errors),
                 within_0_75=sum(e<=0.75 for e in errors),within_2=sum(e<=2 for e in errors),
                 median_absolute_error=errors[len(errors)//2] if errors else None)
    return dict(research_only=True,world_transform_verified=False,layout_id=f'{lid:08X}',
                layout_sha256=hashlib.sha256(data).hexdigest(),geometry_sha256=hashes,
                yaw_sign=sign,euler_order_hypothesis=euler_order,stats=stats,summary=summary,samples=hits)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--data-root',type=Path,required=True)
    parser.add_argument('--layout-id',type=lambda v:int(v,0),required=True)
    parser.add_argument('--samples',type=Path,required=True)
    parser.add_argument('--yaw-sign',type=int,choices=(-1,1),required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--euler-order',choices=('XYZ','XZY','YXZ','YZX','ZXY','ZYX'),
                        help='Include tilted placements using this unverified rotation-order hypothesis')
    args=parser.parse_args()
    raw=args.samples.read_bytes()
    points=json.loads(raw)
    if not isinstance(points,list) or not 0<len(points)<=10000:
        raise ValueError('Expected 1–10000 sample records')
    for p in points:
        if not all(isinstance(p[k],(float,int)) and math.isfinite(p[k]) for k in ('x','y','z')):
            raise ValueError('Invalid sample coordinates')
    report=probe(args.data_root,args.layout_id,points,args.yaw_sign,args.euler_order)
    report['samples_sha256']=hashlib.sha256(raw).hexdigest()
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(report,indent=2,allow_nan=False)+'\n')
    print(json.dumps(dict(summary=report['summary'],stats=report['stats'],report=str(args.output)),indent=2))

if __name__=='__main__':
    main()
